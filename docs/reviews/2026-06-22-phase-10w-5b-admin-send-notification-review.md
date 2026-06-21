# Phase 10W-5B — Admin Send Notification: Engineering Review

**Date:** 2026-06-22
**Sprint:** Phase 10W — Notification System
**Feature:** Admin Send Notification (manual queue to one student via InApp/Email)

---

## Files Changed

### Backend
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs` — added `AdminSendNotificationCommand`, `AdminSendNotificationResult`, `SendNotificationAsync` to `IAdminNotificationHandler`
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` — implemented `SendNotificationAsync`; injected `INotificationService`
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — added `POST /api/admin/notifications/send` + `AdminSendNotificationRequest` record

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminSendNotificationEndpointTests.cs` — 14 new integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `AdminSendNotificationRequest`, `AdminSendNotificationResult`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — added `sendAdminNotification()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts` — extended with send form state, `openSendForm()`, `closeSendForm()`, `lookupRecipient()`, `submitSend()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` — added "Send notification" header button + slide-over form
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` — 16 new send-form tests (total 28 component tests)

---

## Endpoint

`POST /api/admin/notifications/send`

### Request
```json
{
  "recipientUserIds": ["<guid>"],
  "channels": ["InApp", "Email"],
  "title": "...",
  "body": "...",
  "category": "Admin",
  "severity": "Info",
  "deepLinkUrl": null,
  "expiresAtUtc": null
}
```

### Response
```json
{
  "requestedRecipientCount": 1,
  "queuedCount": 2,
  "skippedCount": 0,
  "channelsQueued": ["InApp", "Email"],
  "errors": []
}
```

---

## Supported Channels

| Channel | Supported | Behavior |
|---------|-----------|----------|
| InApp | Yes | Creates `Notification` + queued outbox row |
| Email | Yes | Creates queued outbox row; dispatch job sends asynchronously |
| SMS | No | Rejected with 400 + message; deferred to 10W-6 |

---

## Validation

- Empty recipient list → 400
- Blank title → 400
- Blank body → 400
- Empty channel list → 400
- SMS channel → 400 with "SMS channel is not yet supported"
- Past `expiresAtUtc` → 400
- Unknown user ID → 200, queued=0, skipped=1, errors list populated
- Unknown category/severity → 400

---

## Security

- Endpoint requires `[Authorize(Roles = "Admin")]`
- Response contains no passwords, reset tokens, or raw secrets
- Log statement records: category, severity, recipient count, queued count, channels — not body content
- `deepLinkUrl` is an optional internal navigation path; no secrets stored

---

## Audit / Logging

`AdminAuditLog` entity exists but integration with this handler was skipped: the handler already uses structured `ILogger` with admin user ID, channel, counts. This avoids a full `AdminAuditLog` entity write for a logging-level concern. Documented as a follow-up for 10W-5C or a dedicated audit sweep.

---

## Recipient Selection UX

- Single recipient via email lookup using existing `GET /api/admin/students?search=` endpoint
- Admin types email → clicks Lookup → resolves to `userId`
- Multi-recipient deferred: document states "multi-recipient follow-up" per spec guidance

---

## Gates

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | 0 errors |
| `dotnet test --configuration Release` | 2190 passed (14 new integration tests) |
| `npm run build -- --configuration production` | Success |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 972 passed (16 new component tests) |

---

## Migration Added

No — uses existing `Notification` and `NotificationOutboxItem` tables.

---

## Decisions

| Decision | Rationale |
|----------|-----------|
| Single recipient per send in this phase | Spec allowed it; multi-recipient follow-up documented |
| Email queued, not sent immediately | Consistent with dispatch architecture; avoids blocking HTTP request |
| SMS returns 400, not skipped silently | Admin must know SMS is unavailable rather than having it silently dropped |
| ILogger rather than AdminAuditLog | AdminAuditLog write adds DB round-trip and is more appropriate for a dedicated audit phase |
| Recipient lookup via student search | Reuses existing endpoint; avoids a new user search endpoint just for this feature |

---

## Risks / Unresolved

- Multi-recipient broadcast deferred — documented in TODO-10W-5B follow-up note
- AdminAuditLog write deferred — log via ILogger for now
- Group/role broadcast deferred

---

## Final Verdict

**SHIP.** All gates green. Security constraints satisfied. SMS correctly rejected.

## Next Recommended Action

10W-5C (Notification Configuration) or 10W-6 (SMS provider) per backlog priority.
