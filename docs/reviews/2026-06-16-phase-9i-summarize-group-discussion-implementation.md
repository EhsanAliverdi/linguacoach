---
title: Phase 9I — summarize_group_discussion Implementation Review
date: 2026-06-16
sprint: Phase 9I
status: complete
owner: engineering
---

# Phase 9I — summarize_group_discussion Implementation Review

## Date

2026-06-16

## Related sprint / feature

Phase 9I — final planned speaking/listening format promotion

## What was implemented

`summarize_group_discussion` was promoted from `Planned` to `Ready` and made fully runnable as the last planned speaking/listening format in the current catalog.

### Backend changes

| File | Change |
|---|---|
| `ExercisePatternKey.cs` | Added `SummarizeGroupDiscussion = "summarize_group_discussion"` constant |
| `InteractionMode.cs` | Added `SummarizeGroupDiscussion = 27` (append-only, database-safe) |
| `ExercisePatternSeeder.cs` | Added `ExercisePatternDefinition` for `summarize_group_discussion` (ListeningInput + SpeakingTask kinds, `AiOpenEnded` marking, 7 min) |
| `ExerciseTypeDefinitionSeeder.cs` | Promoted from `Planned()` to `Ready()` with `primarySkill: listening`, `secondarySkills: ["speaking", "summarizing", "communication"]`, `ai_open_ended` evaluator, `ActivityType.SpeakingRolePlay` |
| `ModuleStageContentValidator.cs` | Added `items` as required practice key, `["id", "discussionTitle", "audioScript"]` as required item fields, `items` as item count array |
| `AiActivityGeneratorHandler.cs` | Added `"summarize_group_discussion"` to supported pattern key list |
| `AiOpenEndedEvaluator.cs` | Added prompt key routing: `SummarizeGroupDiscussion => "activity_evaluate_summarize_group_discussion"` |
| `DefaultAiSeeder.cs` | Added `ActivityGenerateSummarizeGroupDiscussionKey` and `ActivityEvaluateSummarizeGroupDiscussionKey` constants, generate prompt, evaluate prompt, and `SeedOrUpgradePromptAsync` registrations |

### Angular changes

| File | Change |
|---|---|
| `activity.models.ts` | Added `'summarizeGroupDiscussion'` to `InteractionMode` union type |
| `summarize-group-discussion.component.ts` | New standalone renderer component with `SummarizeGroupDiscussionContent`, `SummarizeGroupDiscussionItem`, `SummarizeGroupDiscussionSpeaker`, `SummarizeGroupDiscussionAnswer` interfaces |
| `summarize-group-discussion.component.html` | Full template: discussion title/topic/context label, speaker list, audio player (when `audioUrl` available), `audioScript` fallback block, focus area chips, text input per item, submit button |
| `exercise-renderer.component.ts` | Import + `imports[]` entry, `summarizeGroupDiscussionContent` getter, `onSummarizeGroupDiscussionSubmitted` handler, `ExerciseAnswerPayload` union updated |
| `exercise-renderer.component.html` | `@case ('summarizeGroupDiscussion')` block added |

## Submission shape

```json
{
  "items": [
    {
      "itemId": "disc1",
      "answerText": "Student's typed summary of the discussion"
    }
  ]
}
```

Submitted via the existing activity lesson flow through `ExerciseAnswerPayload { kind: 'summarizeGroupDiscussion' }`.

## Audio / script fallback behaviour

- When `audioUrl` is a non-null string: an `<audio controls>` player is rendered with the URL as the source.
- When `audioUrl` is null (the default for all generated content): the `audioScript` text is displayed in a styled fallback block labelled "Discussion".
- No real audio URLs are generated. The AI generation prompt sets `audioUrl: null` explicitly.
- No new audio storage, MinIO lifecycle, TTS generation, or media pipeline was created.

## Evaluation approach and limitations

Evaluation uses the existing `AiOpenEndedEvaluator` infrastructure via a dedicated `activity_evaluate_summarize_group_discussion` prompt.

The evaluator assesses:
- Coverage of main discussion points
- Recognition of speaker views or roles where relevant
- Inclusion of agreements, disagreements, or outcome where present
- Organisation and logical flow
- Clarity and vocabulary use
- Grammar as a secondary consideration

**What is NOT scored:**
- Pronunciation, fluency, or accent
- Phoneme-level accuracy
- Speaker diarization
- Audio quality
- STT (speech-to-text) accuracy — the system evaluates typed text only

All evaluation limitations are noted in the prompt and result. The evaluator returns `overallScore`, `coachSummary`, `strengths`, `improvements`, `missingExpectedPoints`, `itemResults` (per-item `isCorrect`, `score`, `feedback`, `missingPoints`, `betterExample`), `suggestedImprovedResponse`, `miniLesson`, and `nextImprovementStep`.

## Confirmation: only summarize_group_discussion became runnable

`summarize_group_discussion` was the only format whose `ImplementationStatus` changed from `planned` to `ready` in this phase. All other catalog entries were unchanged.

The integration test `AllSpeakingAndListeningTypes_AreNowReady_NoPlannedRemain` confirms no planned types remain in the catalog after Phase 9I.

## Confirmation: all planned speaking formats are now complete

After Phase 9I, **no planned speaking or listening exercise types remain** in the catalog. The complete list promoted across Phases 9A–9I:

| Phase | Format |
|---|---|
| 9A | `answer_short_question` |
| 9D | `read_aloud` |
| 9E | `repeat_sentence` |
| 9F | `respond_to_situation` |
| 9G | `describe_image` |
| 9H | `retell_lecture` |
| 9I | `summarize_group_discussion` |

## Confirmation: not hardcoded as workplace-only

The AI generation prompt explicitly states:

> "Content should suit the student's learning goals — which may include daily life, travel, study, social communication, migration, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless `{{careerContext}}` indicates it."

The `contextLabel` field supports: Social, Study, Travel, Daily life, Workplace, Health, Interview, and others. The `workplaceContext` flag on the `ExercisePatternDefinition` is `false`.

## Confirmation: no new audio/storage/STT/diarization pipeline

No new audio storage, MinIO lifecycle, TTS pipeline, STT provider, speaker diarization, phoneme scoring, or recording infrastructure was created. The format reuses the existing `audioScript` / `audioUrl` convention identical to `retell_lecture`.

## Confirmation: existing endpoints and contracts preserved

- `/activity` endpoint: unchanged
- `exerciseType=` query parameter: unchanged
- `type=` parameter: unchanged
- `pattern=` parameter: unchanged
- Practice Gym v2 start flow: unchanged
- All existing Ready formats: remain runnable (confirmed by integration tests)
- `ActivityDto.interactionMode` / `ExerciseAnswerPayload` union: backward compatible (new member only)

## Tests added / updated

### Backend unit tests

| File | Tests added |
|---|---|
| `ModuleStageContentValidatorTests.cs` | 4 new: valid content passes, missing items fails, missing audioScript fails, missing discussionTitle fails, learnContent-only passes |
| `InteractionModeMarkingModeTests.cs` | 2 updated: `SummarizeGroupDiscussion_IsTwentySeven`, count updated to 28 |

### Backend integration tests

| File | Change |
|---|---|
| `ExerciseTypeCatalogTests.cs` | Removed `OtherPlannedFormats_RemainNonRunnable` (now Ready); added `SummarizeGroupDiscussion_IsNowRunnable`, `AllPlannedSpeakingListeningFormats_AreNowReady`, `AllSpeakingAndListeningTypes_AreNowReady_NoPlannedRemain`; updated `Registry_SummarizeGroupDiscussion_IsNowEligible`; added `[InlineData]` for item count assertion; added `summarize_group_discussion` disable in `Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows` |
| `ExercisePatternPhase1Tests.cs` | Pattern counts updated: total 31→32, active 30→31; SpeakingTask kind count 8→9 with `SummarizeGroupDiscussion` assertion |
| `PracticeGymNextEndpointTests.cs` | Renamed test to `GetNext_WithUnknownExerciseType_ReturnsSafeNoActivity`; uses `nonexistent_future_format` key instead of the now-Ready `summarize_group_discussion` |

### Angular tests

| File | Tests added |
|---|---|
| `summarize-group-discussion.component.spec.ts` | 12 new tests: title, topic, audio script, no audio player when null, speakers list, context label, textarea input, submit button disabled/enabled, payload keyed by itemId, no emit when blank, disabled prop, audio player when URL provided, no script when URL provided |
| `practice-gym.component.spec.ts` | `readySummarizeGroupDiscussion` fixture added; added to `ALL_READY`; replaced locked tests with runnable tests for `summarize_group_discussion`; `plannedFormat` renamed to `some_future_format` |

## Final test counts

| Suite | Before | After |
|---|---|---|
| Backend unit | 898 | 904 |
| Backend integration | 515 | 517 |
| Angular | 166 | 181 |

## Risks and unresolved questions

None blocking. The following are appropriate for Phase 9J hardening:

- No real audio URL support (TTS generation not wired) — by design in this phase
- `speakers[].viewpoint` is collected in the item schema but not exposed in the practice UI (only name/role shown) — intentional to avoid revealing evaluation criteria during practice
- `agreements`, `disagreements`, `decisionOrOutcome` fields exist in the generate prompt schema but are not rendered separately in the UI — the `audioScript` contains the full conversation; evaluation uses these fields from `practiceContent.exerciseData`
- No Playwright tests — the existing project does not have a Playwright baseline for this pattern

## Final verdict

Phase 9I is complete. All CI/CD gates pass. `summarize_group_discussion` is the final planned speaking/listening format. All planned formats in the catalog are now Ready.

## Next recommended action

Phase 9J hardening: review all Phase 9 formats (9D–9I) for production content quality, prompt refinement, and optional TTS audio wiring where appropriate.
