---
status: current
lastUpdated: 2026-06-17 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10F — Integration Test Stabilization Review

**Date:** 2026-06-17
**Related sprint/feature:** Phase 10F — Integration Test Stabilization / ServiceUnavailable Investigation
**Files reviewed:** `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`, `src/LinguaCoach.Application/Ai/IAiContextBuilder.cs`, `src/LinguaCoach.Infrastructure/Ai/DbPromptAiContextBuilder.cs`, `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs`

---

## Failing Test Names

1. `LinguaCoach.IntegrationTests.Api.PatternKeyedActivityEndpointTests.GetNext_WithTypeWritingScenario_StillReturns200`
2. `LinguaCoach.IntegrationTests.Api.ListeningComprehensionActivityTests.GetNext_TypedWritingScenario_ReturnsWritingScenario`
3. `LinguaCoach.IntegrationTests.Api.PracticeGymNextEndpointTests.ExistingGetNext_WithTypeQueryParam_StillWorks`

All three returned HTTP 503 `ServiceUnavailable`.

---

## Root Cause

Phase 10E added duration metadata fields and "Duration rules" instruction blocks to the `activity_generate_writing` prompt template (`DefaultAiSeeder.cs`, `ActivityGenerateWritingContent`). This grew the rendered prompt from approximately 1100 tokens to an estimated 1134 tokens.

The `maxInputTokens` budget for `activity_generate_writing` remained at 1100, which it had been set to before the prompt expansion.

The token budget check in `DbPromptAiContextBuilder.EstimateTokens` uses `(text.Length + 3) / 4`. When the rendered prompt exceeded 1100, `TokenBudgetExceededException` was thrown. This exception propagated up and was caught by the activity generation error handler, which returned `ServiceUnavailable` to the test client.

The other two activity prompts (`activity_generate_listening` budget 1200, `activity_generate_speaking_roleplay` budget 1600) had sufficient headroom after their equivalent 10E prompt additions and did not fail.

### Evidence

```
LinguaCoach.Application.Ai.TokenBudgetExceededException:
Prompt 'activity_generate_writing' estimated 1134 tokens exceeds budget of 1100.
```

This log line appeared consistently across all three failing tests, in every run, confirming it is a deterministic code regression — not an environment-only or flaky issue.

The diff of commit `9ac0a87` (Phase 10E) against its parent shows the following lines were added to `ActivityGenerateWritingContent` without a corresponding budget increase:

```
"estimatedDurationMinutes": <total module time in minutes, e.g. 5>,
"estimatedLearnMinutes": <time to read and study the Learn stage, e.g. 1>,
"estimatedPracticeMinutes": <time for the student to complete the writing task, e.g. 3>,
"estimatedFeedbackMinutes": <time to review feedback, e.g. 1>,

Duration rules:
- estimatedDurationMinutes, estimatedLearnMinutes, estimatedPracticeMinutes,
  estimatedFeedbackMinutes must all be positive integers.
- estimatedLearnMinutes + estimatedPracticeMinutes + estimatedFeedbackMinutes
  must not exceed estimatedDurationMinutes.
- estimatedPracticeMinutes must reflect the actual time needed to complete the
  writing task. A single short writing task is typically 3-5 minutes. Do not
  claim 5+ minutes of practice for a trivial one-sentence prompt.
- Do not pad learnContent to fill time while keeping practiceContent tiny.
```

---

## Fix Made

**File:** `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`, line 4493

**Change:** `maxInputTokens: 1100` → `maxInputTokens: 1300`

This gives approximately 15% headroom above the current 1134-token estimate, matching the headroom pattern of the other activity prompts. No prompt content was modified.

---

## Evidence the Issue Is Resolved

Individual test reruns after the fix:

```
Passed  PatternKeyedActivityEndpointTests.GetNext_WithTypeWritingScenario_StillReturns200  [1 s]
Passed  ListeningComprehensionActivityTests.GetNext_TypedWritingScenario_ReturnsWritingScenario  [1 s]
Passed  PracticeGymNextEndpointTests.ExistingGetNext_WithTypeQueryParam_StillWorks  [1 s]
```

Full suite after the fix:

```
Architecture:   Failed: 0  Passed:   3  Total:   3
Unit:           Failed: 0  Passed: 985  Total: 985
Integration:    Failed: 0  Passed: 534  Total: 534
```

---

## Test Ordering / State Leakage

Not a factor. All three tests failed deterministically in every run, including when run individually, confirming the cause was a code defect (token budget too small) rather than inter-test state contamination.

---

## Compatibility Preserved

- `/activity` endpoint: unaffected
- `exerciseType=`, `type=`, `pattern=` query parameters: unaffected
- Practice Gym v2: unaffected
- Duration metadata backward compatibility from 10E: unaffected (no prompt content changed, only the budget ceiling)

---

## No New Product Features

No product feature was implemented. The only change is the `maxInputTokens` budget correction in the seeder.

---

## Decisions Made

- Raise `activity_generate_writing` budget to 1300 (not to exact 1134) to provide headroom for minor future prompt edits without triggering another budget miss.
- Do not modify the prompt template; the duration rules added in 10E are correct product behaviour.

---

## Risks / Unresolved Questions

None. The failure was deterministic and reproducible, the fix is minimal and targeted, and the full suite confirms resolution.

---

## Final Verdict

Root cause identified and fixed. All 534 integration tests pass. The failures were a direct regression introduced by Phase 10E expanding the writing prompt without adjusting the input token budget.

---

## Next Recommended Action

Phase 11 feature work can proceed. No further stabilization work is required.

Consider adding a unit test that asserts `ActivityGenerateWritingContent.Length` does not exceed a threshold, so future prompt expansions are caught at build time rather than at runtime. This is optional but would prevent a recurrence of this class of failure.
