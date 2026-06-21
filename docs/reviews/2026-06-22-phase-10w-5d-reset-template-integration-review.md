# Phase 10W-5D-RESET-INTEGRATION — Reset & Student-Created Email Template Integration Review

**Date:** 2026-06-22
**Sprint/Feature:** Phase 10W-5D-RESET-INTEGRATION — Wire notification templates to password reset and student-created email flows
**Related sprint:** Enterprise Notification Platform (Phase 10W)
**Prior phase:** 10W-5D (commit e601e71) — Notification Templates Foundation

---

## Summary

This phase wires `PasswordResetHandler` and `CreateStudentHandler` to look up and render the seeded `account.password_reset`/Email and `account.student_created`/Email notification templates via `INotificationTemplateRenderer`, instead of using hard-coded email subject/body strings. If the active template is not found (missing or deactivated), both handlers fall back to the original hard-coded content — email delivery is never blocked by a missing template.

---

## Files Changed

### Backend — Modified

- `src/LinguaCoach.Infrastructure/Auth/PasswordResetHandler.cs`
  - Added `INotificationTemplateRenderer` constructor injection
  - Added `ResolveResetEmailContentAsync` private method: looks up `account.password_reset`/Email template; falls back to hard-coded if none active; logs warning on missing template or unresolved variables
  - Variables supplied: `DisplayName` (email address), `ResetLink` (full URL with encoded token), `AppName` (from config)
  - Token never appears in variables dict, never logged, never in audit/metadata

- `src/LinguaCoach.Infrastructure/Admin/CreateStudentHandler.cs`
  - Added `INotificationTemplateRenderer` and `IConfiguration` constructor injection
  - Added `ResolveStudentCreatedEmailContentAsync` private method: looks up `account.student_created`/Email template; falls back to hard-coded if none active; logs warning on missing template or unresolved variables
  - Variables supplied: `DisplayName` (display name or email), `AppName`, `LoginUrl` (`{baseUrl}/login`), `AppUrl` (`{baseUrl}`)

### Tests — New

- `tests/LinguaCoach.IntegrationTests/Api/PasswordResetTemplateIntegrationTests.cs` — 8 new integration tests

---

## Test Coverage

| Test | What it verifies |
|------|-----------------|
| `SendResetLink_WhenActiveTemplateExists_UsesTemplateSubject` | Outbox item uses template subject when active template found |
| `SendResetLink_WhenActiveTemplateExists_BodyContainsResetLink` | Rendered body includes reset URL (userId, token params) |
| `SendResetLink_WhenNoActiveTemplate_FallsBackAndQueuesEmail` | Fallback used when no active template; reset still queues email |
| `SendResetLink_TokenNotStoredInOutboxOrNotificationMetadata` | No `resetToken`/`rawToken` fields in notifications or outbox JSON |
| `SendResetLink_ApiResponse_DoesNotContainToken` | 204 response body is empty; no token exposed |
| `CreateStudent_WhenActiveTemplateExists_UsesTemplateSubject` | Outbox uses template subject when active template found |
| `CreateStudent_WhenNoActiveTemplate_FallsBackAndStudentCreationSucceeds` | Student creation succeeds even when template deactivated |
| `CreateStudent_TemplateWithMissingVariable_StillQueuesEmailSafely` | Missing variable left as `{{UndefinedVar}}` placeholder; email still queued |

---

## Security Invariants Verified

- Reset token is **never** added to the variables dictionary — only the fully encoded URL is passed as `ResetLink`
- Reset token is **never** logged at any level
- Reset token is **never** stored in `Notification.MetadataJson` or `NotificationOutboxItem.PayloadJson` as a bare field
- API response for send-reset-link is `204 No Content` — body is empty; verified by test
- `CompleteResetAsync` behavior is completely unchanged

---

## Fallback Behavior

Both handlers implement the same pattern:
1. Query DB for active template (`TemplateKey + Channel + IsActive == true`)
2. If not found → log warning, use hard-coded subject/body
3. If found → render via `_templateRenderer.Render(...)`, log warning for any `MissingVariables`, use rendered output

This ensures password reset and student creation never fail due to a misconfigured or deactivated template.

---

## Test Results

| Gate | Count |
|------|-------|
| dotnet build --configuration Release | PASS, 0 errors |
| dotnet test --configuration Release | PASS — 2233 total (3 arch + 1287 unit + 943 integration) |
| npm run build -- --configuration production | PASS |
| npm test -- --watch=false --browsers=ChromeHeadless | PASS — 999 tests |

Prior counts: 2225 .NET / 999 Angular. Net new: +8 integration tests.

---

## Decisions Made

1. **Template lookup in handler, not in INotificationService.** Template rendering is handler-level business logic; the notification service remains a generic queue abstraction.
2. **Fallback to hard-coded, never fail.** Missing template is a configuration gap, not a code error. Password reset must never be blocked.
3. **AppName from config.** `PublicApp:AppName` key used; defaults to `"SpeakPath"` if absent. Same key can be used for future templates.
4. **DisplayName uses email when profile name unavailable.** Safe default for reset flow where the StudentProfile may have no display name set.
5. **No change to CompleteResetAsync or public reset endpoint.** Out of scope; behavior unchanged.

---

## Risks / Unresolved Questions

- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active templates still deferred.
- `TODO-10W-PREFS`: Notification preferences (per-user opt-out) still deferred.
- `TODO-10W-6`: SMS provider still deferred.
- `TODO-10W-FINAL`: Platform audit still deferred.

---

## Final Verdict

**PASS.** Phase 10W-5D-RESET-INTEGRATION complete. Both handlers use notification templates with safe fallback. All security invariants upheld. 8 new integration tests cover the required scenarios. All 4 gates pass.

---

## Next Recommended Action

`TODO-10W-5D-UNIQUE-CONSTRAINT` (optional DB index) or `TODO-10W-PREFS` (notification preferences) or `TODO-10W-6` (SMS provider) or `TODO-10W-FINAL` (platform audit).
