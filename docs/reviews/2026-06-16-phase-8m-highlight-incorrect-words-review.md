# Phase 8M — Highlight Incorrect Words Implementation Review

- **Title**: Phase 8M — `highlight_incorrect_words` listening exercise
- **Date**: 2026-06-16
- **Related sprint**: [Staged Activity Content Migration Sprint](../sprints/2026-06-15-staged-activity-content-migration-sprint.md) (Phase 8M)
- **Related feature**: Exercise Pattern Engine — runnable listening formats

## Summary

`highlight_incorrect_words` is the sixth runnable listening-primary format and the second runnable listening+reading format. The student listens to a short audio script (30-60 words) while reading a `displayTranscript` of the same passage in which 2-4 words have been changed. The transcript renders as clickable word tokens; the student toggles the tokens that differ from the audio and submits `selectedTokenIds`. Evaluation is deterministic: the submitted set is compared against `incorrectTokenIds`, with exact match scoring full marks and partial answers reporting correctly-found, missed, and false-positive tokens plus per-token corrections and explanations. No AI call is made for evaluation. `audioUrl` is always null; the UI falls back to showing `audioScript` as text. Patterned directly on Phases 8H-8L.

## Files reviewed / changed

### Backend
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `HighlightIncorrectWords`.
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added `HighlightIncorrectWords = 18`.
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted `highlight_incorrect_words` Planned → Ready.
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added pattern row.
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added key to `StagedPatternKeys`.
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added prompt key, prompt content, and registration (maxInput:1400, maxOutput:1500).
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — required practice keys + forbidden learn keys.
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` — new `EvaluateHighlightIncorrectWordsAsync` branch + DTOs.

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` — `'highlightIncorrectWords'` interaction mode.
- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-incorrect-words/highlight-incorrect-words.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-incorrect-words/highlight-incorrect-words.component.html` (new)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — import, payload kind, imports array, content getter, submit handler.
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html` — `@case ('highlightIncorrectWords')`.
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` — payload mapping.
- `src/LinguaCoach.Web/src/app/features/activity/presenters/pattern-backed.presenter.ts` — Listening skill badge.

### Tests
- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — value 18 + count 19.
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs` — exact/missing/false-positive/duplicate+unknown/empty/invalid.
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — valid/missing-required/forbidden-learn.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — ready+eligible; write_from_dictation/summarize_spoken_text remain non-runnable; removed key from still-planned list.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern count 22 → 23.
- `tests/LinguaCoach.IntegrationTests/Api/PracticeGymNextEndpointTests.cs` — `GET /api/activity/next?exerciseType=highlight_incorrect_words` returns 200 module_stage_v1.
- `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs` — seeded prompt key + fake AI exerciseData (displayTranscript/tokens/incorrectTokenIds/corrections/tokenExplanations).
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` — ready type + routes.
- `src/LinguaCoach.Web/src/app/features/activity/presenters/pattern-backed.presenter.spec.ts` — listening badge.
- `src/LinguaCoach.Web/e2e/exercise-pattern-renderers.spec.ts` — full e2e flow.

## Decisions

- **Evaluator reuse**: extended the existing `KeyedSelectionEvaluator` (MarkingMode.KeyedSelection) with a dedicated token-set branch rather than a new evaluator class, consistent with how `highlight_correct_summary` reused the single-choice handler. Token-set semantics (missed/false-positive tracking) differ enough from option-id multi-select to warrant their own method and DTOs.
- **Audio**: `audioUrl` always null; text fallback only. No MinIO or TTS lifecycle.
- **UI**: button-like token toggles, no drag/drop, per the constraint.
- **Fake AI fixture**: the integration fake AI returns one combined JSON; added the new required fields so generation validates for this pattern.

## Findings

No defects found. Two test-baseline updates were required and applied: the pattern-count assertion (now 23) and the fake-AI exerciseData (new required keys), both expected when promoting a new pattern.

## Risks / unresolved

- None. The deterministic evaluator handles empty, duplicate, unknown, and invalid submissions safely.

## Confirmations

- ONLY `highlight_incorrect_words` was made runnable this phase.
- `write_from_dictation` and `summarize_spoken_text` remain planned/non-runnable; all speaking formats remain planned/non-runnable.
- No MinIO or audio lifecycle introduced.
- No Today pre-generation introduced.
- `/activity`, `exerciseType=`, `type=`, `pattern=` compatibility preserved.

## Final verdict

Complete and green. Phases 8H-8M are all done.

## Next recommended action

Phase 8N — `write_from_dictation` (the last simpler listening format before `summarize_spoken_text`).
