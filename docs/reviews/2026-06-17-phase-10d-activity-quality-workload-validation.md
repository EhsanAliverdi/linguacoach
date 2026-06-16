---
status: current
lastUpdated: 2026-06-17 08:30
owner: engineering
supersedes:
supersededBy:
---

# Phase 10D — Activity Quality / Workload Validation

**Date:** 2026-06-17
**Sprint:** Phase 10D
**Related phases:** 10A (Dynamic Pattern Selection), 10B (Student Learning Ledger), 10C (Ledger-aware Selection)

---

## Summary

This phase adds quality and workload validation so generated activities are meaningful and feedback wording reflects the student's actual score.

---

## Validation rules added

### WorkloadModeRegistry (`ModuleStageContentValidator.cs`)

A new `WorkloadModeRegistry` static class classifies every pattern key into one of two modes:

| Mode | Behaviour |
|---|---|
| `SingleSubstantialTask` | One item is the full expected exercise. No workload minimum enforced. |
| `MultiItem` | Multiple items are normally expected. `MinItemsPerPractice` is enforced. |

Classification is centralised in one place. Adding a new pattern key requires only one line.

### EnforceWorkloadSanity (new method in `ModuleStageContentValidator`)

Called when `countSettings` is provided (i.e. when the caller has registry access).

- Skips enforcement for `SingleSubstantialTask` formats.
- For `MultiItem` formats: looks up the countable array field from `ItemCountArrayByPattern`, then checks `itemCount < MinItemsPerPractice`.
- If under-workload: adds a clear error: `"Workload too small for \"<key>\": <field> has N item(s) but this format requires at least M. The activity would not provide meaningful practice."`
- Does not duplicate the range error from `EnforceCounts` — both fire independently so callers see both the range violation and the semantic workload message.

### ItemCountArrayByPattern extended

Added missing entries so workload sanity can find the countable array for:
- `gap_fill_workplace_phrase` → `items`
- `listen_and_gap_fill` → `gaps`
- `listen_and_answer` → `questions`
- `phrase_match` → `pairs`

---

## Which formats are single-substantial-task vs. multi-item

### Single-substantial-task (one item = full exercise, no minimum enforced)

| Pattern key | Reason |
|---|---|
| `write_essay` | Full essay is one substantial task |
| `summarize_written_text` | One passage, one summary task |
| `summarize_spoken_text` | One audio, one summary task |
| `open_writing_task` | One open prompt is the full task |
| `spoken_response_from_prompt` | One spoken prompt response |
| `speaking_roleplay_turn` | One roleplay turn |
| `email_reply` | One email reply is the task |
| `teams_chat_simulation` | One chat response is the task |
| `describe_image` | One image description |
| `retell_lecture` | One lecture retell |
| `summarize_group_discussion` | One discussion summary |
| `respond_to_situation` | One situational response |
| `reading_multiple_choice_single` | One passage + one question |
| `reading_multiple_choice_multi` | One passage + multi-select |
| `listening_multiple_choice_single` | One audio + one question |
| `listening_multiple_choice_multi` | One audio + multi-select |
| `select_missing_word` | One audio + one missing word |
| `highlight_correct_summary` | One audio + one summary selection |
| `highlight_incorrect_words` | One audio + one transcript |
| `lesson_reflection` | Read-only reflection, no marking |

### Multi-item (MinItemsPerPractice enforced)

| Pattern key | Countable field | Seeder MinItems |
|---|---|---|
| `gap_fill_workplace_phrase` | `items` | 2 |
| `listen_and_gap_fill` | `gaps` | 2 |
| `listen_and_answer` | `questions` | 2 |
| `phrase_match` | `pairs` | 2 |
| `reading_fill_in_blanks` | `gaps` | 3 |
| `reading_writing_fill_in_blanks` | `gaps` | 3 |
| `listening_fill_in_blanks` | `gaps` | 3 |
| `reorder_paragraphs` | `items` | 4 |
| `highlight_incorrect_words` | `incorrectTokenIds` | 2 |
| `write_from_dictation` | `items` | 2 |
| `answer_short_question` | `items` | 3 |
| `repeat_sentence` | `items` | 3 |

---

## Duration sanity

Duration metadata is not consistently present in generated content JSON.
This phase implements item-count-based workload validation only, which is the reliable signal.
Duration-based sanity (e.g. "5-minute activity must not have only 1 item") is deferred pending a reliable `estimatedDurationMinutes` field in `module_stage_v1` content.
The `ExerciseTypeDefinition.EstimatedDurationMinutes` exists in the catalog and can be used in a future pass.

---

## Feedback wording changes

### `scoreBandLabel()` — four tiers

| Score | Label (was) | Label (now) |
|---|---|---|
| 90–100% | Great work | Excellent |
| 70–89% | Good effort | Good work |
| 40–69% | Keep going | Keep going |
| 0–39% | Keep going | Needs review |

### `scoreRingColour()` — four tiers

| Score | Colour |
|---|---|
| 90–100% | `--sp-success` |
| 70–89% | `--sp-vocabulary` |
| 40–69% | `--sp-warn` (new) |
| 0–39% | `--sp-speaking` |

### `scoreBandInstruction()` — new method, replaces hardcoded "Review the corrections"

| Score | Instruction |
|---|---|
| 90–100% | Ready for the next challenge. |
| 70–89% | Small improvements suggested below. |
| 40–69% | Review the corrections below and try again. |
| 0–39% | Retry recommended — check the corrections below. |

100% no longer shows "Review the corrections below and try again." or any wording implying improvement is needed.

### Spoken response section heading

- Was: always "How to improve"
- Now: "How to improve" when `percentage < 90`, otherwise "Keep practising"

### `showImprovementPrompt` — new getter

Returns `true` when `percentage < 90`. Drives instruction line colour (red vs. green).

---

## Known limitations

- Workload validation only fires when `countSettings` is passed. Callers without registry access (no `ExerciseTypeDefinition` lookup) skip workload enforcement. This is correct -- the validator is designed to be called with counts by the generation handler.
- Duration-based sanity is not implemented. Item count is the proxy for workload quality.
- The `phrase_match` pattern uses `pairs` as the countable field, but `PracticeCountSettings` and `EnforceCounts` do not include a path for `pairs` in `ItemCountArrayByPattern` for `EnforceCounts` (only `items`/`gaps` are listed there). `EnforceWorkloadSanity` looks up `pairs` via `ItemCountArrayByPattern` for the new workload check. Unifying `EnforceCounts` to also cover `pairs` is a future cleanup.
- The `listen_and_answer` array field is `questions` in the validator but the AI may use `questions` nested inside `exerciseData`. This matches the existing `RequiredPracticeKeysByPatternKey` contract.

---

## How workplace-only assumptions were avoided

- `WorkloadModeRegistry` does not reference workplace content, workplace roles, or workplace defaults.
- Workload validation applies to format type only, not content context.
- `EnforceWorkloadSanity` has no mention of workplace, immigration, travel, or other learning contexts.
- The test `Validate_NoWorkplaceDefaultIntroduced_WhenContextIsNull` explicitly verifies that general content passes without workplace-specific rules.
- No new exercise format was added.
- No `exerciseType=` key was changed.

---

## Files changed

| File | Change |
|---|---|
| `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` | Added `WorkloadMode` enum, `WorkloadModeRegistry`, `EnforceWorkloadSanity`; extended `ItemCountArrayByPattern` |
| `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` | Added `CountOverrides` for `phrase_match`, `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`; updated comments |
| `src/LinguaCoach.Web/.../pattern-evaluation-result.component.ts` | Four-tier `scoreRingColour`/`scoreBandLabel`; new `scoreBandInstruction`, `showImprovementPrompt` |
| `src/LinguaCoach.Web/.../pattern-evaluation-result.component.html` | Uses `scoreBandInstruction()`, `showImprovementPrompt`; spoken section heading is score-aware |
| `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` | 23 new workload validation tests |
| `src/LinguaCoach.Web/.../pattern-evaluation-result.component.spec.ts` | New file — 25 Angular unit tests for score-aware labels |

---

## Tests added

### Backend (23 new unit tests)

- `Validate_MultiItemFormat_WithTooFewItems_FailsWorkloadValidation` (5 parameterised cases)
- `Validate_MultiItemFormat_WithSufficientItems_Passes` (4 parameterised cases)
- `Validate_SingleSubstantialTaskFormat_WithOneItem_NoWorkloadError` (9 parameterised cases)
- `WorkloadModeRegistry_KnownSingleSubstantialTaskPatterns_AreClassifiedCorrectly`
- `WorkloadModeRegistry_KnownMultiItemPatterns_AreClassifiedAsMultiItem`
- `Validate_SelectMissingWord_WithOneItem_NoWorkloadError`
- `Validate_ItemCountConfig_IsRespected_ByEnforceCounts`
- `Validate_NoWorkplaceDefaultIntroduced_WhenContextIsNull`

### Angular (25 new unit tests)

- `scoreBandLabel` returns correct tier at 100%, 90%, 89%, 70%, 69%, 40%, 39%, 0%
- `scoreBandInstruction` does not say "Improve your answer" at 100% or 90%
- `scoreBandInstruction` shows positive wording at 90%+
- `scoreBandInstruction` shows minor improvement wording at 75%
- `scoreBandInstruction` shows review wording at 50% and 20%
- `showImprovementPrompt` is false at 100%, false at 90%, true at 89%, true at 0%
- `scoreRingColour` returns correct colour variable at each tier
- `showScoreCard` correct for scoreable and lesson_reflection patterns

---

## Final test counts

| Suite | Count |
|---|---|
| Backend unit | 974 (was 951) |
| Backend integration | 534 |
| Architecture | 3 |
| Angular unit | 229 (was 204) |

---

## Confirmation checklist

- No curriculum engine, CEFR routing, pre-generation, readiness pool, or suggested practice implemented.
- No new exercise format added.
- No workplace-only assumptions introduced.
- `/activity`, `exerciseType=`, `type=`, and `pattern=` compatibility preserved.
- Existing validators for Learn/Practice/Feedback separation still pass.
- All existing tests still pass.

---

## Recommendation for next phase

**Phase 10E — Duration metadata in module_stage_v1**

The workload sanity check currently relies on item counts only. The most impactful next improvement is to add an `estimatedPracticeMinutes` field to `module_stage_v1` so the validator can cross-check stated duration against actual item workload. This would catch the specific case described in the brief: a 5-minute activity containing only one short item. Alternatively, derive estimated duration from item count and `EstimatedDurationMinutes` from the `ExerciseTypeDefinition`.

A less breaking option is to expose `ExerciseTypeDefinition.EstimatedDurationMinutes` in the `PracticeCountSettings` record (as `EstimatedDurationMinutes`) and compute expected minimum item count from it: `minExpectedItems = estimatedDuration / perItemDurationSeconds`.
