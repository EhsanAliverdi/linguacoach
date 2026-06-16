# Phase 9F Implementation Review — respond_to_situation Promotion to Ready

**Date:** 2026-06-16
**Sprint / Feature:** Phase 9F — respond_to_situation open speaking format
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
- `src/LinguaCoach.Web/src/app/features/activity/renderers/respond-to-situation/respond-to-situation.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/respond-to-situation/respond-to-situation.component.html` (new)
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
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`

---

## What Was Implemented

`respond_to_situation` promoted from Planned to Ready as a runnable open speaking format.

The student reads a short real-life situation and types/speaks an appropriate response. AI evaluation assesses relevance, clarity, tone, and natural phrasing.

---

## Submission Shape

```json
{
  "items": [
    { "itemId": "sit1", "answerText": "student typed response" }
  ]
}
```

Identical shape to `answer_short_question`, `read_aloud`, and `repeat_sentence`. Routed through the existing `activity-lesson` submission flow.

---

## Evaluation Approach and Limitations

- Uses `AiOpenEndedEvaluator` (MarkingMode = AiOpenEnded).
- Routes to `activity_evaluate_respond_to_situation` prompt via `ResolvePromptKey`.
- Evaluates: relevance to situation, clarity, completeness, natural phrasing, tone/politeness. Grammar is secondary.
- Returns overall score (0–100), per-item feedback, strengths, improvements, and an optional better example.
- **This is NOT pronunciation scoring.** No phoneme analysis, no STT, no fluency metrics. Only the typed text is evaluated.
- **This is NOT real recording.** Text/transcript fallback only. No audio storage, no MinIO lifecycle, no STT provider required.
- AI unavailability returns a graceful fallback (completed=false, coachSummary with retry message).

---

## Content Is Not Hardcoded as Workplace-Only

The AI generate prompt explicitly:
- Varies context by `{{careerContext}}` (can be daily life, travel, social, academic, interview, workplace, etc.)
- States: "Do not assume a workplace-only context unless {{careerContext}} indicates it."
- Instructs varying `contextLabel` across items (e.g. one daily life, one travel).
- `workplaceContext: false` in `ExercisePatternSeeder`.

---

## Confirmation: Only `respond_to_situation` Became Runnable

The following formats remain Planned/non-runnable:
- `describe_image`
- `retell_lecture`
- `summarize_group_discussion`

Confirmed by updated `OtherPlannedFormats_RemainNonRunnable` integration test.

---

## Findings by Priority

### P0 — Critical constraints

1. **InteractionMode append-only enforced.** `RespondToSituation = 24` added at end. Count test updated 24 → 25 values.
2. **ExercisePatternKey append-only enforced.** `RespondToSituation = "respond_to_situation"` added at end of Speaking section.
3. **No STT, no recording, no audio infrastructure.** `audioScript = null`, `audioUrl = null` in generated items. UI shows text fallback only.
4. **AI evaluation, not word-overlap.** Open-ended format — exact match is inappropriate. `AiOpenEndedEvaluator` used.

### P1 — Architecture decisions

5. **AiOpenEndedEvaluator routing extended.** `ResolvePromptKey` switch now maps `respond_to_situation` → `activity_evaluate_respond_to_situation`. All other keys unchanged.
6. **Staged content pattern followed.** Added to `StagedPatternKeys`. Validator enforces `items` array with required fields `id` and `situation`.
7. **Item count:** (1, 1, 2) — default 1 item, max 2. Per spec `CountOverrides`. AI prompt generates exactly 2 situations.
8. **learnContent isolation enforced.** Prompt instructs learnContent to contain only general strategy, never the actual situations or expectedResponseGuidance.

### P2 — Implementation

9. **ExercisePatternSeeder:** new `ExercisePatternDefinition` with `compatibleKinds = [5]` (SpeakingTask), `markingMode = AiOpenEnded`, `requiresAudio = false`, `workplaceContext = false`. Count: 28 → 29 patterns.
10. **Angular renderer:** standalone component. Shows situation text, optional `contextLabel`/`role`/`audience` chips, optional `prompt`, optional `focusAreas` chips, textarea per item (`data-testid="rts-input-{id}"`), submit button (`data-testid="respond-to-situation-submit-btn"`). `canSubmit` gated on at least one non-empty response.
11. **ExerciseRendererComponent:** added `respondToSituationContent` getter (maps `situation`, `contextLabel`, `role`, `audience`, `prompt`, `focusAreas`, `explanation` from `stagedExerciseData`). Added `@case ('respondToSituation')` in HTML.
12. **ActivityLessonComponent:** added `respondToSituation` branch in `submittedContent` assembly.

### P3 — Tests

13. **Unit tests (863 total):** 3 new `AiOpenEndedEvaluatorTests` for respond_to_situation evaluation parsing and staged content unwrapping. 4 new `ModuleStageContentValidatorTests` covering valid content, missing items, missing situation field, learnContent isolation. `InteractionMode_RespondToSituation_IsTwentyFour` + updated count test (24→25).
14. **Integration tests (511 total):** `RespondToSituation_IsNowRunnable`, `OtherPlannedFormats_RemainNonRunnable` updated (removed respond_to_situation), `Seeder_SeedsCountFields` inline data added `(1, 1, 2, 0, 0, 0)`, `ExercisePatternPhase1Tests` counts 28→29 and SpeakingTask 5→6.
15. **Angular tests (161 total):** `readyRespondToSituation` fixture added to `ALL_READY`. 3 new tests: card renders as button, item count shows, navigation to `/activity` works correctly.

---

## Risks / Unresolved Questions

- **AI evaluation latency:** Unlike `repeat_sentence` (deterministic), `respond_to_situation` makes an AI call per submission. If the AI provider is slow, the feedback step will feel slow. No mitigation in this phase — normal for AI-evaluated formats.
- **Item count default = 1:** The spec says default 1, max 2. This is intentionally low to keep AI evaluation cost per session low. Can be raised in a future phase.
- **Context/goal alignment cleanup:** If a broader student goal infrastructure is added (e.g. a student profile with explicit learning goals), the generate prompt's `{{careerContext}}` variable should be mapped to it. This is deferred.

---

## AskUserQuestion Answers

None required.

---

## Implementation Tasks Produced

All tasks completed in this session. No follow-on tasks created.

---

## Final Verdict

Implementation complete. All CI gates pass:
- Unit tests: 863 passed, 0 failed
- Integration tests: 511 passed, 0 failed
- Angular tests: 161 passed, 0 failed

`respond_to_situation` is fully promoted to Ready. All other planned speaking formats remain non-runnable.

---

## Documentation Impact

- Docs reviewed: `docs/architecture/learning-activity-engine.md`, prior phase reviews
- Docs updated: this review doc (new)
- Docs intentionally not updated: `docs/architecture/learning-activity-engine.md` — no architectural direction changed. `AiOpenEndedEvaluator` already supported open-ended patterns; this phase only adds a new routing key.

---

## Next Recommended Action

Phase 9G: promote `describe_image` (requires image support) or `retell_lecture` (requires audio) — or add student goal/profile context alignment for richer situation generation.
