# Phase 17B: Writing Evaluation Quality Validation — Engineering Review

**Date:** 2026-06-30
**Sprint:** Phase 17B
**Related Phase:** Phase 17A (Writing Evaluation Foundation)
**Author:** Claude (automated agent)

---

## Summary

Phase 17B adds dry-run signal computation, pipeline quality summary endpoints, and admin UI hooks for the writing evaluation pipeline. This mirrors the speaking dry-run pattern from Phase 16H. No signals are applied to mastery, CEFR, objectives, or the Learning Plan.

---

## Files Reviewed

- `src/LinguaCoach.Domain/Entities/WritingEvaluation.cs`
- `src/LinguaCoach.Domain/Enums/WritingEvaluationStatus.cs`
- `src/LinguaCoach.Application/Writing/IAdminWritingEvaluationQuery.cs`
- `src/LinguaCoach.Application/Writing/WritingEvaluationOptions.cs`
- `src/LinguaCoach.Infrastructure/Writing/AdminWritingEvaluationHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminWritingEvaluationController.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`

---

## Files Created / Modified

### Domain Enums
- `src/LinguaCoach.Domain/Enums/WritingDryRunSignalOutcome.cs` — NEW
- `src/LinguaCoach.Domain/Enums/WritingDryRunConfidenceBand.cs` — NEW

### Application Layer
- `src/LinguaCoach.Application/Writing/WritingEvaluationDryRunSignal.cs` — NEW
- `src/LinguaCoach.Application/Writing/WritingDryRunSignalMapper.cs` — NEW (static, pure logic, no DB)
- `src/LinguaCoach.Application/Writing/WritingEvaluationQualitySummaryDto.cs` — NEW
- `src/LinguaCoach.Application/Writing/WritingEvaluationDryRunSignalDto.cs` — NEW
- `src/LinguaCoach.Application/Writing/WritingEvaluationWithDryRunDto.cs` — NEW
- `src/LinguaCoach.Application/Writing/IAdminWritingEvaluationQuery.cs` — MODIFIED (2 methods added)

### Infrastructure Layer
- `src/LinguaCoach.Infrastructure/Writing/AdminWritingEvaluationHandler.cs` — MODIFIED (implements new methods, injects WritingEvaluationOptions)

### API Layer
- `src/LinguaCoach.Api/Controllers/AdminWritingEvaluationController.cs` — MODIFIED (2 endpoints added)

### Angular Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — MODIFIED (3 interfaces added)
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — MODIFIED (2 methods added)

### Tests
- `tests/LinguaCoach.UnitTests/Writing/WritingDryRunSignalMapperTests.cs` — NEW (15 tests)
- `tests/LinguaCoach.IntegrationTests/Api/WritingEvaluationQualitySummaryTests.cs` — NEW (6 tests)

---

## Findings by Priority

### P0 — Safety Invariants

All satisfied:

| Invariant | Status |
|-----------|--------|
| No mastery update | Confirmed — mapper has no mastery mutation path |
| No CEFR update | Confirmed — no `UpdateCefr` field on signal model |
| No objective completion | Confirmed — no `CompleteObjective` field on signal model |
| No Learning Plan regen | Confirmed — mapper is pure read-only |
| WritingEvaluationOptions.AllowMasterySignals | Always returns `false` |

### P1 — Correctness

Confidence computation:
- **High**: status=Completed, overall present, 2+ dimension scores, feedbackText present, correctedText present
- **Medium**: status=Completed, overall present, 1+ dimension score, feedbackText present
- **Low**: everything else

Signal assignment (Completed, overall present, confidence >= Medium):
- `CandidatePositiveSignal`: High confidence, overall >= 75, task_completion >= 75 (or null)
- `CandidateReviewSignal`: Medium/High confidence, overall <= 55 OR grammar <= 55 OR coherence <= 55
- `CandidateNoSignal`: all other Completed cases

SuggestedMasteryDelta = clamp((overall - 60) / 100, 0.05, 0.25) for Positive signals only.

### P2 — Architecture

Clean Architecture boundaries preserved:
- Domain enums have no EF or ASP.NET dependencies
- Mapper is a pure static Application-layer class
- Handler injects only `LinguaCoachDbContext` and `IOptions<WritingEvaluationOptions>`
- Controller depends only on `IAdminWritingEvaluationQuery` (Application interface)

### P3 — API Contract

Two new admin endpoints:
- `GET /api/admin/writing-evaluation/quality-summary` — pipeline-wide metrics and dry-run outcome breakdown
- `GET /api/admin/writing-evaluation/{id}/dry-run` — per-evaluation dry-run signal preview

Both require `Admin` role. Anonymous returns 401. Student returns 403.

---

## Decisions Made

1. `WritingDryRunSignalMapper` is a static class (same pattern as speaking mapper). No DI registration needed.
2. `WritingEvaluationWithDryRunDto` is a separate sealed class (not inheriting from `WritingEvaluationDto` which is `sealed record` and cannot be extended).
3. New endpoints use absolute path override (`/api/admin/writing-evaluation/...`) on the controller that is otherwise rooted at `api/admin/students`.
4. `AdminWritingEvaluationHandler` now requires `IOptions<WritingEvaluationOptions>` — DI resolves this automatically since `WritingEvaluationOptions` was already registered in Phase 17A.

---

## Test Results

| Suite | Before | After | New |
|-------|--------|-------|-----|
| Unit | 1598 | 1613 | +15 |
| Integration | 1296 | 1302 | +6 |
| Architecture | 3 | 3 | 0 |
| Angular | 1527 | 1527 | 0 |

All tests pass. Build is clean (0 errors).

---

## Risks and Limitations

- No Angular UI component renders the quality summary yet (Part J). The service and model interfaces are wired but no admin page displays them. A future phase should add the admin dashboard section.
- `WritingEvaluationOptions.AllowMasterySignals` is hard-coded `false`. Phase 17C will add controlled enablement, mirroring Phase 16I for speaking.
- CorrectedTextAvailabilityRate is computed for all completed evaluations regardless of provider support. NoOp provider will always have 0% corrected text.

---

## Final Verdict

Phase 17B is complete and safe. All invariants are preserved. Backend quality summary and dry-run endpoints are functional and tested. Angular service is wired. No mastery, CEFR, objective, or Learning Plan state is touched.

---

## Next Recommended Action

Phase 17C: Enable writing mastery signal application with full config control (mirroring Phase 16I speaking signal integration). Add admin Angular UI component for quality summary dashboard.
