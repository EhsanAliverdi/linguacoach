# Phase 8H Implementation Review — listening_multiple_choice_single

**Date:** 2026-06-15
**Related sprint:** [2026-06-15-staged-activity-content-migration-sprint.md](../sprints/2026-06-15-staged-activity-content-migration-sprint.md) (Phase 8H section)
**Related architecture:** [learning-activity-engine.md](../architecture/learning-activity-engine.md) (no structural change required)

## Files reviewed / changed

- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — new `ListeningMultipleChoiceSingle` constant
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new pattern entry
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — Planned → Ready
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — required practice keys
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — StagedPatternKeys
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` — dispatch + shared evaluation made pattern-agnostic
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — new generation prompt
- `src/LinguaCoach.Web/.../reading-multiple-choice/reading-multiple-choice.component.ts` and `.html` — audio/scenario support, optional passage
- `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.ts` — `readingMultipleChoiceContent` getter now reads staged `exerciseData`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern count 17 → 18
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — new ready/eligible test, removed from `stillPlanned`, fixed `Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows`
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — new validator tests
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs` — new evaluator tests
- `src/LinguaCoach.Web/.../practice/practice-gym.component.spec.ts` — new fixture + 2 tests

## Findings (by priority)

### High
None.

### Medium
- **Audio fallback shows the transcript before submission.** `audioUrl` is always `null` from the generation prompt (no TTS pipeline wires it up for staged content yet). The frontend falls back to rendering `audioScript` as plain text labelled "Audio is temporarily unavailable." This means the spoken content is visible before the student answers — but `question`, `options`, and `correctOptionId` remain hidden until submission, so the assessment task itself (choosing the correct answer) is not pre-revealed. This is a deliberate, documented tradeoff to avoid blocking the format on MinIO/TTS infrastructure. Future work: once audio generation exists, `audioUrl` will be populated and the fallback path becomes the no-audio edge case only.

### Low
- Fixed a latent bug in `exercise-renderer.component.ts`'s `readingMultipleChoiceContent` getter: it previously read `passage`/`options`/`question`/etc. from the top-level `raw` content instead of `practiceContent.exerciseData` for module_stage_v1 staged content. Fixed with a fallback chain (`ed[...] ?? raw[...]`) — additive and backward compatible, benefits `reading_multiple_choice_single` too.

## Decisions made

- Reuse `InteractionMode.MultipleChoice` and the existing `ReadingMultipleChoiceComponent`/renderer rather than create a new component — extended with optional audio/scenario fields.
- `KeyedSelectionEvaluator`'s reading-single-choice evaluation method made pattern-agnostic (dispatches on `request.ExercisePatternKey` for wording only: "passage" vs "audio").
- `aiEvaluatePromptKey` set on the pattern row for metadata-completeness consistency with `reading_multiple_choice_single`, but no evaluate prompt seeded — evaluation is fully deterministic (`KeyedSelectionEvaluator`, no AI call).
- No new Angular spec file created for the renderer component (none existed before this phase either).

## AskUserQuestion decision summary

None — no AskUserQuestion was needed this phase.

## Implementation tasks produced

None outstanding — all listed backend/frontend/test/doc tasks completed in this phase.

## Risks / unresolved questions

- The audio-transcript-fallback tradeoff (see Medium finding above) should be revisited once TTS/audio asset generation lands for staged content.
- `listening_multiple_choice_multi` and other planned listening formats are natural next candidates but out of scope for this phase.

## Final verdict

Phase 8H complete. `listening_multiple_choice_single` is the first runnable listening-primary format, generation-eligible, Practice-Gym-enabled, deterministically evaluated. All hard constraints honored: no MinIO/audio lifecycle changes, no Today pre-generation, no other planned formats made runnable, `/activity` compatibility preserved, CI/CD green.

## Next recommended action

Consider `listening_multiple_choice_multi` (Phase 8I) as the next runnable listening format, following the same staged pattern and reusing `MultipleChoiceMulti` interaction mode (already used by `reading_multiple_choice_multi`).
