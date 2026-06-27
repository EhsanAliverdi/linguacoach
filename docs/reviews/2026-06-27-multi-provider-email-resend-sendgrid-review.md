# Multi-Provider Email — Resend and SendGrid Support

**Date:** 2026-06-27
**Sprint/Feature:** Email provider expansion
**Related prior phase:** 10W-5C (SMTP email configuration), 10W-5C-2 (DB-backed notification channel config)

---

## Summary

Extended the email notification system to support three providers: SMTP (existing), Resend (resend.com), and SendGrid. Provider is selected at runtime from DB config or appsettings — no code change or redeploy needed to switch.

---

## Files Changed

### Application layer
- `src/LinguaCoach.Application/Notifications/INotificationChannelConfigResolver.cs` — added `Provider` field to `ResolvedEmailConfig`
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs` — added `Provider` to `AdminUpdateEmailConfigCommand`

### Domain layer
- `src/LinguaCoach.Domain/Entities/NotificationChannelConfig.cs` — `UpdateEmail()` now accepts `provider` param (was hardcoded to `"Smtp"`)

### Infrastructure layer
- `src/LinguaCoach.Infrastructure/Notifications/EmailOptions.cs` — added `Provider` property (default `"Smtp"`)
- `src/LinguaCoach.Infrastructure/Notifications/NotificationChannelConfigResolver.cs` — passes `Provider` in both DB and appsettings resolution paths
- `src/LinguaCoach.Infrastructure/Notifications/ResendEmailSender.cs` — **new**: calls Resend REST API via `HttpClient` (no NuGet package; `Resend` package tops at 0.5.1, incompatible with net10)
- `src/LinguaCoach.Infrastructure/Notifications/SendGridEmailSender.cs` — **new**: uses official `SendGrid` SDK (9.29.3)
- `src/LinguaCoach.Infrastructure/Notifications/RoutingEmailSender.cs` — **new**: registered as `IEmailSender`; reads `Provider` from resolved config at send time; routes to correct concrete sender
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registers `ResendEmailSender`, `SendGridEmailSender`, `RoutingEmailSender` as `IEmailSender`; adds named `HttpClient("Resend")`
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` — updated validation (SMTP requires Host+Port; Resend/SendGrid skip those); passes `provider` to `UpdateEmail()`

### API layer
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — added `Provider` to `AdminUpdateEmailConfigRequest`; passes it through to command

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `provider` field to `AdminUpdateEmailConfigRequest`

### Tests
- `tests/LinguaCoach.UnitTests/Notifications/EmailSenderTests.cs` — updated `ResolvedEmailConfig` constructors to include `Provider` argument

---

## Architecture Decisions

### 1. RoutingEmailSender pattern

`IEmailSender` is registered as `RoutingEmailSender`. It resolves the provider name from `INotificationChannelConfigResolver` at send time and delegates to the correct concrete sender. This means a provider switch takes effect immediately after saving DB config — no restart needed.

All three concrete senders are also registered directly in DI so `RoutingEmailSender` can resolve them via `IServiceProvider`. They are `Scoped` (matching the existing sender registrations).

### 2. Resend via HttpClient, not NuGet

The `Resend` NuGet package (latest: 0.5.1) targets older frameworks and does not support net10. `ResendEmailSender` calls the Resend REST API (`https://api.resend.com/emails`) directly via a named `HttpClient("Resend")` registered in DI. The API contract is stable and documented at resend.com/docs.

### 3. Provider validation

When `IsEnabled = true`, the handler validates:
- Provider must be one of `Smtp`, `Resend`, `SendGrid` (case-insensitive)
- SMTP additionally requires `Host` and valid `Port`
- All providers require a valid `FromAddress`

### 4. No DB migration needed

`NotificationChannelConfig.Provider` column already existed (added in T57 migration). It was previously hardcoded to `"Smtp"` in `UpdateEmail()`. Now it accepts whatever provider the admin sets.

### 5. API key storage

For Resend and SendGrid, the API key is stored in the DB `SecretEncrypted` column (encrypted via ASP.NET Core Data Protection), matching the existing SMTP password storage pattern. `PlaintextSecret` in `ResolvedEmailConfig` carries the decrypted value — never returned to API callers.

---

## Configuration Reference

### Appsettings (appsettings.json / environment variables)

| Key | Values | Notes |
|-----|--------|-------|
| `Email:Provider` | `Smtp`, `Resend`, `SendGrid` | Default: `Smtp` |
| `Email:FromAddress` | e.g. `noreply@speakpath.app` | Required when enabled |
| `Email:FromDisplayName` | e.g. `SpeakPath` | Optional |
| `Email:Password` | SMTP password or API key | Used when no DB override |
| `Email:Host` | SMTP host | SMTP only |
| `Email:Port` | e.g. `587` | SMTP only |
| `Email:UseSsl` | `true`/`false` | SMTP only |

### DB config (via admin UI — Integrations page)

Same fields; DB row wins over appsettings. Provider, From address, and secret (API key or password) are the minimum required for Resend/SendGrid.

---

## Security Notes

- API keys stored encrypted in DB (same pattern as SMTP password — Data Protection, AES-256)
- API keys never returned in API responses (`HasPassword: bool` only)
- `ResolvedEmailConfig.PlaintextSecret` is Infrastructure-layer only
- `RoutingEmailSender` logs provider name and recipient address — no secrets

---

## Test Results

| Gate | Result |
|------|--------|
| Full solution build | **0 errors** (14 pre-existing warnings, unrelated) |
| Existing unit tests | Pass (constructor updated for new `Provider` param) |
| Integration tests | Pass (no changes to integration test suite) |

---

## Risks / Unresolved

- **Angular admin UI** does not yet expose a provider dropdown on the Integrations page. Admin must set `Email:Provider` via appsettings or directly in the DB until the UI is updated.
- **Resend API key rotation** follows the same flow as SMTP password rotation — PUT to `/api/admin/notifications/config/email` with `newSecret`.
- **SendGrid unsubscribe / suppression lists** are not wired. Resend handles bounces automatically.

---

## Next Recommended Action

1. Add provider dropdown to the admin Integrations → Email Config form (Angular UI).
2. Wire `F-03` welcome email on student create — SMTP or Resend/SendGrid now all supported via `IEmailSender`.
