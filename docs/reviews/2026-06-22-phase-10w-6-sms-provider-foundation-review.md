# Phase 10W-6 — SMS Provider Foundation: Engineering Review

**Date:** 2026-06-22
**Sprint / Feature:** Phase 10W-6 — SMS Provider Foundation
**Related sprint doc:** docs/sprints/ (notification platform sprint)
**Status:** Complete

---

## Files Reviewed / Created

### New — Application layer
- `src/LinguaCoach.Application/Notifications/ISmsSender.cs`
  - `SmsMessage` record
  - `SmsSendResult` record with `Ok()`, `Skipped(reason)`, `Failure(error)` factories
  - `ISmsSender` interface

### New — Infrastructure layer
- `src/LinguaCoach.Infrastructure/Notifications/SmsOptions.cs`
  - Binds from `Sms` appsettings section
  - `HasApiKey` and `IsConfigured` computed properties
  - ApiKey field is never returned to frontend
- `src/LinguaCoach.Infrastructure/Notifications/DisabledSmsSender.cs`
  - Always returns `SmsSendResult.Skipped(...)`, never throws

### Modified — Application layer
- `src/LinguaCoach.Application/Admin/AdminNotificationQueries.cs`
  - Added `AdminSmsConfigStatus` record: `Enabled`, `Configured`, `StatusLabel`, `Provider`, `SenderId`, `HasApiKey`
  - `AdminNotificationConfigStatus.Sms` changed from `AdminChannelStatus` to `AdminSmsConfigStatus`

### Modified — Infrastructure layer
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs`
  - Injected `IOptions<SmsOptions>`
  - `GetConfigStatusAsync` populates `AdminSmsConfigStatus` from options
- `src/LinguaCoach.Infrastructure/Notifications/NotificationDispatchService.cs`
  - Injected `ISmsSender`
  - Added `SendSmsAsync` private method: resolves `ApplicationUser.PhoneNumber`, extracts body from payload JSON, delegates to `_smsSender`
  - SMS branch added alongside InApp/Email branches
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
  - Registered `SmsOptions` from configuration
  - Registered `DisabledSmsSender` and `ISmsSender` (always `DisabledSmsSender` until real provider added)

### Modified — API
- `src/LinguaCoach.Api/appsettings.json`
  - Added `"Sms": { "Enabled": false, "Provider": "", "SenderId": "", "ApiKey": "" }`

### Modified — Angular
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
  - Added `AdminSmsConfigStatus` interface (`enabled`, `configured`, `statusLabel`, `provider`, `senderId`, `hasApiKey` — no ApiKey value)
  - `AdminNotificationConfigStatus.sms` changed from `AdminChannelStatus` to `AdminSmsConfigStatus`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
  - Config tab: added SMS Configuration detail card (provider, senderId, hasApiKey badge, status badge)
  - Send slide-over: retains "SMS is not yet available" note (no checkbox added)
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts`
  - Updated mock config fixture: `sms` object changed to `AdminSmsConfigStatus` shape

### Modified — Tests
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationDispatchTests.cs`
  - `new DisabledSmsSender()` injected into `DispatchSvc`
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationEmailDispatchTests.cs`
  - `new DisabledSmsSender()` injected into `DispatchSvc(emailSender)`

---

## Findings

### Priority: High (security)

**ApiKey never returned to frontend.**
`SmsOptions.ApiKey` is read-only in the config class. `AdminSmsConfigStatus` does not include `ApiKey` — only `HasApiKey` (bool). `AdminNotificationHandler` explicitly maps `HasApiKey` from `_smsOptions.Value.HasApiKey`. No path returns the raw key. Verified.

### Priority: Medium (architecture)

**Double-block on SMS.**
SMS outbox items cannot normally be created: `INotificationPreferenceService` blocks SMS in `NotificationService.QueueAsync` (SMS always returns false). If an outbox item somehow exists, `DisabledSmsSender` returns `Skipped`, and the dispatch service records it as skipped. Belt-and-suspenders.

**Phone number resolution.**
`SendSmsAsync` looks up `ApplicationUser.PhoneNumber` from ASP.NET Identity. This field typically null. If null, returns `SmsSendResult.Skipped("Recipient phone number not found.")` — no crash. Phone collection deferred to `TODO-10W-PHONE`.

**No real SMS provider.**
`ISmsSender` always resolves to `DisabledSmsSender` via DI. A real provider (`TwilioSmsSender` or similar) is wired up when `TODO-10W-SMS-PROVIDER` is implemented. Feature-flag is `Sms:Enabled` in appsettings.

### Priority: Low

**Config tab — SMS card.**
Shows provider name, sender ID, HasApiKey badge, and status label (Deferred by default). Displays note about phone number collection being a future phase. No test email equivalent for SMS (deferred until real provider exists).

---

## Test Results

| Suite | Pass | Fail |
|---|---|---|
| .NET (Release) | 2246 | 0 |
| Angular (Karma headless) | 1011 | 0 |

Gates: `dotnet build --configuration Release` ✓, `dotnet test --configuration Release` ✓, `npm run build -- --configuration production` ✓, `npm test -- --watch=false --browsers=ChromeHeadless` ✓.

---

## Decisions Made

1. `DisabledSmsSender` always registered (not conditional on `Sms:Enabled`) — keeps DI simple, real sender added later.
2. `AdminSmsConfigStatus` is a separate record (not `AdminChannelStatus`) to carry SMS-specific safe fields without a generic `HasApiKey` smell on email.
3. `SmsOptions.ApiKey` uses `HasApiKey` computed property — the raw key is never serialized into DTOs.
4. No SMS checkbox added to admin send form — SMS always blocked at preference layer; would silently do nothing.
5. Phone number collection and verification are a separate future TODO (TODO-10W-PHONE), not part of this phase.

---

## Risks / Unresolved Questions

- Phone number verification flow is undefined. Collecting an unverified number and sending SMS is an opt-in / STOP compliance risk.
- Rate limiting per user per day not implemented (deferred to real provider phase).
- Twilio/other provider credentials require secret management — must use environment variable injection, not appsettings.json.

---

## Implementation Tasks Produced

- TODO-10W-PHONE: Phone number collection and verification
- TODO-10W-SMS-PROVIDER: Real SMS provider (`TwilioSmsSender`)

---

## Final Verdict

Phase 10W-6 complete. SMS abstraction is in place and disabled by default. No breaking changes. Security constraint (ApiKey never to frontend) verified. All gates pass.

## Next Recommended Action

Proceed to remaining notification TODOs (TODO-10W-5D-UNIQUE-CONSTRAINT, TODO-10W-FINAL) or begin a new sprint.
