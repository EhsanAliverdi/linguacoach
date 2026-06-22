---
title: Phase 10Auth-F-0 — Enterprise Auth/Security Gap Check
date: 2026-06-23
phase: 10Auth-F-0
type: audit
status: complete
author: Claude Code
---

# Phase 10Auth-F-0 — Enterprise Auth/Security Gap Check

Date: 2026-06-23
Related sprint: Post-10W (next enterprise risk area: authentication and security)
Files reviewed: Program.cs, JwtTokenService.cs, LoginHandler.cs, AuthController.cs, ApplicationUser.cs, PasswordResetHandler.cs, CreateStudentHandler.cs, GlobalExceptionMiddleware.cs, RequestLoggingMiddleware.cs, AdminAuditLog.cs, appsettings.json, appsettings.Development.json, Identity migration T4_IdentitySchema.cs

---

## 1. Current Authentication Model

### Identity setup

- ASP.NET Core Identity with `ApplicationUser : IdentityUser<Guid>` and `IdentityRole<Guid>`.
- Custom field: `UserRole Role` (Admin=0, Student=1) on `ApplicationUser`.
- Custom field: `bool MustChangePassword` for temp-password enforcement.
- `EmailConfirmed` used as account-active flag (admin sets it on creation).

### JWT token issuance

- All authentication is JWT Bearer only. No session cookies.
- Signing algorithm: HMAC-SHA256 with symmetric key.
- Key sourced from `Jwt:Key` config; minimum 32 chars enforced outside Development.
- Placeholder key rejected at startup in non-Development environments.
- Token lifetime: `Jwt:ExpiryHours`, default 24 hours. Configurable but fixed per deployment.
- Claims issued: `sub` (userId), `email`, `role` (UserRole string), `jti` (Guid per token).
- All standard JWT validation flags enabled: issuer, audience, lifetime, signing key.

### Refresh tokens

**NOT IMPLEMENTED.** Sessions are purely access-token-based. Expiry = re-login.

### Token revocation

**NOT IMPLEMENTED.** No token blocklist. A compromised 24-hour token cannot be revoked before expiry.

### Role/claim model

- Two roles: `Admin`, `Student`.
- Admin endpoints: `[Authorize(Roles = nameof(UserRole.Admin))]` on `AdminController`.
- Student endpoints: `[Authorize]` (authenticated only, no role restriction).
- Admins can technically call student endpoints; student handlers do per-user ownership checks.

### Current user ID resolution

- `ClaimTypes.NameIdentifier` (i.e. `sub`) from JWT.
- Handlers query DB by user ID from claim.

---

## 2. Login / Signup / Password Reset Flow

### Login

- `POST /api/auth/login` — `[AllowAnonymous]`, returns JWT + role + `MustChangePassword`.
- Generic error message: "Invalid credentials." for both unknown email and wrong password (no enumeration).
- "Account is not active." for unconfirmed email or archived student (no distinction exposed).
- No failed-login counter incremented. No lockout triggered.

### Admin-created student flow

- Admin calls `POST /api/admin/students` → `CreateStudentHandler`.
- Temp password generated; `MustChangePassword = true`; `EmailConfirmed = true` set by admin.
- Student-created email queued via notification outbox (temp password not in email body).

### Student first login / force-password-change

- Middleware intercepts every authenticated request.
- If `MustChangePassword == true` and path ≠ `/api/auth/change-password` → 403.
- Student must call `POST /api/auth/change-password` (requires current password) to clear flag.

### Reset-password link flow

- Admin calls `POST /api/admin/students/{id}/send-reset-link`.
- `PasswordResetHandler` generates ASP.NET Identity token, Base64Url-encodes it, builds reset link.
- Token never logged, never stored in metadata, never returned in API response.
- Reset link emailed via notification outbox.
- `POST /api/auth/reset-password` (public): decodes token, validates via Identity `ResetPasswordAsync`, generic errors on failure. Clears `MustChangePassword` on success.
- Token lifetime: ASP.NET Identity default for data-protection tokens = 1 day.

### Email confirmation

- `RequireConfirmedEmail = false` in Identity options.
- Email confirmation is admin-controlled: admin sets `EmailConfirmed = true` on student creation.
- No self-service email verification flow exists.

### Account lockout

**NOT CONFIGURED.** `LockoutOptions` not set in `Program.cs`. ASP.NET Identity default: lockout disabled. `CheckPasswordAsync` does not fail-count or lock.

---

## 3. Password / Security Policy

| Rule | Current value |
|------|---------------|
| Minimum length | 8 characters |
| Require digit | Yes |
| Require uppercase | No |
| Require non-alphanumeric | No |
| Unique email | Enforced |
| Password history | Not implemented |
| Password expiry | Not implemented |
| Account lockout | Not configured (disabled) |
| Brute-force protection | None (no rate limit on login) |
| Reset token lifetime | ASP.NET Identity default (~1 day via Data Protection) |
| Token replay protection | ASP.NET Identity invalidates token after use (single-use) |
| Generic error responses | Yes (no user/password distinction) |

---

## 4. Authorization

### Admin-only protection

- `AdminController`: `[Authorize(Roles = nameof(UserRole.Admin))]` at class level.
- All `/api/admin/` routes covered.

### Student-only protection

- All student controllers: `[Authorize]` (no role restriction).
- Cross-user isolation enforced in handlers (ownership checks on StudentProfileId / UserId).
- An Admin user can call student API endpoints — this is by design (admin-impersonation not implemented but also not blocked).

### Claims / user ID resolution

- `ClaimTypes.NameIdentifier` used throughout.
- Middleware for `MustChangePassword` check resolves user from DB by claim.

### Role constants / policies

- `UserRole` enum used directly as role string in `[Authorize(Roles = ...)]`.
- No named authorization policies defined beyond default `[Authorize]`.
- No resource-based policies.

### Tests for auth guards

- Integration tests exist for admin/student separation (present in `LinguaCoach.IntegrationTests`).
- No dedicated auth guard unit tests found in review scope.

---

## 5. Existing Features Summary

| Feature | Status |
|---------|--------|
| Google OAuth / external login | Not implemented |
| Enterprise SSO (SAML/OIDC) | Not implemented |
| MFA / 2FA | Not implemented (schema column exists, unused) |
| Email verification (self-service) | Not implemented |
| Password policy configuration (admin UI) | Not implemented |
| Account lockout configuration | Not implemented |
| Session / device management | Not implemented |
| Admin revoke sessions | Not implemented |
| Auth event audit log | Not implemented |
| Security notification events | Not implemented |
| Rate limiting on auth endpoints | Not implemented |
| Captcha / abuse protection | Not implemented |
| Refresh token / rotation | Not implemented |

---

## 6. Existing Reset-Password Work

- Token-based reset link: implemented (Phase 10W-4B).
- Reset email queuing: implemented via notification outbox.
- Generic error behavior: implemented.
- Token not logged or stored: confirmed.
- Admin send-reset-link: implemented.
- Temp-password flow: still present and works in parallel with reset links.
- `MustChangePassword` cleared on both flows (change-password and reset-password).
- **Recommendation:** Temp-password flow (`MustChangePassword`) can coexist with reset links. No need to deprecate. Deprecation only if OAuth replaces all initial provisioning.

---

## 7. Data Model Gaps

| Gap | Severity | Notes |
|-----|----------|-------|
| Refresh tokens table | Medium | No token rotation, no revocation |
| Login audit event log | High | No per-attempt record with IP, user-agent, outcome |
| Auth event log (password change, reset, lockout) | High | No audit trail for auth security events |
| External login provider records | Low | Only needed when OAuth added |
| MFA secrets / recovery codes | Low | Only needed when MFA added |
| Device / session records | Medium | No way to revoke specific sessions |
| Password policy config table | Low | Currently hardcoded; no admin tuning |
| Security settings config table | Low | No per-deployment lockout/policy config |
| `AccessFailedCount` usage | High | Column exists in Identity schema, never incremented |
| `LockoutEnd` / `LockoutEnabled` usage | High | Columns exist, never activated |

---

## 8. Admin UI Gaps

| Gap | Notes |
|-----|-------|
| Security settings page | No dedicated security config UI |
| Password policy settings | Policy is hardcoded |
| OAuth provider configuration | Not applicable yet |
| User session / revoke view | No session management UI |
| Auth audit log view | No auth event log to show |
| Account lockout / unlock | Lockout not implemented |
| Force password reset (manual) | Partial: admin can send reset link |
| Email verification state | `EmailConfirmed` visible but not editable in UI |
| Security notifications | Not configured |

---

## 9. Risk Areas — Ranked

### Critical

- **No account lockout / brute-force protection.** `/api/auth/login` is unprotected. An attacker can attempt unlimited passwords with no throttle. Mitigation effort: low (configure `LockoutOptions` + add rate limiter policy).

- **No login rate limiting.** Generic error messages help but do not prevent password spray. A dedicated rate limiter on login (IP-scoped + user-scoped) is required before enterprise deployment.

### High

- **No token revocation.** A stolen 24-hour JWT cannot be revoked. Until refresh tokens + token blocklist or short-lived tokens exist, a compromised session lives until expiry.

- **No auth event audit log.** No record of login attempts, password changes, password resets, or failed logins linked to IP/user-agent. Required for SOC 2 / enterprise security compliance.

- **JWT key in `appsettings.json`.** The placeholder is rejected in production, but there is no secrets-manager integration. Real deployments must inject `JWT_KEY` via environment variable or a secrets manager. No guidance or validation beyond the placeholder check.

### Medium

- **Password policy is weak for enterprise.** 8 chars + digit only. No uppercase, no special chars requirement. No password history. Acceptable for MVP; not acceptable for enterprise.

- **No session/device management.** Users cannot see active sessions or revoke them. Admins cannot revoke a student's active token.

- **Token lifetime is 24 hours with no refresh.** Long-lived tokens increase exposure window. Should move to short-lived access tokens (15–60 min) + refresh token rotation.

- **No security notifications.** No event fires when a password is changed, reset link is sent, or login fails repeatedly.

### Low

- **MFA column exists but unused.** `TwoFactorEnabled` in Identity schema is never activated. Not a gap yet, but creates confusion.

- **Admin can call student endpoints.** By design, but undocumented. Cross-role call surface should be explicit policy, not coincidence.

- **CORS allows `AllowAnyMethod()` in dev.** Acceptable for dev, but the policy should be tightened to allowed verbs only even in dev.

- **No security headers.** No `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy` middleware. Partially mitigated by Angular serving from same origin in production.

---

## 10. Recommended Architecture

### Auth model target state

```
Short-lived JWT (15 min)
  + Refresh token (HttpOnly cookie, 30-day sliding)
  + Refresh token table (userId, tokenHash, issuedAt, expiresAt, revokedAt, device/IP)
  + Auth event log (userId, event type, IP, user-agent, outcome, timestamp)
```

### Lockout / rate limiting target state

```
Identity LockoutOptions: MaxFailedAccessAttempts=5, LockoutTimeSpan=15min, LockoutEnabled=true
Rate limiter policy "AuthLogin": IP-scoped fixed-window (10 req / 5 min)
Rate limiter policy "AuthReset": IP-scoped fixed-window (3 req / 15 min)
```

### Password policy target state

```
MinLength=10, RequireUppercase=true, RequireNonAlphanumeric=true, RequireDigit=true
No password history (optional — add PasswordHash history table if enterprise needs it)
```

### Auth event types to log

```
LoginSuccess, LoginFailed, LoginLockedOut
PasswordChanged, PasswordResetRequested, PasswordResetCompleted
TokenRefreshed, TokenRevoked
AccountLocked, AccountUnlocked
```

---

## 11. Recommended Phased Roadmap

### 10Auth-F-1 — Auth security baseline (no migrations required except lockout config)

- Enable ASP.NET Identity lockout: `MaxFailedAccessAttempts=5`, `LockoutTimeSpan=15min`, `LockoutEnabled=true`.
- Add rate limiter policy "AuthLogin" (IP-scoped, 10 req / 5 min) to `POST /api/auth/login`.
- Add rate limiter policy "AuthReset" (IP-scoped, 3 req / 15 min) to `POST /api/auth/reset-password` and `POST /api/admin/students/{id}/send-reset-link`.
- Strengthen password policy: `MinLength=10`, `RequireUppercase=true`, `RequireNonAlphanumeric=true`.
- Add security response headers middleware (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`).
- Add integration tests for lockout behavior and rate limiting.
- **Scope:** ~1 day. No migration. No UI change.

### 10Auth-F-2 — Auth event audit log

- New entity: `AuthAuditEvent` (userId, eventType, ipAddress, userAgent, outcome, createdAt).
- Migration: `T58_AuthAuditEvents`.
- Log: login success/failure, lockout, password change, password reset request/completion.
- Admin: `GET /api/admin/students/{id}/auth-events` (read-only, paginated).
- Admin UI: auth events tab in student detail (read-only list).
- **Scope:** ~2 days. One migration.

### 10Auth-F-3 — Security notifications

- Wire auth events into notification platform.
- Notify student on: password changed (in-app + email), reset link sent (in-app), account locked (in-app + email if unlockable by user).
- Notify admin on: repeated failed logins (in-app), account locked (in-app).
- New notification templates: `auth.password_changed`, `auth.account_locked`, `auth.reset_requested`.
- **Scope:** ~1 day. No migration (uses existing notification platform).

### 10Auth-F-4 — Refresh token / session management

- New entity: `RefreshToken` (userId, tokenHash, issuedAt, expiresAt, revokedAt, ipAddress, userAgent, deviceLabel).
- Migration: `T59_RefreshTokens`.
- New endpoint: `POST /api/auth/refresh` (validates refresh token cookie, issues new short-lived JWT + rotates refresh token).
- New endpoint: `POST /api/auth/logout` (revokes refresh token).
- Reduce JWT expiry to 15 min; refresh token lifetime 30 days sliding.
- HttpOnly, Secure, SameSite=Strict cookie for refresh token.
- Admin: `GET /api/admin/students/{id}/sessions`, `DELETE /api/admin/students/{id}/sessions/{id}`.
- Admin UI: sessions tab in student detail.
- **Scope:** ~3 days. One migration. Angular token-refresh interceptor.

### 10Auth-F-5 — Google OAuth / external login foundation

- Add `AddGoogleOpenIdConnect` or `AddGoogle` to Identity.
- External login provider table (ASP.NET Identity `UserLogin` — already in schema via `AspNetUserLogins`).
- Angular OAuth callback page + Google sign-in button on login.
- Admin: see external logins for a student.
- **Scope:** ~2 days. No new migration (Identity schema already has the table).

### 10Auth-F-6 — Admin security settings UI

- Security settings page: password policy config (editable, persisted in `AppConfiguration` or new table), lockout policy config.
- Account management: force-lock, force-unlock, force-password-reset buttons in student detail.
- Auth event log view for admins (system-wide, filterable by event type / date).
- **Scope:** ~2 days.

### 10Auth-F-FINAL — Auth/security closure audit

- All 10Auth-F sub-phases verified closed.
- Security invariants confirmed.
- Integration test coverage gate passed.
- Docs updated.

---

## 12. Decisions Made

- Temp-password flow (`MustChangePassword`) is retained. It is complementary to reset links, not deprecated.
- OAuth is deferred to 10Auth-F-5 (not in immediate scope).
- MFA/2FA is deferred beyond 10Auth-F-6 (requires device management first).
- Enterprise SSO (SAML) deferred until a customer demands it.
- Rate limiting on auth endpoints is the single highest-priority gap.

---

## 13. Risks / Unresolved Questions

- **JWT key rotation:** No rotation mechanism. A key compromise requires a deployment change and invalidates all active sessions. Resolve in 10Auth-F-4 (refresh token refresh + key versioning).
- **Password policy migration:** Strengthening policy does not retroactively invalidate existing weak passwords. Existing users will only be prompted on next change/reset. Accept this trade-off.
- **Lockout DoS:** Enabling lockout creates a new attack surface (attacker locks all accounts). Mitigate with IP rate limiting before enabling lockout, and offer unlock-by-email flow.
- **Admin-calling-student-API:** No formal policy. Currently depends on handler ownership checks. Should be documented as accepted design in AGENTS.md.

---

## 14. Final Verdict

The current auth model is **acceptable for a closed MVP / admin-provisioned SaaS** but has several gaps that block enterprise readiness:

1. No brute-force/lockout protection (critical).
2. No auth audit log (required for SOC 2).
3. No session revocation (required for enterprise key rotation / incident response).
4. Weak password policy (insufficient for enterprise).
5. No security notifications (required for enterprise trust signals).

The roadmap above addresses these in priority order. 10Auth-F-1 (lockout + rate limiting + password policy hardening) can ship in one day and closes the critical gap immediately.

---

## 15. Next Recommended Action

**Start 10Auth-F-1:** Enable lockout, add auth rate limiter policies, strengthen password policy, add security response headers. No migration, no UI change, ~1 day scope.

---

## Implementation Tasks Produced

See TODOS.md for `TODO-10Auth-F-1` through `TODO-10Auth-F-FINAL`.

---

## Gate Result

- Audit-only. No code changes required or made.
- `git diff --check`: clean (docs change only).
- Code changed: **No.**
- Commit/push status: pending (docs added, awaiting commit instruction).
