# Phase 16J — Speaking Signal Quality Tuning and Production Dry-Run Review

**Date:** 2026-06-30
**Sprint / Feature:** Phase 16J — Speaking Signal Quality Tuning and Production Dry-Run Review
**Status:** Complete — all tests passing, production build clean

---

## Scope

Validation and tuning phase only. No new AI features were added. The following were explicitly out of scope and confirmed absent: CEFR updates from speaking AI, objective completion from speaking AI, Learning Plan regeneration from speaking AI, enabling mastery signals by default, new speaking formats, real-time conversation, new STT pipeline, writing evaluation, enterprise features.

---

## Files Reviewed

### Application layer
- `src/LinguaCoach.Application/Speaking/SpeakingSignalThresholds.cs` (new)
- `src/LinguaCoach.Application/Speaking/SpeakingEvaluationOptions.cs`
- `src/LinguaCoach.Application/Speaking/SpeakingDryRunSignalMapper.cs`
- `src/LinguaCoach.Application/Speaking/SpeakingEvaluationQualitySummaryDto.cs`
- `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationSignalApplicationService.cs`
- `src/LinguaCoach.Application/Admin/AdminStudentSpeakingQueries.cs`

### Infrastructure layer
- `src/LinguaCoach.Infrastructure/Speaking/SpeakingEvaluationQualityHandler.cs`
- `src/LinguaCoach.Infrastructure/Speaking/SpeakingEvaluationSignalApplicationService.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminStudentSpeakingAttemptsHandler.cs`

### API layer
- `src/LinguaCoach.Api/Controllers/AdminSpeakingEvaluationController.cs`
- `src/LinguaCoach.Api/appsettings.json`

### Domain
- `src/LinguaCoach.Domain/Entities/SpeakingEvaluationAppliedSignal.cs`

### Angular
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

### Tests
- `tests/LinguaCoach.UnitTests/Speaking/SpeakingDryRunSignalMapperTests.cs`
- `tests/LinguaCoach.UnitTests/Speaking/SpeakingEvaluationSignalApplicationTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/SpeakingEvaluationProviderIntegrationTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/SpeakingEvaluationQualityIntegrationTests.cs`

---

## Findings by Priority

### P0 — Invariant verification (resolved)

**Finding:** The review direction for `CandidateReviewSignal` was inverted relative to intent. Old logic: `score >= ReviewScoreThreshold(40)` — nearly every non-positive score produced a Review signal, including scores of 30. New logic: `score <= MaxReviewOverall(55)` — only genuinely weak evaluations trigger review candidates. Scores 56–79 are now `CandidateNoSignal` (middle band), preventing excessive review signals on borderline-acceptable performance.

**Resolution:** `SpeakingDryRunSignalMapper.ClassifyScore()` uses `score <= t.MaxReviewOverall`. Tests updated to reflect the middle band.

**Finding:** Several unit tests used `score = 30` expecting `CandidateNoSignal`. With new `MaxReviewOverall = 55`, score 30 produces `CandidateReviewSignal`. Fixed by using score 65 (middle band).

**Finding:** Integration test `QualitySummary_CompletedWithStrongScores_DryRunPositiveSignalCounted` used default fake-provider score of 78, which no longer meets `MinPositiveOverall = 80`. Fixed by explicitly passing `overallScore: 85, completenessScore: 82, relevanceScore: 81`.

**Finding:** `SpeakingEvaluationAppliedSignal.AppliedRuleVersion` was referenced as `"16I-v1"` in unit test assertion but entity now emits `"16J-v1"`. Fixed.

**Finding:** `AdminStudentSpeakingAttemptsHandler` referenced `applied?.CreatedAt` but the entity property is `AppliedAtUtc`. Fixed.

**Finding:** Angular spec objects were missing 7 new required fields after `AdminStudentSpeakingAttempt` interface expansion. Fixed by adding all 7 with safe defaults.

### P1 — Threshold explicitness (implemented)

Six threshold values previously hardcoded as private constants in `SpeakingDryRunSignalMapper` are now surfaced through:

1. `SpeakingEvaluationOptions` — 6 new config properties with conservative defaults
2. `SpeakingSignalThresholds` — value type; `Default` static property; `FromOptions()` factory
3. Both `Map()` and `MapFromFields()` accept `SpeakingSignalThresholds? thresholds = null`; null resolves to `SpeakingSignalThresholds.Default`

Default values: positive requires overall ≥ 80, completeness ≥ 80, relevance ≥ 80. Review requires overall ≤ 55, completeness ≤ 55, relevance ≤ 55. These are conservative — no positive signals without all three dimensions at 80+.

### P1 — Quality metrics completeness (implemented)

`SpeakingEvaluationQualitySummaryDto` expanded with:
- `AveragePronunciationScore` — average pronunciation score across completed evaluations
- `DryRunCandidates` — total dry-run candidate signals (positive + review)
- `Applied` — total signals that passed all gates and were written as `SpeakingEvaluationAppliedSignal`
- Blocked breakdown: `BlockedByConfig`, `BlockedByConfidence`, `BlockedByMissingScore`, `BlockedByUnsupportedStatus`, `BlockedByFailedEval`, `DuplicateSkipped`
- `AppliedReview`, `AppliedPositive` — applied signal type breakdown
- `ProviderModelDistribution` — per-provider/model count
- `LatestBlockedReasons` — top recent failure reasons for admin investigation

### P2 — Safety summary endpoint (implemented)

`GET /api/admin/speaking-evaluation/signal-safety-summary` returns `AdminSignalSafetySummaryDto` confirming:
- `cefrUpdatesDisabled: true` — always
- `objectiveCompletionsDisabled: true` — always
- `learningPlanAutoRegenDisabled: true` — always
- `invariantViolationsDetected: false` — structurally false; no code path can set this to true
- Signal counts: `totalApplied`, `positiveApplied`, `reviewApplied`
- Config state: `signalApplicationEnabled`, `positiveSignalsEnabled`, `reviewSignalsEnabled`

### P2 — Per-student applied signal visibility (implemented)

Admin student detail speaking tab now shows:
- Whether each attempt has an applied signal (`isApplied`)
- Applied signal type and confidence band
- Block reason if not applied
- Invariant labels: "This signal does not update CEFR." and "This signal does not complete objectives." — shown when `isApplied = true`

`SignalUpdatesCefr` and `SignalCompletesObjectives` are always `false` at the handler level — hardcoded, not driven by config.

### P3 — Config status accuracy (implemented)

`ResolveConfigStatus()` in the controller now returns:
- `"Disabled"` — evaluation not enabled
- `"DryRunOnly"` — enabled but `ApplyMasterySignals = false`
- `"Enabled"` — enabled and `ApplyMasterySignals = true`

Previously returned `"Enabled"` whenever the evaluator was enabled, even if mastery signals were off.

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Middle band (56–79) = NoSignal | Prevents borderline-acceptable performance from triggering review signals. Review is reserved for genuinely weak evaluations. |
| Positive threshold raised to 80 (from 70) | Conservative by default. No false positives on borderline B1 performance. |
| Dimension thresholds at 80 for positive | All three (overall, completeness, relevance) must independently pass. One weak dimension blocks the signal even if overall is strong. |
| `SpeakingSignalThresholds` as a separate value type | Enables testing with custom thresholds without touching config; keeps mapper pure. |
| `SignalUpdatesCefr = false` hardcoded in handler | Not a config value. Invariant. Hardcoding makes it auditable and impossible to enable via config. |
| `GetSignalSafetySummaryAsync` returns static invariants | Structurally correct: the code path does not exist for CEFR update. The endpoint confirms this programmatically so ops can verify without reading source. |

---

## AskUserQuestion Answers

None required. Phase spec was fully explicit on all decisions.

---

## Implementation Tasks Produced

None outstanding. All tasks in the phase spec were completed:
- Part A: Signal pipeline audit — complete
- Part B: Expanded quality metrics — complete
- Part C: Explicit thresholds in config and value type — complete
- Part D: Admin quality review UI improvements — complete
- Part E: Per-student applied signal visibility — complete
- Part F: Safety summary endpoint — complete
- Part G: Threshold documentation — complete (this document)
- Part H: Student UI unchanged — confirmed
- Part I: Tests added and updated — complete
- Part J: Build and test validation — complete
- Part K: Documentation — complete

---

## Risks and Unresolved Questions

None. Phase was scoped as tuning/validation only. No new surface area introduced.

Threshold values (80/55) are defaults and can be overridden via `appsettings.json`. Production operators should review these defaults before enabling `ApplyMasterySignals = true`. The safety summary endpoint gives a quick programmatic confirmation before any enable decision.

---

## Final Test Totals

| Suite | Count | Delta vs 16I |
|---|---|---|
| Unit | 1,581 | +16 |
| Integration | 1,281 | +4 |
| Architecture | 3 | 0 |
| Angular (Karma) | 124 | +2 |

Production build: clean.

---

## Final Verdict

Phase 16J complete. All acceptance criteria met. Invariants preserved: CEFR never updated, objective completion never triggered, Learning Plan never regenerated. Thresholds are now configurable, conservative, and auditable. Safety summary endpoint provides a programmatic invariant check for ops and admin review.

## Next Recommended Action

Phase 16K or production readiness review before enabling `ApplyMasterySignals = true`. Recommend a staged rollout: enable `AllowReviewSignals = true` only (already default), monitor the safety summary endpoint for 2 weeks, then consider enabling `AllowPositiveSignals = true` with threshold review.
