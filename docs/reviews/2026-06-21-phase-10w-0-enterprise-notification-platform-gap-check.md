# Phase 10W-0 — Enterprise Notification Platform Gap Check

**Date:** 2026-06-21
**Sprint:** Phase 10W-0 (audit only)
**Type:** Gap check / architecture review
**Author:** Claude Code (audit)

---

## 1. Files Inspected

- `src/LinguaCoach.Web/src/app/core/services/toast.service.ts`
- `src/LinguaCoach.Web/src/app/core/components/toast-host/toast-host.component.ts`
- `src/LinguaCoach.Web/src/app/admin/services/admin-toast.service.ts`
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../notification-dropdown.component.ts`
- `src/LinguaCoach.Application/UsageGovernance/IUsageQuotaService.cs`
- `src/LinguaCoach.Infrastructure/UsageGovernance/UsageQuotaService.cs`
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs`
- `src/LinguaCoach.Api/Controllers/AuthController.cs`
- `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs`
- `src/LinguaCoach.Infrastructure/Auth/LoginHandler.cs`
- `appsettings.json`, `appsettings.Development.json`
- `docs/handoffs/current-product-state.md`

---

## 2. Findings by Area

### 2.1 Email capability

**Status: NOT IMPLEMENTED**

- No `IEmailSender` interface or implementation.
- No SMTP, SendGrid, Mailgun, or SES configuration in any `appsettings` file.
- No email confirmation flow. `ApplicationUser.EmailConfirmed` is set to `true` directly by the admin-create student handler — bypassing any confirmation.
- No reset password email flow. `AuthController` and `LoginHandler` handle login and password-change-on-next-login (`MustChangePassword` flag) only. No token-based reset, no email delivery.
- No email templates of any kind.
- No test coverage for email delivery.

**Current workaround:** Admin sets a temporary password manually. Student logs in and is forced to change it. No email is involved.

---

### 2.2 In-app notification capability

**Status: UI PLACEHOLDER ONLY — no backend**

- `NotificationDropdownComponent` exists (TailAdmin template). Renders a bell icon with animated ping dot and a static hard-coded list of demo notifications. No dynamic data binding, no service injection, no read/unread state.
- `ToastService` and `ToastHostComponent` are fully functional for **transient** in-app toasts (success / error / warning / info, auto-dismiss 4.5 s). Used widely across the app.
- `AdminToastService` is a thin admin wrapper over `ToastService`.
- No `Notification` database entity.
- No `NotificationRecipient` table.
- No `/api/notifications` endpoint.
- No persistent read/unread state.
- No notification preferences.

---

### 2.3 SMS capability

**Status: NOT IMPLEMENTED**

- No SMS provider (Twilio, AWS SNS, Vonage, etc.) anywhere in the codebase.
- No SMS config, templates, or usage limits.

---

### 2.4 Existing event / alert producers

| Event | Current delivery mechanism |
|---|---|
| Reset password | None — admin sets password manually |
| Student created | None |
| Onboarding complete | None |
| AI quota exceeded | API response body (`QuotaDecision.Allowed=false`) only |
| Zero-cost AI calls | Admin dashboard summary card only |
| Background job failure | `ILogger` only (Quartz jobs: LessonBuffer, PracticeGym, AudioCleanup, ReadinessPool, etc.) |
| Failed AI content generation | `ILogger` only |
| Readiness pool failures | `ILogger` only |

No event or alert is delivered to users or admins via email, SMS, or persistent in-app notification.

---

### 2.5 Data model gaps

None of the following entities or tables exist:

| Missing entity | Purpose |
|---|---|
| `Notification` | Persistent in-app notification record |
| `NotificationRecipient` | Delivery targets per notification |
| `NotificationTemplate` | Reusable channel-specific templates |
| `NotificationDeliveryAttempt` | Retry tracking, status, timestamps |
| `NotificationPreference` | Per-user channel opt-in/out |
| `NotificationChannel` config | Global or per-tenant channel enable/disable |
| `NotificationCategory` | Severity / category classification |
| `ActionLink` / deep link | Click-through destination per notification |
| `ExpiresAt` / TTL | Auto-expiry for time-sensitive notices |

---

### 2.6 Enterprise requirements gap summary

| Requirement | Status |
|---|---|
| Channel enable/disable per tenant | Missing |
| Per-user preferences | Missing |
| Template management | Missing |
| Audit / delivery logging | Missing |
| Retry on failure | Missing |
| Rate limiting | Missing |
| Delivery status tracking | Missing |
| Background worker / queue | Missing (Quartz exists but not wired for notifications) |
| Admin test-send | Missing |
| Security / PII handling | Missing |
| Unsubscribe / opt-out | Missing |

---

## 3. Recommended Architecture

### 3.1 Core abstractions

```
INotificationService
  SendAsync(NotificationRequest) — routes to channel workers

INotificationChannel
  CanHandle(NotificationChannelType)
  DeliverAsync(NotificationDelivery)

Implemented channels:
  InAppNotificationChannel    — writes to DB, signals frontend
  EmailNotificationChannel    — wraps IEmailSender (abstraction)
  SmsNotificationChannel      — wraps ISmsProvider (abstraction)
```

### 3.2 Data flow

```
Event producer (job, handler, controller)
  → INotificationService.SendAsync(NotificationRequest)
    → writes NotificationOutbox row (atomic with business TX)
    → NotificationDispatchJob (Quartz, every 30s)
      → reads pending outbox rows
      → routes to channel workers
      → writes NotificationDeliveryAttempt per channel
      → marks outbox row Dispatched / Failed
```

### 3.3 In-app delivery

```
NotificationOutbox → DB row → Notification table (persistent)
GET /api/notifications        — paged, unread-first
PATCH /api/notifications/{id}/read
PATCH /api/notifications/read-all
WebSocket / SSE (optional, phase 2) — push bell count update
```

### 3.4 Email abstraction

```
IEmailSender
  SendAsync(EmailMessage)

Implementations:
  SmtpEmailSender   (dev / self-hosted)
  SendGridSender    (production SaaS)
  LoggingEmailSender (test / stub)
```

### 3.5 Template renderer

```
INotificationTemplateRenderer
  RenderAsync(templateKey, variables) → (subject, htmlBody, plainText, smsBody)

Templates stored in DB (NotificationTemplate table).
Variables: {{ studentName }}, {{ resetLink }}, {{ quotaRemaining }}, etc.
```

---

## 4. Recommended Phased Roadmap

### Phase 10W-1 — Backend notification foundation

- `Notification`, `NotificationOutbox`, `NotificationDeliveryAttempt`, `NotificationPreference` entities + migration.
- `INotificationService` interface in Application layer.
- `InAppNotificationService` implementation (writes to DB only).
- `NotificationDispatchJob` (Quartz, polls outbox).
- Wire one producer: zero-cost AI usage alert → in-app notification.
- Unit + integration tests.

### Phase 10W-2 — In-app notification APIs

- `GET /api/notifications` (paged, unread-first, auth-scoped).
- `PATCH /api/notifications/{id}/read`.
- `PATCH /api/notifications/read-all`.
- `GET /api/notifications/unread-count`.
- Admin endpoint: `GET /api/admin/notifications` (all users, filterable).
- Integration tests for all endpoints.

### Phase 10W-3 — Student and admin bell UI

- Wire `NotificationDropdownComponent` to `/api/notifications/unread-count` and `/api/notifications`.
- Real read/unread state. Polling (30 s) for count updates; SSE optional stretch.
- Admin bell (separate endpoint).
- Replace hard-coded demo items with live data.

### Phase 10W-4 — Email provider + reset password wiring

- `IEmailSender` interface in Application layer.
- `SmtpEmailSender` (dev) and `SendGridSender` (prod) in Infrastructure.
- `LoggingEmailSender` for test projects.
- Wire reset password flow: token generation, email delivery.
- Wire student-created email: temporary password delivery.
- Config: `Email__Provider`, `Email__Smtp__*`, `Email__SendGrid__ApiKey`.
- Integration tests with `LoggingEmailSender`.

### Phase 10W-5 — Templates and preferences

- `NotificationTemplate` DB entity + migration.
- `INotificationTemplateRenderer` with Liquid or Handlebars engine.
- Admin CRUD for templates.
- `NotificationPreference` per user, per channel.
- Per-user opt-out respected in dispatch worker.

### Phase 10W-6 — SMS provider

- `ISmsProvider` interface.
- `TwilioSmsProvider` implementation.
- Config: `Sms__Provider`, `Sms__Twilio__AccountSid`, `Sms__Twilio__AuthToken`.
- Wire one SMS producer: quota warning to enrolled SMS users.
- Rate limiting per user per day.

### Phase 10W-FINAL — Audit and closure

- Full delivery audit log review.
- Security / PII audit (no PII in notification body unless encrypted at rest).
- Unsubscribe / opt-out compliance.
- Load test dispatch worker under synthetic notification volume.
- Docs update.

---

## 5. Decisions Made

1. In-app notifications are first priority (10W-1 through 10W-3). No external provider risk.
2. Email provider abstraction (`IEmailSender`) precedes any specific provider choice. Keeps Infrastructure swappable.
3. Outbox pattern is required. Prevents lost notifications when business TX commits but dispatch fails.
4. SMS is last priority (10W-6). No current user demand signal; risk is provider cost and compliance overhead.
5. Quartz is already available for the dispatch worker. No new scheduler needed.
6. Template rendering deferred to 10W-5. Hard-coded strings acceptable for 10W-1 through 10W-4.

---

## 6. Risks and Unresolved Questions

| Risk | Notes |
|---|---|
| Email deliverability (SPF/DKIM) | Must be configured before any production email goes out. |
| PII in notification body | Review privacy requirements before persisting student data in notification rows. |
| Outbox polling latency | 30 s Quartz poll acceptable for in-app; email SLA may require shorter interval. |
| SMS compliance (opt-in, STOP) | Twilio requires documented opt-in. Must be captured in `NotificationPreference`. |
| Reset password token security | Token must be time-limited, single-use, hashed in DB. Use ASP.NET Identity `UserManager.GeneratePasswordResetTokenAsync`. |
| Multi-tenant channel config | `TODO-018` (workspace/cohort policy) intersects notification channel config. Coordinate. |

---

## 7. Implementation Tasks Produced

See TODOS.md entries `TODO-10W-*` and current-sprint.md for 10W roadmap.

---

## 8. Final Verdict

**Zero existing notification infrastructure beyond transient toasts.**
The gap is large but well-scoped. Start with 10W-1 (backend foundation + in-app DB writes) and 10W-2 (APIs). Those two phases are low-risk and unblock the bell UI without any external provider dependency.

**Next recommended action:** Begin Phase 10W-1.
