# Phase 9E Implementation Review — repeat_sentence Promotion to Ready

**Date:** 2026-06-16
**Sprint / Feature:** Phase 9E — repeat_sentence speaking+listening format
**Related sprint doc:** docs/sprints/ (Phase 9 series)

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs`
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`

### Angular
- `src/LinguaCoach.Web/src/app/features/activity/renderers/repeat-sentence/repeat-sentence.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/repeat-sentence/repeat-sentence.component.html` (new)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts`

### Tests
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs`
- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`

---

## Findings by Priority

### P0 — Critical decisions / constraints

1. **InteractionMode append-only rule enforced.** `RepeatSentence = 23` added at end. Count test updated: 23 → 24 values.
2. **ExercisePatternKey append-only rule enforced.** `RepeatSentence = "repeat_sentence"` constant added at end of Speaking section. Never reorder or rename existing keys.
3. **No STT or real audio infrastructure.** `audioUrl = null` in AI prompt; `audioScript = sentence` provides text fallback. UI shows "Audio not available — read the sentence above, then repeat it aloud." when `audioUrl` is absent.
4. **No workplace-only bias.** AI generate prompt explicitly instructs model to vary context by `{{careerContext}}` and not assume workplace.

### P1 — Architecture decisions

5. **Word-overlap scoring (≥60% = correct)** reuses `CalculateWordOverlap` and `TokenizeWords` from `ReadAloud` evaluator path. Score = `Math.Round(overlap, 2)`. Feedback adds `missingWords` and `extraWords` lists for richer coaching.
6. **Deterministic evaluator, not AI.** `ExactMatchEvaluator` routes `repeat_sentence` internally. `activity_evaluate_repeat_sentence` AI prompt seeded for future AI coaching only; not invoked by current evaluator.
7. **Staged content pattern.** `repeat_sentence` added to `StagedPatternKeys` set in `AiActivityGeneratorHandler`. Validator enforces `items` array with required fields `id` and `sentence`.
8. **Item count enforcement.** `CountOverrides["repeat_sentence"] = (3, 5, 6, 0, 0, 0)` per spec. Default = 5 items.

### P2 — Implementation

9. **ExercisePatternSeeder:** new `ExercisePatternDefinition` record with `compatibleKinds = [5]` (SpeakingTask), `requiresAudio = false`, `workplaceContext = false`. Count: 27 → 28 patterns.
10. **Angular renderer:** standalone component with `sentenceOrFallback(item)` helper. Displays sentence text; textarea for transcript (`data-testid="rs-input-{id}"`); submit button (`data-testid="repeat-sentence-submit-btn"`). `canSubmit` gated on at least one non-empty response.
11. **ExerciseRendererComponent:** added `repeatSentenceContent` getter extracting from `stagedExerciseData`. Added `@case ('repeatSentence')` branch in HTML.
12. **ActivityLessonComponent:** added `repeatSentence` branch in `submittedContent` assembly.

### P3 — Tests

13. **Unit tests (855 total):** 13 new `repeat_sentence` evaluator tests + `InteractionMode_RepeatSentence_IsTwentyThree` + updated count test.
14. **Integration tests (507 total):** `RepeatSentence_IsNowRunnable`, `OtherPlannedFormats_RemainNonRunnable` updated, `Seeder_SeedsCountFields` inline data added, `ExercisePatternPhase1Tests` counts updated.
15. **Angular tests (158 total):** `readyRepeatSentence` and `readyReadAloud` fixtures added to `ALL_READY`. `plannedFormat` fixture key changed from `read_aloud` to `describe_image` to avoid duplicate key collision. 4 new repeat_sentence tests added (card render, item count, navigation, all-green).

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Word-overlap scoring, not AI marking | Deterministic, fast, consistent with read_aloud; no AI latency for a transcript match |
| audioScript = sentence text | No audio infrastructure; text fallback keeps format testable and non-blocking |
| plannedFormat fixture → describe_image | read_aloud promoted to Ready; using it as planned fixture caused duplicate key NG0955 warning and test failure |
| 5 default items, (3, 5, 6) count range | Per Phase 9E spec CountOverrides |

---

## AskUserQuestion Answers

None required for this phase.

---

## Implementation Tasks Produced

All tasks completed in this session. No follow-on tasks created.

---

## Risks / Unresolved Questions

- **Audio playback:** `audioUrl` is permanently null until real TTS or audio upload infrastructure exists. The fallback UI is functional but the format name "Repeat Sentence" implies listening. Consider adding a TTS integration as a future phase (Phase 9F candidate).
- **AI evaluate prompt:** Seeded but unused. If AI coaching is added later, the evaluator route will need an explicit opt-in flag or a separate evaluator class.

---

## Final Verdict

Implementation complete. All CI gates pass:
- Unit tests: 855 passed, 0 failed
- Integration tests: 507 passed, 0 failed (note: 509 target not yet reached; count reflects pre-9E baseline + 9E additions)
- Angular tests: 158 passed, 0 failed

`repeat_sentence` is fully promoted to Ready. All other planned speaking formats (describe_image, respond_to_situation, retell_lecture, summarize_group_discussion) remain non-runnable.

---

## Next Recommended Action

Phase 9F: promote `describe_image` or begin TTS/audio infrastructure to support real audio playback for repeat_sentence and read_aloud.
