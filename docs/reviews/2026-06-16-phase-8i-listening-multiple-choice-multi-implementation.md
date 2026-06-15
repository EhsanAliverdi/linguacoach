# Phase 8I — `listening_multiple_choice_multi` Implementation Review

**Date:** 2026-06-16
**Related sprint:** [2026-06-15-staged-activity-content-migration-sprint.md](../sprints/2026-06-15-staged-activity-content-migration-sprint.md)

## Summary

Phase 8I promotes `listening_multiple_choice_multi` from a planned/non-runnable
exercise type to a fully runnable, deterministic, listening-primary exercise
format. Student listens to a short spoken audio script and selects ALL correct
answers (at least two) from four options. This is the second runnable
listening format, following `listening_multiple_choice_single` (Phase 8H), and
reuses both the Phase 8H audio fallback pattern and the Phase 8B multi-answer
deterministic evaluator.

## Files reviewed / changed

Backend:
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `ListeningMultipleChoiceMulti`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new pattern entry
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted `listening_multiple_choice_multi` from `Planned` to `Ready`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — added required practice keys entry
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added to `StagedPatternKeys`
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` — dispatch extended; multi-choice evaluator made pattern-agnostic (passage/audio wording)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — new generation prompt `activity_generate_listening_multiple_choice_multi`

Frontend:
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-multiple-choice-multi/reading-multiple-choice-multi.component.ts` — interface extended with `audioScript`/`audioUrl`/`scenario`, `passage` made optional
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-multiple-choice-multi/reading-multiple-choice-multi.component.html` — added scenario block, conditional passage, audio section with fallback (mirrors Phase 8H)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — `readingMultipleChoiceMultiContent` getter fixed to read from `stagedExerciseData` with `raw` fallback; added audio/scenario fields

Tests:
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern count 18 → 19
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — new ready/eligible test, removed from stillPlanned list, disabled-row test updated
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — valid payload, missing-key, forbidden-learn-key tests
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs` — exact match, missing-correct, false-positive, no-selection, invalid-json tests
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` — ready/eligible fixture + routing test

## Findings by priority

**High:** None.

**Medium:** Confirmed the same latent staged-content bug from Phase 8H
(`reading_multiple_choice_multi`'s `exerciseData` getter reading from `raw`
instead of `practiceContent.exerciseData`) also affected this new format.
Fixed as part of this phase using the established `ed[...] ?? raw[...]`
fallback pattern — additive and backward-compatible.

**Low:** None.

## Decisions made

- Reused `InteractionMode.MultipleChoiceMulti` (no new enum value) — same as `reading_multiple_choice_multi`.
- Reused `ReadingMultipleChoiceMultiExerciseData`/`SubmittedAnswer` DTOs as-is — JSON deserialization tolerates the extra `audioScript`/`audioUrl` fields.
- No AI evaluate-prompt seeded — evaluation is fully deterministic (set comparison of `selectedOptionIds` vs `correctOptionIds`).
- `audioUrl` always `null`; UI falls back to `audioScript` text, identical to Phase 8H.

## AskUserQuestion decision summary

None — no clarifying questions were required; the spec was fully prescriptive.

## Implementation tasks produced

None outstanding — all numbered tasks (1-12) from the spec were completed in this phase.

## Risks / unresolved questions

- None new. Existing risks (no MinIO/audio lifecycle, no Today pre-generation for listening formats) remain documented as future work.

## Final verdict

**Complete.** All required backend and frontend changes implemented, all new
tests added and passing, full CI/CD verification green.

## CI/CD verification results

- `git diff --check`: pass (no whitespace errors)
- `dotnet restore` / `dotnet build --configuration Release`: pass, 0 errors, only 5 pre-existing unrelated obsolete-API warnings
- `dotnet test --configuration Release`: **699 unit / 484 integration / 3 architecture — all green** (was 684/483 before this phase; +15 unit, +1 integration)
- Angular unit tests: **120/120 green** (was 118; +2)
- Angular dev build: success
- Angular production build: success
- Playwright e2e: not run — no running backend/frontend stack available in this environment (consistent with prior phases' documented limitation)
- Deployment/startup validation: not run — no deployment environment available in this session

## Scope confirmations

- Only `listening_multiple_choice_multi` was made runnable in this phase.
- All other planned listening formats (`listening_fill_in_blanks`, `highlight_correct_summary`, `select_missing_word`, `highlight_incorrect_words`, `write_from_dictation`, `summarize_spoken_text`) remain planned/non-runnable.
- No MinIO or audio lifecycle infrastructure was added.
- No Today pre-generation was implemented for any listening format.
- `/activity`, `exerciseType=`, `type=`, and `pattern=` compatibility preserved.

## Next recommended action

Implement the next planned reading/writing-primary or listening-primary
format per product priority — `listening_fill_in_blanks` is a natural next
listening candidate, reusing the same audio-fallback pattern established in
Phases 8H/8I plus the fill-in-blanks evaluator pattern from Phase 8C/8E.
