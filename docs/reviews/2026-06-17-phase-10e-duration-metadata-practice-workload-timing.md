---
status: current
lastUpdated: 2026-06-17
owner: engineering
sprint: phase-10e
---

# Phase 10E — Duration Metadata / Practice Workload Timing

**Date:** 2026-06-17
**Sprint/feature:** Phase 10E
**Related:** Phase 10D (activity quality / workload validation), `docs/architecture/learning-activity-engine.md`

---

## Summary

Phase 10E extends `module_stage_v1` generated content with optional duration metadata fields and adds validation rules that cross-check claimed practice time against actual item workload. This closes the remaining quality gap from Phase 10D: an activity could claim 5 minutes of practice but contain only a single 10-second item.

---

## Files Reviewed

- `src/LinguaCoach.Application/Activity/ModuleStageContent.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`

---

## Duration Metadata Fields Added

Four optional fields added to `StageContentDto` and `ModuleStageWireDto` in `ModuleStageContent.cs`:

```csharp
int? EstimatedDurationMinutes = null,
int? EstimatedLearnMinutes = null,
int? EstimatedPracticeMinutes = null,
int? EstimatedFeedbackMinutes = null,
```

These correspond to the following JSON fields in `module_stage_v1` content:

```json
{
  "estimatedDurationMinutes": 5,
  "estimatedLearnMinutes": 1,
  "estimatedPracticeMinutes": 3,
  "estimatedFeedbackMinutes": 1
}
```

All fields are optional and nullable. Old content without these fields continues to pass validation unchanged.

---

## Validation Rules

Added `ValidateDurationMetadata()` and `TryGetPositiveInt()` to `ModuleStageContentValidator`.

### Rule 1 — Values must be positive when present

If a duration field is present (non-null), it must be a positive integer. Zero or negative values fail validation.

```
"estimatedDurationMinutes" must be positive (got 0).
"estimatedPracticeMinutes" must be positive (got -1).
```

### Rule 2 — Parts must not exceed total duration

When `estimatedDurationMinutes` is present alongside any part fields, the sum of parts must not exceed the total (with 1-minute rounding tolerance).

```
Duration parts total 7 min exceeds estimatedDurationMinutes 5 min.
```

### Rule 3 — Practice time vs item workload (multi-item formats only)

For multi-item formats (those in `ItemCountArrayByPattern` and not in `WorkloadModeRegistry.SingleSubstantialTaskPatterns`):

When `estimatedPracticeMinutes` is present and `countSettings` is available, the item count must be at least `min(estimatedPracticeMinutes, MinItemsPerPractice)`. This is conservative: it only fires when item count falls below both thresholds simultaneously.

```
Workload mismatch for "gap_fill_workplace_phrase":
estimatedPracticeMinutes is 5 but items has only 1 item(s).
Increase item count or reduce estimated practice time.
```

### Backward compatibility

- Null values for duration fields are treated as absent — no error.
- Missing duration fields entirely — no error.
- Old stored activities without duration metadata continue to pass all validation.

---

## Single-Substantial-Task Handling

Formats in `WorkloadModeRegistry.SingleSubstantialTaskPatterns` (e.g. `write_essay`, `summarize_spoken_text`, `retell_lecture`) are exempt from the practice-time vs item-count check. A single prompt or task is the full expected workload for these formats, regardless of `estimatedPracticeMinutes`. This preserves Phase 10D behaviour and avoids false failures.

---

## Prompt / Generation Changes

Duration metadata instructions added to all three core generation prompts in `DefaultAiSeeder.cs`:

- `activity_generate_writing` — writing scenario prompt
- `activity_generate_listening` — listening comprehension prompt
- `activity_generate_speaking_roleplay` — speaking role-play prompt

Each prompt now includes:

1. Duration fields in the JSON template with guidance values (e.g. `e.g. 5`).
2. A "Duration rules" section with:
   - All four fields must be positive integers.
   - Parts must not exceed total.
   - `estimatedPracticeMinutes` must reflect actual task complexity (not padded time).
   - Format-specific guidance (e.g. "a single 35-word audio clip with one question is not 5 minutes").

---

## Examples of Invalid Tiny-Practice Activities (Now Caught)

### Example 1 — Negative duration

```json
{ "estimatedPracticeMinutes": -1 }
```
Fails: `"estimatedPracticeMinutes" must be positive (got -1).`

### Example 2 — Parts exceed total

```json
{ "estimatedDurationMinutes": 5, "estimatedLearnMinutes": 2, "estimatedPracticeMinutes": 3, "estimatedFeedbackMinutes": 2 }
```
Fails: `Duration parts total 7 min exceeds estimatedDurationMinutes 5 min.`

### Example 3 — Multi-item format with one item and claimed 5 minutes

```json
{ "estimatedPracticeMinutes": 5, "practiceContent": { "exerciseData": { "items": [{ "id": "1", ... }] } } }
```
With pattern `gap_fill_workplace_phrase` and `MinItemsPerPractice=2`:
Fails: `Workload mismatch for "gap_fill_workplace_phrase": estimatedPracticeMinutes is 5 but items has only 1 item(s).`

---

## Known Limitations

1. **No per-item time estimation.** The rule uses a simple 1-item-per-minute heuristic. Formats with longer items (e.g. `retell_lecture`, `describe_image`) would benefit from format-specific per-item time estimates in a future phase.

2. **Single-substantial-task formats are not checked.** A `write_essay` claiming 1 minute of practice is not flagged, since the item count cannot be used as a proxy for time. Prompt instructions address this via generation guidance.

3. **Duration metadata is optional.** Old content without metadata passes silently. The prompt changes will cause new generated content to include metadata, but existing stored activities remain unaffected.

4. **No UI display added.** The Angular frontend is not modified. Duration fields are present in DTOs and available to the frontend if needed in a future phase.

---

## How Workplace-Only Assumptions Were Avoided

- Duration rules use neutral language: "realistic time", "short audio clip", "writing task" — not "workplace meeting" or "corporate email".
- No format, validation rule, or example was tied to workplace context.
- The `careerContext` and `topicHint` prompt variables already handle context; duration rules are independent.
- The test cases use general scenarios (delayed task, gap fill, essay writing) that are not workplace-specific.

---

## Findings by Priority

### P0 — Completed

- Duration fields added and backward-compatible.
- Validator extended with positive-value, total-sanity, and workload-mismatch checks.
- Three core generation prompts updated with duration instructions.
- 11 new unit tests added; all pass.

### P1 — Deferred

- Per-item time estimation for format-specific duration scoring (Phase 10F candidate).
- Angular display of `estimatedPracticeMinutes` in activity header (low-risk future task).

---

## Decisions Made

- Duration fields are optional and nullable in DTOs. Existing content is never broken.
- Workload-mismatch check only fires when both `estimatedPracticeMinutes` and `countSettings` are present, and the item count falls below both `estimatedPracticeMinutes` and `MinItemsPerPractice`. This is intentionally conservative.
- Single-substantial-task formats are exempt from the workload-mismatch check (Phase 10D rule preserved).
- Prompt changes are applied to all three core prompts; pattern-specific per-format prompts are unchanged (they have their own item-count instructions).

---

## Implementation Tasks Produced

None outstanding. All tasks in scope are complete.

---

## Risks / Unresolved Questions

- AI models may not always honour the duration instructions. Validation catches the most obvious mismatches at generation time and triggers retry.
- The 1-minute-per-item heuristic may be too lenient for very short items (e.g. single-word dictation) or too strict for long items (e.g. role-play recordings). Monitor in production.

---

## Test Counts (Post Phase 10E)

| Suite | Before | After |
|---|---|---|
| Unit | 974 | 985 |
| Integration | 534 | 534 |
| Architecture | 3 | 3 |

Integration failures (3) are pre-existing from before Phase 10E and are not caused by these changes (verified by running against Phase 10D baseline).

---

## Final Verdict

Phase 10E is complete. Duration metadata is added, validated, and covered by tests. Backward compatibility is preserved. No workplace defaults, no new exercise formats, no out-of-scope features.

---

## Next Recommended Action

**Phase 10F candidates:**

1. Per-item time estimation — replace the 1-item-per-minute heuristic with a format-specific seconds-per-item table for more accurate mismatch detection.
2. Angular activity header — surface `estimatedPracticeMinutes` in the practice stage header so students know how long to expect.
3. Pre-existing integration test investigation — the 3 failing tests (`PracticeGymNextEndpointTests`, `ListeningComprehensionActivityTests`, `PatternKeyedActivityEndpointTests`) return `ServiceUnavailable` and may indicate an environment or seed issue worth diagnosing.
