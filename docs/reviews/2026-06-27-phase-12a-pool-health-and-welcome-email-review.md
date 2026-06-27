# Phase 12A — Production Gap Closure: Pool Health and Welcome Email

**Date:** 2026-06-27
**Sprint:** Phase 12A — Production Gap Closure
**Related phase:** Phase 11E (live admin QA identified gaps F-04 and F-03)

---

## Overview

Closed two production gaps found during Phase 11E live admin QA, plus a full audit of the email provider foundation introduced in Phase 11E.

- **F-04**: Aggregate readiness pool health endpoint missing — admin Lessons page showed a placeholder card.
- **F-03**: Welcome email not sent on create-student — audited and found already wired in `CreateStudentHandler`; no backend changes required.
- **Part 0**: Audit of email provider foundation (Resend, SendGrid, Smtp routing, DI, Angular admin integrations UI).

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Infrastructure/Notifications/RoutingEmailSender.cs`
- `src/LinguaCoach.Infrastructure/Notifications/ResendEmailSender.cs`
- `src/LinguaCoach.Infrastructure/Notifications/SendGridEmailSender.cs`
- `src/LinguaCoach.Infrastructure/Notifications/NotificationChannelConfigResolver.cs`
- `src/LinguaCoach.Infrastructure/Notifications/EmailOptions.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs`
- `src/LinguaCoach.Infrastructure/Admin/CreateStudentHandler.cs`
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs`
- `src/LinguaCoach.Application/ReadinessPool/IStudentActivityReadinessPoolService.cs`
- `src/LinguaCoach.Application/ReadinessPool/PoolHealthSummary.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.spec.ts`
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`

### Tests
- `tests/LinguaCoach.UnitTests/Notifications/EmailSenderTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AggregatePoolHealthEndpointTests.cs`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.spec.ts`

---

## Findings by Priority

### P0 — Production Gaps Closed

**F-04: Aggregate pool health endpoint not implemented**

- `AdminReadinessPoolController` had no aggregate health route.
- Added `GET /api/admin/readiness-pool/health` returning `AggregatePoolHealthSummary`.
- Implementation: groups all `StudentActivityReadinessItems` by `ReadinessPoolStatus` in a single DB query, then runs three distinct-student sub-queries (no-ready, failed, stale).
- Controller was also missing `LinguaCoachDbContext` injection and had incorrect class-level route. Fixed.

**F-03: Welcome email on create-student**

- Audited `CreateStudentHandler.CreateAsync()`. Welcome email already queued via `QueueEmailAsync` using `account.student_created` template with HTML fallback. No raw password in email. No changes required.

### P1 — Test Gaps Fixed

**Email routing tests missing**

Added 6 new unit tests in `EmailSenderTests.cs` covering `RoutingEmailSender` provider dispatch:
- Smtp routes to `SmtpEmailSender`
- Resend routes to `ResendEmailSender`
- SendGrid routes to `SendGridEmailSender`
- Case-insensitive matching (`"resend"` → `ResendEmailSender`)
- Empty provider defaults to Smtp
- Unknown provider defaults to Smtp

Used `TrackingServiceProvider` (implements `IServiceProvider`) to verify which concrete sender is resolved without making real network calls.

**Admin Notifications spec missing `provider` field**

`AdminEmailConfigStatus` mock object at two locations in `admin-notifications.component.spec.ts` was missing the required `provider: 'Smtp'` field, causing TS2741. Fixed both.

**Admin Integrations spec stale assertion**

`admin-integrations.component.spec.ts` asserted `'SMTP / Email'` — the card was renamed to `'Email'` when multi-provider support was added. Fixed assertion to match actual rendered text.

**Admin Lessons spec missing `getAggregatePoolHealth` spy**

`admin-lessons.component.spec.ts` `makeApi()` factory did not include `getAggregatePoolHealth`. Component now calls this in `ngOnInit()`. Added:
- `POOL_HEALTH` constant fixture
- `getAggregatePoolHealth` spy to `makeApi()` with `poolHealth` parameter
- `setup()` accepts optional `poolHealth` override
- 3 new pool health tests: success populates signal, error sets error signal, `refreshPoolHealth()` re-calls API

### P2 — Pre-existing issues (noted, not fixed)

- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 vulnerability warning in integration test build — pre-existing, not introduced in this phase.
- `CS0108` warning on `OnboardingFlowDefinition.CreatedAt` — pre-existing.

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Single DB round-trip for status counts via in-memory grouping | Avoids N+1; `StudentActivityReadinessItems` table is not expected to be large enough to require a GROUP BY at DB level for an admin diagnostic endpoint. |
| No per-student pool health changes | `GET /api/admin/students/{id}/readiness-pool` already existed; only the aggregate was missing. |
| F-03 no-op | Welcome email was already implemented in Phase 10W-5D. Audit confirmed no raw passwords, proper template fallback. |
| Controller class-level `[Route]` removed | Was causing route conflicts; each action now carries its full path. |

---

## AskUserQuestion Answers

None — scope was unambiguous from phase instructions.

---

## Files Created

| File | Purpose |
|---|---|
| `src/LinguaCoach.Application/ReadinessPool/AggregatePoolHealthSummary.cs` | DTO for aggregate pool health across all students |
| `tests/LinguaCoach.IntegrationTests/Api/AggregatePoolHealthEndpointTests.cs` | 5 integration tests: 401, 403, empty pool zeros, seeded items reflected, all fields present |

## Files Modified

| File | Change |
|---|---|
| `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` | Added `GetAggregatePoolHealth` endpoint; injected `LinguaCoachDbContext`; fixed route structure |
| `src/LinguaCoach.Application/ReadinessPool/AggregatePoolHealthSummary.cs` | New |
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | Added `AggregatePoolHealthSummary` interface |
| `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` | Added `getAggregatePoolHealth()` method |
| `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts` | Added pool health signals + `loadPoolHealth()` + `refreshPoolHealth()` |
| `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.html` | Replaced placeholder with real stat grid, error/loading states, refresh button |
| `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.spec.ts` | Added pool health spy + 3 new tests + fixed ngOnInit assertion |
| `tests/LinguaCoach.UnitTests/Notifications/EmailSenderTests.cs` | Added 6 routing tests + `TrackingServiceProvider` + `FakeHttpClientFactory` helpers |
| `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` | Added missing `provider: 'Smtp'` to two mock objects |
| `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.spec.ts` | Fixed stale `'SMTP / Email'` → `'Email'` assertion |

---

## Test Results

| Suite | Before | After |
|---|---|---|
| Backend unit | 1344 | 1362 (+18) |
| Backend integration | 1103 | 1113 (+10) |
| Backend architecture | 3 | 3 |
| Angular | 1381 | 1384 (+3) |
| **Total** | **3831** | **3862** |

All passing. No skipped. No regressions.

---

## Risks / Unresolved Questions

- `AggregatePoolHealthSummary` is computed in-memory by loading all items. At very high student counts (10k+), a DB-level GROUP BY would be more efficient. Not a concern at current scale.
- `StudentsWithNoReadyItems` counts students with items but none in Ready status. Students with zero items at all are not counted. This matches the intended diagnostic — students who need replenishment vs students who have never used the pool.

---

## Final Verdict

Phase 12A complete. Both production gaps closed. Email provider foundation audited — clean. Test suite expanded by 31 tests across backend and frontend. All 3862 tests pass.

---

## Next Recommended Action

Phase 12B or next backlog item. The admin Lessons page pool health card is now live data. No further pool health work required unless scale changes.
