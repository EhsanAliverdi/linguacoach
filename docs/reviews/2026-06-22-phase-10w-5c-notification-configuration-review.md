# Phase 10W-5C — Notification Configuration Review

**Date:** 2026-06-22
**Sprint/Feature:** Phase 10W-5C — Admin Notification Configuration
**Related sprint:** Enterprise Notification Platform (Phase 10W)
**Prior phase:** 10W-5B (commit abbea78)

---

## Files Reviewed / Changed

### Backend
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs` — new DTOs and two new interface methods
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` — `GetConfigStatusAsync` + `TestEmailAsync` implementations
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — two new endpoints + `AdminTestEmailRequest` record
- `tests/LinguaCoach.IntegrationTests/Api/AdminNotificationConfigEndpointTests.cs` — 13 new integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — config TS interfaces
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getNotificationConfig()` + `testEmail()` methods
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts` — config signals, `loadConfig()`, `onConfigTabActivated()`, `sendTestEmail()`, `configTone()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` — Configuration tab button + full config tab content
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` — 10 new config unit tests
- `TODOS.md` — 10W-5C marked done; deferred DB-backed toggle item added

---

## Findings

### Priority 1 (Security) — All resolved

**SMTP password never exposed.** `AdminEmailConfigStatus` uses `HasPassword: bool`, not the raw `Password` string. `GetConfigStatusAsync` never assigns the password field. Integration test `GetConfig_DoesNotExposePassword` asserts via `JsonElement` property check (not string substring) that `email.password` / `email.Password` properties are absent and `hasPassword` is a boolean.

**Test email response clean.** `TestEmailAsync` returns `AdminTestEmailResult` with only `Succeeded`, `WasSkipped`, `Message` fields. The `Message` string is constructed from non-secret data (recipient address, status description). Integration test `TestEmail_ResponseDoesNotExposeSmtpSecret` asserts body does not contain "password", "smtp", or "credential".

**Log safety.** `TestEmailAsync` logs `AdminId` and `ToAddress` only — no SMTP credentials appear in log output.

### Priority 2 (Architecture) — Clean

- `GetConfigStatusAsync` injects `IOptions<EmailOptions>` (Application-layer interface from Microsoft.Extensions.Options). No direct Infrastructure-to-appsettings coupling.
- `TestEmailAsync` routes through `IEmailSender.SendAsync` — either `SmtpEmailSender` or `DisabledEmailSender` depending on config. No direct SMTP code in the handler.
- `AdminNotificationHandler` constructor extended cleanly: `IEmailSender emailSender` + `IOptions<EmailOptions> emailOptions` alongside existing params.

### Priority 3 (Scope) — All constraints respected

- Email config editing is deferred (appsettings-only, no PUT endpoint). A TODO is recorded.
- SMS status shown as `Deferred` (accurate — deferred to 10W-6, not permanently disabled).
- Reset-password behavior unchanged.
- Notification bell user APIs unchanged.
- No template management added.
- No SMS setup form added.

### Priority 4 (Frontend) — Implemented

- Config tab lazy-loads via `onConfigTabActivated()` — only calls API once (guards on `config()` being null).
- Channel status cards for InApp, Email, SMS, DispatchJob.
- Email detail panel: host, port, from address, from display name, SSL, hasUsername, hasPassword — all read-only.
- Appsettings-only notice rendered in UI.
- Test email form: address input + Send test button; result shown with appropriate color coding (green=success, amber=skipped, red=failed).
- `configTone()` maps status labels to badge tones consistently.

---

## Test Results

| Gate | Before | After |
|------|--------|-------|
| .NET tests | 2190 | 2203 (+13 integration) |
| Angular unit tests | 972 | 982 (+10 config) |
| Angular build | PASS | PASS |

---

## Decisions Made

1. **Read-only config endpoint only.** PUT/PATCH endpoint for enabling/disabling channels deferred. Requires DB-backed `NotificationChannelConfig` table to survive redeploys. Recorded as `TODO-10W-5C-DEFERRED`.

2. **SMS label = "Deferred" not "Disabled".** Accurate: SMS will be enabled in Phase 10W-6.

3. **`@if (config())` not `@if (config(); as cfg)`.** Angular 17 `@if ... as` alias exposes `cfg` only in template expressions where the compiler can prove it non-null. Repeated `config()!` calls throughout the block are safe within the `@if (config())` guard.

4. **`HasPassword: bool` only.** No `Password`, no `MaskedPassword`, no hint string. Boolean presence check is the least-leaky representation.

---

## Risks / Unresolved Questions

- **DB-backed toggles:** Without a DB-backed enable/disable mechanism, toggling a channel requires a config change + redeploy. Acceptable for 10W-5C; should be addressed in a future phase.
- **`Email:Enabled` wiring in dispatch job:** The dispatch worker checks `IEmailSender` type at send time (DisabledEmailSender short-circuits). A future `NotificationChannelConfig.EmailEnabled` DB flag would need wiring into `NotificationDispatchService` as well.
- **SMS deferred:** `AdminChannelStatus.Sms.StatusLabel = "Deferred"` is intentional. Phase 10W-6 will update this when `ISmsProvider` is introduced.

---

## Final Verdict

**PASS.** Phase 10W-5C is complete. All scope constraints met. SMTP password never exposed at any layer. Both backend and frontend gates pass. No architecture violations.

---

## Next Recommended Action

Continue with Phase 10W-5D: Notification templates and per-user preferences (`NotificationTemplate` entity, `NotificationPreference` per-user per-channel, opt-out respected in dispatch worker), or Phase 10W-6 (SMS provider).
