# Phase 10W-4 — Email Provider + Reset Password Wiring + Dispatch Job — Engineering Review

**Date:** 2026-06-21
**Sprint:** Phase 10W-4
**Status:** Complete

---

## Files changed

### New — Application layer

- `src/LinguaCoach.Application/Notifications/IEmailSender.cs`
  — `IEmailSender` interface, `EmailMessage` record, `EmailSendResult` record (with `Ok()`, `Skipped()`, `Failure()` factories)

### New — Infrastructure layer

- `src/LinguaCoach.Infrastructure/Notifications/EmailOptions.cs`
  — Options class bound from `"Email"` config section. All fields have safe defaults; `Enabled = false` by default.
- `src/LinguaCoach.Infrastructure/Notifications/DisabledEmailSender.cs`
  — Implements `IEmailSender`. Always returns `Skipped`. Never throws. Used when `Email:Enabled=false` or `Host` is missing.
- `src/LinguaCoach.Infrastructure/Notifications/SmtpEmailSender.cs`
  — Implements `IEmailSender` via `System.Net.Mail.SmtpClient`. Returns `Skipped` if disabled/no-host. Catches all exceptions and returns `Failure(message)` — never throws.
- `src/LinguaCoach.Infrastructure/Jobs/NotificationDispatchJob.cs`
  — Quartz `IJob` with `[DisallowConcurrentExecution]`. Calls `INotificationDispatchService.DispatchDueAsync(batchSize:50)`. Wraps exceptions in `JobExecutionException`.

### Modified — Infrastructure layer

- `src/LinguaCoach.Infrastructure/Notifications/NotificationDispatchService.cs`
  — Injected `IEmailSender` + `UserManager<ApplicationUser>`. Email outbox items: resolves user email via `UserManager.FindByIdAsync`, parses title/body from payload JSON, calls `IEmailSender.SendAsync`. Success → `RecordAttempt(true)` + marks notification Delivered. Skipped → `RecordAttempt(false, reason)`, counted as skipped. Failure → `RecordAttempt(false, error)`, counted as failed. SMS still skipped (10W-6).
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
  — Injected `INotificationService`. `ResetStudentPasswordAsync` now queues an email notification after password reset. Body does NOT include the raw password — only instructs user to log in. If queueing fails, logs warning and continues (reset still succeeds).
- `src/LinguaCoach.Infrastructure/Admin/CreateStudentHandler.cs`
  — Injected `INotificationService` + `ILogger`. `HandleAsync` now queues a welcome email notification after student creation. Body does NOT include the temporary password. If queueing fails, logs warning and continues (creation still succeeds).
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
  — Registered `EmailOptions` (Configure from section or no-op). Registered `SmtpEmailSender` and `DisabledEmailSender` as scoped. `IEmailSender` resolved at runtime: SmtpEmailSender if `Enabled && !string.IsNullOrWhiteSpace(Host)`, else DisabledEmailSender. Registered `NotificationDispatchJob` as scoped.

### Modified — API layer

- `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs`
  — Added `NotificationDispatchJob` trigger: runs every 2 minutes, `[DisallowConcurrentExecution]`, persisted to Quartz store.
- `src/LinguaCoach.Api/appsettings.json`
  — Added `"Email"` section with safe disabled defaults: `Enabled: false`, empty `Host/Username/Password/FromAddress`, `Port: 587`, `UseSsl: true`, `FromDisplayName: "SpeakPath"`.

### New — Tests

- `tests/LinguaCoach.UnitTests/Notifications/EmailSenderTests.cs`
  — 11 unit tests: DisabledEmailSender returns Skipped/never throws, EmailOptions defaults/binding, SmtpEmailSender returns Skipped when disabled or no-host (no network call), EmailSendResult factory methods.
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationEmailDispatchTests.cs`
  — 9 integration tests (SQLite in-memory + FakeEmailSender):
    - Email dispatch with fake sender → marks outbox and notification Delivered
    - Sent email has correct recipient address and subject
    - DisabledEmailSender → counted as skipped, AttemptCount incremented
    - Failing sender → counted as failed, error stored
    - Password not stored in notification metadata or payload JSON
    - SMS still skipped
  — 2 unit tests (no DB): NotificationDispatchJob invokes DispatchDueAsync with batchSize=50; job wraps service exceptions in JobExecutionException.

### Modified — Existing tests

- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationDispatchTests.cs`
  — Updated `NotificationDispatchService` constructor call to pass `DisabledEmailSender` and a real `UserManager` (InApp/SMS tests do not need email).

---

## Security decisions

| Concern | Decision |
|---------|----------|
| Raw temporary password in email body | NOT included — email body only says "log in with your new credentials" |
| Raw temporary password in notification metadata | NOT stored — `MetadataJson` is null |
| Raw temporary password in outbox payload JSON | NOT present — payload only contains title/body/category/severity |
| Raw temporary password in audit logs | NOT logged — `LogWarning` only logs `UserId`, never the password |
| Cross-user notification delivery | Not possible — dispatch resolves email from Identity by `RecipientUserId` stored at queue time |
| Startup crash if email unconfigured | Impossible — `DisabledEmailSender` is the fallback; `EmailOptions.Enabled = false` by default |

---

## Constraints respected

- No SMS provider added (deferred to 10W-4/10W-6 comment preserved in dispatch service)
- No notification template admin UI
- No notification preferences
- No major auth redesign
- No changes to reset-password HTTP behavior — only notification queueing added after the operation succeeds
- If notification queueing fails, operation still succeeds (catch + log warning)
- No external SMTP calls in tests (SmtpEmailSender only used if `Enabled=true` AND `Host` set)

---

## Gaps documented for future phases

- **Token-based password reset link**: current flow sends a temp password by admin action. A self-service reset link (GeneratePasswordResetTokenAsync → email link → ResetPasswordAsync) is not yet built. See TODO-10W-4b in TODOS.md.
- **Email body templating**: body is a hard-coded string. Template rendering (Liquid/Handlebars) deferred to 10W-5.
- **SMS dispatch**: skipped with informative error, deferred to 10W-6.
- **Notification preferences / opt-out**: deferred to 10W-5.

---

## Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors, pre-existing warnings only)
- `dotnet test --configuration Release`: PASS (2150/2150 — 3 arch + 1287 unit + 860 integration; +20 new)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

---

## No migration added

No new DB tables or columns. Notification and outbox tables from 10W-1 are unchanged. Email sender is a pure application/infrastructure concern with no persistence impact.

---

## Next recommended action

Phase 10W-5: notification template rendering + per-user preferences.
