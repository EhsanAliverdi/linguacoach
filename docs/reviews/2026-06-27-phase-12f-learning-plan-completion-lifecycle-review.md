---
status: current
lastUpdated: 2026-06-27 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 12F — Learning Plan Completion Lifecycle: Engineering Review

**Date:** 2026-06-27
**Sprint:** Phase 12F
**Related sprint doc:** docs/sprints/current-sprint.md

---

## Summary

Phase 12F closes the Learning Plan lifecycle loop by introducing deterministic
`Completed` and `Mastered` transitions for plan objectives, wiring them into
the existing mastery evaluation pipeline, expanding progress metrics, and
ensuring background jobs continue to respect completed objectives.

---

## Files Reviewed

- `src/LinguaCoach.Application/LearningPlan/ILearningPlanService.cs`
- `src/LinguaCoach.Application/Mastery/StudentMasteryReport.cs`
- `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`
- `src/LinguaCoach.Infrastructure/Mastery/StudentMasteryEvaluationService.cs`
- `src/LinguaCoach.Infrastructure/Jobs/StudentMasteryEvaluationJob.cs`
- `src/LinguaCoach.Domain/Entities/StudentLearningPlanObjective.cs`
- `src/LinguaCoach.Domain/Enums/LearningPlanObjectiveStatus.cs`
- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanCompletionTests.cs`
- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanDomainTests.cs`

---

## Part A — Audit Findings

### Where completion is currently detected

| Signal | Source | Detail |
|---|---|---|
| Mastered | `StudentMasteryEvaluationService.EvaluateStudentAsync` | 5+ consecutive successes, avg score >= 70, >= threshold events |
| NeedsReview | Same service, `ClassifyStatus` | 1+ consecutive success, avg 50-79 |
| NeedsPractice / AtRisk | Same service | Lower evidence or consecutive failures |
| Learning events | `StudentLearningEvent` records | Created on activity submission |

### Where mastery becomes available

- `StudentMasteryReport.MasteredObjectiveKeys` — full mastery
- `StudentMasteryReport.CompletedObjectiveKeys` (new in 12F) — NeedsReview signal = sufficient evidence
- Both are available immediately after `EvaluateStudentAsync` returns

### Where Learning Plan should be updated

- In `StudentMasteryEvaluationJob.Execute` — after each student evaluation sweep
- In `LearningPlanService.MarkObjectiveMasteredAsync` / `MarkObjectiveCompletedAsync` — per objective

### Duplicate event risks

- Both new methods are idempotent: already-Completed → no-op on second Completed call;
  already-Mastered → no-op regardless of incoming signal
- `StudentMasteryEvaluationJob` calls both in a loop per student; each iteration is idempotent
- No unique constraint needed — status guard is sufficient

### Ordering guarantees

- Job processes mastered keys first, then completed keys
- Regeneration only triggers after both loops complete
- `RegeneratePlanAsync` replaces the whole plan; objectives re-sequenced from current mastery state

---

## Part B — Completion Rules

An objective may transition to:

**Completed** when:
- Mastery signal is `NeedsReview` (consecutive successes >= 1, avg score 50-79, events >= 3)
- This represents: student has worked through the objective and shows improvement

**Mastered** when:
- Mastery signal is `Mastered` (consecutive successes >= threshold, avg score >= 70, events >= threshold)
- This represents: full objective mastery confirmed by evidence

Neither transition is triggered simply by assignment or routing. Evidence from
`StudentLearningEvent` history is required.

---

## Part C — Completion Service Changes

### `ILearningPlanService` additions

```csharp
Task MarkObjectiveCompletedAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct);
Task MarkObjectiveMasteredAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct);
```

### `LearningPlanService` implementation

Both methods share a `TransitionObjectiveAsync` helper that:
1. Loads the active plan with objectives
2. Finds the matching objective by key
3. Checks idempotency: Completed/Mastered → no-op for Completed; Mastered → no-op for Mastered
4. Applies the domain method (`MarkCompleted()` / `MarkMastered()`)
5. Saves and logs
6. Logs plan exhaustion warning if no Active/InProgress objectives remain

### Idempotency contract

| Current status | MarkCompleted call | Result |
|---|---|---|
| Active | → | Completed |
| InProgress | → | Completed |
| Completed | → | No-op (already done) |
| Mastered | → | No-op (higher terminal) |

| Current status | MarkMastered call | Result |
|---|---|---|
| Any non-Mastered | → | Mastered |
| Mastered | → | No-op |

---

## Part D — Event Integration

`StudentMasteryEvaluationJob.Execute` now:
1. Calls `EvaluateStudentAsync` (unchanged)
2. For each `MasteredObjectiveKey` → calls `MarkObjectiveMasteredAsync` (warning-only on failure)
3. For each `CompletedObjectiveKey` → calls `MarkObjectiveCompletedAsync` (warning-only on failure)
4. Triggers `RegeneratePlanAsync("mastery_sweep")` when mastered > 0, completed > 0, or demoted > 0

Failures at steps 2-3 do not block step 4. Generation continues regardless.

---

## Part E — Automatic Plan Advancement

No explicit "advance next objective" mechanism is needed. The queue advances
naturally because:

- `GetNextPlannedObjectiveAsync` selects `Active` objectives only
- Completed and Mastered objectives are automatically excluded
- The next `Active` objective in `PlannedOrder` becomes the focus

When all objectives are exhausted:
- `LogPlanExhaustionIfNeeded` logs an informational warning
- `RegeneratePlanAsync("mastery_sweep")` is triggered by the job's existing logic
- New objectives are generated from updated mastery state

---

## Part F — Progress Calculation

`LearningPlanProgressSummary` expanded with:

| New field | Meaning |
|---|---|
| `TotalObjectives` | Total objectives in plan |
| `ObjectivesMastered` | Count with Mastered status |
| `ObjectivesInProgress` | Count with InProgress status |
| `DeferredObjectives` | Count with Deferred status |
| `CompletionPercentage` | (Completed + Mastered) / Total * 100 |
| `LastCompletedAt` | Latest `LastEvaluatedAt` from Completed/Mastered objectives |

`MasteryPercentage` now specifically reflects Mastered / Total, not all-done / total.

`DeterminePhase` now uses `completionPct` instead of `masteryPct` so it reflects
student progress through the plan, not just peak mastery.

---

## Part G/H — Admin API/UI

The two existing endpoints are unchanged in signature:
- `GET /api/admin/students/{id}/learning-plan` — returns `LearningPlanSummary` (already has CompletedObjectives, MasteredObjectives)
- `GET /api/admin/students/{id}/learning-plan/progress` — now returns expanded `LearningPlanProgressSummary`

No new endpoints required. Angular admin view automatically receives new fields
via JSON serialization — no frontend changes needed.

---

## Part I — Background Jobs

`LessonBatchGenerationJob.GetNextPlannedObjectiveAsync` already filters:
```csharp
.Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
```

Completed and Mastered objectives are excluded by this filter. No changes required.

`PracticeGymGenerationJob.GetPracticeGymObjectivesAsync` similarly filters to
non-blocked objectives and excludes terminal statuses. No changes required.

---

## Part J — Idempotency

All completion paths are safe for:
- Duplicate events: status guard prevents double-transition
- Repeated mastery sweeps: same objective checked repeatedly → no-op after first transition
- Concurrent events: EF Core optimistic concurrency on `LastEvaluatedAt`; worst case is
  a `DbUpdateConcurrencyException` caught at the service boundary (warning-only logging)

---

## Tests Added

File: `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanCompletionTests.cs`

| # | Test | What it verifies |
|---|---|---|
| 1 | `Objective_CanTransition_ActiveToInProgressToCompleted` | Full lifecycle transition |
| 2 | `Objective_MarkCompleted_FromActive_SetsCompleted` | Domain allows transition |
| 3 | `MarkCompleted_Idempotency_AlreadyCompleted_NoStatusChange` | Duplicate call safe |
| 4 | `MarkMastered_Idempotency_CalledTwice_StatusRemainsKMastered` | Repeated mastery safe |
| 5 | `Objective_CompletedToMastered_AllowedUpgradeOnly` | Completed upgrades to Mastered |
| 6 | `CompletedObjective_IsExcluded_FromActiveCandidates` | NewLearning exclusion |
| 7 | `ReviewObjective_WithActiveStatus_StillSelectable` | Review routing preserved |
| 8 | `ProgressSummary_CompletionPercentage_ReflectsCompletedAndMastered` | Progress calc |
| 9 | `MarkCompleted_SetsLastEvaluatedAt` | Completion timestamp recorded |
| 10 | `MarkMastered_SetsLastEvaluatedAt` | Mastery timestamp recorded |
| 11 | `StudentMasteryReport_WithMasteredKeys_CanDriveCompletion` | Report drives mastered path |
| 12 | `StudentMasteryReport_CompletedObjectiveKeys_IncludesNeedsReviewKeys` | NeedsReview = Completed evidence |
| 13 | `BlockedObjective_StartsBlocked_NotSelectable` | Blocked excluded correctly |
| 14 | `Mastered_IsHigherTerminal_ThanCompleted` | Terminal state ordering |
| 14 | `Unblock_TransitionsBlocked_ToActive` | Blocked → Active unblock |
| 15 | `Mastered_IsHigherTerminal_ThanCompleted` | Terminal state ordering |
| 16 | `InProgressObjective_NotInActiveCandidates_NextActiveSelected` | Background job filter |

`LearningPlanDomainTests` updated: test 23 updated to use expanded `LearningPlanProgressSummary` signature.

---

## Build/Test Totals

```
dotnet build    → 0 errors, 0 warnings (code)
dotnet test     → 3 arch + 1460 unit + 1155 integration = 2618 total, 0 failures
```

---

## Lifecycle Changes

```
Active → InProgress     (unchanged — routing picks objective)
InProgress → Completed  (NEW — NeedsReview mastery signal, via MarkObjectiveCompletedAsync)
Active → Completed      (NEW — allowed, e.g. if mastery eval runs before routing picks it)
* → Mastered            (NEW — full Mastered signal, via MarkObjectiveMasteredAsync)
Completed → Mastered    (NEW — upgrade allowed)
Mastered → (any)        (blocked — highest terminal state, idempotency guard)
```

---

## Completion Trigger Sources

| Trigger | Evidence required | Service called |
|---|---|---|
| Daily mastery sweep | `MasteredObjectiveKeys` in `StudentMasteryReport` | `MarkObjectiveMasteredAsync` |
| Daily mastery sweep | `CompletedObjectiveKeys` in `StudentMasteryReport` (NeedsReview signal) | `MarkObjectiveCompletedAsync` |

Both triggers fire from `StudentMasteryEvaluationJob`. No direct coupling to UI.

---

## Regeneration Rules

Plan regenerates when (unchanged from Phase 12D, now also includes completion):
- Any objective mastered or completed (new in 12F)
- Any readiness item demoted
- CEFR level changes
- Student preferences change

Plan does NOT regenerate:
- On routing alone (InProgress transition)
- On duplicate completion events
- More than once per mastery sweep per student

---

## Remaining Limitations

1. `MarkObjectiveCompletedAsync` / `MarkObjectiveMasteredAsync` are not called from
   the student-facing activity submission path — they rely on the daily mastery sweep.
   Real-time plan updates require wiring into `ActivityAttempt` persistence (future phase).

2. Concurrent `SaveChangesAsync` calls from two simultaneous mastery sweep jobs for
   the same student could cause a `DbUpdateConcurrencyException`. The warning-only
   logging pattern means the second call silently skips. This is acceptable at current scale.

3. `CompletedObjectiveKeys` in `StudentMasteryReport` includes the key in both
   `CompletedObjectiveKeys` AND `WeakObjectiveKeys`. Consumers should treat them as
   overlapping, not exclusive.

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Use `NeedsReview` signal for `Completed` evidence | Deterministic, uses existing `ClassifyStatus` without new scoring models |
| Completed also appears in WeakObjectiveKeys | Preserves existing weak-skill routing; plan treats it as complete but routing may still reinforce |
| No explicit "advance" mechanism | Queue advances naturally via `Active`-only filter in `GetNextPlannedObjectiveAsync` |
| Regeneration triggered on completion | Ensures plan gets fresh objectives when old ones are consumed |
| No student UI changes | Phase 12F scope is backend only |

---

## Next Recommended Action

**Phase 12G** or equivalent: Wire `MarkObjectiveCompletedAsync` / `MarkObjectiveMasteredAsync`
into the student activity submission path (after `ActivityAttempt` saved) so plan updates
happen in near-real-time rather than waiting for the daily mastery sweep.
