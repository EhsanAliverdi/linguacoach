# Phase 8C — Reading Fill in Blanks Implementation

**Date:** 2026-06-15
**Sprint:** Phase 8C
**Related feature:** `reading_fill_in_blanks` exercise format (third runnable planned reading format)

---

## Files Created

- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs` — added `ReadingFillInBlanksContent`, `ReadingFillInBlanksGapDto`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-fill-in-blanks/reading-fill-in-blanks.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-fill-in-blanks/reading-fill-in-blanks.component.html`

## Files Modified

### Backend
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `ReadingFillInBlanks`
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added `ReadingFillInBlanks = 13`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — added forbidden keys and required practice keys for `reading_fill_in_blanks`
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs` — added `reading_fill_in_blanks` branch in `ParseExpectedItems`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `reading_fill_in_blanks` to `StagedPatternKeys`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added pattern row
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted to `Ready()`, removed `Planned()` entry
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added generate prompt

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` — added `'readingFillInBlanks'` to `InteractionMode` union
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — imported component, added getter, handler, payload union member
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html` — added `@case ('readingFillInBlanks')`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` — added `readingFillInBlanks` payload serialization

### Tests
- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — pin `ReadingFillInBlanks = 13`, count 13 → 14
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern counts 12 → 13, deactivated 11 → 12, idempotent 12 → 13
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — added `ReadingFillInBlanks_IsReadyAndEligible`, updated `OtherPlannedReadingTypes_RemainUnchanged`
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs` — three new tests for `reading_fill_in_blanks`
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — valid payload, missing key, forbidden key tests
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` — fixture + two new tests

---

## JSON Schema

```json
{
  "passageWithBlanks": "The {{gap1}} ran quickly across the {{gap2}}.",
  "gaps": [
    { "id": "gap1", "answer": "dog", "options": ["dog","cat","bird","fish"], "explanation": "..." },
    { "id": "gap2", "answer": "park", "options": ["park","river","mountain","desert"], "explanation": "..." }
  ]
}
```

Gaps use `{{gapN}}` tokens. The renderer replaces each token with a `<select>` dropdown.

## Evaluator

`ExactMatchEvaluator` — deterministic. Submitted answer shape: `{ answers: { gap1: "dog", gap2: "park" } }`. Normalizes case and trailing punctuation.

## Decisions

- Dropdown (`<select>`) per gap rather than free-text, to match the `options` array in the JSON schema and keep evaluation fully deterministic.
- Reused `ExactMatchEvaluator` rather than creating a new evaluator — the existing gap-keyed path with normalization covers the requirements exactly.
- `InteractionMode.ReadingFillInBlanks = 13` appended as per append-only rule.
- `ExerciseTypeDefinitionSeeder` row count remains 36 (planned entry replaced by ready entry with comment).

---

## CI/CD Results

| Suite | Before | After |
|-------|--------|-------|
| Unit tests | 621 | 631 |
| Integration tests | 475 | 476 |
| Architecture tests | 3 | 3 |
| Angular tests | 106 | 108 |
| Angular prod build | PASS | PASS |

All green. Total backend: 1110/1110.

---

## Risks / Notes

- gstack not available in this environment (Codex Online/browser) — skipped per Phase 8C instructions.
- No Today pre-generation wired (constraint from spec).
- No MinIO/audio lifecycle (constraint from spec).
- All other planned formats remain non-runnable.

## Next recommended action

Phase 8D or next planned reading/listening format, or integration smoke test of the generated activity flow end-to-end.
