---
status: current
lastUpdated: 2026-06-27 00:00
owner: engineering
sprint: Phase 12E
---

# Phase 12E — Learning Plan Guided Routing: Audit & Implementation Review

## Date
2026-06-27

## Related Sprint
Phase 12E — Learning Plan Guided Routing

## Files Reviewed

- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs`
- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRecommendation.cs`
- `src/LinguaCoach.Application/Curriculum/RoutingMode.cs`
- `src/LinguaCoach.Application/LearningPlan/ILearningPlanService.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingRequestFactory.cs`
- `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`
- `src/LinguaCoach.Domain/Entities/StudentLearningPlan.cs`
- `src/LinguaCoach.Domain/Entities/StudentLearningPlanObjective.cs`
- `src/LinguaCoach.Domain/Enums/LearningPlanObjectiveStatus.cs`
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs`
- `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs`

---

## Part A — Audit Findings

### Where PreferredObjectiveKey is passed today

1. **CurriculumRoutingRequest.PreferredObjectiveKey** — field exists, documented as Phase 12D.
2. **CurriculumRoutingRequestFactory.Build()** — accepts `preferredObjectiveKey` param, sets it on the request.
3. **LessonBatchGenerationJob.BuildCompactSummaryAsync()** — calls `ILearningPlanService.GetNextPlannedObjectiveAsync`, assigns result key to `plannedObjectiveKey`, passes it to factory as `preferredObjectiveKey`. ✓
4. **PracticeGymGenerationJob.MaterializeAsync()** — calls `ILearningPlanService.GetPracticeGymObjectivesAsync`, takes first objective key, passes as `preferredObjectiveKey`. ✓

### Why PreferredObjectiveKey is ignored today

`CurriculumRoutingService.RecommendAsync()` never reads `request.PreferredObjectiveKey`. The routing pipeline (Steps 1–5) uses only: CEFR level, context tags, focus areas, primary skill, difficulty, mastery exclusions. The hint passes through but affects nothing.

### Where routing must consume it

**CurriculumRoutingService.RecommendAsync()** — immediately after building the candidate list (after Step 2b FilterByMastered), before SelectBestCandidate. Logic:

1. If `PreferredObjectiveKey` is non-null, query the syllabus for that specific objective.
2. Validate: objective must be in the normal candidate pool OR pass a controlled acceptance check (same/lower CEFR with review allowed, compatible skill if PrimarySkill is set, runnable, not mastered when Mode=NewLearning unless it's reviewable).
3. If accepted: return it directly with `RoutingReason.LearningPlan` debug note.
4. If rejected: log reason, continue existing routing unchanged.

### Safety checks required

| Check | Rule |
|---|---|
| Objective exists in syllabus | Must be fetchable by key |
| Active + enabled | Objective must be active (IsActive flag at query level) |
| CEFR match | Key's CEFR == normalized student level OR (lower level AND AllowReviewOrScaffold) |
| No silent downgrade | If key is lower level and AllowReviewOrScaffold=false → reject |
| Skill compatibility | If PrimarySkill set, key must match or request has no skill filter |
| Runnable | ActivityCompatibilityConstants.IsRunnable(objective.PrimarySkill) must be true |
| Mastery exclusion | If MasteredObjectiveKeys contains it and Mode=NewLearning and !AllowReviewOfMastered → reject |
| Non-mastered reviewable | If mastered and IsReviewable and AllowReviewOfMastered → accept |

### Learning plan status updates needed

- **Planned → InProgress**: when a planned objective is successfully routed via PreferredObjectiveKey.
- `LearningPlanObjectiveStatus` enum missing `InProgress` value — needs addition.
- `StudentLearningPlanObjective` needs `MarkInProgress()` method.
- `ILearningPlanService` needs `MarkObjectiveInProgressAsync(studentProfileId, objectiveKey)`.
- `LearningPlanService` implements it: find the active plan objective matching the key, call `MarkInProgress()`, save.
- Jobs call `MarkObjectiveInProgressAsync` after `RecommendAsync` returns a `LearningPlan` reason.

---

## Decisions Made

1. Add `InProgress = 6` to `LearningPlanObjectiveStatus` (non-breaking, additive).
2. Implement preferred routing as a pre-step inside `RecommendAsync` — before `SelectBestCandidate`, after `FilterByMastered`. Does not replace any existing step.
3. Use `RoutingReason.LearningPlan` as the new reason. Add it to the `RoutingReason` enum.
4. Jobs call `MarkObjectiveInProgressAsync` only when `RoutingReason == LearningPlan` — this is the signal that the plan objective was consumed.
5. No UI changes required. Admin routing preview backend already accepts `PreferredObjectiveKey` (it's on the request). If it returns `LearningPlan` reason, that surfaces in `Explanation` and `RoutingReason` fields already returned.
6. Rejection is always a no-op fall-through — existing routing continues.

---

## Implementation Tasks

- [x] Add `InProgress = 6` to `LearningPlanObjectiveStatus`
- [x] Add `LearningPlan = 5` to `RoutingReason` enum
- [x] Add `MarkInProgress()` to `StudentLearningPlanObjective`
- [x] Add `MarkObjectiveInProgressAsync` to `ILearningPlanService` and implement in `LearningPlanService`
- [x] Implement preferred objective pre-step in `CurriculumRoutingService.RecommendAsync`
- [x] `LessonBatchGenerationJob` calls `MarkObjectiveInProgressAsync` on LearningPlan routing
- [x] `PracticeGymGenerationJob` calls `MarkObjectiveInProgressAsync` on LearningPlan routing
- [x] Add 15 unit tests in `CurriculumRoutingServiceTests`

---

## Risks / Unresolved Questions

- `GetCandidatesForStudentAsync` queries by CEFR level — preferred key lookup requires a `GetByKeyAsync` on the syllabus query. Need to check if this method exists or needs to be added to `ICurriculumSyllabusQuery`.
- If the objective lookup adds an extra DB query per routing call, it is conditional (only when `PreferredObjectiveKey != null`) so the hot path is unchanged.

---

## Final Verdict

Audit complete. Implementation is safe and additive. No existing routing logic is removed or bypassed. Rejection always falls back silently.

## Next Recommended Action

Implement per tasks above.
