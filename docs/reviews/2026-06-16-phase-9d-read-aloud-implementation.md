# Phase 9D — Read Aloud Implementation Review

**Date:** 2026-06-16
**Sprint/Feature:** Phase 9D — `read_aloud` First Runnable Speaking-Primary Format
**Commit:** e7d79f0

---

## Scope

End-to-end promotion of `read_aloud` from Planned to Ready. Transcript/text fallback only — no real audio recording, STT, phoneme scoring, or MinIO changes. Word-overlap similarity scoring.

---

## Files Changed

### Backend

- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — `ReadAloud = 22` (append-only)
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — `ReadAloud = "read_aloud"` (bottom of file)
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — `Planned(...)` → `Ready(...)`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added to `StagedPatternKeys`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — required practice keys `["items"]`; required item fields `["id", "text"]`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — `ActivityGenerateReadAloudKey` + `ActivityEvaluateReadAloudKey` constants; two prompt strings seeded via `SeedOrUpgradePromptAsync`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new `ExercisePattern` entry
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs` — `EvaluateReadAloudAsync` method; `ReadAloudContent`, `ReadAloudItem`, `ReadAloudSubmittedAnswer`, `ReadAloudSubmittedItem` DTOs; `CalculateWordOverlap` and `TokenizeWords` private helpers

### Angular

- `src/LinguaCoach.Web/.../renderers/read-aloud/read-aloud.component.ts` (new)
- `src/LinguaCoach.Web/.../renderers/read-aloud/read-aloud.component.html` (new)
- `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.ts` — import, payload union arm, `readAloudContent` getter, `onReadAloudSubmitted` handler
- `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.html` — `@case ('readAloud')` block
- `src/LinguaCoach.Web/.../core/models/activity.models.ts` — `| 'readAloud'` in `InteractionMode` union
- `src/LinguaCoach.Web/.../activity-lesson/activity-lesson.component.ts` — `readAloud` payload handler

### Tests

- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — `ReadAloud_IsTwentyTwo`; count test renamed and updated 22→23
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs` — 9 new read_aloud evaluator tests
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — `ReadAloud_IsNowRunnable`; removed `read_aloud` from `FutureTypes_AreNotGenerationEligibleUntilReady` and `OtherPlannedFormats_RemainNonRunnable`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — counts 26→27 (Seeds, Idempotent, GetAllActive); deactivated 25→26; SpeakingTask count 3→4 with `ReadAloud` assertion

---

## Design Decisions

### Word-overlap scoring (not phoneme scoring)

`read_aloud` uses text transcript submitted via a `<textarea>`. The evaluator tokenises the expected text and the student's transcript into normalised word sets and computes the fraction of expected words present in the submitted transcript. 60% overlap = correct item. This is honest — the UI and feedback never claim phoneme-level or pronunciation scoring.

### Submit payload identical to `answer_short_question`

`{ items: [{ itemId, answerText }] }` — same shape, same `AnswerShortQuestionSubmittedAnswer` DTO pattern (separate DTOs `ReadAloudSubmittedAnswer` / `ReadAloudSubmittedItem` for forward-compatibility).

### Content shape: `text` + `expectedText`

AI generates `text` (the text to display and read aloud) and `expectedText` (same value, used for scoring). The evaluator reads `ExpectedText ?? Text` so a fallback is always available if the AI omits `expectedText`.

### Learn content constraint enforced

The generate prompt explicitly forbids `learnContent` from containing `text`, `expectedText`, item ids, or scoring details. It teaches general read-aloud strategy only (pacing, stress, punctuation pauses).

### Generic per-item fallback (`isGenericItemResult`) covers read_aloud

`PatternEvaluationResultComponent` added in Phase 9C already has a generic fallback block for any pattern producing `PatternEvaluationItemResult` items that is not covered by a dedicated section. `read_aloud` uses this fallback without requiring a new HTML block.

### No new audio infrastructure

No recording, no MinIO lifecycle, no STT provider, no audio upload. Explicitly out of scope per Phase 9D constraints.

---

## Content Shape

### practiceContent.exerciseData

```json
{
  "items": [
    {
      "id": "t1",
      "text": "Please send the updated report by end of day.",
      "displayTitle": "Email Instruction",
      "difficulty": "medium",
      "expectedText": "Please send the updated report by end of day.",
      "focusAreas": ["sentence rhythm", "word stress"],
      "explanation": "Stress 'updated' and 'end of day' to convey urgency."
    }
  ]
}
```

### Submitted answer

```json
{ "items": [{ "itemId": "t1", "answerText": "Please send the updated report by end of day." }] }
```

### Evaluation

- Word-overlap per item (case-insensitive, punctuation-stripped tokenisation)
- `isCorrect` = overlap ≥ 60%
- `score` = overlap fraction (0.0–1.0) per item
- Overall pass = average score ≥ 60%
- Per-item `PatternEvaluationItemResult` with `ItemKey`, `StudentAnswer`, `CorrectAnswer`, `IsCorrect`, `Score`, `MaxScore`, `Feedback`

---

## Constraints Respected

- `InteractionMode` append-only — value 22 added at end
- `ExercisePatternKey` append-only — added at bottom of Speaking section
- No other Planned format promoted
- No phoneme scoring, no audio recording architecture, no MinIO, no STT
- Learn content cannot expose `text`, `expectedText`, item ids, or scoring
- `/activity`, `exerciseType=`, `type=`, `pattern=`, Practice Gym v2, existing session/activity history, `answer_short_question` — all unchanged

---

## Final Test Counts

| Suite | Tests | Delta | Result |
|---|---|---|---|
| LinguaCoach.UnitTests | 841 | +11 | PASS |
| LinguaCoach.IntegrationTests | 507 | +1 | PASS |
| LinguaCoach.ArchitectureTests | 3 | 0 | PASS |
| Angular unit tests | 154 | 0 | PASS |
| Angular prod build | — | — | PASS |

**Total backend: 1351. Total Angular: 154. Zero failures.**

---

## Risks / Unresolved

- No Playwright E2E test — consistent with project policy (no Playwright for activity flows).
- `CalculateWordOverlap` tokenises on `[a-z']+` regex (strips punctuation, lowercases). This may over-match for very short items with common words. Acceptable for an MVP transcript-based format.
- The evaluate prompt in `DefaultAiSeeder` is seeded but the evaluator does not call AI — evaluation is deterministic word-overlap only. The evaluate prompt exists for future AI-assisted feedback if desired.

---

## Documentation Impact

- **Docs reviewed:** `AGENTS.md`, `docs/architecture/README.md`, `docs/reviews/2026-06-16-phase-9c-speaking-submission-foundation-review.md`
- **Docs updated:** This review doc (new)
- **Docs intentionally not updated:** Architecture docs — no architectural decision changed; existing pattern-evaluation architecture is confirmed correct as-is. Sprint doc — no active Phase 9D sprint doc; this review serves as the record.

---

## Next Recommended Action

Phase 9E or similar: add `read_aloud` to the Practice Gym v2 spec test (`practice-gym.component.spec.ts`) with a `readyReadAloud` fixture and tests for the runnable card and start flow. This was deferred from this phase to keep scope tight — the catalog-driven UI already renders it correctly because `implementationStatus = ready` and `supportsPracticeGym = true`.
