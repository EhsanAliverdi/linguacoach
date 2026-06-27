---
status: current
lastUpdated: 2026-06-27
owner: engineering
supersedes:
supersededBy:
---

# Phase 12G — Real-Time Learning Plan Progress Integration: Engineering Review

**Date:** 2026-06-27
**Sprint:** Phase 12G
**Related sprint doc:** docs/sprints/current-sprint.md
**Related phase:** Phase 12F (docs/reviews/2026-06-27-phase-12f-learning-plan-completion-lifecycle-review.md)

---

## Summary

Phase 12G wires learning plan objective progress updates directly into the activity
submission path. Prior to this phase, plan progress only updated during the nightly
mastery sweep (background job). After this phase, every activity attempt with a
pattern key triggers an immediate mastery evaluation and — when evidence is sufficient
— a live status transition on the corresponding learning plan objective.

Background jobs are now reconciliation-only, not the primary update mechanism.

---

## Files Modified

- `src/LinguaCoach.Application/LearningPlan/ILearningPlanService.cs`
- `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`
- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanDomainTests.cs`
- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanCompletionTests.cs`

## Files Added

- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanRealtimeProgressTests.cs`

---

## Part A — Architecture

### Real-Time Pipeline

```
ActivityAttempt persisted
  → learning event recorded (IStudentLearningLedger.RecordAsync)
    → TryUpdateLearningPlanProgressAsync called (ActivitySubmitHandler)
      → ILearningPlanService.TryUpdateObjectiveProgressAsync
        → IStudentMasteryEvaluationService.EvaluateObjectiveMasteryAsync
          → if Mastered: MarkObjectiveMasteredAsync
          → if NeedsReview: MarkObjectiveCompletedAsync
          → else: return (insufficient evidence)
```

### Background Job (Reconciliation)

`StudentMasteryEvaluationJob` continues to run daily. It now acts as a
reconciliation sweep: catches any transitions missed by the real-time path
(failures, race conditions, objectives without pattern keys).

---

## Part B — Scope Decisions

### Paths wired (real-time update active)

| Path | Condition |
|---|---|
| Pattern evaluation (`HandlePatternEvaluationAsync`) | Always — these always have an `ExercisePatternKey` |
| Legacy writing path (`HandleAsync`) | Only when `activity.ExercisePatternKey` is set |

### Paths excluded (no real-time update)

| Path | Reason |
|---|---|
| VocabularyPractice | No `StudentLearningEvent` recorded; mastery engine has no evidence |
| ListeningComprehension | Same — no learning event in this path |

VocabularyPractice and ListeningComprehension may be wired in a future phase
once their activity attempts are connected to the learning ledger.

### Regeneration

`TryUpdateObjectiveProgressAsync` does NOT call `RegeneratePlanAsync` inline.
Regeneration remains a background job responsibility. This avoids expensive
plan regeneration on every activity submission. Background sweep triggers
regeneration when it detects transitions during its reconciliation pass.

---

## Part C — Interface Changes

### `ILearningPlanService` additions

```csharp
Task<LearningPlanObjectiveProgressUpdate> TryUpdateObjectiveProgressAsync(
    Guid studentProfileId,
    string objectiveKey,
    CancellationToken ct = default);
```

### `LearningPlanProgressSummary` — three new fields

```csharp
string? CurrentObjectiveKey    // InProgress objective key, or first Active if none InProgress
string? NextObjectiveKey       // Next Active objective by PlannedOrder after current
int ObjectivesCompletedToday   // Completed/Mastered count where LastEvaluatedAt.Date >= UTC today
```

### `LearningPlanObjectiveProgressUpdate` record (new)

```csharp
public sealed record LearningPlanObjectiveProgressUpdate(
    string ObjectiveKey,
    LearningPlanObjectiveStatus? PreviousStatus,
    LearningPlanObjectiveStatus? NewStatus,
    bool StatusChanged,
    string Reason);
```

Reason values: `mastered`, `needs_review`, `insufficient_evidence_{MasteryStatus}`,
`no_active_plan`, `objective_not_in_plan`, `already_terminal`, `error`.

---

## Part D — `TryUpdateObjectiveProgressAsync` Implementation

```
1. Load active plan for student — return no_active_plan if none.
2. Find objective by key (case-insensitive) — return objective_not_in_plan if missing.
3. Check terminal: Active or InProgress only — return already_terminal for Completed/Mastered.
4. Call EvaluateObjectiveMasteryAsync for this single objective.
5. Mastered signal → MarkObjectiveMasteredAsync → return {StatusChanged=true, Reason="mastered"}.
6. NeedsReview signal → MarkObjectiveCompletedAsync → return {StatusChanged=true, Reason="needs_review"}.
7. Other signals → return {StatusChanged=false, Reason="insufficient_evidence_{signal}"}.
8. Any exception → log warning, return {StatusChanged=false, Reason="error"}.
```

The method never throws. All failures produce a valid `LearningPlanObjectiveProgressUpdate`.

---

## Part E — `ActivitySubmitHandler` Changes

### New dependency

```csharp
private readonly ILearningPlanService _learningPlan;
```

Constructor parameter added: `ILearningPlanService learningPlan`. No new DI
registration required — both services are already Scoped in Infrastructure DI.

### Helper

```csharp
private async Task TryUpdateLearningPlanProgressAsync(
    Guid studentProfileId, string? objectiveKey, CancellationToken ct)
```

Early-exits when `objectiveKey` is null or empty. Logs when `StatusChanged=true`.
Never throws — any exception from the inner service is already caught by
`TryUpdateObjectiveProgressAsync`.

### Injection points

- **Pattern path** (`HandlePatternEvaluationAsync`): after `_learningLedger.RecordAsync(learningEvent, ct)`
- **Legacy path** (`HandleAsync`): after `_learningLedger.RecordAsync(legacyEvent, ct)`

Both call `TryUpdateLearningPlanProgressAsync(profile.Id, activity.ExercisePatternKey, ct)`.

---

## Part F — `GetProgressAsync` New Fields

### CurrentObjectiveKey

```
1. Find InProgress objectives ordered by PlannedOrder.
2. If found: use first InProgress key.
3. If not found: use first Active (non-blocked) key ordered by PlannedOrder then Priority.
```

### NextObjectiveKey

```
1. If InProgress objective exists: NextObjectiveKey = first Active (non-blocked) key.
2. If no InProgress: NextObjectiveKey = second Active (non-blocked) key in order.
```

### ObjectivesCompletedToday

```
Count(o => Status in {Completed, Mastered} AND LastEvaluatedAt.HasValue AND LastEvaluatedAt.Value.Date >= DateTime.UtcNow.Date)
```

Both early-return paths (no plan) return `CurrentObjectiveKey: null, NextObjectiveKey: null, ObjectivesCompletedToday: 0`.

---

## Part G — Idempotency and Failure Isolation

| Scenario | Behaviour |
|---|---|
| Same objective evaluated twice | `TryUpdateObjectiveProgressAsync` checks terminal status first — no-op after first transition |
| EvaluateObjectiveMasteryAsync throws | Caught in outer try/catch, returns reason="error", submission path continues |
| Database save fails | Exception caught in `TryUpdateObjectiveProgressAsync`, submission succeeds |
| No pattern key on activity | `TryUpdateLearningPlanProgressAsync` returns early with no-op |
| Background job runs after real-time update | Job sees already-Mastered/Completed objective, idempotency guard skips it |

---

## Part H — Admin API / UI Impact

The two existing endpoints are unchanged in route:
- `GET /api/admin/students/{id}/learning-plan/progress` — now returns the three new fields
  in `LearningPlanProgressSummary`; Angular admin view receives them automatically via JSON

No new endpoints, no Angular changes required.

---

## Tests Added

File: `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanRealtimeProgressTests.cs`

| # | Test | What it verifies |
|---|---|---|
| 1 | `ProgressUpdate_StatusChanged_InitialisesAllFields` | Record initialises correctly with StatusChanged=true |
| 2 | `ProgressUpdate_NoChange_PreviousEqualsNew` | No-change: Previous == New, StatusChanged=false |
| 3 | `ProgressUpdate_NoActivePlan_NullStatusesAndCorrectReason` | Null statuses and reason="no_active_plan" |
| 4 | `ProgressUpdate_ObjectiveNotInPlan_CorrectReason` | reason="objective_not_in_plan" |
| 5 | `ProgressUpdate_ErrorPath_ReturnsErrorReasonAndFalse` | Error path returns reason="error" |
| 6 | `ProgressUpdate_AlreadyMastered_AlreadyTerminalReason` | Mastered is terminal, reason="already_terminal" |
| 7 | `ProgressUpdate_AlreadyCompleted_AlreadyTerminalReason` | Completed is terminal for real-time path |
| 8 | `ProgressUpdate_MasteredSignal_StatusChangedTrue_NewStatusMastered` | Mastered signal → StatusChanged=true |
| 9 | `ProgressUpdate_NeedsReviewSignal_TransitionsToCompleted` | NeedsReview → Completed, not Mastered |
| 10 | `CurrentObjectiveKey_PrefersInProgress_OverActive` | InProgress wins over Active for CurrentKey |
| 11 | `CurrentObjectiveKey_FallsBackToFirstActive_OrderedByPlannedOrder` | First Active by PlannedOrder is CurrentKey |
| 12 | `CurrentObjectiveKey_ExcludesBlockedActive` | Blocked Active excluded from key selection |
| 13 | `CurrentAndNextObjectiveKeys_AreNull_WhenNoObjectives` | Empty plan yields null keys |
| 14 | `ObjectivesCompletedToday_CountsCompletedAndMasteredSinceMidnight` | Today's completions counted correctly |
| 15 | `ObjectivesCompletedToday_ExcludesYesterdayCompletions` | Yesterday's completions excluded |

`LearningPlanDomainTests.cs` test 23: updated to pass three new `LearningPlanProgressSummary` params.
`LearningPlanCompletionTests.cs` test 8: updated to pass three new `LearningPlanProgressSummary` params.

---

## Build / Test Totals

```
dotnet build    → 0 errors, 0 warnings (code)
dotnet test     → 3 arch + 1475 unit + 1155 integration = 2633 total, 0 failures
```

---

## Lifecycle After Phase 12G

```
Activity attempt submitted
  → Learning event recorded
    → Real-time mastery eval (new)
      → Objective transitions to Completed or Mastered immediately
        → Background job reconciles any gaps daily
```

Prior to 12G:
```
Activity attempt submitted
  → Background job runs nightly
    → Objective transitions the next morning
```

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Best-effort only — never throws | Submission must not fail because of plan update |
| No inline regeneration | RegeneratePlanAsync is expensive; background job handles it |
| Exclude VocabularyPractice and ListeningComprehension | These paths have no learning event; mastery engine has no evidence to evaluate |
| ILearningPlanService added to ActivitySubmitHandler | Both are Scoped in same DI container; no new registration needed |
| Check terminal before eval | Avoids calling mastery eval for already-done objectives |

---

## Remaining Limitations

1. VocabularyPractice and ListeningComprehension activity types do not trigger
   real-time plan updates. These paths do not record a `StudentLearningEvent`, so
   the mastery engine has no data to evaluate. Future phase: wire these paths to
   the learning ledger.

2. Regeneration still delayed to nightly sweep. After a real-time Completed/Mastered
   transition, the next Active objective is already selectable (queue advances
   naturally), but the plan is not regenerated with fresh objectives until the job runs.

3. Concurrent simultaneous submissions for the same student + objective could result
   in two `TryUpdateObjectiveProgressAsync` calls racing. The idempotency guard (terminal
   status check) and EF Core optimistic concurrency handle this gracefully.

---

## Next Recommended Action

**Phase 12H** or equivalent: Wire VocabularyPractice and ListeningComprehension paths
to `IStudentLearningLedger.RecordAsync` so that real-time plan updates extend to all
activity types. Alternatively, expose real-time plan progress to the student UI so
students see objective transitions immediately.
