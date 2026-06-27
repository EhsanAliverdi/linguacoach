# Phase 12D — Learning Plan Orchestrator Foundation: Engineering Review

**Date:** 2026-06-27
**Sprint:** Phase 12D — Learning Plan Orchestrator Foundation
**Status:** Complete

---

## Related sprint / backlog

- Sprint doc: `docs/sprints/current-sprint.md`
- Product state: `docs/handoffs/current-product-state.md`

---

## Files reviewed / changed

### New files
- `src/LinguaCoach.Domain/Entities/StudentLearningPlan.cs`
- `src/LinguaCoach.Domain/Entities/StudentLearningPlanObjective.cs`
- `src/LinguaCoach.Domain/Enums/LearningPlanStatus.cs`
- `src/LinguaCoach.Domain/Enums/LearningPlanObjectiveStatus.cs`
- `src/LinguaCoach.Application/LearningPlan/ILearningPlanService.cs`
- `src/LinguaCoach.Application/LearningPlan/LearningPlanOptions.cs`
- `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`
- `src/LinguaCoach.Persistence/Configurations/StudentLearningPlanConfiguration.cs`
- `src/LinguaCoach.Persistence/Configurations/StudentLearningPlanObjectiveConfiguration.cs`
- `src/LinguaCoach.Persistence/Migrations/T61_LearningPlanOrchestrator` (generated)
- `tests/LinguaCoach.UnitTests/LearningPlan/LearningPlanDomainTests.cs`
- `tests/LinguaCoach.IntegrationTests/LearningPlan/LearningPlanIntegrationTests.cs`

### Modified files
- `src/LinguaCoach.Application/Mastery/MasteryEvaluationReason.cs` — added `PlanGeneration = 5`
- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs` — added `PreferredObjectiveKey`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingRequestFactory.cs` — added `preferredObjectiveKey` parameter
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `ILearningPlanService` + `LearningPlanOptions`
- `src/LinguaCoach.Infrastructure/Jobs/StudentMasteryEvaluationJob.cs` — mastery-change regeneration trigger
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs` — plan consultation + `ILearningPlanService` injection
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs` — plan consultation + `ILearningPlanService` injection
- `src/LinguaCoach.Infrastructure/Profile/ProfileCommandHandler.cs` — preference-change regeneration trigger
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — CEFR-change regeneration trigger
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` — two new admin visibility endpoints
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — two new `DbSet<>` properties
- `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs` — updated by migration
- `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs` — `ILearningPlanService` added to job constructor call

---

## Findings, by priority

### P0 — Critical (none)

No blocking issues. Build clean. All 2587 tests pass.

### P1 — Important

**Migration generated correctly.** Migration T61 creates `student_learning_plans` and `student_learning_plan_objectives` tables with proper FK cascade to objectives. Startup project set to `LinguaCoach.Persistence` (not Api, which lacks the Design package).

**BaseEntity compatibility.** `BaseEntity` has only `Id` (Guid) and `CreatedAt` (DateTime). All `DateTimeOffset?` references in domain entities were changed to `DateTime?` during implementation. EF configurations omit `UpdatedAt` (not present on `BaseEntity`).

**`StudentActivityReadinessItem.StudentId` used correctly.** Field is `StudentId` not `StudentProfileId`. `GetProgressAsync` uses `i.StudentId == studentProfileId` — correct.

**Regeneration triggers are non-blocking.** All three trigger sites (`StudentMasteryEvaluationJob`, `ProfileCommandHandler`, `AdminHandler`) wrap `RegeneratePlanAsync` in try/catch and log `LogWarning` only. Plan failure never blocks the primary operation.

**Free routing fallback preserved.** Both jobs (`LessonBatch`, `PracticeGym`) catch plan query exceptions and fall back to free routing. `preferredObjectiveKey = null` means routing behaves identically to pre-12D.

### P2 — Notable

**`GetProgressAsync` returns zero-state for unknown student (not throws).** Intentional: progress summary degrades gracefully when no plan or profile exists. Integration test updated to assert zero-state rather than exception.

**`PreferredObjectiveKey` is a routing hint only.** `CurriculumRoutingService` receives it as a field — the routing service is not modified in this phase to act on it. It is a no-op until Phase 12E wires routing to prefer the key. This is by design: the field is established and the plumbing is in place, but routing behavior is unchanged.

**`ProfileCommandHandler` now has `ILogger` dependency.** Added alongside `ILearningPlanService`. Constructor updated; DI resolves both automatically.

---

## Decisions made

1. **No AI calls in the learning plan layer.** Objective selection is deterministic: skill rotation + mastery state + curriculum routing. AI is only called downstream in `LessonBatchGenerationJob` for session content.
2. **Plan regeneration is synchronous within the trigger call.** Not queued as a background job. Acceptable for current scale; can be made async in a future phase.
3. **`IServiceProvider` used in `AdminHandler` for `ILearningPlanService`.** `AdminHandler` already holds `_services`; avoids adding an 11th constructor parameter.
4. **Admin endpoints added to `AdminReadinessPoolController`** (not a new controller). Fits with the existing diagnostic/health endpoint pattern.
5. **Unit tests use `FluentAssertions` (already in UnitTests.csproj).** Integration tests use `Assert.*` (xUnit only, no FluentAssertions in IntegrationTests.csproj).

---

## Implementation tasks produced

All implemented in this phase. No follow-up tasks generated.

---

## Risks / unresolved questions

1. **`PreferredObjectiveKey` is not yet consumed by `CurriculumRoutingService`.** Routing behavior is unchanged. Phase 12E should wire the routing service to prefer the plan's objective key when it is set.
2. **Plan regeneration on every preference change** may be expensive for students who update preferences frequently. Consider debouncing in a future phase (e.g. minimum 1-hour cooldown).
3. **No student-facing plan UI.** Students cannot see their plan. Admin-only visibility for now. Phase 12E or later can expose a student plan summary endpoint.
4. **`GetOrCreatePlanAsync` throws `InvalidOperationException` for unknown students.** This matches the pattern used throughout the codebase. Admin endpoint handles this with `NotFound`.

---

## Final verdict

Phase 12D is complete. The Learning Plan Orchestrator Foundation is in place: domain entities, application interface, deterministic infrastructure implementation, EF migration, DI registration, three regeneration triggers, admin visibility endpoints, background job integration, and 38 tests. Build and test suite are clean. Ready for Phase 12E (routing service consumption of `PreferredObjectiveKey`).

---

## Next recommended action

**Phase 12E:** Wire `CurriculumRoutingService` to prefer `PreferredObjectiveKey` when set — bias objective selection toward the plan's next objective rather than treating it as a no-op hint.
