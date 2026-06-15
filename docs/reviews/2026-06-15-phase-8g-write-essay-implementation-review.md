# Phase 8G — Write Essay Implementation Review

Date: 2026-06-15
Related sprint: [docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md](../sprints/2026-06-15-staged-activity-content-migration-sprint.md) (Phase 8G)
Related handoff: [docs/handoffs/current-product-state.md](../handoffs/current-product-state.md)

## Summary

Made `write_essay` the seventh runnable planned future exercise format, following the
Phase 8 pattern established by 8A-8F. Student reads an essay prompt (with optional
topic/context) and writes a structured response; AI evaluates task response, structure,
idea development, grammar/vocabulary, coherence, and tone via `AiStructuredEvaluator`.

## Files reviewed / changed

### Domain / Persistence
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `WriteEssay = "write_essay"`.
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new pattern entry
  (`ActivityType.WritingScenario`, `InteractionMode.FreeTextEntry`, `MarkingMode.AiStructured`,
  `compatibleKindsJson: [4]` WritingTask, 10 min estimate).
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — `write_essay` promoted
  from `Planned` to `Ready`.
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — seeded
  `activity_generate_write_essay` (maxInput:1000, maxOutput:1600) and
  `activity_evaluate_write_essay` (maxInput:2000, maxOutput:1400).

### Application
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
  - Added required practice keys for `write_essay`: `["prompt", "topic"]`.
  - Extended forbidden learnContent keys with `modelEssay`, `expectedEssay`, `submittedEssay`.
  - **Fixed pre-existing Phase 8F bug**: removed `keyPoints` (a standard learnContent
    field present in nearly every format) and duplicate/overly-broad `answerKey`,
    `textarea`, `submit` entries from `ForbiddenLearnContentKeys`. These were silently
    failing ~17 unit tests across multiple patterns before this phase.

### Infrastructure
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiStructuredEvaluator.cs` —
  `ResolvePromptKey` routes `write_essay` to `activity_evaluate_write_essay`.
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` —
  `StagedPatternKeys` includes `write_essay` (module_stage_v1 generation + validation path).

### Frontend
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
  - `freeTextContent` getter: `situation` falls back to `exerciseData.topic`; added
    `wordCountTarget` mapped from `exerciseData.requirements.targetWordCount`.
  - Removed a duplicate, type-conflicting `wordCountTarget` assignment from `raw['wordLimit']`.
- `src/LinguaCoach.Web/src/app/features/activity/renderers/free-text-entry/free-text-entry.component.ts`
  - `FreeTextEntryContent.wordCountTarget` changed from `number | null` to `string | null`
    (target is a range string, e.g. "180-250 words").
- `src/LinguaCoach.Web/src/app/features/activity/renderers/free-text-entry/free-text-entry.component.html`
  - Displays word-count target next to the live word count.

## Tests added / updated

- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`
  - New `ValidWriteEssayJson` fixture + 3 new tests (valid payload, missing required key
    `prompt`/`topic`, forbidden learnContent keys).
  - Fixed `Validate_SummarizeWrittenText_WithForbiddenKeyInLearnContent_Fails` theory
    (removed stale `keyPoints`/`submit` cases, now-invalid after the validator fix above).
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` —
  pattern-count assertions updated 16→17 / 15→16 (three occurrences).
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`
  - New `WriteEssay_IsReadyAndEligible` test.
  - Replaced stale `WriteEssay_RemainsPlanned` with `OtherPlannedFormats_RemainNonRunnable`
    (asserts the 15 remaining planned formats stay non-runnable).
  - `FutureTypes_AreNotGenerationEligibleUntilReady` now checks `read_aloud` instead of
    `write_essay`.
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` —
  new `readyWriteEssay` fixture object + 2 new tests (available in Practice Gym,
  routes correctly on selection).

## CI/CD verification

| Suite | Result |
|---|---|
| `git diff --check` | clean |
| `dotnet build --configuration Release` | 0 errors, 6 pre-existing unrelated warnings (obsolete `SetTrackRequest`/`SetLearningTrack`) |
| `dotnet test` (UnitTests) | 671/671 passed |
| `dotnet test` (IntegrationTests) | 482/482 passed |
| Angular `ng test` (ChromeHeadless) | 116/116 passed |
| Angular `ng build --configuration production` | succeeds (pre-existing SCSS lint warnings only) |
| Playwright e2e | no per-pattern e2e tests exist for Phase 8 formats (none added in 8B-8F either); not added here, consistent with prior phases |
| Deployment / startup validation | not run — no local deployment environment available in this session |

The 35 integration-test failures reported mid-session were investigated: a full clean
run of `dotnet test tests/LinguaCoach.IntegrationTests` after completing the validator
fix shows **0 failures (482/482)**. Confirmed not a Phase 8G regression.

## Scope constraints confirmed

- Only `write_essay` was made runnable. All other planned formats remain `planned`/non-runnable
  (verified by `OtherPlannedFormats_RemainNonRunnable`).
- No audio/speaking formats touched, no Today pre-generation, no MinIO/audio lifecycle work.
- `/activity` route and `exerciseType=`/`type=`/`pattern=` query param compatibility preserved.
- No "PTE-style" terminology used; "planned future exercise format" used throughout.

## Findings grouped by priority

**High** — Fixed: pre-existing Phase 8F validator bug (`keyPoints` wrongly forbidden in
learnContent) was silently breaking ~17 unit tests across multiple exercise patterns.
Now corrected; full unit suite green.

**None outstanding.**

## Decisions made

- `write_essay` reuses `InteractionMode.FreeTextEntry` (no new enum value needed),
  matching the `summarize_written_text` precedent from Phase 8F.
- `wordCountTarget` represented as a free-form string (e.g. "180-250 words") rather than
  a numeric type, since essay requirements specify ranges, not single numbers.

## Risks / unresolved questions

None identified. Backend and frontend suites fully green.

## Final verdict

Phase 8G complete. `write_essay` is runnable end-to-end (catalog, registry, generation,
validation, evaluation, Learn/Practice/Feedback rendering, Practice Gym). All backend
and frontend automated tests pass.

## Next recommended action

Phase 8H candidate: next planned future exercise format per the catalog (e.g. a
listening-primary or speaking-primary format), following the same module_stage_v1 +
AiStructuredEvaluator pattern established across Phases 8A-8G.
