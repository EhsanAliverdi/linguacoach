# Phase 10Auth-F-5 — Google OAuth / External Login Foundation: Engineering Review

**Date:** 2026-06-23
**Sprint/Phase:** 10Auth-F-5
**Reviewer:** Claude Code (Sonnet 4.6)
**Status:** Complete — all gates passed

---

## Files Changed

### New files

- `src/LinguaCoach.Application/Auth/ExternalLoginOptions.cs` — `GoogleExternalLoginOptions` bound from `Authentication:ExternalProviders:Google`
- `src/LinguaCoach.Application/Auth/IGoogleTokenValidator.cs` — abstraction over Google ID token validation; testable without real Google API calls
- `src/LinguaCoach.Application/Auth/IExternalLoginService.cs` — `IExternalLoginService` interface + `ExternalLoginRequest` / `ExternalLoginResult` records
- `src/LinguaCoach.Infrastructure/Auth/GoogleTokenValidator.cs` — production implementation using `Google.Apis.Auth.GoogleJsonWebSignature`
- `src/LinguaCoach.Infrastructure/Auth/ExternalLoginService.cs` — account linking, token issuance, audit events, security notifications
- `tests/LinguaCoach.IntegrationTests/Api/ExternalLoginTests.cs` — 20 integration tests with `FakeGoogleTokenValidator`

### Modified files

- `src/LinguaCoach.Domain/Enums/AuthEventType.cs` — 7 new event types
- `src/LinguaCoach.Infrastructure/LinguaCoach.Infrastructure.csproj` — `Google.Apis.Auth 1.69.0` package added
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — `GoogleExternalLoginOptions`, `IGoogleTokenValidator`, `IExternalLoginService` registered
- `src/LinguaCoach.Api/Program.cs` — `AuthExternalLogin` rate limiter policy added (20 req / 5 min / IP)
- `src/LinguaCoach.Api/Controllers/AuthController.cs` — `POST /api/auth/external/google` endpoint added
- `src/LinguaCoach.Persistence/Seed/NotificationTemplateSeeder.cs` — 2 new templates: `account.external_login_linked` (InApp + Email)

---

## Summary

Adds a backend-first Google external login foundation. The existing local email/password login, JWT access tokens, refresh token session management (10Auth-F-4), audit log (10Auth-F-2), and security notifications (10Auth-F-3) are all unchanged and regression-tested.

Google ID tokens are validated server-side using `Google.Apis.Auth` with audience verification. The raw ID token is never stored, logged, or included in audit metadata. Accounts are linked via Identity's `AspNetUserLogins` table (no new migration required). JWT access tokens and refresh tokens are issued using the same services as local login.

---

## Configuration

Bound from `Authentication:ExternalProviders:Google` section.

| Setting | Default | Notes |
|---------|---------|-------|
| `Enabled` | `false` | Provider disabled by default — safe out of the box |
| `ClientId` | `""` | Must be set for Google login to work |
| `ClientSecret` | `""` | Never exposed through APIs or logged |
| `AllowedDomains` | `[]` | Empty = any Google account; set to restrict by hosted domain (hd claim) |
| `AllowAutoLinkByEmail` | `true` | Auto-link existing local account by verified email |
| `AllowStudentAutoProvisioning` | `false` | No public self-registration by default |

---

## Endpoint Added

| Endpoint | Auth | Rate Limiter | Description |
|----------|------|--------------|-------------|
| `POST /api/auth/external/google` | Anonymous | `AuthExternalLogin` (20/5min/IP) | Validate Google ID token, link/sign in, return JWT + refresh token |

Response shape is identical to `POST /api/auth/login`:
```json
{
  "token": "...",
  "role": "student",
  "mustChangePassword": false,
  "refreshToken": "...",
  "refreshExpiresAtUtc": "..."
}
```

---

## Account Linking Rules

| Case | Behaviour |
|------|-----------|
| Existing `AspNetUserLogins` record for Google sub | Sign in directly |
| Existing local account with same verified email, `AllowAutoLinkByEmail=true` | Link automatically, then sign in |
| Existing local account with same verified email, `AllowAutoLinkByEmail=false` | Reject — generic 401 |
| No existing account, `AllowStudentAutoProvisioning=false` (default) | Reject — generic 401 |
| No existing account, `AllowStudentAutoProvisioning=true` | Provision Student account, link, sign in |
| Admin account (existing) | Auto-link by email if `AllowAutoLinkByEmail=true`; never auto-created |
| Unverified Google email | Always reject |
| Google token fails validation | Always reject — generic 401 |
| Provider disabled | Always reject — generic 401 |
| Hosted domain not in `AllowedDomains` | Always reject — generic 401 |

---

## Auto-Provisioning Stance

`AllowStudentAutoProvisioning` defaults to `false`. No public self-registration occurs by default. When enabled, only `UserRole.Student` accounts can be provisioned — admin accounts are never auto-created via Google login.

---

## Allowed-Domain Behaviour

When `AllowedDomains` is non-empty, the Google `hd` (hosted domain) claim is checked. If the claim is missing or does not match any entry in the list, login is rejected with `ExternalDomainRejected` audit event. Domain comparison is case-insensitive.

---

## Token / Session Issuance

After successful Google login, the same token issuance path as local login is used:

1. `ITokenService.GenerateToken` — JWT access token (24h default).
2. `IRefreshTokenService.IssueAsync` — refresh token (14d default, stored as SHA-256 hash only).
3. Same `RefreshTokenIssued` audit event written.
4. Raw Google ID token is never stored.
5. Google access token is never requested or stored.

---

## Audit Events Added

| Event | Outcome | When |
|-------|---------|------|
| `ExternalLoginSucceeded` | Success | Successful Google login |
| `ExternalLoginFailed` | Failure | Token invalid, provisioning failed, link failed |
| `ExternalLoginLinked` | Success | Google sub linked to existing or new account |
| `ExternalLoginRejected` | Blocked | No account + no provisioning, auto-link disabled, domain rejected, archived account |
| `ExternalProviderDisabled` | Blocked | Provider is disabled in config |
| `ExternalEmailUnverified` | Blocked | Google email not verified |
| `ExternalDomainRejected` | Blocked | Hosted domain not in allowed list |

Raw Google ID token is never written to `MetadataJson` or any other audit field.

---

## Notification Templates Added

| Template Key | Channel | Trigger |
|-------------|---------|---------|
| `account.external_login_linked` | InApp | When Google account is linked to existing account |
| `account.external_login_linked` | Email | When Google account is linked to existing account |

Notifications are queued only on account link — not on every subsequent Google login (to avoid noise). Templates use `NotificationCategory.Account` (bypasses user opt-out via `IsRequired`).

---

## Sensitive Data Exclusions

- Raw Google ID token: never stored, logged, or in audit metadata
- Google ClientSecret: `HasClientSecret` bool only (not exposed in API responses in this phase; config-only)
- Google access token: not requested or stored
- Google refresh token: not requested or stored

---

## Tests Added (`ExternalLoginTests.cs` — 20 tests)

| Test | Verifies |
|------|----------|
| `GoogleLogin_ProviderDisabled_ReturnsUnauthorized` | Disabled provider → 401 |
| `GoogleLogin_InvalidToken_ReturnsGenericUnauthorized` | Bad token → generic 401 |
| `GoogleLogin_UnverifiedEmail_ReturnsUnauthorized` | Unverified email → 401 |
| `GoogleLogin_UnknownUser_NoProvisioning_ReturnsUnauthorized` | No account, no provisioning → 401 |
| `GoogleLogin_ExistingLinkedAccount_ReturnsTokens` | Linked account → 200 with tokens |
| `GoogleLogin_ExistingLinkedAccount_RefreshTokenStoredAsHashNotRaw` | Hash-only storage verified |
| `GoogleLogin_AutoLinkByEmail_LinksExistingAccount` | Email auto-link creates AspNetUserLogins row |
| `GoogleLogin_AutoLinkDisabled_ExistingEmailRejected` | Auto-link disabled → 401 |
| `GoogleLogin_WrongDomain_ReturnsUnauthorized` | Domain not in allowed list → fail |
| `GoogleLogin_AllowedDomain_Passes` | Correct domain → success |
| `GoogleLogin_ExistingRole_IsPreserved` | Student role preserved after Google login |
| `GoogleLogin_UnknownAdminEmail_NotAutoProvisioned` | Unknown user → not provisioned |
| `GoogleLogin_Session_CanBeRefreshed` | Refresh token from Google login works |
| `GoogleLogin_Session_CanBeRevoked` | Logout revokes Google login session |
| `GoogleLogin_Success_WritesExternalLoginSucceededAuditEvent` | Audit event written |
| `GoogleLogin_AutoLink_WritesExternalLoginLinkedAuditEvent` | Link audit event written |
| `GoogleLogin_AuditMetadata_DoesNotContainRawIdToken` | Raw token absent from MetadataJson |
| `GoogleLogin_AutoLink_QueuesLinkedNotifications` | InApp + Email notifications queued on link |
| `LocalLogin_StillWorks_AfterExternalLoginPhase` | Local login regression |
| `RefreshToken_StillWorks_AfterExternalLoginPhase` | Refresh token regression |

`FakeGoogleTokenValidator` — test double injected via `ActivatorUtilities.CreateInstance` or `PostConfigure` — never calls real Google APIs.

---

## Not Implemented (Confirmed Out of Scope)

- Angular login button / Google sign-in UI → deferred
- Admin provider configuration UI → Phase 10Auth-F-6
- Enterprise SSO / SAML / OIDC multi-tenant → not in scope
- MFA → later
- SMS → not in scope
- Full Angular login redesign → not in scope
- Distributed/Redis token store → not in scope
- Phone verification → not in scope
- OAuth server-side callback flow (`/challenge` + `/callback`) → ID token flow chosen instead (simpler, no redirect needed for API-first architecture)
- Richer account-link management UI → later

---

## Migration

**No new migration required.** `AspNetUserLogins` table already exists from ASP.NET Core Identity (`IdentityDbContext`). External login records are stored there via `UserManager.AddLoginAsync` / `FindByLoginAsync`.

---

## Documentation Impact

- Docs reviewed: TODOS.md, docs/sprints/current-sprint.md, docs/handoffs/current-product-state.md
- Docs updated: all three
- Reason: new endpoint, account linking rules, and external login model are product-visible changes

---

## Gate Results

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| `dotnet restore` | OK |
| `dotnet build --configuration Release` | 0 errors, 13 pre-existing warnings |
| `dotnet test --configuration Release` | 2369/2369 passed (Architecture 3, Unit 1310, Integration 1056) |

**Frontend gates: not required — no frontend source changed.**

---

## Final Verdict

**APPROVED — COMPLETE.**

Security invariants met: raw Google token never stored/logged/audited, provider disabled by default, no public auto-provisioning by default, domain restriction enforced, admin never auto-created. 2369/2369 tests pass. No migration required.

## Next Recommended Action

Phase 10Auth-F-6: Admin security settings UI — admin panel for managing auth configuration (notification channel config, provider enable/disable, session management).
