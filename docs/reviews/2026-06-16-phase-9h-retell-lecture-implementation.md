# Phase 9H Implementation Review — retell_lecture Promotion to Ready

**Date:** 2026-06-16
**Sprint / Feature:** Phase 9H — retell_lecture listening + speaking format
**Related sprint doc:** docs/sprints/ (Phase 9 series)

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiOpenEndedEvaluator.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`

### Angular
- `src/LinguaCoach.Web/src/app/features/activity/renderers/retell-lecture/retell-lecture.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/retell-lecture/retell-lecture.component.html` (new)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts`

### Tests
- `tests/LinguaCoach.UnitTests/Activity/AiOpenEndedEvaluatorTests.cs`
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`
- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/PracticeGymNextEndpointTests.cs`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`

---

## What Was Implemented

`retell_lecture` promoted from Planned to Ready as a runnable listening + speaking format.

The student reads or listens to a short lecture (80–150 words), then retells the main ideas in their own words using a text response area. AI evaluation assesses coverage of main ideas, inclusion of supporting details, use of own words, organisation, clarity, and vocabulary.

No real audio recording, no STT pipeline, no audio storage, no MinIO lifecycle, and no new media infrastructure were created.

---

## Submission Shape

```json
{
  "items": [
    { "itemId": "lec1", "answerText": "student typed retelling" }
  ]
}
```

Identical shape to `answer_short_question`, `read_aloud`, `repeat_sentence`, `respond_to_situation`, and `describe_image`. Routed through the existing `activity-lesson` submission flow.

---

## Audio / Script Fallback Behaviour

- `audioUrl` is always `null` in AI-generated content for this phase. No real audio URLs are generated or stored.
- When `audioUrl` is present (future): the Angular renderer shows an HTML5 `<audio>` player with the URL.
- When `audioUrl` is absent or null (current phase): the renderer shows a styled lecture text card (`data-testid="rl-audio-script-{id}"`) with the `audioScript` text and a left-border accent in the listening colour.
- No new audio storage, MinIO lifecycle, or TTS pipeline was created.

---

## Evaluation Approach and Limitations

- Uses `AiOpenEndedEvaluator` (MarkingMode = AiOpenEnded).
- Routes to `activity_evaluate_retell_lecture` prompt via `ResolvePromptKey` in `AiOpenEndedEvaluator`.
- Evaluates: coverage of main ideas, inclusion of key supporting details, use of own words (not verbatim copy), organisation, clarity, vocabulary, and grammar (secondary).
- Returns overall score (0–100), per-item feedback with `missingPoints`, strengths, improvements, optional better example, and a mini-lesson tip.
- **This is NOT pronunciation scoring.** No phoneme-level analysis, no fluency scoring, no STT accuracy scoring.
- **This is NOT real audio analysis.** The evaluator receives the typed text retelling only — not any audio file.
- **No recording pipeline.** Text/transcript fallback only. No microphone access, no audio storage, no MinIO lifecycle required.
- AI unavailability returns a graceful fallback (completed=false, coachSummary with retry message), inherited from the existing `AiOpenEndedEvaluator` pattern.
- If a student copies the lecture word-for-word, the evaluator notes this and encourages paraphrasing but still awards partial credit for coverage.

---

## Content Is Not Hardcoded as Workplace-Only

The AI generate prompt explicitly:
- Varies content by `{{careerContext}}` (can be daily life, travel, health, science, study, social, interview, workplace, etc.).
- States: "Content should suit the student's learning goals — which may include daily life, travel, study, social communication, migration, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it."
- Uses `contextLabel` to label lectures (e.g. Health, Study, Travel, Daily life, Workplace, Science, Social, Interview).
- `workplaceContext: false` in `ExercisePatternSeeder`.

---

## Confirmation: Only `retell_lecture` Became Runnable

The following format remains Planned/non-runnable:
- `summarize_group_discussion`

Confirmed by updated `OtherPlannedFormats_RemainNonRunnable` and `Registry_ReturnsPlannedDefinitions_ButExcludesThemFromGeneration` integration tests (both now target `summarize_group_discussion`).

---

## Findings by Priority

### P0 — Critical constraints

1. **InteractionMode append-only enforced.** `RetellLecture = 26` added at end. Count test updated 26 → 27 values.
2. **ExercisePatternKey append-only enforced.** `RetellLecture = "retell_lecture"` added at end of Speaking section.
3. **No STT, no recording, no audio upload infrastructure.** `audioUrl = null` in all generated items. UI shows text script fallback only.
4. **AI evaluation, not exact-match.** Open-ended format — `AiOpenEndedEvaluator` used with new `activity_evaluate_retell_lecture` prompt.
5. **No audio storage or media pipeline.** No MinIO, no file storage lifecycle, no TTS added.

### P1 — Architecture decisions

6. **AiOpenEndedEvaluator routing extended.** `ResolvePromptKey` switch now maps `retell_lecture` → `activity_evaluate_retell_lecture`. All other keys unchanged.
7. **Staged content pattern followed.** Added to `StagedPatternKeys`. Validator enforces `items` array with required fields `id`, `lectureTitle`, and `audioScript`.
8. **Item count:** (1, 1, 1) — 1 lecture item per practice. AI prompt generates exactly 1 lecture. Per spec.
9. **primarySkill = "listening"** (the primary activity is listening to/reading the lecture). SecondarySkills = `["speaking", "summarizing", "communication"]`.
10. **learnContent isolation enforced.** Prompt instructs learnContent to contain only general retelling strategy, never the actual lecture script, key points, or expected summary guidance.

### P2 — Implementation

11. **ExercisePatternSeeder:** new `ExercisePatternDefinition` with `compatibleKinds = [2, 5]` (ListeningInput, SpeakingTask), `markingMode = AiOpenEnded`, `requiresAudio = false`, `workplaceContext = false`. Pattern count: 30 → 31.
12. **Angular renderer:** standalone component. Shows audio player if `audioUrl` exists. Shows styled lecture script card from `audioScript` text when `audioUrl` is null. Shows `contextLabel`, `lectureTitle`, `lectureTopic`, `focusAreas` chips. Textarea per item (`data-testid="rl-input-{id}"`), submit button (`data-testid="retell-lecture-submit-btn"`).
13. **ExerciseRendererComponent:** added `retellLectureContent` getter and `onRetellLectureSubmitted`. Added `@case ('retellLecture')` in HTML. Added to imports list.
14. **ActivityLessonComponent:** added `retellLecture` branch in `submittedContent` assembly.
15. **activity.models.ts:** added `'retellLecture'` to `InteractionMode` union type.

### P3 — Tests

16. **Unit tests (898 total, +8 new):** 3 new `AiOpenEndedEvaluatorTests` for retell_lecture evaluation parsing and staged content isolation. 4 new `ModuleStageContentValidatorTests` covering valid content, missing items, missing audioScript field, learnContent isolation. `InteractionMode_RetellLecture_IsTwentySix` + count test updated 26→27.
17. **Integration tests (515 total, +2 new):** `RetellLecture_IsNowRunnable` new test added. `OtherPlannedFormats_RemainNonRunnable` updated (now only `summarize_group_discussion`). `Registry_ReturnsPlannedDefinitions_ButExcludesThemFromGeneration` updated to use `summarize_group_discussion`. `Seeder_SeedsCountFields` inline data added `("retell_lecture", 1, 1, 1, 0, 0, 0)`. `ExercisePatternPhase1Tests` counts 30→31 and SpeakingTask 7→8. `PracticeGymNextEndpointTests.GetNext_WithPlannedExerciseType` updated from `retell_lecture` to `summarize_group_discussion`. `Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows` updated to also disable `retell_lecture` (now listening-primary). Pattern deactivation count updated 29→30.
18. **Angular tests (166 total, +3 new):** `readyRetellLecture` fixture added to `ALL_READY`. `plannedFormat` updated from `retell_lecture` to `summarize_group_discussion`. 3 new tests: retell_lecture card renders as button, item count shows, summarize_group_discussion remains locked.

---

## Risks / Unresolved Questions

- **AI evaluation latency:** `retell_lecture` makes an AI call per submission. No mitigation in this phase — normal for AI-evaluated formats.
- **No real audio pipeline:** The format works with text lecture scripts only. If the product later needs real audio playback for lectures, a TTS or audio storage pipeline will be needed. That is explicitly deferred.
- **STT not possible:** Evaluation is against the typed retelling only. The AI evaluates text, not spoken audio.
- **Copy detection is soft:** If a student copies the lecture verbatim, the evaluator notes it but awards partial credit. Stricter copy detection is not in scope for this phase.

---

## AskUserQuestion Answers

None required.

---

## Implementation Tasks Produced

All tasks completed in this session. No follow-on tasks created.

---

## Final Verdict

Implementation complete. All CI gates pass:
- Unit tests: 898 passed, 0 failed
- Integration tests: 515 passed, 0 failed
- Angular tests: 166 passed, 0 failed
- Angular production build: succeeded (warnings are pre-existing)

`retell_lecture` is fully promoted to Ready. `summarize_group_discussion` remains non-runnable.

---

## Documentation Impact

- Docs reviewed: `docs/architecture/learning-activity-engine.md`, prior phase reviews, `docs/reviews/2026-06-16-phase-9g-describe-image-implementation.md`
- Docs updated: this review doc (new)
- Docs intentionally not updated: `docs/architecture/learning-activity-engine.md` — no architectural direction changed. `AiOpenEndedEvaluator` already supported open-ended patterns; this phase only adds a new routing key and a new format using existing infrastructure.

---

## Next Recommended Action

Phase 9I or 9J: promote `summarize_group_discussion` (requires audio/group discussion script infrastructure), or add student goal/profile context alignment for richer context-aware generation across all speaking formats. Alternatively, build a real TTS pipeline for `retell_lecture` audio playback.
