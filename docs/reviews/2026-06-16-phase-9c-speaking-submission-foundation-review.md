# Phase 9C — Speaking Submission Foundation Review

**Date:** 2026-06-16
**Sprint/Feature:** Phase 9C — Speaking Submission Foundation Review / Cleanup
**Commit:** a2fabbe

---

## Scope

End-to-end audit of the speaking submission and evaluation foundation in preparation for future speaking formats (`read_aloud`, `repeat_sentence`, `respond_to_situation`, `describe_image`, `retell_lecture`, `summarize_group_discussion`).

---

## Files Audited

### Backend

- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs`
- `src/LinguaCoach.Application/Activity/IPatternEvaluator.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs`

### Frontend

- `src/LinguaCoach.Web/.../activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/.../renderers/answer-short-question/answer-short-question.component.ts`
- `src/LinguaCoach.Web/.../renderers/answer-short-question/answer-short-question.component.html`
- `src/LinguaCoach.Web/.../pattern-evaluation-result/pattern-evaluation-result.component.ts`
- `src/LinguaCoach.Web/.../pattern-evaluation-result/pattern-evaluation-result.component.html`

---

## Current Speaking Submission Shape

### `answer_short_question`

**Content shape (practiceContent.exerciseData):**
```json
{
  "items": [
    {
      "id": "q1",
      "question": "Where is the meeting?",
      "audioScript": null,
      "audioUrl": null,
      "expectedAnswer": "room 3",
      "acceptedAnswers": ["room 3", "in room 3"],
      "explanation": null
    }
  ]
}
```

**Submit payload (`submittedContent` JSON):**
```json
{ "items": [{ "itemId": "q1", "answerText": "room 3" }] }
```

**Evaluation:** `ExactMatchEvaluator.EvaluateAnswerShortQuestionAsync`
- Keyed by item id (safe, matches `write_from_dictation` convention)
- Contains-match in addition to exact match (appropriate for short spoken answers)
- Handles empty, missing, and unknown items safely
- 60% pass threshold
- Returns per-item `PatternEvaluationItemResult` with `ItemKey`, `StudentAnswer`, `CorrectAnswer`, `AcceptedAnswers`, `IsCorrect`, `Score`, `MaxScore`, `Feedback`

### Legacy `SpeakingRolePlay`

Uses audio recording flow via `MediaRecorder` → `submitSpeakingAttempt` → `POST /activity/{id}/speaking-attempt`. Entirely separate from the pattern-backed text submission path. No interference.

---

## What Is Reusable

1. **`UnwrapStagedContent`** — already shared across all ExactMatchEvaluator patterns. Extracts `practiceContent.exerciseData` from `module_stage_v1` JSON. Future speaking formats can use it as-is.

2. **Per-item result shape** (`PatternEvaluationItemResult`) — consistent across gap-fill, dictation, and answer_short_question. Future formats should follow the same shape.

3. **`Normalize(string?)`** — shared static helper: lowercase, trim, collapse whitespace, strip trailing punctuation. Appropriate for text transcripts from spoken answers.

4. **`onRendererSubmit(payload: ExerciseAnswerPayload)` dispatch** — the Angular lesson component dispatches to `submitAttempt` for all pattern-backed formats via the `ExerciseAnswerPayload` union type. Future speaking text formats just add a new `kind` arm.

5. **`PatternEvaluationResultComponent`** — renders score ring, coach summary, and per-item blocks. Extended in this phase to cover `answer_short_question` items. A generic fallback block (`isGenericItemResult`) now covers any future format sharing the same item shape before a dedicated block is added.

6. **`AnswerShortQuestionComponent`** — `canSubmit` allows partial (at least one answer filled). Submit emits `{ items: [{ itemId, answerText }] }`. Clean and reusable as a pattern for similar per-item speaking prompt formats.

---

## What Was Fixed / Refactored

### Bug fixed: missing per-item feedback for `answer_short_question`

`PatternEvaluationResultComponent` had no `isAnswerShortQuestion` getter and no HTML branch. When `answer_short_question` evaluation results arrived, only the score card and coach summary rendered — per-item Q&A results (your answer, expected answer, per-item feedback) were completely invisible to the student.

**Fix:** Added `isAnswerShortQuestion` getter and a new HTML block (section F) showing per-item results with question numbering, correct/incorrect icons, student answer, expected answer, and per-item feedback text.

### Added: generic per-item fallback block (section G)

Added `isGenericItemResult` computed getter and an HTML block that renders for any pattern not covered by sections A–F. This means future speaking formats that produce `PatternEvaluationItemResult` items will show meaningful per-item feedback immediately, without requiring a dedicated HTML block first.

### Added: 13 `answer_short_question` unit tests

`ExactMatchEvaluatorTests.cs` had no coverage for the ASQ path. Tests added:
- `AnswerShortQuestion_AllCorrect_ReturnsFullScore`
- `AnswerShortQuestion_ContainsMatch_IsAccepted`
- `AnswerShortQuestion_AcceptedAlternative_IsAccepted`
- `AnswerShortQuestion_CaseInsensitive_Matches`
- `AnswerShortQuestion_OneWrong_ReturnsPartialScore`
- `AnswerShortQuestion_EmptyAnswer_IsIncorrect`
- `AnswerShortQuestion_MissingAnswer_IsIncorrectNotError`
- `AnswerShortQuestion_AllWrong_IsCompletedNotPassed`
- `AnswerShortQuestion_UnknownItem_NotScoredAndReported`
- `AnswerShortQuestion_EmptySubmissionJson_ReturnsZeroScore`
- `AnswerShortQuestion_StagedContent_IsUnwrappedCorrectly`
- `AnswerShortQuestion_60PercentThreshold_PassFail`

---

## What Should Wait Until `read_aloud` / `repeat_sentence`

The following are explicitly deferred — they require audio recording infrastructure decisions not part of this phase:

1. **Speech-to-text transcript attachment** — the `AnswerShortQuestionItem` has `audioUrl` and `audioScript` fields present in the content shape. When audio recording support is added, the submitted item should carry an `audioUrl` alongside `answerText`. The `AnswerShortQuestionSubmittedItem` DTO would gain an `AudioUrl` field.

2. **`read_aloud` / `repeat_sentence` evaluators** — these require pronunciation/fluency scoring, not contains-match. A new evaluator class (e.g., `SpeechEvaluator`) would be added, not a new branch in `ExactMatchEvaluator`.

3. **Per-item audio playback in feedback** — the feedback component currently shows text only. When audio is available, a playback control per item would be added to section F.

4. **Playwright E2E for speaking flow** — deferred; no suitable Playwright pattern exists for microphone simulation in this project's E2E suite.

---

## Confirmation: No New Speaking Format Made Runnable

Confirmed. No `ExerciseTypeDefinitionSeeder`, `ExercisePatternSeeder`, `InteractionMode`, or `ExercisePatternKey` changes were made. No planned format had its `implementationStatus` changed. `read_aloud`, `repeat_sentence`, `respond_to_situation`, `describe_image`, `retell_lecture`, and `summarize_group_discussion` remain `planned`.

---

## Compatibility

- `/activity` route: unchanged
- `exerciseType=`, `type=`, `pattern=` query params: unchanged
- Practice Gym v2 start flow: unchanged
- Existing session/activity history: unchanged
- Legacy `SpeakingRolePlay` audio recording flow: unchanged

---

## Final Test Counts

| Suite | Tests | Result |
|---|---|---|
| LinguaCoach.UnitTests | 830 (+12 from 818) | PASS |
| LinguaCoach.IntegrationTests | 506 | PASS |
| LinguaCoach.ArchitectureTests | 3 | PASS |
| Angular unit tests | 154 | PASS |
| Angular prod build | — | PASS |

**Total backend: 1339. Total Angular: 154. Zero failures.**

---

## Documentation Impact

- **Reviewed:** `AGENTS.md`, `docs/architecture/README.md`, `docs/sprints/2026-06-10-pattern-evaluation-engine-sprint.md`
- **Updated:** This review doc (new)
- **Not updated:** Architecture docs — no architectural decision changed; existing pattern-evaluation architecture is confirmed correct as-is
