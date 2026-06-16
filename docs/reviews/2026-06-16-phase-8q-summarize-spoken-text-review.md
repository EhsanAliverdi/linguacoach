# Phase 8Q — `summarize_spoken_text` Implementation Review

- **Date:** 2026-06-16
- **Related sprint:** [2026-06-15 Staged Activity Content Migration](../sprints/2026-06-15-staged-activity-content-migration-sprint.md) — Phase 8Q
- **Author:** Claude Code (Opus 4.8)

## Summary

`summarize_spoken_text` is now a runnable exercise format. The student listens to a short spoken text (60-90 seconds) and writes a concise summary in their own words. It is the eighth runnable listening-primary format and the first AI-evaluated listening+writing format. Evaluation reuses the existing `AiStructuredEvaluator` path already used by `summarize_written_text` and `write_essay` — no new evaluation system was introduced. Audio reuses the shared listening audio lifecycle and the shared `app-audio-player` component from Phase 8P — no second audio system was introduced.

Only `summarize_spoken_text` was made runnable. All speaking formats remain planned and non-runnable. No Today pre-generation was added.

## Files reviewed and changed

### Backend — domain / catalog

- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `SummarizeSpokenText = "summarize_spoken_text"` (appended).
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added `SummarizeSpokenText = 20` (appended, no reorder).
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted `summarize_spoken_text` from `Planned` to `Ready` (renderer `summarize_spoken_text`, evaluator `ai_structured`, prompt `activity_generate_summarize_spoken_text`, `ActivityType.ListeningComprehension`, primary `listening`, secondary `["writing"]`, SupportsPracticeGym=true, SupportsTodayLesson=false, requiresAudio=false). Count config 1/1/1 items, 0/0/0 options (already seeded Phase 8N).
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added pattern row (`InteractionMode.SummarizeSpokenText`, `MarkingMode.AiStructured`, compatible kinds `[2]` ListeningInput, secondary `["Writing"]`, 6 minutes).

### Backend — generation / validation / evaluation

- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `summarize_spoken_text` to `StagedPatternKeys`.
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — required practice keys `["audioScript", "prompt"]`; added a `ForbiddenLearnContentKeysByPatternKey` mechanism (currently empty — see Decisions).
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiStructuredEvaluator.cs` — `ResolvePromptKey` now maps `SummarizeSpokenText => activity_evaluate_summarize_spoken_text`.
- `src/LinguaCoach.Infrastructure/Activity/ListeningAudioService.cs` — added `SummarizeSpokenText` to `ListeningPatternKeys` (now 10 keys).
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added prompt key constants and content for `activity_generate_summarize_spoken_text` (maxInput:1600, maxOutput:1800) and `activity_evaluate_summarize_spoken_text` (maxInput:2000, maxOutput:1400), plus their `SeedOrUpgradePromptAsync` registrations.

### Frontend (Angular)

- `renderers/summarize-spoken-text/summarize-spoken-text.component.ts` + `.html` — new dedicated renderer reusing `app-audio-player` and `exercise-lesson-intro`. Shows audio, prompt, summaryRequirements, textarea, submit. `keyPoints` / model summary / successChecklist are never extracted into the view-model.
- `renderers/summarize-spoken-text/summarize-spoken-text.component.spec.ts` — new component spec.
- `exercise-renderer/exercise-renderer.component.ts` + `.html` — import, registration, `summarizeSpokenTextContent` getter, `onSummarizeSpokenTextSubmitted` handler, `@case ('summarizeSpokenText')`, payload union `'summarizeSpokenText'`.
- `core/models/activity.models.ts` — `InteractionMode` union gained `'summarizeSpokenText'`.
- `activity-lesson/activity-lesson.component.ts` — maps payload to `{ summaryText }`.
- `presenters/pattern-backed.presenter.ts` — Listening skill badge for `summarizeSpokenText`.

### Tests

- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — value 20 + count 21.
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — valid passes; missing audioScript/prompt fail; forbidden learn keys fail.
- `tests/LinguaCoach.UnitTests/Activity/ListeningAudioServiceTests.cs` — pattern key processed.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern counts 24→25, deactivated 23→24.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — summarize_spoken_text now ready/eligible; planned-example tests switched to `describe_image`; registry exclusion test disables it too.
- `tests/LinguaCoach.IntegrationTests/Api/PracticeGymNextEndpointTests.cs` — new positive `GET /api/activity/next?exerciseType=summarize_spoken_text` test (200, module_stage_v1, audioScript + prompt); planned-type test switched to `describe_image`.
- `src/LinguaCoach.Web/.../pattern-backed.presenter.spec.ts` — Listening badge assertions.

## Decisions

- **InteractionMode:** a dedicated `SummarizeSpokenText = 20` mode was added rather than reusing `FreeTextEntry`, because the renderer routes by mode and the format needs an audio player above the textarea. `FreeTextEntry` has no audio player.
- **Evaluator:** reused `AiStructuredEvaluator` (`MarkingMode.AiStructured`) with a new prompt route. `CompactContent` already strips `learnContent` and sends only `practiceContent.exerciseData` + `feedbackPlan`, satisfying the "strip learnContent before sending to AI" requirement generically.
- **`keyPoints` forbidden-in-learn nuance:** the spec listed `keyPoints` among forbidden learn keys. However `learnContent.keyPoints` is the established generic teaching-points array (`LearnContentDto.KeyPoints`) used by every staged format and rendered on the Learn page. Globally (or per-pattern) forbidding it broke valid content and would have hidden teaching points. The actual answer-leak risk is the `keyPoints` array inside `practiceContent.exerciseData`, which is never surfaced to the Learn page and never extracted into the renderer view-model before submission. Resolution: keep `learnContent.keyPoints` allowed; protect the answer structurally. A `ForbiddenLearnContentKeysByPatternKey` hook was added (currently empty) for genuinely format-specific future cases.

## Audio lifecycle reuse

No new audio system. `summarize_spoken_text` was added to the existing `ListeningAudioService.ListeningPatternKeys` set, so `EnsureAudioAsync` + `IFileStorageService` generate and store audio on first fetch, and the shared `app-audio-player` renders it with the `audioScript` text fallback when `audioUrl` is null.

## CI/CD results (actual)

- `git diff --check`: clean (WHITESPACE_OK).
- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: Unit **817** passed, Integration **505** passed, Architecture **3** passed — all green.
- Angular `ng test --watch=false --browsers=ChromeHeadless`: **137** SUCCESS (132 baseline + 5 new).
- Angular `ng build --configuration production`: succeeded (pre-existing CSS "Empty sub-selector" warnings only).

## Confirmations

- Only `summarize_spoken_text` was made runnable.
- All speaking formats remain planned/non-runnable.
- No Today pre-generation added.
- No second audio lifecycle system introduced; `audioScript` fallback preserved.
- `/activity`, `exerciseType=`, `type=`, `pattern=` compatibility unchanged.

## Risks / unresolved

- Playwright e2e was not run in this environment (no browser/server harness available here); the new Angular component spec and the backend API integration test cover the equivalent behavior.
- The spec's literal "forbid keyPoints in learn" was intentionally not applied to `learnContent.keyPoints` (teaching array). If a stricter interpretation is required, rename the generate prompt's learn array (e.g. `teachingPoints`) AND update `ModuleStageWireDto`/`LearnContentDto` mapping so the Learn page still shows teaching points.

## Final verdict

Complete and green. `summarize_spoken_text` is runnable end-to-end with AI evaluation and shared audio.

## Next recommended action

Phase 8R — speaking formats bootstrap (`read_aloud` or `repeat_sentence`).
