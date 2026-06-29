# Phase 16H — Speaking Evaluation Quality Validation and Mastery Signal Dry-Run

**Date:** 2026-06-30
**Sprint:** Phase 16H
**Status:** Complete

---

## Overview

This phase adds a quality validation and dry-run learning-signal layer over the Phase 16G speaking evaluation pipeline.

The goal is to answer:
- Are provider results useful?
- Are scores present often enough?
- Are scores consistent enough?
- Can these results safely become learning signals later?

**No real mastery, CEFR, Learning Plan progress, or skill profiles were modified.**

---

## Part A — Speaking Evaluation Field Audit

| Field | Available? | Nullable? | Reliable Enough? | Used for Dry-Run? | Notes |
| ----- | ---------- | --------- | ---------------- | ----------------- | ----- |
| Status | Yes | No | Yes | Yes — gate | Pending/Evaluating/Completed/Failed/NotSupported/Skipped |
| ProviderName | Yes | Yes (pending) | Yes | No | Informational |
| ModelName | Yes | Yes | Partial | No | Set by provider |
| Transcript | Yes | Yes | Partial | No — informational | OpenAI Whisper provides it; NoOp does not |
| OverallScore | Yes | Yes | Yes | Yes — primary gate | Required for any positive signal |
| FluencyScore | Yes | Yes | Yes | Yes — confidence | Provided by OpenAI; NoOp returns null |
| PronunciationScore | Yes | Yes | No | No | OpenAI does not claim pronunciation scoring |
| CompletenessScore | Yes | Yes | Yes | Yes — dimension gate | Checked against 50-point threshold for positive signal |
| RelevanceScore | Yes | Yes | Yes | Yes — dimension gate | Checked against 50-point threshold for positive signal |
| FeedbackText | Yes | Yes | Yes | Yes — confidence | Required for High confidence |
| SuggestedImprovement | Yes | Yes | Partial | No | Qualitative; not machine-readable |
| FailureReason | Yes | Yes | Yes | Yes — gate | Shown in quality summary |
| ModelName | Yes | Yes | Partial | No | Stored from provider response |
| InputTokens / OutputTokens | Yes (result only) | N/A | Partial | Not stored | Not persisted in SpeakingEvaluation entity |
| CostUsd | Yes (result only) | N/A | Partial | Not stored | Not persisted; usage governance tracks this separately |
| Latency | Not stored | N/A | N/A | N/A | Not yet available; document gap below |
| AudioDuration | Not stored | N/A | N/A | N/A | Document gap below |

**Safe candidates for future learning signals:** OverallScore, FluencyScore, CompletenessScore, RelevanceScore.
**Not safe yet:** PronunciationScore (provider does not claim it), Transcript (informational only), SuggestedImprovement (free text).

---

## Part B — Quality Metrics

### New files

- `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationQualityQuery.cs`
- `src/LinguaCoach.Application/Speaking/SpeakingEvaluationQualitySummaryDto.cs`
- `src/LinguaCoach.Infrastructure/Speaking/SpeakingEvaluationQualityHandler.cs`

### Metrics tracked

- Total evaluations
- Completed / Failed / NotSupported (or Skipped) / Pending (or Evaluating) counts
- Completion rate (%)
- Failure rate (%)
- Average OverallScore, FluencyScore, CompletenessScore, RelevanceScore (null-safe, Completed only)
- Null field rates for all four score fields (Completed only)
- Latest failure reasons (up to 5, most recent first)
- Dry-run signal counts: CandidatePositiveSignal, CandidateReviewSignal, CandidateNoSignal, Blocked

### Known gaps

- **Latency**: not stored in `SpeakingEvaluation`. Would require a `LatencyMs` field and migration. Not added in this phase.
- **Audio duration/size**: not stored. Available in the request options but not persisted on the entity. Not added in this phase.
- **Cost/usage**: `InputTokens`, `OutputTokens`, `CostUsd` are available in `SpeakingEvaluationProviderResult` but are not persisted on the entity. Usage governance tracks AI calls separately via `AiUsageHandler`. Speaking evaluation calls are not yet wired into usage governance tracking. Document gap: speaking evaluation cost is not visible in quality summary.

---

## Part C — Admin Quality Dashboard

### New endpoint

`GET /api/admin/speaking-evaluation/quality-summary`

Returns:
- `configStatus`: Disabled | NoOp | ProviderConfigured | ProviderUnsupported | Enabled
- `providerName`
- `enabled`
- `supportsTranscript`, `supportsPronunciationScore`
- `quality` (the full `SpeakingEvaluationQualitySummaryDto`)

Authorization: Admin role required.

---

## Part D — Dry-Run Learning Signal Model

### New types

- `src/LinguaCoach.Domain/Enums/SpeakingDryRunSignalOutcome.cs`
- `src/LinguaCoach.Domain/Enums/SpeakingDryRunConfidenceBand.cs`
- `src/LinguaCoach.Application/Speaking/SpeakingEvaluationDryRunSignal.cs`

### Signal fields

| Field | Description |
| ----- | ----------- |
| `EvaluationId` | Links back to source evaluation |
| `AttemptId` | Source audio attempt |
| `ConfidenceBand` | Low / Medium / High (null if status blocked before computing) |
| `Outcome` | See enum below |
| `CandidateSkill` | "Speaking" for positive/review signals; null otherwise |
| `BlockedReason` | Human-readable reason if blocked |
| `IsDryRunOnly` | Always true |
| `IsCandidate` | True for Positive or Review outcomes |
| `IsBlocked` | True for all Blocked* outcomes |

### Outcome enum

```
CandidatePositiveSignal   — strong scores, medium/high confidence
CandidateReviewSignal     — mid-range score, medium/high confidence
CandidateNoSignal         — low score, not blocked
BlockedMissingScore       — Completed but OverallScore is null
BlockedFailedEvaluation   — Status == Failed
BlockedUnsupportedProvider — Status == NotSupported or Skipped
BlockedLowConfidence      — confidence too low for any signal
BlockedInsufficientData   — Pending or Evaluating
```

**Not persisted** — computed on demand. Admin-only. Never affects student-visible state.

---

## Part E/F — Mapping Rules and Confidence Gate

### Confidence calculation (Completed evaluations only)

Inputs scored 0–3:
- +1 if OverallScore is present
- +1 if at least one dimension score (Fluency, Completeness, or Relevance) is present
- +1 if FeedbackText is present

Bands:
- 0–1 → Low (blocked)
- 2 → Medium
- 3 → High

### Signal mapping rules

1. Status != Completed → Blocked (Failed, NotSupported/Skipped, Pending/Evaluating each have specific outcomes)
2. Status == Completed, OverallScore == null → BlockedMissingScore
3. Confidence == Low → BlockedLowConfidence
4. Confidence >= Medium:
   - OverallScore ≥ 70 AND CompletenessScore (if present) ≥ 50 AND RelevanceScore (if present) ≥ 50 → CandidatePositiveSignal (skill: Speaking)
   - OverallScore ≥ 40 → CandidateReviewSignal (skill: Speaking)
   - OverallScore < 40 → CandidateNoSignal

**Missing PronunciationScore does not block any signal.** The OpenAI Whisper+GPT pipeline does not claim pronunciation scoring.

---

## Part G — Student UI

No changes. Students see the same Completed/Pending/Failed/NotSupported states from Phase 16F/G. Dry-run outcomes, confidence bands, blocked reasons, and admin diagnostics are never shown to students.

---

## Part H — Admin UI

Added per-attempt dry-run signal column to the speaking submissions table in `admin-student-detail`:

- Column header: "Dry-run signal"
- Per row: outcome, confidence band, candidate skill (if any), blocked reason (if any)
- "Dry-run only" label visible on each row with a signal
- New column `data-testid="dry-run-col-header"` on the header
- New cell `data-testid="speaking-dry-run-signal"` per row

---

## Part I — Usage/Cost Visibility

**Gap documented:** Speaking evaluation cost/latency is not visible in the quality summary because:
- `InputTokens`, `OutputTokens`, `CostUsd` are returned by `SpeakingEvaluationProviderResult` but not persisted on `SpeakingEvaluation`.
- Speaking evaluation calls are not yet registered with the usage governance system (`AiUsageHandler`).

No fake cost data was invented. To add cost visibility in a future phase:
1. Add `InputTokens`, `OutputTokens`, `CostUsd`, `LatencyMs` columns to `SpeakingEvaluation` and a migration.
2. Populate them in `SpeakingEvaluationService.ProcessSingleAsync` after `MarkCompleted`.
3. Include them in the quality summary DTO.

---

## Files Changed

### New — Backend

| File | Purpose |
| ---- | ------- |
| `src/LinguaCoach.Domain/Enums/SpeakingDryRunSignalOutcome.cs` | Dry-run outcome enum |
| `src/LinguaCoach.Domain/Enums/SpeakingDryRunConfidenceBand.cs` | Confidence band enum |
| `src/LinguaCoach.Application/Speaking/SpeakingEvaluationDryRunSignal.cs` | Signal record DTO |
| `src/LinguaCoach.Application/Speaking/SpeakingDryRunSignalMapper.cs` | Pure mapping logic |
| `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationQualityQuery.cs` | Quality query interface |
| `src/LinguaCoach.Application/Speaking/SpeakingEvaluationQualitySummaryDto.cs` | Quality summary DTO |
| `src/LinguaCoach.Infrastructure/Speaking/SpeakingEvaluationQualityHandler.cs` | EF Core implementation |

### Modified — Backend

| File | Change |
| ---- | ------ |
| `src/LinguaCoach.Api/Controllers/AdminSpeakingEvaluationController.cs` | Added `GET quality-summary` endpoint; injected `ISpeakingEvaluationQualityQuery` |
| `src/LinguaCoach.Application/Admin/AdminStudentSpeakingQueries.cs` | Added 4 dry-run fields to `AdminStudentSpeakingAttemptDto` |
| `src/LinguaCoach.Infrastructure/Admin/AdminStudentSpeakingAttemptsHandler.cs` | Compute dry-run signal per attempt using `SpeakingDryRunSignalMapper.MapFromFields` |
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered `ISpeakingEvaluationQualityQuery → SpeakingEvaluationQualityHandler` |

### New — Tests

| File | Tests |
| ---- | ----- |
| `tests/LinguaCoach.UnitTests/Speaking/SpeakingDryRunSignalMapperTests.cs` | 15 unit tests |
| `tests/LinguaCoach.IntegrationTests/Api/SpeakingEvaluationQualityIntegrationTests.cs` | 10 integration tests |

### Modified — Tests

| File | Change |
| ---- | ------ |
| `tests/LinguaCoach.IntegrationTests/Api/SpeakingEvaluationQualityIntegrationTests.cs` | Inherits factory from Phase 16G provider tests |

### Modified — Frontend

| File | Change |
| ---- | ------ |
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | Added 4 dry-run fields to `AdminStudentSpeakingAttempt`; added `SpeakingEvaluationQualityMetrics` and `AdminSpeakingEvaluationQualitySummary` interfaces |
| `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` | Added `getSpeakingEvaluationQualitySummary()` |
| `admin-student-detail.component.html` | Added "Dry-run signal" column with outcome, confidence, skill, blocked reason |
| `admin-student-detail.component.spec.ts` | Added 4 dry-run null fields to both test attempt objects |

---

## Proof: Mastery/CEFR/Learning Plan Not Changed

1. `SpeakingDryRunSignalMapper` is a pure static class with no DB access and no side effects.
2. `SpeakingEvaluationQualityHandler` only calls `_db.SpeakingEvaluations.ToListAsync(ct)` — read-only.
3. No mastery service, no learning plan service, no CEFR update service is called anywhere in this phase.
4. Integration test `DryRunSignals_NeverUpdateMastery` verifies CEFR and `StudentSkillProfiles` count are unchanged after running the evaluation job.
5. Architecture tests enforce layer dependencies — Domain/Application cannot call Infrastructure.

---

## Test Results

| Suite | Passed | New |
| ----- | ------ | --- |
| Backend unit (LinguaCoach.UnitTests) | 1,528 | +15 |
| Backend integration (LinguaCoach.IntegrationTests) | 1,260 | +10 |
| Architecture (LinguaCoach.ArchitectureTests) | 3 | 0 |
| Angular unit (Karma/Jasmine) | 1,525 | 0 (existing tests updated) |
| Total | **4,316** | **+25** |

Angular production build: clean.
Playwright: not extended in this phase (no student-visible changes). Existing 262 pass / 3 skipped unchanged.

---

## Known Limitations

1. **Latency not tracked**: no `LatencyMs` on `SpeakingEvaluation`. Add in a future migration.
2. **Cost not tracked**: `CostUsd` is returned by the provider but not persisted. Usage governance does not cover speaking evaluation calls yet.
3. **Audio duration not tracked**: not stored. Would require a column on `ActivityAttempt` or `SpeakingEvaluation`.
4. **Pronunciation scoring not available**: OpenAI Whisper+GPT does not output pronunciation scores. The field is nullable and excluded from all signal mapping.
5. **Quality summary loads all evaluations**: suitable for early-phase volumes. Will need pagination/aggregation at scale.
6. **Dry-run signals are computed, not stored**: admin page recomputes them on every request. This is correct for dry-run but means historical signal snapshots are not available.

---

## Recommendations for Next Phase

**Do not start yet:**
- Applying AI scores to real mastery
- Updating CEFR from speaking results
- Updating Learning Plan from speaking results

**Safe to consider when quality data shows:**
- CompletionRate > 90%
- NullOverallScoreRate < 10%
- DryRunCandidatePositiveSignal rate is consistent with expected student performance

**Suggested next phase (16I):**
- Persist `LatencyMs`, `InputTokens`, `OutputTokens`, `CostUsd` on `SpeakingEvaluation`
- Wire speaking evaluation cost into usage governance
- After sufficient dry-run data, evaluate whether to enable mastery signal application

---

## Final Verdict

Phase 16H complete. Quality metrics are observable. Dry-run signal mapping is correct and conservative. Mastery, CEFR, and Learning Plan are demonstrably unchanged. All tests pass.
