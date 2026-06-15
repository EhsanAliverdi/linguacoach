---
title: Phase 8B — reading_multiple_choice_multi Implementation Review
date: 2026-06-15
sprint: 2026-06-15-staged-activity-content-migration-sprint
status: complete
---

# Phase 8B — reading_multiple_choice_multi Implementation Review

## Summary

Phase 8B implements `reading_multiple_choice_multi` as the second runnable planned future reading exercise format, following the Phase 8A reference architecture for `reading_multiple_choice_single`. The student reads a workplace passage and selects **all** answers supported by the text. Evaluation is fully deterministic — no AI evaluation call.

## Files Changed

| File | Change |
|------|--------|
| `src/LinguaCoach.Domain/ExercisePatternKey.cs` | Added `ReadingMultipleChoiceMulti = "reading_multiple_choice_multi"` |
| `src/LinguaCoach.Domain/Enums/InteractionMode.cs` | Added `MultipleChoiceMulti = 12` (append-only, pinned) |
| `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` | New pattern: `ReadingInput`, `MultipleChoiceMulti`, `KeyedSelection`, 5 min |
| `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` | Promoted `reading_multiple_choice_multi` from `Planned` → `Ready`; catalog total stays 36 |
| `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` | Added `correctOptionIds`, `optionExplanations` to forbidden learn keys; added multi required practice keys |
| `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` | New `EvaluateReadingMultipleChoiceMultiAsync` — set comparison, missed/false-positive detection, option-level feedback |
| `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` | New `activity_generate_reading_multiple_choice_multi` prompt; constant and seed call |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Added `reading_multiple_choice_multi` to `StagedPatternKeys` |
| `src/LinguaCoach.Web/.../renderers/reading-multiple-choice-multi/` | New `ReadingMultipleChoiceMultiComponent` with checkbox-style multi-select |
| `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.ts` | Import, getter, handler, payload union extended |
| `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.html` | Added `@case ('multipleChoiceMulti')` |
| `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` | Added `'multipleChoiceMulti'` to `InteractionMode` union |
| `tests/.../Sessions/ExerciseTypeCatalogTests.cs` | New `ReadingMultipleChoiceMulti_IsReadyAndEligible`; updated `OtherPlannedReadingTypes_RemainUnchanged` |
| `tests/.../Sessions/ExercisePatternPhase1Tests.cs` | Pattern counts updated: 11→12, 10→11 |
| `tests/.../Domain/InteractionModeMarkingModeTests.cs` | Pin for `MultipleChoiceMulti = 12`; count 12→13 |
| `tests/.../Activity/KeyedSelectionEvaluatorTests.cs` | 5 new evaluator tests |
| `tests/.../Activity/ModuleStageContentValidatorTests.cs` | 1 valid + 4 missing-key + 7 forbidden-learn-key tests |
| `tests/.../practice/practice-gym.component.spec.ts` | Multi fixture + 2 new tests |
| `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` | Phase 8B section appended |
| `docs/architecture/learning-activity-engine.md` | Runnable planned formats table + Phase 8B entry |
| `docs/handoffs/current-product-state.md` | Reading/multiple-choice rows updated; Phase 8A/8B summary |

## Evaluator Design

The multi-choice evaluator uses set comparison:
- Parse `correctOptionIds` from `practiceContent.exerciseData`
- Parse `selectedOptionIds` from submitted answer JSON
- `passed = selected_set == correct_set` (exactly)
- Identifies: missed correct options, false positives
- Includes per-option explanations from `optionExplanations` in feedback
- Handles null/empty submission and invalid JSON safely

## CI/CD Verification

| Check | Result |
|-------|--------|
| `git diff --check` | PASS |
| `dotnet restore` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors) |
| Backend unit tests | 621/621 PASS |
| Backend integration tests | 475/475 PASS |
| Architecture tests | 3/3 PASS |
| Angular unit tests | 106/106 PASS |
| Angular production build | PASS |
| Playwright | Not run (no browser/gstack in this environment) |

## Scope Boundaries

- Only `reading_multiple_choice_multi` became runnable
- All other planned future exercise formats remain `planned` and non-generation-eligible
- No audio formats implemented
- Today pre-generation not implemented
- MinIO/audio lifecycle not implemented
- No `/activity`, `exerciseType=`, `type=`, `pattern=` compatibility broken
- No student data or activity history changes

## Risks / Unresolved Questions

- Playwright e2e not run (no browser in this environment); recommend running in local gstack environment before deploy
- `MultipleChoiceMulti = 12` is a new enum value persisted as integer in `exercise_patterns.interaction_mode`; a migration is not required since the column already allows any int, but the seeder will update any existing rows on startup

## Next Recommended Action

Phase 8C candidate: `reading_fill_in_blanks` or `reorder_paragraphs`. Both have deterministic evaluation shapes and are already in the catalog as `Planned`. Follow the same recipe: pattern → catalog → validator → evaluator → prompt → renderer.
