# Phase 8D — Reorder Paragraphs Implementation

**Date:** 2026-06-15
**Sprint:** 2026-06-15 staged activity content migration
**Related feature:** `reorder_paragraphs` exercise format (fourth runnable planned future reading format)

---

## Files Created

- `src/LinguaCoach.Web/src/app/features/activity/renderers/reorder-paragraphs/reorder-paragraphs.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reorder-paragraphs/reorder-paragraphs.component.html`
- `docs/reviews/2026-06-15-phase-8d-reorder-paragraphs-implementation.md` (this file)

## Files Modified

### Backend
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `ReorderParagraphs = "reorder_paragraphs"`
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added `ReorderParagraphs = 14`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — added forbidden keys (`items`, `correctOrder`, `selectedOrder`, `checkAnswer`) and required practice keys for `reorder_paragraphs`
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs` — added `ReorderParagraphsContent`, `ReorderParagraphsItemDto`
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs` — added `reorder_paragraphs` early-dispatch branch (`EvaluateReorderParagraphsAsync`); added `ReorderParagraphsSubmittedAnswer` DTO; added `using LinguaCoach.Application.Activity`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `"reorder_paragraphs"` to `StagedPatternKeys`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added `ActivityGenerateReorderParagraphsKey` constant, `ActivityGenerateReorderParagraphsContent` prompt, seed call
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added `ReorderParagraphs` pattern row
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — promoted to `Ready()`, replaced `Planned()` entry with comment

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` — added `'reorderParagraphs'` to `InteractionMode` union
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — imported component, added getter, handler, payload union member
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.html` — added `@case ('reorderParagraphs')`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` — added `reorderParagraphs` payload serialization (`{ orderedIds }`)

### Tests
- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` — pin `ReorderParagraphs = 14`, count 14 → 15
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` — pattern counts 13 → 14, deactivated 12 → 13, idempotent 13 → 14
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` — added `ReorderParagraphs_IsReadyAndEligible`, updated `OtherPlannedReadingTypes_RemainUnchanged` to exclude 4 ready keys
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs` — 5 new tests: correct order, one misplaced, duplicate ids, empty submission, all wrong
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` — valid payload, missing items, missing correctOrder, forbidden key in learnContent tests
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` — fixture + 2 new tests

### Docs
- `docs/architecture/learning-activity-engine.md` — added Phase 8C and 8D entries
- `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` — added Phase 8C complete, Phase 8D complete, Phase 8E candidate
- `docs/handoffs/current-product-state.md` — updated Reading row and summary paragraph

---

## Exercise JSON Schema

```json
{
  "schemaVersion": "module_stage_v1",
  "exerciseType": "reorder_paragraphs",
  "primarySkill": "reading",
  "secondarySkills": [],
  "practiceContent": {
    "exerciseData": {
      "items": [
        { "id": "p1", "text": "..." },
        { "id": "p2", "text": "..." },
        { "id": "p3", "text": "..." },
        { "id": "p4", "text": "..." }
      ],
      "correctOrder": ["p1", "p2", "p3", "p4"],
      "explanation": "Why this order is logical.",
      "itemExplanations": { "p1": "...", "p2": "...", "p3": "...", "p4": "..." }
    }
  }
}
```

Items in the `items` array are shuffled (NOT in `correctOrder` sequence). The renderer presents them as-is; the student reorders using move-up/move-down buttons.

## Evaluator

`ExactMatchEvaluator` — early dispatch on `"reorder_paragraphs"`. Submitted answer: `{ orderedIds: ["p2","p1","p3","p4"] }`. Per-position comparison: position `i` is correct if `submittedOrder[i] == correctOrder[i]`. Duplicate ids are deduplicated (first occurrence kept). Missing positions score 0. Partial credit supported. Passes at ≥ 60%.

## UI Choice

Move-up/move-down buttons per paragraph block — chosen over drag-and-drop because:
- No drag-and-drop library is used in the project
- Fully keyboard/accessibility friendly
- Easy to test deterministically

## Decisions

- `InteractionMode.ReorderParagraphs = 14` — appended, never reordered
- `ExactMatchEvaluator` reused (not a new evaluator class) — same `MarkingMode.ExactMatch`; early dispatch keeps existing gap-fill paths clean
- `ReorderParagraphsContent` DTO in `PatternContentDtos.cs`; `ReorderParagraphsSubmittedAnswer` in `ExactMatchEvaluator.cs` — avoids duplication
- Items are shuffled by the AI prompt (instructed not to match `correctOrder` order)
- `SupportsTodayLesson = false` — Today pre-generation not implemented

---

## CI/CD Results

| Suite | Before (8C baseline) | After (8D) |
|-------|----------------------|------------|
| Unit tests | 631 | 644 |
| Integration tests | 476 | 477 |
| Architecture tests | 3 | 3 |
| Angular tests | 108 | 110 |
| Angular prod build | PASS | PASS |

Total backend: 1124/1124. All green.

---

## Confirmation

- Only `reorder_paragraphs` was made runnable in this phase
- All other planned future exercise formats remain non-runnable
- No audio formats implemented
- No Today pre-generation implemented
- No MinIO/audio lifecycle implemented
- gstack skipped — not available in this environment (Codex Online/browser)

## Next recommended format (Phase 8E)

`reading_writing_fill_in_blanks` — already in catalog as Planned, has deterministic evaluation shape (word selection from options), similar to `reading_fill_in_blanks` but involves both reading and writing skills.

Alternatives: `listening_multiple_choice_single` (deterministic, single answer) or `summarize_written_text` (AI evaluation required).
