---
status: current
lastUpdated: 2026-06-16 12:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 8L — Highlight Correct Summary Implementation

**Date:** 2026-06-16
**Related sprint:** [Staged Activity Content Migration](../sprints/2026-06-15-staged-activity-content-migration-sprint.md)
**Format:** `highlight_correct_summary` — fifth runnable listening-primary format, first runnable listening+reading format

## Summary

Implemented `highlight_correct_summary` end-to-end. The student listens to a short
workplace audio script (40-80 words), then chooses the one-sentence summary that
best matches its meaning from four selectable summary cards. Primary skill is
listening; secondary skill is reading. The format reuses the deterministic
single-choice KeyedSelection evaluation path established in Phases 8H and 8K, and
introduces a dedicated frontend renderer component with the listening audio
fallback pattern from Phases 8H-8J. Audio is never pre-generated: `audioUrl` is
always `null` and the UI falls back to showing the `audioScript` transcript.

## Files reviewed and changed

### Backend

- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `HighlightCorrectSummary = "highlight_correct_summary"`.
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added `HighlightCorrectSummary = 17` (appended, no reorder).
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — added required practice keys `["audioScript", "options", "correctOptionId"]` for `highlight_correct_summary`; extended forbidden learn keys with `summaryOptions`, `checkAnswer`.
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` — routed `highlight_correct_summary` to the single-choice handler; extended audio source-noun wording.
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `highlight_correct_summary` to `StagedPatternKeys`.
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added Highlight Correct Summary pattern (`ListeningComprehension`, `HighlightCorrectSummary` interaction mode, `KeyedSelection`, secondary skill Reading, `requiresAudio: false`).
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted `highlight_correct_summary` from `Planned` (`requiresAudio: true`) to `Ready` (`requiresAudio: false`, renderer/evaluator/prompt keys populated, generation-eligible).
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added `ActivityGenerateHighlightCorrectSummaryKey`, the generation prompt content, and the seed call (maxInput:1400, maxOutput:1100).

### Frontend (Angular)

- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-correct-summary/highlight-correct-summary.component.ts` — new component.
- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-correct-summary/highlight-correct-summary.component.html` — new template (audio fallback, question, selectable summary cards, submit).
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — registered component, content getter, payload kind, submit handler.
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html` — added `@case ('highlightCorrectSummary')`.
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` — added `'highlightCorrectSummary'` to the `InteractionMode` union.
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` — mapped the new payload kind to `{ selectedOptionId }`.
- `src/LinguaCoach.Web/src/app/features/activity/presenters/pattern-backed.presenter.ts` — mapped `highlightCorrectSummary` (and `listeningFillInBlanks`) to the Listening skill badge.

### Tests

- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — added value 17 assertion; total count 17 → 18.
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs` — added correct/incorrect/no-selection/invalid-JSON tests.
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — added valid/missing-key/forbidden-learn-key tests.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern counts 21 → 22 (and 20 → 21 for the deactivation case).
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — added `HighlightCorrectSummary_IsReadyAndEligible`; removed `highlight_correct_summary` from the still-planned list; added it to the disabled-listening registry exclusion.
- `tests/LinguaCoach.IntegrationTests/Api/PracticeGymNextEndpointTests.cs` — added `GET /api/activity/next?exerciseType=highlight_correct_summary` returns OK with `module_stage_v1`.
- `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs` — seeded `activity_generate_highlight_correct_summary`; added `options`/`correctOptionId`/`distractorExplanations` to the FakeAiProvider exerciseData.
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` — added ready entry plus available/route tests.
- `src/LinguaCoach.Web/src/app/features/activity/presenters/pattern-backed.presenter.spec.ts` — added listening badge assertions.
- `src/LinguaCoach.Web/e2e/exercise-pattern-renderers.spec.ts` — added the highlight_correct_summary Learn → Practice → submit e2e.

## Findings

No defects found. One infrastructure note: the new generation prompt is longer than
the select_missing_word prompt and exceeded the initial 900-token input budget
(estimated 1125 tokens). Raised `maxInputTokens` to 1400 to fit. The integration
API test surfaced this via `TokenBudgetExceededException`.

## Decisions

- Used a dedicated InteractionMode (`HighlightCorrectSummary = 17`) and a dedicated
  renderer component, per task scope, rather than reusing `multipleChoice` like
  Phase 8K did. The evaluation path still reuses the single-choice KeyedSelection
  handler, so no new backend evaluation logic was added.
- Catalog promotion is seeder-driven via `SyncCatalogMetadata` on startup (same as
  Phases 8A-8K). No standalone EF migration file is required; the `Planned` row is
  upgraded to `Ready` in place.
- `requiresAudio` set to `false` (the Planned row had `true`) to match the
  audioUrl-null fallback behaviour shared by all runnable listening formats.

## CI/CD verification

- `git diff --check` — clean.
- `dotnet build --configuration Release` — succeeded (pre-existing obsolete-API warnings only).
- `dotnet test --configuration Release` — 750 unit, 488 integration, 3 architecture — all green.
- `ng test --watch=false --browsers=ChromeHeadless` — 126 SUCCESS.
- `ng build` — succeeded.
- `ng build --configuration production` — succeeded (pre-existing admin CSS budget warnings only).
- Playwright `exercise-pattern-renderers.spec.ts` + `practice-gym.spec.ts` — 32 passed, including the new highlight_correct_summary e2e.

## Risks / unresolved questions

- None. Audio remains text-fallback only; MinIO/TTS lifecycle and Today
  pre-generation remain explicitly out of scope and unimplemented.

## Confirmations

- Only `highlight_correct_summary` was made runnable in this phase.
- All other planned listening formats (`summarize_spoken_text`,
  `highlight_incorrect_words`, `write_from_dictation`) remain planned and
  non-runnable.
- No MinIO or audio lifecycle implemented.
- No Today pre-generation implemented.
- `/activity`, `exerciseType=`, `type=`, and `pattern=` compatibility preserved.

## Final verdict

Complete. The format is generation-eligible, deterministically evaluated, rendered
with a dedicated component and audio fallback, surfaced in Practice Gym under
Listening, and fully covered by backend, Angular, and Playwright tests.

## Next recommended action / format

Next recommended listening format: `summarize_spoken_text` (listening + writing,
free-text/AI-structured evaluation), or `highlight_incorrect_words` (listening +
reading, multi-token selection). `summarize_spoken_text` reuses the existing
AI-structured writing evaluation path and is the lowest-risk next step.
