# Phase 10Auth-F-4 — Refresh Tokens and Session Management: Engineering Review

**Date:** 2026-06-23
**Sprint/Phase:** 10Auth-F-4
**Reviewer:** Claude Code (Sonnet 4.6)
**Status:** Complete — all gates passed

---

## Files Changed

### New files
- `src/LinguaCoach.Domain/Entities/UserRefreshToken.cs` — session/refresh token entity
- `src/LinguaCoach.Persistence/Configurations/UserRefreshTokenConfiguration.cs` — EF config
- `src/LinguaCoach.Application/Auth/IRefreshTokenService.cs` — Application interface + commands/results
- `src/LinguaCoach.Infrastructure/Auth/RefreshTokenService.cs` — Infrastructure implementation
- `src/LinguaCoach.Persistence/Migrations/20260623021608_T59_RefreshTokensAndSessions.cs` — migration
- `tests/LinguaCoach.IntegrationTests/Api/RefreshTokenTests.cs` — 20 integration tests

### Modified files
- `src/LinguaCoach.Domain/Enums/AuthEventType.cs` — 6 new event types
- `src/LinguaCoach.Application/Auth/LoginCommand.cs` — `LoginResult` extended with `RefreshToken`, `RefreshExpiresAtUtc`
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — `UserRefreshTokens` DbSet
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — `IRefreshTokenService` registered
- `src/LinguaCoach.Infrastructure/Auth/LoginHandler.cs` — issues refresh token on success
- `src/LinguaCoach.Infrastructure/Auth/ChangePasswordHandler.cs` — revokes all sessions on password change
- `src/LinguaCoach.Infrastructure/Auth/PasswordResetHandler.cs` — revokes all sessions on password reset
- `src/LinguaCoach.Api/Program.cs` — `AuthRefresh` rate limiter policy added
- `src/LinguaCoach.Api/Controllers/AuthController.cs` — 3 new endpoints, login response extended

---

## Summary

Adds production-grade refresh token and user session management. JWT access tokens are unchanged (24h expiry by default). Refresh tokens enable session continuation without re-authentication. Password change/reset invalidates all sessions. Token rotation prevents replay. Reuse detection revokes the entire session family.

---

## Entity / Table

**`UserRefreshToken`** → table `UserRefreshTokens`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| UserId | Guid | Indexed with RevokedAtUtc |
| TokenHash | string(64) | SHA-256 hex; unique index; raw token never stored |
| ExpiresAtUtc | DateTime | Default 14 days from issue |
| RevokedAtUtc | DateTime? | Set on revocation |
| ReplacedByTokenId | Guid? | Set during rotation |
| LastUsedAtUtc | DateTime? | Updated on successful refresh |
| IpAddress | string(64)? | From request |
| UserAgent | string(512)? | Truncated at 512 chars |
| DeviceDescription | string(256)? | Reserved for future use |
| CorrelationId | string(64)? | From X-Correlation-ID header |
| RevocationReason | string(64)? | Logout / PasswordChanged / PasswordReset / Rotated / ReuseDetected |

Indexes: unique on `TokenHash`, composite on `(UserId, RevokedAtUtc)`.

---

## Refresh Token Expiry

Default: **14 days** (configurable via `Jwt:RefreshTokenExpiryDays`). Access token expiry unchanged (default 24h, `Jwt:ExpiryHours`).

---

## Token Rotation Behaviour

1. Client calls `POST /api/auth/refresh` with raw refresh token.
2. Service hashes the token and looks up by hash.
3. If token is active: old token is revoked with reason `Rotated` and `ReplacedByTokenId` set; new refresh token and new access token are issued.
4. New raw refresh token is returned to client — client must replace its stored value.
5. Old token cannot be reused (it is now revoked).

---

## Reuse Detection

If a client presents a token that is already revoked AND has `ReplacedByTokenId` set (i.e., it was previously rotated, not just explicitly revoked), the service treats this as a potential replay/theft attack:
- All active sessions for that user are revoked immediately.
- `RefreshTokenReuseDetected` audit event is written.
- Generic `401 Unauthorized` is returned.

---

## Revocation / Logout Behaviour

| Action | What is revoked |
|--------|----------------|
| `POST /api/auth/logout` with token | That single token |
| `POST /api/auth/logout` without token | No-op (204 returned) |
| `POST /api/auth/revoke-sessions` (authenticated) | All active tokens for current user |
| Password changed | All active tokens for that user (`PasswordChanged`) |
| Password reset succeeded | All active tokens for that user (`PasswordReset`) |

Logout always returns 204 regardless of whether the token existed — no information leak.

---

## Sensitive Data Exclusions

- Raw refresh tokens are never stored in the database.
- Raw refresh tokens are never written to audit metadata.
- Raw refresh tokens are never logged.
- Only SHA-256 hex hash is stored (64 char, constant length).
- Token generation uses `RandomNumberGenerator.Fill` (cryptographically secure, 32 bytes = 256 bits).

---

## Endpoints Added

| Endpoint | Auth | Rate Limiter | Description |
|----------|------|--------------|-------------|
| `POST /api/auth/refresh` | Anonymous | `AuthRefresh` (30/5min/IP) | Rotate refresh token, get new access + refresh tokens |
| `POST /api/auth/logout` | Anonymous | `AuthRefresh` | Revoke a single refresh token; no-op if missing |
| `POST /api/auth/revoke-sessions` | Authorized | None | Revoke all active sessions for current user |

Login response extended: `refreshToken` and `refreshExpiresAtUtc` fields added. Existing fields (`token`, `role`, `mustChangePassword`) unchanged.

---

## Audit Events Added

| Event | Outcome | When |
|-------|---------|------|
| `RefreshTokenIssued` | Success | After successful login |
| `RefreshTokenRotated` | Success | After successful refresh |
| `RefreshTokenRevoked` | Success | After explicit revoke/logout |
| `RefreshTokenReuseDetected` | Blocked | When rotated token is reused |
| `LogoutSucceeded` | (not used — logout writes `RefreshTokenRevoked`) | — |
| `AllSessionsRevoked` | Success | After revoke-all / password change / reset |

---

## Password Change / Reset Session Invalidation

- **Password changed** (`ChangePasswordHandler`): `RevokeAllAsync(userId, "PasswordChanged")` called after successful change. All sessions invalidated; user must log in fresh on all devices.
- **Password reset** (`PasswordResetHandler`): `RevokeAllAsync(userId, "PasswordReset")` called after successful reset. Same behaviour.
- Existing `MustChangePassword` clearing, audit events, and security notifications from prior phases all continue working.

---

## Tests Added (`RefreshTokenTests.cs` — 20 tests)

| Test | Verifies |
|------|----------|
| `Login_Success_ReturnsRefreshToken` | Login response includes refresh token |
| `Login_RefreshToken_IsStoredAsHash_NotRaw` | Only hash stored; raw token not in DB |
| `Refresh_ValidToken_ReturnsNewAccessAndRefreshToken` | Rotation returns new tokens |
| `Refresh_RotatesToken_OldTokenCannotBeReused` | Old token rejected after rotation |
| `Refresh_InvalidToken_ReturnsUnauthorized` | Unknown token → 401 |
| `Refresh_InvalidToken_ResponseIsGeneric` | Response reveals no internals |
| `Refresh_RevokedToken_ReturnsUnauthorized` | Revoked token → 401 |
| `Refresh_RawToken_NotStoredInDatabase` | Raw value absent from all DB columns |
| `Logout_RevokesToken_SubsequentRefreshFails` | Logout → refresh fails |
| `Logout_NoToken_ReturnsNoContent` | Empty logout → 204 |
| `Logout_ResponseDoesNotLeakTokenValidity` | Fake token → still 204 |
| `RevokeAll_RevokesAllActiveSessions` | revoke-sessions invalidates all tokens |
| `ChangePassword_RevokesExistingRefreshTokens` | Password change → refresh fails |
| `ResetPassword_RevokesExistingRefreshTokens` | Password reset → refresh fails |
| `Login_Success_WritesRefreshTokenIssuedAuditEvent` | Audit event written on login |
| `Refresh_Rotated_WritesRefreshTokenRotatedAuditEvent` | Audit event written on rotation |
| `AdminLogin_StillWorks` | Admin login regression |
| `StudentLogin_StillWorks` | Student login regression |
| `SecurityNotifications_StillFire_OnPasswordChange` | 10Auth-F-3 notifications still work |

---

## Not Implemented (Confirmed Out of Scope)

- Angular session/device UI → deferred
- Admin session revoke UI → Phase 10Auth-F-6
- Google OAuth → Phase 10Auth-F-5
- MFA → later
- SMS → later
- Distributed/Redis token store → infrastructure phase if multi-instance scaling required
- Full device fingerprinting → not needed for MVP
- Remember-me UI → not in scope

---

## Documentation Impact

- Docs reviewed: TODOS.md, docs/sprints/current-sprint.md, docs/handoffs/current-product-state.md
- Docs updated: all three
- Reason: new entity, endpoints, and session model are product-visible changes

---

## Final Verdict

**APPROVED — COMPLETE.**

Refresh token security invariants met (hash-only storage, cryptographically secure generation, rotation, reuse detection). Password change/reset properly revoke all sessions. 2349/2349 tests pass. Migration T59 generated correctly. No Angular/frontend changes.

## Next Recommended Action

Phase 10Auth-F-5: Google OAuth / external login foundation — `AddGoogle` to Identity, OAuth callback Angular page, Google sign-in button. Uses existing `AspNetUserLogins` schema (no new migration needed).
