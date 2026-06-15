# Practice Item Sets — Design Rule

Date: 2026-06-16
Related: Phase 8N — Configurable Practice Item Counts Foundation
Status: Active design rule

## Module shape

One module is exactly:

- one **Learn** stage (`learnContent`)
- one **Practice** stage (`practiceContent`)
- one **Feedback** stage (`feedbackPlan`)

This is the `module_stage_v1` contract enforced by `ModuleStageContentValidator`.

## Items live inside Practice

A Practice stage can contain **multiple items** inside `practiceContent.exerciseData`.
Depending on format these items are questions, gaps, paragraphs to reorder, or
transcript tokens to flag. Examples:

- `reading_fill_in_blanks` / `listening_fill_in_blanks`: multiple `gaps`.
- `reorder_paragraphs`: multiple `items`.
- `highlight_incorrect_words`: multiple `incorrectTokenIds`.
- MCQ formats: a single question with multiple `options`.

The Learn stage must never carry practice or exercise data. `learnContent` is
forbidden from containing any item/answer keys.

## Feedback shape

A Feedback stage produces one overall result and may optionally include
per-item results. The count of per-item results, when present, tracks the
number of practice items.

## Count settings

Each exercise type carries six configurable count settings on
`ExerciseTypeDefinition`:

| Field | Meaning |
| --- | --- |
| `MinItemsPerPractice` | minimum items/gaps/tokens in one practice |
| `DefaultItemsPerPractice` | target item count for generation |
| `MaxItemsPerPractice` | maximum items in one practice |
| `MinOptionsPerItem` | minimum options for a choice-based item |
| `DefaultOptionsPerItem` | target option count for generation |
| `MaxOptionsPerItem` | maximum options for a choice-based item |

Invariants (enforced in the entity and on admin update):

- all values are non-negative
- `min <= default <= max` for both items and options

### How counts are used

- **Generation**: `AiActivityGeneratorHandler` looks up the type by pattern
  key and injects `minItemsPerPractice`, `defaultItemsPerPractice`,
  `maxItemsPerPractice`, `minOptionsPerItem`, `defaultOptionsPerItem`,
  `maxOptionsPerItem` into the prompt variable context so prompts can target
  a specific count.
- **Validation**: `ModuleStageContentValidator.Validate` accepts an optional
  `PracticeCountSettings`. When provided, item-count formats (fill-in-blanks,
  reorder, highlight-incorrect-words) and option-count formats (MCQ, select
  missing word, highlight correct summary) are checked against `[min, max]`.
  When omitted, count enforcement is skipped for backward compatibility.

### Counts are configuration only

Updating counts never changes `ImplementationStatus`, `IsEnabled`, or runnable
surface flags. A planned format stays non-runnable regardless of its counts.
