# Phase 8A — `reading_multiple_choice_single` Implementation Review

**Date:** 2026-06-15
**Related sprint:** `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` (Phase 8A section)
**Related feature:** First runnable planned future exercise format (reading)

## Files reviewed / changed

- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-multiple-choice/reading-multiple-choice.component.{ts,html}` (new)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.{ts,html}`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/presenters/pattern-backed.presenter.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs`
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs`

## Findings, grouped by priority

### High

None. The implementation follows the established Phase 7B recipe closely:
new pattern definition, catalog row converted from `Planned` to `Ready`,
validator entries, deterministic evaluator branch, generation prompt,
frontend renderer wiring.

### Medium

- `ExercisePatternPhase1Tests.cs` contained hardcoded pattern counts (10 and
  9) that needed updating to 11 and 10 respectively after adding the 11th
  pattern definition. These were pre-existing tests not directly related to
  Phase 8A's stated scope, but they failed once the new pattern was seeded.
  Fixed as part of this phase (counts now 11 active / 10 after one test
  deactivates `phrase_match`).

### Low

- Angular unit tests, dev build, production build, and Playwright e2e could
  not be run in this worktree because `node_modules` is not installed
  (`@angular-devkit/build-angular:karma` builder missing). This is a
  pre-existing environment gap in this worktree, not introduced by Phase 8A.
  Per the verification constraints, no package installation was performed.
  New Angular spec changes (`practice-gym.component.spec.ts` plus the new
  renderer files) are present and structurally consistent with existing
  specs but unverified by a test run in this environment.

## Decisions made

- Used `ActivityType.ReadingTask` and `InteractionMode.MultipleChoice` —
  both pre-existing, previously-unused enum values — avoiding new enum
  members.
- Used `MarkingMode.KeyedSelection` with a new branch inside the existing
  `KeyedSelectionEvaluator`, rather than a new MarkingMode or evaluator
  class, following the `ExactMatchEvaluator`/`KeyedSelectionEvaluator`
  multi-shape-per-evaluator pattern already established for
  `phrase_match`/`listen_and_gap_fill`.
- Added a full `ExercisePatternDefinition` row (not just a catalog row)
  because `PracticeGymPoolService` requires a non-empty
  `ExercisePatternKey` on `PracticeActivityCache` entries for pool
  reservation to work.
- `RequiredPracticeKeysByPatternKey` entry added (not
  `RequiredPracticeKeysByType`), since pattern-key entries take precedence
  and `ActivityType.ReadingTask` has no other patterns to conflict with.

## AskUserQuestion decisions

None — no AskUserQuestion prompts were used in this phase.

## Implementation tasks produced

All 8 task groups from the original spec were completed:

1. Catalog/registry seeder — done (pattern + catalog row)
2. Prompt seeding — done (`activity_generate_reading_multiple_choice_single`)
3. Generation routing — done (`AiActivityGeneratorHandler`)
4. Validator required/forbidden keys — done
5. Deterministic evaluator (no AI) — done (`KeyedSelectionEvaluator` branch)
6. Frontend Learn + Practice renderers + Practice Gym wiring — done
7. API compatibility verification — confirmed, no changes to
   `exerciseType=`/`type=`/`pattern=` routing
8. Pool infra wiring — done via full pattern definition row

## Risks or unresolved questions

- Angular-side verification (unit tests, dev/prod builds, e2e) is
  outstanding due to the missing `node_modules` in this worktree. A future
  agent with a properly provisioned worktree should run:
  `npm install` (if appropriate per repo policy) and then `ng test`,
  `ng build`, `ng build --configuration production`, and Playwright e2e for
  the new reading renderer.
- No Playwright e2e test was written for
  Learn → Practice → Feedback for `reading_multiple_choice_single` due to
  the same environment constraint; this should be added alongside the
  Angular verification above.

## Final verdict

Backend implementation complete and verified: build clean (0 errors, 6
pre-existing unrelated warnings), architecture tests 3/3, unit tests
603/603 (+14), integration tests 474/474 (+2). Frontend implementation
complete but unverified by automated tests/builds in this environment.

## Next recommended action

In an environment with `node_modules` installed, run the Angular
verification gate (`ng test`, `ng build`, `ng build --configuration
production`, Playwright e2e) for the new renderer and Practice Gym spec
changes, and add the missing Playwright e2e for
`reading_multiple_choice_single`. Then consider Phase 8B: pick another
planned reading/writing format with a simple keyed-selection or
exact-match shape and repeat this recipe (see "Reference implementation"
section in `docs/architecture/learning-activity-engine.md`).
