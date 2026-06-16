# Phase 9G Implementation Review — describe_image Promotion to Ready

**Date:** 2026-06-16
**Sprint / Feature:** Phase 9G — describe_image open speaking format
**Related sprint doc:** docs/sprints/ (Phase 9 series)

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs`
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiOpenEndedEvaluator.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`

### Angular
- `src/LinguaCoach.Web/src/app/features/activity/renderers/describe-image/describe-image.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/describe-image/describe-image.component.html` (new)
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

`describe_image` promoted from Planned to Ready as a runnable open speaking format.

The student reads a detailed image prompt (a text description of a scene) and types or speaks a description of what they see. AI evaluation assesses relevance, detail, organisation, vocabulary range, and clarity.

No real image upload, no image storage, no computer-vision scoring, and no STT pipeline were created. This phase uses text/image-prompt representation only.

---

## Submission Shape

```json
{
  "items": [
    { "itemId": "img1", "answerText": "student typed description" }
  ]
}
```

Identical shape to `answer_short_question`, `read_aloud`, `repeat_sentence`, and `respond_to_situation`. Routed through the existing `activity-lesson` submission flow.

---

## Evaluation Approach and Limitations

- Uses `AiOpenEndedEvaluator` (MarkingMode = AiOpenEnded).
- Routes to `activity_evaluate_describe_image` prompt via `ResolvePromptKey`.
- Evaluates: relevance to image prompt, amount of detail, organisation, vocabulary range, clarity, and natural spoken description style. Grammar is secondary.
- Returns overall score (0–100), per-item feedback, strengths, improvements, and an optional better example.
- **This is NOT computer-vision scoring.** No image recognition, no visual analysis. Only the typed text description is evaluated against the AI-generated image prompt.
- **This is NOT real recording.** Text/transcript fallback only. No audio storage, no MinIO lifecycle, no STT provider required.
- **No real image URLs** are generated or stored. `imageUrl` is always `null` in generated content.
- AI unavailability returns a graceful fallback (completed=false, coachSummary with retry message), inherited from the existing `AiOpenEndedEvaluator` pattern.

---

## Image Rendering Behaviour and Fallback Behaviour

- The Angular renderer (`DescribeImageComponent`) checks `item.imageUrl`.
- **If `imageUrl` is present and non-null:** renders an `<img>` tag with the URL and accessible alt text. (Not expected in this phase since the generator always sets `imageUrl: null`.)
- **If `imageUrl` is absent or null:** renders a visual prompt card (`data-testid="di-image-prompt-{id}"`) displaying `item.imagePrompt` text with a left border accent in the speaking colour, and `item.imageDescription` as an italic accessibility caption if present.
- Context label chips, focus area chips, and coaching tip are shown if present.
- Textarea fallback per item (`data-testid="di-input-{id}"`), submit button (`data-testid="describe-image-submit-btn"`). `canSubmit` gated on at least one non-empty response.

---

## Content Is Not Hardcoded as Workplace-Only

The AI generate prompt explicitly:
- Varies context by `{{careerContext}}` (can be daily life, travel, social, nature, city, academic, interview, workplace, etc.)
- States: "Content should suit the student's learning goals — which may include day-to-day English, travel, social conversation, academic English, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it."
- Uses `contextLabel` to label scenes (e.g. Daily life, Travel, Social, Nature, City, Study, Workplace, Interview).
- `workplaceContext: false` in `ExercisePatternSeeder`.

---

## Confirmation: Only `describe_image` Became Runnable

The following formats remain Planned/non-runnable:
- `retell_lecture`
- `summarize_group_discussion`

Confirmed by updated `OtherPlannedFormats_RemainNonRunnable` integration test.

---

## Findings by Priority

### P0 — Critical constraints

1. **InteractionMode append-only enforced.** `DescribeImage = 25` added at end. Count test updated 25 → 26 values.
2. **ExercisePatternKey append-only enforced.** `DescribeImage = "describe_image"` added at end of Speaking section.
3. **No STT, no recording, no audio infrastructure.** `imageUrl = null`, `audioUrl = null` (not present) in generated items. UI shows text fallback only.
4. **AI evaluation, not exact-match.** Open-ended format — `AiOpenEndedEvaluator` used with new `activity_evaluate_describe_image` prompt.
5. **No image storage or media pipeline.** `imageUrl` is always null in this phase. No MinIO, no file storage lifecycle added.

### P1 — Architecture decisions

6. **AiOpenEndedEvaluator routing extended.** `ResolvePromptKey` switch now maps `describe_image` → `activity_evaluate_describe_image`. All other keys unchanged.
7. **Staged content pattern followed.** Added to `StagedPatternKeys`. Validator enforces `items` array with required fields `id` and `imagePrompt`.
8. **Item count:** (1, 1, 1) — 1 item per practice. AI prompt generates exactly 1 image prompt. Per spec.
9. **learnContent isolation enforced.** Prompt instructs learnContent to contain only general image-description strategy, never the actual image prompt or expectedResponseGuidance.

### P2 — Implementation

10. **ExercisePatternSeeder:** new `ExercisePatternDefinition` with `compatibleKinds = [5]` (SpeakingTask), `markingMode = AiOpenEnded`, `requiresAudio = false`, `workplaceContext = false`. Count: 29 → 30 patterns.
11. **Angular renderer:** standalone component. Shows image URL if present (safe `<img>`), otherwise shows visual prompt card from `imagePrompt` text. Shows `imageDescription`, optional `contextLabel`, `displayTitle`, `focusAreas` chips, `explanation` tip. Textarea per item (`data-testid="di-input-{id}"`), submit button (`data-testid="describe-image-submit-btn"`).
12. **ExerciseRendererComponent:** added `describeImageContent` getter and `onDescribeImageSubmitted`. Added `@case ('describeImage')` in HTML.
13. **ActivityLessonComponent:** added `describeImage` branch in `submittedContent` assembly.

### P3 — Tests

14. **Unit tests (871 total):** 3 new `AiOpenEndedEvaluatorTests` for describe_image evaluation parsing and staged content isolation. 4 new `ModuleStageContentValidatorTests` covering valid content, missing items, missing imagePrompt field, learnContent isolation. `InteractionMode_DescribeImage_IsTwentyFive` + count test updated 25→26.
15. **Integration tests (513 total):** `DescribeImage_IsNowRunnable` new test added. `OtherPlannedFormats_RemainNonRunnable` updated (removed describe_image, now only retell_lecture and summarize_group_discussion). `Seeder_SeedsCountFields` inline data added `("describe_image", 1, 1, 1, 0, 0, 0)`. `ExercisePatternPhase1Tests` counts 29→30 and SpeakingTask 6→7. `PracticeGymNextEndpointTests.GetNext_WithPlannedExerciseType` updated from `describe_image` to `retell_lecture`. `Registry_ReturnsPlannedDefinitions_ButExcludesThemFromGeneration` updated from `describe_image` to `retell_lecture`. `FutureTypes_AreNotGenerationEligibleUntilReady` updated to remove `describe_image` assertion.
16. **Angular tests (163 total):** `readyDescribeImage` fixture added to `ALL_READY`. `plannedFormat` updated from `describe_image` to `retell_lecture`. 2 new tests: card renders as button, item count shows. Two existing tests referencing `describe_image` as locked updated to use `retell_lecture`.

---

## Risks / Unresolved Questions

- **AI evaluation latency:** `describe_image` makes an AI call per submission. If the AI provider is slow, the feedback step will feel slow. No mitigation in this phase — normal for AI-evaluated formats.
- **No real image pipeline:** The format works with text prompts only. If the product later needs real image display, a separate image storage and generation pipeline will be needed. That is explicitly deferred.
- **Computer-vision scoring not possible:** Evaluation is against the typed description only. The AI cannot see the actual image because no image exists — only a text description.
- **Context/goal alignment cleanup:** If a broader student goal infrastructure is added (e.g. a student profile with explicit learning goals beyond `careerContext`), the generate prompt's `{{careerContext}}` variable should be mapped to it. Deferred.

---

## AskUserQuestion Answers

None required.

---

## Implementation Tasks Produced

All tasks completed in this session. No follow-on tasks created.

---

## Final Verdict

Implementation complete. All CI gates pass:
- Unit tests: 871 passed, 0 failed
- Integration tests: 513 passed, 0 failed
- Angular tests: 163 passed, 0 failed
- Angular production build: succeeded (warnings are pre-existing)

`describe_image` is fully promoted to Ready. All other planned speaking formats remain non-runnable.

---

## Documentation Impact

- Docs reviewed: `docs/architecture/learning-activity-engine.md`, prior phase reviews
- Docs updated: this review doc (new)
- Docs intentionally not updated: `docs/architecture/learning-activity-engine.md` — no architectural direction changed. `AiOpenEndedEvaluator` already supported open-ended patterns; this phase only adds a new routing key and a new format using existing infrastructure.

---

## Next Recommended Action

Phase 9H: promote `retell_lecture` (requires audio infrastructure) or `summarize_group_discussion` (requires audio) — or add student goal/profile context alignment for richer context-aware generation across all speaking formats. Alternatively, build the real image display pipeline to replace text-only image prompts.
