# Phase 8N — Configurable Practice Item Counts Foundation — Implementation Review

- Date: 2026-06-16
- Related sprint: 2026-06-15 Staged Activity Content Migration
- Type: implementation + design rule review
- Verdict: complete, all tests green

## Summary

Phase 8N is an architecture/configuration foundation, not a new exercise format.
It adds configurable min/default/max item and option counts per exercise type
and wires them into the registry, admin API/UI, generation prompt context, and
content validation. No exercise format was made runnable.

## Files reviewed / changed

Backend:
- `src/LinguaCoach.Domain/Entities/ExerciseTypeDefinition.cs` — six count
  properties, ctor params, `ValidateCounts`, `UpdateItemCounts`, sync.
- `src/LinguaCoach.Persistence/Configurations/ExerciseTypeDefinitionConfiguration.cs`
  — six columns with defaults.
- `src/LinguaCoach.Persistence/Migrations/20260615230936_AddPracticeItemCounts.cs`
  (+ Designer + snapshot) — additive migration.
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` —
  `CountOverrides` table applied to all ready and planned types.
- `src/LinguaCoach.Application/Activity/ExerciseTypeRegistry.cs` — entry counts.
- `src/LinguaCoach.Application/Admin/ExerciseTypeCatalogQueries.cs` — DTO +
  command counts.
- `src/LinguaCoach.Infrastructure/Activity/ExerciseTypeRegistry.cs` — `ToEntry`.
- `src/LinguaCoach.Infrastructure/Admin/ExerciseTypeCatalogService.cs` —
  `ToDto` + count update in `UpdateAsync`.
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — request counts + 400 on
  invalid range.
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` —
  `PracticeCountSettings`, count enforcement.
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` —
  count lookup, prompt variables, validation wiring.

Frontend:
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — model counts.
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
  — count columns, inputs, `countError`, `saveCounts`.

Docs:
- `docs/architecture/practice-item-sets.md` (new design rule)
- `docs/architecture/learning-activity-engine.md` (note)
- `docs/handoffs/current-product-state.md` (note)
- `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` (entry)

Tests:
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`
  (gap count below/above/within range, no-settings skip).
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`
  (seeded counts, valid ranges, admin valid/invalid/negative update, status
  unchanged, registry entry includes counts).
- `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs` (fixture
  bumped to 2 incorrect tokens to satisfy seeded min).
- `src/LinguaCoach.Web/.../admin-exercise-types.component.spec.ts` (new).

## Findings

- Validator count enforcement is opt-in via an optional parameter, preserving
  all existing call sites and tests. Low risk.
- Counts are configuration only: `UpdateItemCounts` never alters
  `ImplementationStatus`, `IsEnabled`, or surface flags. Verified by test.
- The shared highlight-incorrect-words fixture had 1 token vs a seeded min of 2;
  it was updated to 2 valid tokens rather than relaxing the seeded count.

## Test results

- Unit: 777 passed
- Integration: 503 passed
- Architecture: 3 passed
- Angular: 132 passed
- Angular dev + production builds: succeed

## Confirmations

- No new exercise format made runnable.
- `write_from_dictation`, `summarize_spoken_text`, all speaking formats remain
  planned/non-runnable.
- No MinIO/audio lifecycle changes.
- No Today pre-generation.

## Next recommended action

Phase 8O — implement `write_from_dictation` using the new item-count foundation.
