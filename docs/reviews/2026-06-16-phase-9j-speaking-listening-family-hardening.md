---
status: current
lastUpdated: 2026-06-16 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 9J — Speaking/Listening Family QA & Hardening Review

**Date:** 2026-06-16
**Related sprint:** Phase 9 Speaking/Listening family (9A–9I)
**Review type:** QA & Hardening

---

## Formats Audited

### Speaking formats
- `answer_short_question` (9A)
- `read_aloud` (9D)
- `repeat_sentence` (9E)
- `respond_to_situation` (9F)
- `describe_image` (9G)
- `retell_lecture` (9H)
- `summarize_group_discussion` (9I)
- `spoken_response_from_prompt` (legacy speaking pattern)
- `speaking_roleplay_turn` (legacy speaking pattern)

### Listening formats
- `listening_multiple_choice_single`
- `listening_multiple_choice_multi`
- `listening_fill_in_blanks`
- `select_missing_word`
- `highlight_correct_summary`
- `highlight_incorrect_words`
- `write_from_dictation`
- `summarize_spoken_text`
- `listen_and_answer`
- `listen_and_gap_fill`

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs`
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiOpenEndedEvaluator.cs`
- `tests/LinguaCoach.UnitTests/Activity/AiOpenEndedEvaluatorTests.cs`
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`

### Angular
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/summarize-group-discussion/summarize-group-discussion.component.spec.ts`
- All 7 Phase 9 renderer component directories (confirmed present)

---

## Findings by Priority

### P0 — Bug Fixed

**`summarize_group_discussion` submission serialization missing**

File: `activity-lesson.component.ts` around line 508.

The `if/else if` chain in `onAnswerSubmitted` handled `retellLecture` but fell through to the `else` branch for `summarizeGroupDiscussion`. The `else` branch serializes the entire payload object including the `kind` field, rather than `{ items: [...] }`. This would cause the backend AiOpenEndedEvaluator to receive a malformed `SubmittedAnswerJson` instead of the expected items array, producing a 500 or empty AI evaluation.

Fix: Added the missing `else if (payload.kind === 'summarizeGroupDiscussion')` branch.

### P2 — Stale Comment Fixed

**`ExerciseTypeDefinitionSeeder.CountOverrides` comment incorrectly said "Speaking (planned, non-runnable)"**

The Phase 9A–9I formats were all promoted to Ready during Phases 9A–9I, but the comment in `CountOverrides` was not updated. Fixed to read "Speaking (Ready)".

### P3 — Missing Tests Added

**No AiOpenEndedEvaluator tests for `summarize_group_discussion`**

`retell_lecture` had 3 tests (high score, low score, compactContent exclusion). `summarize_group_discussion` had none. Added 3 equivalent tests:
- `ParseAndNormalise_SummarizeGroupDiscussion_HighScore_Passes`
- `ParseAndNormalise_SummarizeGroupDiscussion_LowScore_Fails`
- `CompactContent_SummarizeGroupDiscussionStaged_ExcludesLearnContent`

**No Angular dispatch tests for any Phase 9 renderer**

The `exercise-renderer.component.spec.ts` file did not exist. Created it with 23 tests covering:
- Renderer dispatch (correct component rendered for each interaction mode)
- Content extraction from `module_stage_v1` `practiceContent.exerciseData`
- Submission event emission with correct `kind` and `items` shape
- `audioScript` fallback present when `audioUrl` is null (for `retellLecture`, `summarizeGroupDiscussion`)

---

## Confirmed Wiring (No Changes Needed)

### Backend — All OK
- All 7 Phase 9 keys present in `ExercisePatternKey.cs`
- All 7 seeded in `ExercisePatternSeeder.cs` with correct `InteractionMode`, `MarkingMode`, `aiGeneratePromptKey`, `aiEvaluatePromptKey`
- All 7 promoted to `Ready` in `ExerciseTypeDefinitionSeeder.cs` with `supportsPracticeGym: true`
- `ModuleStageContentValidator` has `RequiredPracticeKeysByPatternKey` entries for all 7 (all require `items`)
- `RequiredItemFieldsByPatternKey` covers all 7 with appropriate per-item field checks
- `ItemCountArrayByPattern` covers all 7 for item-count enforcement
- `ExactMatchEvaluator` handles `answer_short_question`, `read_aloud`, `repeat_sentence`
- `AiOpenEndedEvaluator.ResolvePromptKey` routes `respond_to_situation`, `describe_image`, `retell_lecture`, `summarize_group_discussion` to correct prompt keys
- Empty/missing submission handling tested for `answer_short_question` (line 904 in ExactMatchEvaluatorTests)

### Angular — All OK (post-fix)
- All 7 Phase 9 formats imported in `exercise-renderer.component.ts`
- All 7 wired in `exercise-renderer.component.html` via `@switch (mode)`
- All 7 have content getter methods in the renderer component
- All 7 have `onXxxSubmitted` handlers emitting correct `ExerciseAnswerPayload` shapes
- All 7 serialized in `activity-lesson.component.ts` `onAnswerSubmitted` (after fix)
- Practice Gym spec covers all 7 Phase 9 formats: `readyAnswerShortQuestion`, `readyReadAloud`, `readyRepeatSentence`, `readyRespondToSituation`, `readyDescribeImage`, `readyRetellLecture`, `readySummarizeGroupDiscussion`

---

## Tests Added

### Backend unit tests added (3)
In `tests/LinguaCoach.UnitTests/Activity/AiOpenEndedEvaluatorTests.cs`:
- `ParseAndNormalise_SummarizeGroupDiscussion_HighScore_Passes`
- `ParseAndNormalise_SummarizeGroupDiscussion_LowScore_Fails`
- `CompactContent_SummarizeGroupDiscussionStaged_ExcludesLearnContent`

### Angular unit tests added (23)
New file: `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.spec.ts`

Covers all 7 Phase 9 speaking/listening formats across:
- Renderer dispatch (7 tests)
- Content extraction from staged exerciseData (7 tests)
- Submit event emission with correct payload shape (7 tests)
- audioScript fallback when audioUrl is null (2 tests: retellLecture, summarizeGroupDiscussion)

---

## Final Test Counts

| Suite | Before | After |
|---|---|---|
| Backend unit | 904 | 907 |
| Backend integration | 517 | 517 |
| Architecture | 3 | 3 |
| Angular unit | 181 | 204 |

---

## Known Limitations

- No real STT, audio recording storage, or phoneme-level pronunciation scoring was added. All speaking formats use text transcript input as a proxy for spoken output. This is explicit by design for this phase.
- `read_aloud` and `repeat_sentence` use word-overlap scoring (60% threshold). This is a heuristic that works without STT but cannot assess actual pronunciation or fluency.
- `audioUrl` for retell_lecture and summarize_group_discussion is always null in the current generator (no TTS generation for these formats). The `audioScript` fallback is the student experience. This is acceptable and consistent with how other listening patterns behave when TTS is not configured.
- The `describe_image` format renders a text description prompt without an actual image. The `imagePrompt` field describes what the AI imagines for the image. This is the documented design from Phase 9G.
- The workplace-only content assumption was audited. The Phase 9 seeder entries that were already corrected in Phase 9K (`respond_to_situation`, `describe_image`, `repeat_sentence`, `retell_lecture`, `summarize_group_discussion`) have `workplaceContext: false`. No further workplace-only assumptions were found in the Phase 9J scope that require immediate fixing.

---

## Confirmation

- **No new exercise format was added.** This phase performed QA and hardening only.
- **No new audio/STT/storage/phoneme scoring pipeline was added.**
- **Practice Gym v2 still shows only runnable formats as clickable.** The `runnable` flag in `PracticeGymComponent` is `isEnabled && isAvailableForGeneration && implementationStatus === 'ready' && supportsPracticeGym`. All 7 Phase 9 formats meet this. Planned/disabled formats render as non-button elements.
- **`/activity`, `exerciseType=`, `type=`, and `pattern=` compatibility preserved.** No existing routes or query parameters were changed.

---

## Workplace Assumptions Found

- All 7 Phase 9 seeder entries use `workplaceContext: false` or non-workplace content framing (corrected in Phase 9K).
- No workplace-only copy was found in the renderer components or validator.
- No workplace-only hardcoding was found in the submission path.

---

## Risks and Unresolved Questions

- Live AI calibration for `summarize_group_discussion` evaluation prompt has not been done against a real AI provider. The prompt key `activity_evaluate_summarize_group_discussion` is registered but only tested via `FakeAiProvider`.
- The `summarize_group_discussion` serialization bug (P0 above) means that any student who attempted this format before this fix would have received a failed or empty evaluation. No migration or retroactive fix is needed for historical attempts — attempts are immutable once recorded.

---

## Decisions Made

- Fixed the `summarizeGroupDiscussion` serialization bug as a P0 code defect within Phase 9J scope.
- Fixed the stale comment in `ExerciseTypeDefinitionSeeder` as a low-risk cosmetic fix.
- Added 3 backend + 23 Angular tests to harden the family.

---

## Documentation Impact

- Docs reviewed: `AGENTS.md`, `docs/architecture/README.md`, `docs/sprints/current-sprint.md`, Phase 9G/9H/9I review docs
- Docs updated: `docs/sprints/current-sprint.md` (Phase 9J entry added), this review doc created
- Docs intentionally not updated: `docs/architecture/exercise-pattern-library.md` (no architectural change), individual Phase 9 sprint/review docs (those are historical records of each phase)

---

## Recommendation for Next Phase

The speaking/listening family is now fully wired and hardened. Recommended next steps:

1. **Live AI calibration pass** — run all AI-evaluated speaking formats against a real AI provider to verify prompt output quality and evaluation scoring distribution.
2. **Phase 10 — Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR level, duration, and repetition history. This is the highest-value next sprint per the current backlog.
3. **Session Reflection** — now that all speaking evaluation patterns are stable, `session_reflection` AI prompt can be wired.
