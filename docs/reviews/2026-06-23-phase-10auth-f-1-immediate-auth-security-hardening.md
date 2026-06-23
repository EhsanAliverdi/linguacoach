---
title: Phase 10Auth-F-1 — Immediate Auth/Security Hardening
date: 2026-06-23
phase: 10Auth-F-1
type: implementation-review
status: complete
author: Claude Code
---

# Phase 10Auth-F-1 — Immediate Auth/Security Hardening

Date: 2026-06-23
Related sprint: 10Auth-F
Preceding phase: 10Auth-F-0 (gap check, commit 90e7815)
Files changed: Program.cs, LoginHandler.cs, AuthController.cs, SecurityHeadersMiddleware.cs (new), AuthSecurityTests.cs (new), AuthEndpointTests.cs (password fix), AdminEndpointTests.cs (password fix), TODOS.md, docs/sprints/current-sprint.md, docs/handoffs/current-product-state.md

---

## What was hardened

### 1. Account lockout — enabled

**File:** `src/LinguaCoach.Api/Program.cs` (Identity options block)

```
options.Lockout.AllowedForNewUsers = true;
options.Lockout.MaxFailedAccessAttempts = 5;
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
```

**File:** `src/LinguaCoach.Infrastructure/Auth/LoginHandler.cs`

- Added `IsLockedOutAsync` check before password validation.
- On wrong password: `AccessFailedAsync` called to increment counter.
- On success: `ResetAccessFailedCountAsync` called to clear counter.
- Lockout returns generic "Invalid credentials." — no distinction between wrong password and locked account.

**Default lockout configuration:**

| Setting | Value |
|---------|-------|
| MaxFailedAccessAttempts | 5 |
| DefaultLockoutTimeSpan | 15 minutes |
| AllowedForNewUsers | true |

### 2. Login rate limiting — added

**File:** `src/LinguaCoach.Api/Program.cs` (AddRateLimiter block)
**File:** `src/LinguaCoach.Api/Controllers/AuthController.cs` ([EnableRateLimiting] attributes)

**Auth rate limiter policies:**

| Policy | Scope | Limit | Window | Applied to |
|--------|-------|-------|--------|-----------|
| `AuthLogin` | Per IP | 10 requests | 5 minutes | POST /api/auth/login |
| `AuthReset` | Per IP | 3 requests | 15 minutes | POST /api/auth/reset-password, POST /api/auth/change-password |

On rejection: generic JSON `{ "error": "Too many requests. Please try again later." }` with HTTP 429. No distinction from writing-AI rate limit message (avoids endpoint fingerprinting).

### 3. Password policy — hardened

**File:** `src/LinguaCoach.Api/Program.cs` (Identity options block)

| Rule | Before | After |
|------|--------|-------|
| RequiredLength | 8 | 10 |
| RequireDigit | true | true |
| RequireLowercase | (default=true) | explicit true |
| RequireUppercase | false | true |
| RequireNonAlphanumeric | false | true |

All existing test passwords were audited and updated where they fell below the new policy:
- `Temp@1234` (9 chars) → `Temp@12345` (10 chars) in `AuthEndpointTests.cs`
- `Temp@5678` (9 chars) → `Temp@56789` (10 chars) in `AdminEndpointTests.cs`

All other test passwords (`Admin@1234`, `Student@1234`, `TempPass123!`, `NewPass@5678`, etc.) already comply.

### 4. Security response headers — added

**File:** `src/LinguaCoach.Api/Middleware/SecurityHeadersMiddleware.cs` (new)

Added via `app.UseMiddleware<SecurityHeadersMiddleware>()` in the pipeline (before routing/auth).

| Header | Value |
|--------|-------|
| X-Content-Type-Options | nosniff |
| X-Frame-Options | DENY |
| Referrer-Policy | no-referrer |
| Permissions-Policy | camera=(), microphone=(), geolocation=(), payment=() |

**Deliberately deferred:**
- `Content-Security-Policy` — requires a dedicated pass aligned with Angular build nonce strategy. Deferred to a separate frontend-coordination phase.
- `Strict-Transport-Security` — should only be enabled after confirming the production reverse-proxy terminates TLS correctly. Deferred until deployment config is finalized.

---

## Tests added / updated

**New file:** `tests/LinguaCoach.IntegrationTests/Api/AuthSecurityTests.cs` — 13 tests

| Test | Coverage |
|------|----------|
| `Login_AfterMaxFailedAttempts_AccountIsLocked` | Lockout activates after 5 failed attempts; UserManager level |
| `Login_SuccessfulLogin_ResetFailedCount` | Success resets `AccessFailedCount` to 0 |
| `Login_LockedAccount_CorrectPasswordStillRejected` | Locked account rejects correct password via HTTP |
| `CreateStudent_WeakPassword_TooShort_Returns400` | Policy: min 10 chars |
| `CreateStudent_WeakPassword_NoUppercase_Returns400` | Policy: uppercase required |
| `CreateStudent_WeakPassword_NoSpecialChar_Returns400` | Policy: special char required |
| `CreateStudent_StrongPassword_Returns201` | Policy: strong password accepted |
| `ChangePassword_WeakNewPassword_Returns400` | Policy applied on change-password |
| `ApiResponse_IncludesSecurityHeaders` | X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy on 401 |
| `AuthenticatedApiResponse_IncludesSecurityHeaders` | Headers on 200 admin response |
| `Login_ValidAdmin_StillReturns200` | Regression: admin login still works |
| `Login_ValidStudent_StillReturns200WithMustChangePassword` | Regression: student login still works |
| `MustChangePassword_StillEnforced_Returns403` | Regression: force-change middleware still blocks |

**Note on rate-limit tests:** HTTP-level rate-limit tests were omitted from the integration test suite. The test host assigns all requests the same IP (null → "unknown"), making per-IP window assertions unreliable across parallel xUnit test runs. Rate-limiting policy registration and endpoint wiring are verified by successful compilation and [EnableRateLimiting] attribute presence on AuthController. A dedicated isolated rate-limit test can be added in a future phase if needed.

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| `dotnet restore` | All packages up to date |
| `dotnet build --configuration Release` | 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | 2304 passed, 0 failed |
| Architecture tests | 3/3 |
| Unit tests | 1310/1310 |
| Integration tests | 991/991 (+13 new AuthSecurityTests) |
| Frontend (Angular/Playwright) | Not required — no frontend source changed |

---

## What remains deferred

- `TODO-10Auth-F-2`: Auth event audit log (`AuthAuditEvent` entity, migration T58).
- `TODO-10Auth-F-3`: Security notifications (password changed, account locked, reset sent).
- `TODO-10Auth-F-4`: Refresh token / session management (migration T59, short-lived JWT, HttpOnly cookie).
- `TODO-10Auth-F-5`: Google OAuth / external login.
- `TODO-10Auth-F-6`: Admin security settings UI (lockout config, password policy config, force-lock/unlock).
- `TODO-10Auth-F-FINAL`: Closure audit.
- CSP header — deferred pending Angular nonce strategy.
- HSTS — deferred pending production reverse-proxy TLS confirmation.
- Distributed rate limiting (Redis) — deferred until horizontal scaling is required.
- Captcha / abuse protection — deferred.
- Admin lock/unlock via UI — deferred to 10Auth-F-6.
- Unlock-by-email flow — deferred (lockout currently self-expires after 15 min).

---

## Confirmed: not implemented in this phase

- No migrations.
- No OAuth.
- No refresh tokens or session table.
- No auth audit event table.
- No admin security settings UI.
- No MFA.
- No notification / security event integration.
- No student/admin UI changes.
- No changes to notification platform behaviour.
- No changes to usage governance or AI pricing.
