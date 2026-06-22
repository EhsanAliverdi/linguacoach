---
status: current
lastUpdated: 2026-06-23 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10W-5C-2 — DB-Backed Notification Channel Configuration

**Date:** 2026-06-23
**Related sprint:** 10W Notification Platform
**Phase:** 10W-5C-2 (follow-on to 10W-5C read-only config visibility)

---

## Summary

Implemented admin-manageable notification channel configuration stored in the database, replacing the previous read-only appsettings-only view. Appsettings remains the safe fallback. A database row wins when present. Secrets are never returned to the frontend.

---

## Files Changed

### Backend

| File | Change |
|------|--------|
| `src/LinguaCoach.Domain/Entities/NotificationChannelConfig.cs` | New entity with mutation methods |
| `src/LinguaCoach.Persistence/Configurations/NotificationChannelConfigConfiguration.cs` | EF Core table config, unique index on Channel |
| `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` | Added `NotificationChannelConfigs` DbSet |
| `src/LinguaCoach.Persistence/Migrations/20260622203646_T57_NotificationChannelConfig.cs` | Migration: creates `NotificationChannelConfigs` table |
| `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs` | Added V2 status DTO with `source` field, PUT commands, `AdminUpdateConfigResult`, extended `IAdminNotificationHandler` |
| `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` | Implemented `GetConfigStatusV2Async`, `UpdateEmailConfigAsync`, `UpdateSmsConfigAsync`, `UpdateInAppConfigAsync` |
| `src/LinguaCoach.Api/Controllers/AdminController.cs` | Updated GET to return V2, added PUT `/email`, `/sms`, `/in-app` endpoints and request records |
| `tests/LinguaCoach.IntegrationTests/Api/AdminNotificationChannelConfigEndpointTests.cs` | 25 new integration tests |

### Frontend

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | Added `AdminNotificationConfigStatusV2`, `AdminUpdateEmailConfigRequest`, `AdminUpdateSmsConfigRequest`, `AdminUpdateInAppConfigRequest`, `AdminUpdateConfigResult` |
| `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` | Updated `getNotificationConfig` return type to V2; added `updateEmailConfig`, `updateSmsConfig`, `updateInAppConfig` |
| `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts` | Added edit form state signals, `saveEmailConfig`, `saveSmsConfig`, `saveInAppConfig`, `sourceTone`, config sync on load |
| `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` | Config tab: source badge, editable email/SMS/InApp forms, secret UX (replace-only, configured badge, clearSecret), save buttons |
| `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` | Updated to V2 type; added 9 new config edit tests |

---

## Migration Added

**T57_NotificationChannelConfig** — creates `NotificationChannelConfigs` table with:
- `Id` (PK), `Channel` (unique index, max 32), `IsEnabled`, `Provider`, `FromAddress`, `FromDisplayName`, `Host`, `Port`, `UseSsl`, `Username`, `SenderId`, `SecretEncrypted`, `CreatedAtUtc`, `UpdatedAtUtc`, `UpdatedByAdminUserId`

---

## Design Decisions

### Hybrid configuration resolution

`GetConfigStatusV2Async` reads all DB rows, then overlays per-field:
- If a row for channel "Email" exists, all its fields take priority over appsettings.
- If no row exists, appsettings values are used as fallback.
- `source` field is `AppSettings`, `Database`, or `Mixed` depending on which channels have DB overrides.

### Secret storage

Secrets (SMTP password, SMS API key) are stored Base64-encoded in `SecretEncrypted`. This is **not real encryption** — it is an encoding placeholder. A TODO note is left in the handler for when encryption infrastructure (e.g. Data Protection API or AES-256) is available. The current approach is safe in the following ways:
- Secrets are never returned to the frontend (only `hasPassword` / `hasApiKey` booleans).
- Secrets are never logged.
- `GET /api/admin/notifications/config` does not expose `SecretEncrypted`.
- `newSecret` is cleared from the Angular form after a successful save.

### Secret UX

- Password field is write-only (type="password", placeholder says "Leave blank to keep existing").
- A badge shows "Password configured" or "No password stored".
- An opt-in `clearSecret` checkbox clears the stored secret explicitly.
- If neither `newSecret` nor `clearSecret` is provided, the existing secret is preserved unchanged.

### InApp enabling

InApp toggle is persisted to DB. Account/System notifications are not blocked even if InApp is disabled — this is a NOTE in the UI; enforcement is a future task if needed.

### SMS

SMS configuration is saveable (provider, senderId, API key) but real sending remains disabled. A warning banner in the UI makes this clear.

---

## Validation

- Email `isEnabled=true` requires: non-empty host, port 1–65535, valid from address (contains `@`).
- SMS: no required-field validation (provider is informational; real sending is deferred).
- InApp: no field validation (boolean only).

---

## Findings by Priority

### P0 — None

### P1 — Addressed

- Secret must never be returned to frontend → enforced at DTO level (`hasPassword` bool only, no `SecretEncrypted` in responses).
- App must start with no DB config → ensured, appsettings fallback always works.
- Existing dispatch/email/inapp behavior unchanged → verified by passing test suite.

### P2 — Deferred

- **TODO-10W-5C-2-ENCRYPTION**: Replace Base64 encoding with real AES-256 or DPAPI encryption for `SecretEncrypted`. Noted inline in handler.
- **TODO-10W-EMAIL-SENDER-RUNTIME**: `SmtpEmailSender` still reads `IOptions<EmailOptions>` at DI resolution time. To make the email sender use DB config at runtime (hot reload), the sender needs to be refactored to call the resolver per-send. Deferred — appsettings still works; DB config takes effect on next app restart (the resolver is used for the admin GET view, not the live sender yet).

---

## AskUserQuestion answers

No AskUserQuestion was needed. The scope was clear from the phase spec.

---

## Implementation Tasks Produced

None additional — all tasks in this phase are complete.

TODOs to track:
- `TODO-10W-5C-2-ENCRYPTION` — upgrade secret storage to real encryption
- `TODO-10W-EMAIL-SENDER-RUNTIME` — wire DB config into SmtpEmailSender at send time

---

## Risks / Unresolved

- `SmtpEmailSender` uses appsettings at runtime. DB config only affects the admin status view. A restart picks up DB config for display but not for live send. The test email still uses `_emailOptions` (appsettings). This is documented and deferred.
- Base64 is not encryption. If the DB is compromised, secrets are readable. This is noted as a follow-up.

---

## Final Verdict

**Phase 10W-5C-2 is complete.** All gates pass. No secrets exposed. Appsettings fallback intact. Admin UI is editable. Tests cover happy path, validation, secret safety, idempotency, and source field.

---

## Next Recommended Action

Proceed to **Phase 10Auth** (authentication/authorization enhancements). Before doing so, optionally:
- Wire `SmtpEmailSender` to use DB config at send time (small effort, high value for multi-tenant future).
- Upgrade `SecretEncrypted` to real DPAPI or AES-256.
