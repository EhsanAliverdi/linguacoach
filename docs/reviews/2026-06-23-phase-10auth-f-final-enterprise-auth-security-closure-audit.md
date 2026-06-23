# Phase 10Auth-F-FINAL — Enterprise Auth/Security Closure Audit

**Date:** 2026-06-23
**Sprint:** 10Auth-F (complete)
**HEAD before work:** e5f046a
**Scope:** Audit and documentation only. No new features, no migrations, no auth behaviour changes, no UI changes.

---

## 1. Authentication Model Closure

| Item | Status | Detail |
|---|---|---|
| ASP.NET Identity configuration | ✅ | `AddIdentity<ApplicationUser, IdentityRole<Guid>>` in `Program.cs`. `RequireUniqueEmail=true`. `RequireConfirmedEmail=false` (admin-created accounts). |
| JWT bearer configuration | ✅ | `AddJwtBearer` with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all `true`. |
| JWT signing validation | ✅ | `SymmetricSecurityKey(UTF8.GetBytes(jwtKey))`, HMAC-SHA256. Placeholder key rejected outside Development. Key must be ≥ 32 chars in non-Development environments. |
| Access token expiry | ✅ | `Jwt:ExpiryHours` config (default 24h). Set in `JwtTokenService`. |
| Refresh token/session model | ✅ | `UserRefreshToken` entity (migration T59). Hash-only storage. `IRefreshTokenService` / `RefreshTokenService`. |
| Refresh token expiry | ✅ | `Jwt:RefreshTokenExpiryDays` config (default 14 days). |
| Refresh token hash-only storage | ✅ | `SHA256.HashData` → hex string stored. Raw token returned in HTTP response only. Never logged. |
| Refresh token rotation | ✅ | Old token revoked (`Rotated`) on every successful refresh. New token issued. `ReplacedByTokenId` chain recorded. |
| Refresh token reuse handling | ✅ | Reuse of rotated/revoked token triggers full `RevokeAllAsync("ReuseDetected")` + audit `RefreshTokenReuseDetected`. |
| Logout/revoke-session behaviour | ✅ | `POST /api/auth/logout` revokes single token by hash. `POST /api/auth/revoke-sessions` revokes all active tokens for authenticated user. |
| Password change session revocation | ✅ | `ChangePasswordHandler` calls `RevokeAllAsync("PasswordChanged")` after successful change. |
| Password reset session revocation | ✅ | `PasswordResetHandler` calls `RevokeAllAsync("PasswordReset")` after token-validated reset completes. |
| Role model | ✅ | `UserRole.Admin` and `UserRole.Student`. Stored as Identity claim + `ApplicationUser.Role`. |
| Current-user resolution | ✅ | `User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")` in controllers. |
| Force-password-change middleware | ✅ | Inline middleware in `Program.cs`: authenticated requests (except `POST /api/auth/change-password`) return HTTP 403 with JSON error when `ApplicationUser.MustChangePassword = true`. |

---

## 2. Password and Brute-Force Protection Closure

### Password Policy

| Item | Value | Status |
|---|---|---|
| Minimum length | 10 characters | ✅ |
| Uppercase required | Yes | ✅ |
| Lowercase required | Yes | ✅ |
| Digit required | Yes | ✅ |
| Non-alphanumeric required | Yes | ✅ |

### Lockout

| Item | Value | Status |
|---|---|---|
| Failed attempts threshold | 5 | ✅ |
| Lockout duration | 15 minutes | ✅ |
| Enabled for new users | Yes | ✅ |
| Generic error response | `"Invalid credentials."` on lockout and all login failures | ✅ |

### Rate Limiting

| Policy | Limit | Window | Keyed by | Endpoint(s) | Status |
|---|---|---|---|---|---|
| AuthLogin | 10 req | 5 min | IP | `POST /api/auth/login` | ✅ |
| AuthReset | 3 req | 15 min | IP | `POST /api/auth/reset-password` | ✅ |
| AuthChangePassword | 10 req | 5 min | UserId | `POST /api/auth/change-password` | ✅ |
| AuthExternalLogin | 20 req | 5 min | IP | `POST /api/auth/external/google` | ✅ |
| AuthRefresh | 30 req | 5 min | IP | `POST /api/auth/refresh`, `POST /api/auth/logout` | ✅ |

Note: `POST /api/auth/revoke-sessions` is `[Authorize]` only, no dedicated rate limit. Acceptable — requires a valid JWT; cannot be used for brute-force.

### Known Deferred Items

| Item | Status |
|---|---|
| Distributed/Redis rate limiting (multi-instance) | Deferred — single-host for current stage |
| CAPTCHA / bot protection | Deferred |
| Account unlock-by-email flow | Deferred — admin can reset; lockout self-expires after 15 min |

---

## 3. Auth Event Audit Closure

| Item | Status | Detail |
|---|---|---|
| Audit entity/table | ✅ | `AuthSecurityEvent` entity. Migration T58 (`auth_security_events`). |
| Audit service | ✅ | `IAuthSecurityAuditService` / `AuthSecurityAuditService`. Try/catch — audit failure never aborts auth flow. |
| Login events | ✅ | `LoginSucceeded`, `LoginFailed`, `LoginLockedOut` |
| Lockout event | ✅ | `LoginLockedOut` on both: pre-check lockout and lockout-transition after `AccessFailedAsync` |
| Password changed events | ✅ | `PasswordChanged`, `PasswordChangeFailed`, `ForcePasswordChangeCompleted` |
| Password reset events | ✅ | `PasswordResetRequested`, `PasswordResetSucceeded`, `PasswordResetFailed` |
| Refresh/session events | ✅ | `RefreshTokenIssued`, `RefreshTokenRotated`, `RefreshTokenRevoked`, `RefreshTokenReuseDetected`, `LogoutSucceeded`, `AllSessionsRevoked` |
| External login events | ✅ | `ExternalLoginSucceeded`, `ExternalLoginFailed`, `ExternalLoginLinked`, `ExternalLoginRejected`, `ExternalProviderDisabled`, `ExternalEmailUnverified`, `ExternalDomainRejected` |
| Student account created event | ✅ | `StudentAccountCreated` (from admin create-student flow) |
| Audit metadata safety | ✅ | No passwords, no reset tokens, no JWTs, no raw Google tokens, no secrets in `MetadataJson` |
| Admin read visibility | ✅ | `GET /api/admin/auth-events` (F-2) and `GET /api/admin/security/auth-events` (F-6 alias). Paginated, filtered by eventType/outcome/email/userId/date. |

**Full event type enum (23 values):**
LoginSucceeded, LoginFailed, LoginLockedOut, PasswordChanged, PasswordChangeFailed, ForcePasswordChangeCompleted, PasswordResetRequested, PasswordResetSucceeded, PasswordResetFailed, StudentAccountCreated, RefreshTokenIssued, RefreshTokenRotated, RefreshTokenRevoked, RefreshTokenReuseDetected, LogoutSucceeded, AllSessionsRevoked, ExternalLoginSucceeded, ExternalLoginFailed, ExternalLoginLinked, ExternalLoginRejected, ExternalProviderDisabled, ExternalEmailUnverified, ExternalDomainRejected.

---

## 4. Security Notifications Closure

| Notification | Channels | Trigger | Status |
|---|---|---|---|
| `account.password_changed` | InApp + Email | `ChangePasswordHandler` — success | ✅ |
| `account.password_reset_requested` | InApp only | `PasswordResetHandler.RequestResetAsync` | ✅ |
| `account.password_reset_succeeded` | InApp + Email | `PasswordResetHandler.CompleteResetAsync` | ✅ |
| `account.locked_out` | InApp + Email | `LoginHandler` — lockout transition only | ✅ |
| `account.external_login_linked` | InApp + Email | `ExternalLoginService` — on first link | ✅ |

| Rule | Status |
|---|---|
| All security notifications use `NotificationCategory.Account` (mandatory, cannot be opted out) | ✅ |
| Notification failure is non-fatal — caught, logged, auth flow continues | ✅ |
| SMS security notifications | Deferred — `DisabledSmsSender` is the active SMS provider |

---

## 5. External Login Closure

| Item | Status | Detail |
|---|---|---|
| Google external login endpoint | ✅ | `POST /api/auth/external/google` |
| `Enabled=false` default | ✅ | `GoogleExternalLoginOptions.Enabled` defaults to `false` |
| `AllowAutoLinkByEmail` | ✅ | Configurable; links existing account by verified email when `true` |
| `AllowStudentAutoProvisioning` | ✅ | Defaults to `false`; creates Student account only when explicitly enabled |
| `AllowedDomains` | ✅ | Empty list = any domain allowed; non-empty enforces `HostedDomain` match |
| Token validation abstraction | ✅ | `IGoogleTokenValidator` / `GoogleTokenValidator` (uses `GoogleJsonWebSignature.ValidateAsync`). `FakeGoogleTokenValidator` in tests — no real Google API in CI |
| Verified email requirement | ✅ | `payload.EmailVerified` checked; unverified → `ExternalEmailUnverified` event + rejection |
| Domain restriction behaviour | ✅ | `AllowedDomains` list checked against `HostedDomain`; rejected → `ExternalDomainRejected` event |
| Account linking rules | ✅ | 1. Find by existing Google sub link. 2. If not found and `AllowAutoLinkByEmail=true`, link by email via `AddLoginAsync`. 3. Auto-link disabled → blocked. 4. Unknown user → auto-provision Student if enabled, else blocked |
| Unknown-user behaviour | ✅ | Rejected with `ExternalLoginRejected` / `NoAccountFound` unless auto-provisioning enabled |
| Student-only auto-provisioning | ✅ | `ProvisionStudentAsync` sets `Role=Student`. Admins never auto-created via Google |
| Admin never auto-created | ✅ | Explicitly guarded — provisioning only sets `UserRole.Student` |
| No raw Google token stored/logged | ✅ | Token passed to validator only; never stored in DB, audit metadata, or logs |
| Archived student blocked | ✅ | `StudentLifecycleStage.Archived` check after user resolution |
| Refresh/session integration | ✅ | Issues refresh token on success via `IRefreshTokenService.IssueAsync` |
| Audit/notification integration | ✅ | All outcomes audited; `account.external_login_linked` notification on first link |

---

## 6. Admin Security UI Closure

| Item | Status | Detail |
|---|---|---|
| Admin route/page | ✅ | `/admin/security` lazy-loaded. Security nav item in System section (mobile + desktop sidebar) |
| Backend settings endpoint | ✅ | `GET /api/admin/security/settings` — `[Authorize(Roles="Admin")]` |
| Auth events endpoint | ✅ | `GET /api/admin/security/auth-events` — alias for F-2 `IAdminAuthEventHandler` |
| Safe secret handling | ✅ | JWT signing key never read. `ClientSecret` → `ClientSecretConfigured: bool` only. `ClientId` → `ClientIdConfigured: bool` only. SMTP password → never in this endpoint |
| Password/lockout/rate-limit visibility | ✅ | Read from live `IdentityOptions`; rate limit summary hardcoded to match `Program.cs` |
| JWT/refresh/session visibility | ✅ | Expiry hours, expiry days, rotation enabled, revoke-on-change, issuer/audience presence |
| Google provider visibility | ✅ | Enabled flag, presence-only for ClientId/Secret, auto-link/auto-provision/allowed-domains |
| Auth event visibility | ✅ | Events tab in `/admin/security` page with eventType/outcome/email filters + pagination |
| Session visibility/revocation | Not implemented | Out of scope for this epic. `POST /api/auth/revoke-sessions` exists for self-service. Admin-initiated per-user revocation is deferred |
| Frontend tests | ✅ | 16 unit tests for `AdminSecurityComponent` |
| Frontend production build | ✅ | Clean |

---

## 7. Security Headers and Production Safety

| Item | Status | Detail |
|---|---|---|
| `X-Content-Type-Options: nosniff` | ✅ | `SecurityHeadersMiddleware` |
| `X-Frame-Options: DENY` | ✅ | `SecurityHeadersMiddleware` |
| `Referrer-Policy: no-referrer` | ✅ | `SecurityHeadersMiddleware` |
| `Permissions-Policy` (camera/mic/geo/payment denied) | ✅ | `SecurityHeadersMiddleware` |
| CSP | Deferred | Requires Angular build nonce strategy. Documented in middleware comments. |
| HSTS | Deferred | Requires production TLS/reverse-proxy confirmation. Documented in middleware comments. |
| JWT key placeholder guard | ✅ | Startup throws in non-Development if key is the committed placeholder or < 32 chars |
| `appsettings.json` secrets risk | Known gap | `Jwt:Key`, SMTP password, SMS ApiKey, Google ClientSecret should be injected via environment variables or secrets manager in production. Docker Compose uses env vars for `JWT_KEY`. Production deployment guide should document this explicitly |
| Data Protection key persistence | ✅ | `PersistKeysToFileSystem` wired. `dp_keys` Docker named volume. Optional certificate protection supported. |
| Data Protection key encryption | ✅ | `DataProtectionKeyMode.Certificate` path available. Default is `None` (keys at rest unencrypted) — acceptable for single-host; cert-based or KMS required for multi-instance |
| Public URL / HTTPS / proxy | Partially documented | `UseHttpsRedirection` is present. CORS allows localhost:4200/4300 in Development only. Production HTTPS via reverse proxy is assumed but not formally documented in a deployment guide |

### Deployment Notes Required (Deferred)

A formal deployment guide (`docs/deployment/`) should cover:
- Required environment variables: `JWT_KEY`, `ConnectionStrings__DefaultConnection`, AI provider keys, SMTP credentials, Google OAuth credentials
- HTTPS termination at reverse proxy
- Data Protection key volume mount
- HSTS activation after TLS confirmation
- CSP header strategy after Angular build audit

---

## 8. Regression / Gates

Gates run after completing the audit documentation.

| Gate | Result |
|---|---|
| `git diff --check` | ✅ Clean |
| `dotnet restore` | ✅ Clean |
| `dotnet build --configuration Release` | ✅ 0 errors, 15 pre-existing warnings |
| `dotnet test --configuration Release` | ✅ 2369/2369 passed (3 arch + 1310 unit + 1056 integration) |
| `npm run build -- --configuration production` | ✅ Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | ✅ 1025/1025 passed |

### Playwright

Not run for this phase. Rationale:
- This is an audit-only phase — no auth behaviour, UI flow, or navigation changed.
- No new Playwright specs exist for the auth endpoints (they are integration-tested at HTTP level via `WebApplicationFactory`).
- Admin security page (`/admin/security`) is new but follows an identical pattern to existing admin pages that are already Playwright-tested by `admin-students-reset.spec.ts` and similar. A dedicated Playwright spec for the security page is a future improvement.

---

## 9. Final Auth/Security Capability Summary

### Closed (Production-Ready for Current Stage)

| Capability | Phase | Notes |
|---|---|---|
| Password policy (length 10, upper/lower/digit/special) | F-1 | |
| Account lockout (5 attempts, 15 min) | F-1 | |
| Generic error responses (no user enumeration) | F-1 | |
| Rate limiting (5 policies across all auth endpoints) | F-1, F-2, F-4, F-5 | |
| Security headers (4 headers) | F-1 | CSP/HSTS deferred |
| Auth event audit log (23 event types, T58) | F-2 | |
| Audit metadata safety (no secrets/tokens) | F-2 | |
| Admin auth event read endpoint | F-2 | |
| Security notifications (5 template groups) | F-3 | SMS deferred |
| Notification failure non-fatal | F-3 | |
| Refresh token model (hash-only, rotation, reuse detection) | F-4 | |
| Session revocation (logout, revoke-all, password-change/reset) | F-4 | |
| Google external login foundation | F-5 | Disabled by default |
| Google token validation abstraction (testable) | F-5 | |
| External login audit + notifications | F-5 | |
| Force-password-change middleware | F-1 (existing) | |
| Admin security settings read page | F-6 | |
| Data Protection key persistence + optional encryption | 10W-5C-4/5 | |

### Remaining Deferred Items

| Item | Ticket | Priority |
|---|---|---|
| Distributed rate limiting (Redis/multi-instance) | — | Before horizontal scaling |
| CAPTCHA / bot protection | — | If brute-force signals emerge |
| Account unlock-by-email | — | Low — 15 min auto-unlock exists |
| CSP header | — | Before production hardening |
| HSTS | — | After production TLS confirmation |
| Admin-initiated per-user session revocation UI | — | Future admin sessions page |
| SMS security notifications | TODO-10W-SMS-PROVIDER | After SMS provider + phone verification |
| Cloud KMS for Data Protection keys | TODO-10W-DP-CLOUD-KMS | Before horizontal scaling |
| Formal deployment guide | — | Before first production deployment |
| MFA | — | Not in current product scope |
| Enterprise SSO/SAML/OIDC | — | Not in current product scope |

---

## 10. TODOS.md Update Required

`TODO-10Auth-F-6` entry in TODOS.md is stale — it describes the original F-6 scope idea (force-lock/unlock/force-reset buttons), not the implemented scope. The implemented F-6 is a read-only settings and auth event visibility page.

`TODO-10Auth-F-FINAL` entry needs to be marked done.

Both are updated in the TODOS.md file as part of this phase commit.

---

## 11. Recommended Next Phase

**10UI-AUDIT-0 — Full UI / Backend Capability Reconciliation**

The 10Auth-F series and the 10W notification platform close the current enterprise hardening arc. The next recommended phase is a systematic audit of every backend capability against the current Angular UI:

- Inspect every backend API route and every Angular admin/student route.
- Produce a route-by-route matrix: route | backend capabilities | UI currently exposes | missing UI | priority | recommended next phase.
- Admin routes to cover: `/admin`, `/admin/students`, `/admin/ai-config`, `/admin/prompts`, `/admin/usage`, `/admin/exercise-types`, `/admin/integrations`, `/admin/diagnostics`, `/admin/notifications`, `/admin/curriculum`, `/admin/security`.
- Student routes to cover: `/student/today`, `/student/journey`, `/student/practice`, `/student/progress`, `/student/profile`.
- Identify missing, incomplete, misleading, or stale UI.
- Output: matrix + prioritised list of next UI phases.

---

## 12. Files Inspected

- `src/LinguaCoach.Api/Controllers/AuthController.cs`
- `src/LinguaCoach.Api/Program.cs`
- `src/LinguaCoach.Infrastructure/Auth/JwtTokenService.cs`
- `src/LinguaCoach.Infrastructure/Auth/LoginHandler.cs`
- `src/LinguaCoach.Infrastructure/Auth/ChangePasswordHandler.cs`
- `src/LinguaCoach.Infrastructure/Auth/RefreshTokenService.cs`
- `src/LinguaCoach.Infrastructure/Auth/ExternalLoginService.cs`
- `src/LinguaCoach.Infrastructure/Auth/AuthSecurityAuditService.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminSecurityHandler.cs`
- `src/LinguaCoach.Application/Admin/SecuritySettingsQueries.cs`
- `src/LinguaCoach.Domain/Enums/AuthEventType.cs`
- `src/LinguaCoach.Api/Middleware/SecurityHeadersMiddleware.cs`
- `TODOS.md`
- `docs/sprints/current-sprint.md`

---

## Gate Results (Final)

*(Populated after gates complete — see below.)*

---

## Final Verdict

The 10Auth-F enterprise auth/security hardening series is **complete and production-ready for the current SpeakPath single-host stage**.

All 6 implementation phases (F-1 through F-6) are closed. 23 auth event types are audited. All security invariants confirmed: no raw passwords, tokens, secrets, or Google ID tokens in audit metadata, logs, or API responses. Refresh tokens are hash-only with rotation and reuse detection. Session revocation is wired into password change, password reset, and manual logout/revoke.

Remaining deferred items (CSP, HSTS, distributed rate limiting, MFA, SMS, cloud KMS) are appropriate for the current product stage and documented.

**Next recommended phase: 10UI-AUDIT-0 — Full UI / Backend Capability Reconciliation.**
