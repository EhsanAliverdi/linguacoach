# Phase 10W-5A — Admin Notification Center: Engineering Review

**Date:** 2026-06-22
**Sprint:** Phase 10W — Notification System
**Feature:** Admin Notification Center (read-only + outbox management)

---

## Files Reviewed / Created

### Backend
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs` — new
- `src/LinguaCoach.Domain/Entities/NotificationOutboxItem.cs` — added `MarkCancelled()`
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` — new
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration added
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — 4 new endpoints

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — DTOs added
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — 4 methods added
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts` — new
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` — new
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` — new
- `src/LinguaCoach.Web/src/app/app.routes.ts` — route added
- `src/LinguaCoach.Web/src/app/layouts/admin-app-layout/admin-app-layout.component.html` — nav items added

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminNotificationEndpointTests.cs` — 18 integration tests

---

## Findings

### Priority: None (clean pass)
- All 4 API endpoints protected by `[Authorize(Roles = "Admin")]` — verified by 401/403 tests
- No raw secrets, reset tokens, or passwords exposed in DTOs — verified by assertion in tests
- `MarkCancelled()` follows the same pattern as `ResetForRetry()` on the domain entity
- Batch email resolution via `UserManager<ApplicationUser>` avoids N+1 lookups per page
- Retry guard: only `Failed` or `Queued` items may be retried; returns 400 + message otherwise
- Cancel guard: `Delivered` and `Archived` items cannot be cancelled; returns 400 + message otherwise

---

## Gates

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | 0 errors, 0 warnings |
| `dotnet test --configuration Release` | 2176 passed (1287 unit + 886 integration + 3 architecture) |
| `npm run build -- --configuration production` | Success |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 956 passed |

---

## Security Decisions

Per spec constraints:
- DTOs include: id, recipientUserId, recipientEmail, title, channel, category, severity, status, dates, attemptCount, lastError, deepLinkUrl
- DTOs exclude: raw passwords, reset tokens, sensitive metadata
- Reset-password behavior: untouched
- Notification bell user APIs: untouched

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| `MarkCancelled()` on domain entity | Consistent with `ResetForRetry()` precedent |
| Retry returns 204 (no content) | Admin action is idempotent from caller perspective |
| Cancel returns 204 (no content) | Consistent with retry |
| Batch email lookup per page | Avoids N+1; acceptable since page size is bounded at 100 |
| Notifications tab loads on init | Both tabs load eagerly; avoids stale display on tab switch |

---

## Risks / Unresolved Questions

- `lastError` field from outbox may contain provider-specific error strings — these are already non-sensitive (SMTP error codes, HTTP status text) but should be reviewed if SMS is added (10W-6).
- No send-notification UI per spec constraint; deferred to 10W-5B.

---

## Implementation Tasks Produced

None — all tasks in scope for 10W-5A are complete.

---

## Final Verdict

**SHIP.** All gates green. Security constraints satisfied. No deferred items in scope.

## Next Recommended Action

Mark 10W-5A complete in sprint doc. Begin 10W-5B (Admin Send Notification) or 10W-6 (SMS provider) per backlog priority.
