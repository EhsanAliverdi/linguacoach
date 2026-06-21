# Phase 10W-4B — Token-Based Password Reset Link: Implementation Review

**Date:** 2026-06-21
**Sprint:** 10W-4B
**Author:** Engineering (Claude Code)

---

## Overview

Phase 10W-4B adds a token-based password reset link flow to SpeakPath. An admin triggers a reset email for a student. The email contains a link with an embedded Identity token. The student opens the link, enters a new password on a public Angular page, and submits. The existing temp-password admin flow is unchanged.

---

## Files reviewed

### New files

- `src/LinguaCoach.Application/Auth/PasswordResetCommands.cs`
- `src/LinguaCoach.Infrastructure/Auth/PasswordResetHandler.cs`
- `src/LinguaCoach.Web/src/app/features/auth/reset-password/reset-password.component.ts`
- `src/LinguaCoach.Web/src/app/features/auth/reset-password/reset-password.component.html`
- `src/LinguaCoach.Web/src/app/features/auth/reset-password/reset-password.component.spec.ts`
- `tests/LinguaCoach.IntegrationTests/Api/PasswordResetEndpointTests.cs`

### Modified files

- `src/LinguaCoach.Api/Controllers/AuthController.cs` — public reset-password endpoint
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — admin send-reset-link endpoint
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration
- `src/LinguaCoach.Api/appsettings.json` — PublicApp:BaseUrl
- `src/LinguaCoach.Web/src/app/core/models/auth.models.ts` — ResetPasswordRequest
- `src/LinguaCoach.Web/src/app/core/services/auth.service.ts` — resetPassword()
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — sendStudentResetLink()
- `src/LinguaCoach.Web/src/app/app.routes.ts` — /reset-password route
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — signals + sendResetLink() + button
- `tests/LinguaCoach.IntegrationTests/Api/AdminManagementEndpointTests.cs` — (spec file extended)

---

## Findings

### Priority 1 — Security (all addressed)

| Finding | Resolution |
|---|---|
| Token must not appear in logs or metadata | Token logged only as user ID in `LogInformation` — raw token never passed to logger |
| Public endpoint must not reveal user-not-found vs expired token | All failure paths return the same generic string: "The reset link is invalid or has expired." |
| Token must be URL-safe | `Base64UrlEncode` / `Base64UrlDecode` implemented in handler — replaces `+`, `/`, `=` |
| Admin endpoint must require auth | `[Authorize(Roles = "Admin")]` inherited from AdminController class attribute |
| Raw password must not be stored or emailed | Reset link contains only token — no password ever stored or sent |

### Priority 2 — Correctness

| Finding | Resolution |
|---|---|
| `MustChangePassword` flag must be cleared on successful reset | `user.MustChangePassword = false; await _userManager.UpdateAsync(user)` in `CompleteResetAsync` |
| `CompletePasswordResetResult.Ok()` / `.Fail()` factory methods missing from record | Added as static methods directly to the record — `file static class` approach removed |
| `Notification.UserId` property used in test (does not exist) | Fixed to `RecipientUserId` |

### Priority 3 — Coverage

- 7 frontend unit tests for `ResetPasswordComponent`
- 2 frontend unit tests for admin send-reset-link signals
- 8 backend integration tests covering: auth gates (401/403), outbox queued, email body structure, metadata safety, unknown student 404, invalid token 400, mismatched passwords 400, user-not-found generic response

---

## Decisions made

1. **Token encoded in URL via Base64Url** — ASP.NET Identity tokens contain non-URL-safe characters. Base64Url encode in handler, decode before calling `ResetPasswordAsync`.
2. **Static factory methods on record** — Moved `Ok()`/`Fail()` into the `CompletePasswordResetResult` record definition rather than a `file static class`, which does not provide accessible methods to the handler.
3. **`PublicApp:BaseUrl` in appsettings.json** — `http://localhost:4200` default; production override via environment variable / deployment config.
4. **Existing temp-password flow unchanged** — Additive only. `resetStudentPassword` endpoint and admin UI flow untouched.
5. **Email body includes link but NOT the token separately** — The `PayloadJson` outbox item body contains the full reset URL (which embeds the encoded token inline). No separate `token` field in metadata.

---

## AskUserQuestion answers

None required. Spec provided all constraints explicitly.

---

## Gates

| Gate | Result |
|---|---|
| `dotnet build --configuration Release` | Pass (0 errors, 5 warnings pre-existing) |
| `dotnet test --configuration Release` | Pass 2159 / 2159 |
| `npm run build -- --configuration production` | Pass |
| `npm test -- --watch=false --browsers=ChromeHeadless` | Pass 925 / 925 |

---

## Risks and unresolved questions

- **Token expiry UI** — ASP.NET Identity default token lifetime is 24 hours. No expiry hint is shown to the student in the UI. A future UX improvement could show a time-limited message.
- **Rate limiting on public reset endpoint** — No rate limiting on `POST /api/auth/reset-password` yet. Addressed as a future hardening item.
- **SMS deferred** — Out of scope per spec. Remains deferred to 10W-6.
- **Notification preferences / template admin** — Deferred to 10W-5.

---

## Final verdict

Implementation complete. All security invariants enforced. All gates green. No regressions.

## Next recommended action

Proceed to Phase 10W-4C (gitignored dropdown) or 10W-5 (notification templates/preferences) per backlog priority.
