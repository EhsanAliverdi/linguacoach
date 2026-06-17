# Phase 10R — Usage Governance: Token Tracking & Quota Enforcement

**Date:** 2026-06-18
**Sprint:** Phase 10R
**Type:** Implementation review
**Related architecture doc:** `docs/architecture/usage-governance.md`

---

## Summary

Phase 10R adds enterprise-grade usage governance to SpeakPath. It tracks all AI feature usage per student, supports configurable quota policies, and enforces hard limits by blocking expensive AI calls before they occur.

---

## Files reviewed

All new files under:

- `src/LinguaCoach.Domain/Enums/` (4 new enums)
- `src/LinguaCoach.Domain/Entities/` (7 new entities)
- `src/LinguaCoach.Application/UsageGovernance/` (4 new interfaces/DTOs)
- `src/LinguaCoach.Infrastructure/UsageGovernance/` (2 new services)
- `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs` (modified)
- `src/LinguaCoach.Persistence/` (7 EF configs, migration, seeder)
- `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs`
- `src/LinguaCoach.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/`
- `tests/LinguaCoach.IntegrationTests/UsageGovernance/`

---

## Findings

### P0 — Critical

None.

### P1 — High

None.

### P2 — Medium

**Cost is caller-estimated, not centrally computed.**
The system stores `EstimatedCost` passed in by the caller. There is no provider pricing table. This is acceptable for Phase 10R (explicit scope exclusion) but must be addressed before billing goes live.

**Monthly/weekly limits stored but not checked.**
`UsagePolicyRule` has `WeeklyLimit` and `MonthlyLimit` columns. The quota service only enforces `DailyLimit` today. This is documented as deferred.

### P3 — Low

**AiExecutionService GovernanceKeyMap is a static dictionary.**
Mapping from internal AI keys to governance keys is hardcoded. Acceptable for Phase 10R; should move to feature registry or configuration when more features are added.

**StudentUsageDaily per-feature counters are hardcoded columns.**
`WritingEvaluations`, `SpeakingEvaluations`, `LessonGenerations`, etc. are explicit columns. Adding new expensive features requires a migration. A JSON blob or separate rows-per-feature model would be more flexible long term.

---

## Architecture decisions

### Decision 1: Quota enforcement in AiExecutionService, not HTTP middleware

Pre-call quota check happens inside `AiExecutionService.ExecuteAsync` rather than in a request filter. This ensures the check is co-located with the call, failed calls are never recorded, and the check can pass `estimatedUnits` before the call runs.

### Decision 2: UsageEvent is append-only ledger; StudentUsageDaily is the read model

Quota checks read `StudentUsageDaily` (one row per student per day), not the full event ledger. This keeps quota enforcement O(1) per check rather than O(events).

### Decision 3: Policy hierarchy is Global → Student only for Phase 10R

Workspace and cohort inheritance are structurally present (ScopeType enum, DB column) but not implemented. Deferred to a future phase.

### Decision 4: HTTP 429 with structured body on QuotaExceededException

`GlobalExceptionMiddleware` maps `QuotaExceededException` to 429 with `{message, featureKey, availableAlternatives, resetAt, correlationId}`. Client can show alternatives without parsing the error message.

---

## Test coverage

| Layer | Tests |
|---|---|
| Domain (unit) | QuotaDecision factories, UsagePolicyRule validation, StudentUsageDaily.Apply() |
| DB integration | 16 tests: seeding, record+aggregate, hard limit, track-only, policy resolution, audit log, daily rollup, usage summary, admin CRUD |
| API integration | 5 tests: list features, 403 non-admin, create policy, student usage, non-admin assign |
| Angular unit | 8 component tests + 6 service HTTP tests |
| Total | ~35 new tests; full suite 1885 .NET + 302 Angular |

---

## CI gate results

All gates passed on 2026-06-18:

- `dotnet build` — clean
- `dotnet test` — 1885 tests, 0 failures
- `ng build` — clean production build
- `ng test` — 302 tests, 0 failures

---

## Risks

1. **Cost estimation accuracy** — EstimatedCost from callers may diverge from actual provider invoices. No reconciliation mechanism yet.
2. **Daily aggregate desync** — If `UsageEvent` is saved but `StudentUsageDaily` upsert fails (partial transaction), the aggregate lags. Both writes are in the same EF `SaveChangesAsync` call; a future improvement is an explicit transaction scope.
3. **Quota bypass via direct DB** — No domain invariant prevents inserting UsageEvents without going through `QuotaService.RecordAsync`. All callers must use the service.

---

## Decisions made during implementation

- Fixture isolation: used `UsageGovernanceTestFactory` subclass + `IAsyncLifetime` per-class SQLite to avoid xUnit parallel fixture sharing bugs.
- FK constraint: DB-layer tests create real `StudentProfile` rows via `CreateStudentAsync()` helper.
- `CreateUsagePolicyRequest` uses positional record parameters, not named.

---

## Implementation tasks produced

None — Phase 10R is complete. Deferred items tracked in `TODOS.md`.

---

## Final verdict

Phase 10R is production-ready for its defined scope. The foundation for enterprise quota governance is in place. Deferred items (billing, workspace/cohort inheritance, monthly enforcement, provider pricing) are clearly bounded and do not affect current functionality.

---

## Next recommended action

Phase 10S or backlog grooming. See `TODOS.md` for deferred Phase 10R follow-ups.
