# Phase 10Auth-F-3 — Security Notifications: Engineering Review

**Date:** 2026-06-23
**Sprint/Phase:** 10Auth-F-3
**Reviewer:** Claude Code (Sonnet 4.6)
**Status:** Complete — all gates passed

---

## Files Changed

### Modified files
- `src/LinguaCoach.Persistence/Seed/NotificationTemplateSeeder.cs` — 7 new security notification templates
- `src/LinguaCoach.Infrastructure/Auth/ChangePasswordHandler.cs` — injected `INotificationService`, `IConfiguration`, `ILogger`; added `TryNotifyPasswordChangedAsync`
- `src/LinguaCoach.Infrastructure/Auth/PasswordResetHandler.cs` — added `TryNotifyResetRequestedAsync`, `TryNotifyResetSucceededAsync`
- `src/LinguaCoach.Infrastructure/Auth/LoginHandler.cs` — injected `INotificationService`, `IConfiguration`; added `TryNotifyAccountLockedAsync`, wired to lockout transition

### New files
- `tests/LinguaCoach.IntegrationTests/Api/AuthSecurityNotificationTests.cs` — 11 integration tests

---

## Summary

Adds user-facing security notifications for four important account security events. All notifications use the existing `INotificationService`/outbox platform. Notification failures are non-fatal (caught, logged, never abort auth flows). No new schema, no new migrations, no frontend changes.

---

## Security Events Implemented

| Event | In-App | Email | Template Key |
|-------|--------|-------|--------------|
| Password changed | Yes | Yes | `account.password_changed` |
| Password reset requested | Yes | No* | `account.password_reset_requested` |
| Password reset succeeded | Yes | Yes | `account.password_reset_succeeded` |
| Account locked out | Yes | Yes | `account.locked_out` |

*The reset-link email (existing `account.password_reset` template) already serves as the email notification for this event. Sending a separate email would duplicate it.

---

## Templates Added

Seven new templates seeded via `NotificationTemplateSeeder.BuildDefaults()`:

1. `account.password_changed` / InApp — Warning severity
2. `account.password_changed` / Email — Warning severity; variables: `DisplayName`, `AppName`
3. `account.password_reset_succeeded` / InApp — Warning severity
4. `account.password_reset_succeeded` / Email — Warning severity; variables: `DisplayName`, `AppName`
5. `account.password_reset_requested` / InApp — Warning severity (no email variant; reset-link email serves that role)
6. `account.locked_out` / InApp — Warning severity
7. `account.locked_out` / Email — Warning severity; variables: `DisplayName`, `AppName`

All templates use `NotificationCategory.Account`, which is mandatory (cannot be disabled by the user). Seeder is idempotent: only inserts if no active template exists for the same key + channel.

---

## Notification Category and Preference Behaviour

`NotificationCategory.Account` is already declared `IsRequired` in `NotificationPreference.IsRequired()`:

```csharp
public static bool IsRequired(NotificationCategory category) =>
    category is NotificationCategory.Account or NotificationCategory.System;
```

`NotificationService.QueueAsync` calls `IsChannelEnabledAsync`, which returns `true` immediately for required categories. All four security events use `Account` category and therefore bypass user opt-out. No new category needed. No new migration needed.

---

## Anti-Spam / Idempotency for Account Lockout

Lockout notification fires only on **lockout transition** — the moment `AccessFailedAsync` pushes the failed-attempt count over the threshold and `IsLockedOutAsync` returns `true` for the first time.

The already-locked code path (user was locked before this request) hits the early `IsLockedOutAsync` check and throws `UnauthorizedAccessException` before reaching `AccessFailedAsync`. No notification is queued on that path.

This means at most one lockout notification per lockout window per user without any additional deduplication infrastructure.

---

## Sensitive Data Exclusions

No notification body, title, or metadata contains:
- Passwords (current, new, or temporary)
- Reset tokens (raw or encoded)
- Raw JWT tokens
- IP addresses of failed login attempts
- Authorization headers
- Failed password attempt counts

Verified by integration tests `ChangePassword_Notification_DoesNotContainPassword`, `SendResetLink_Notification_DoesNotContainToken`, `ResetPassword_Success_NotificationDoesNotContainToken`, `Login_LockoutNotification_DoesNotContainPasswordOrIp`.

---

## Integration Points

### ChangePasswordHandler
- After successful `ChangePasswordAsync` and audit: queues InApp + Email via `TryNotifyPasswordChangedAsync`.
- `wasForcedChange` branch: same notification (password was changed regardless of whether it was forced).

### PasswordResetHandler
- `SendResetLinkAsync` success: queues InApp via `TryNotifyResetRequestedAsync` (no email — reset-link email already sent).
- `CompleteResetAsync` success: queues InApp + Email via `TryNotifyResetSucceededAsync`.
- Failure paths: audit only, no notification.

### LoginHandler
- Wrong password → `AccessFailedAsync` → `IsLockedOutAsync` returns `true`: queues InApp + Email via `TryNotifyAccountLockedAsync`.
- Wrong password → not yet locked: no notification.
- Already-locked login: no notification.

---

## Test Results

- Architecture tests: 3/3
- Unit tests: 1310/1310
- Integration tests: 1017/1017 (11 new `AuthSecurityNotificationTests`)
- **Total: 2330/2330**

### Tests added (`AuthSecurityNotificationTests.cs` — 11 tests)

| Test | Verifies |
|------|----------|
| `ChangePassword_Success_QueuesInAppAndEmailNotifications` | Both channels queued on successful change |
| `ChangePassword_Notification_DoesNotContainPassword` | Password absent from notification body |
| `ChangePassword_Failure_DoesNotQueueNotification` | No notification on failed change |
| `SendResetLink_QueuesResetLinkEmailAndInAppNotification` | Reset-link email + in-app both queued |
| `SendResetLink_Notification_DoesNotContainToken` | Reset token absent from in-app notification |
| `ResetPassword_Success_QueuesInAppAndEmailNotifications` | Both channels queued on successful reset |
| `ResetPassword_Success_NotificationDoesNotContainToken` | Token absent from all notifications |
| `Login_LockoutTransition_QueuesAccountLockedNotification` | Lockout transition queues InApp + Email |
| `Login_AlreadyLockedAccount_DoesNotQueueAdditionalLockoutNotification` | No duplicate on already-locked login |
| `Login_LockoutNotification_DoesNotContainPasswordOrIp` | Password and IP absent from lockout notification |
| `Account_Category_IsMandatory_CannotBeDisabledByUser` | `IsRequired(Account)` is true |

---

## Not Implemented (Confirmed Out of Scope)

- Refresh tokens / session management → Phase 10Auth-F-4
- Google OAuth → Phase 10Auth-F-5
- Admin security settings UI → Phase 10Auth-F-6
- MFA → later
- SMS security notifications → deferred (no real SMS provider or phone verification yet)
- Unlock-by-email → not in scope
- Distributed rate limiting → infrastructure phase
- New auth audit schema → no changes

---

## Documentation Impact

- Docs reviewed: TODOS.md, docs/sprints/current-sprint.md, docs/handoffs/current-product-state.md
- Docs updated: all three (see tracking file updates below)
- Docs intentionally not updated: architecture/README.md (notification platform unchanged structurally)
- Reason: no architectural changes; only new wiring and templates

---

## Final Verdict

**APPROVED — COMPLETE.**

All security notification events implemented. Notification failures are non-fatal. Sensitive data absent from all notification payloads (verified by tests). Account category is mandatory (verified). Lockout anti-spam confirmed by test. 2330/2330 tests pass. No new migration. No frontend changes.

## Next Recommended Action

Phase 10Auth-F-4: Refresh token / session management — `RefreshToken` entity, migration T59, short-lived JWT (15 min), HttpOnly sliding refresh cookie (30 days), `POST /api/auth/refresh`, `POST /api/auth/logout`, admin session list/revoke, Angular token-refresh interceptor.
