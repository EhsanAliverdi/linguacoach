# Phase 10W-5C-3 — Runtime Notification Config Resolver + Secret Encryption

**Date:** 2026-06-23
**Sprint / Feature:** Phase 10W — Notification Platform; sub-phase 5C-3
**Status:** Complete

---

## Related Phase

Phase 10W-5C-2 (DB-backed notification channel config) created the `NotificationChannelConfigs` table and stored secrets as Base64 (a placeholder, not real encryption). This phase replaces that placeholder with real ASP.NET Core Data Protection and adds a runtime config resolver so `SmtpEmailSender` reads from the DB at send time rather than from static `IOptions`.

---

## Files Reviewed / Modified

### New files
- `src/LinguaCoach.Application/Notifications/ISecretProtector.cs`
- `src/LinguaCoach.Application/Notifications/INotificationChannelConfigResolver.cs`
- `src/LinguaCoach.Infrastructure/Notifications/DataProtectionSecretProtector.cs`
- `src/LinguaCoach.Infrastructure/Notifications/NotificationChannelConfigResolver.cs`
- `tests/LinguaCoach.UnitTests/Notifications/SecretProtectorTests.cs`

### Modified files
- `src/LinguaCoach.Infrastructure/Notifications/SmtpEmailSender.cs` — constructor changed from `IOptions<EmailOptions>` to `INotificationChannelConfigResolver`
- `src/LinguaCoach.Infrastructure/Admin/AdminNotificationHandler.cs` — added `ISecretProtector` + `INotificationChannelConfigResolver`; replaced `Convert.ToBase64String` with `_secretProtector.Protect()`; `TestEmailAsync` now uses resolver
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered Data Protection, `ISecretProtector`, `INotificationChannelConfigResolver`
- `tests/LinguaCoach.UnitTests/Notifications/EmailSenderTests.cs` — updated two `SmtpEmailSender` tests from `IOptions<EmailOptions>` constructor to fake `INotificationChannelConfigResolver`

---

## Findings

### Priority 1 — Security

**Secrets never leave Infrastructure.** `ResolvedEmailConfig` and `ResolvedSmsConfig` (which carry `PlaintextSecret`) are defined in `LinguaCoach.Application.Notifications` but consumed only within Infrastructure (`SmtpEmailSender`, `AdminNotificationHandler.TestEmailAsync`). No controller or DTO exposes plaintext. Verified by code inspection.

**Data Protection uses application-scoped purpose string.** `DataProtectionSecretProtector` uses purpose `"LinguaCoach.NotificationChannelSecret.v1"`. `SetApplicationName("LinguaCoach")` is configured in DI. Purpose isolation prevents cross-purpose decryption.

**Base64 fallback on unprotect.** For backward compatibility with Phase 10W-5C-2 stored values, `DataProtectionSecretProtector.Unprotect` catches `CryptographicException` and falls back to UTF-8 Base64 decode. This is intentional and temporary — once all stored values have been re-protected (admin re-saves config), the fallback path becomes unreachable. A TODO is added for cleanup.

**Secrets are never logged.** `SmtpEmailSender` logs only `ToAddress`, `Subject`, and `Source`. `NotificationChannelConfigResolver` logs warnings on failure but not secrets. Confirmed.

### Priority 2 — Architecture

`ISecretProtector` and `INotificationChannelConfigResolver` live in `LinguaCoach.Application.Notifications`, satisfying the Clean Architecture rule: Application owns the interface, Infrastructure owns the implementation. `SmtpEmailSender` remains in Infrastructure (correct — it uses SMTP, an external system).

`ResolvedEmailConfig` / `ResolvedSmsConfig` are sealed records. They contain `PlaintextSecret` which is safe only inside Infrastructure. The Application layer should not use these records for anything other than passing to Infrastructure implementations; this is enforced by convention (no Application handler references them). A future Architecture test could codify this boundary.

### Priority 3 — Resilience

`NotificationChannelConfigResolver` wraps both the DB query and the secret unprotect in try/catch. On any DB error it falls back to appsettings. On any unprotect error it falls back to null credentials. The SMTP sender then skips if host or from-address is missing, returning `EmailSendResult.Skipped(...)` rather than throwing. App never crashes due to misconfigured email.

### Priority 4 — Key Persistence (Known Gap)

`AddDataProtection()` with no key persistence means key ring is ephemeral in development and container restarts will invalidate stored ciphertext. This is documented in DI comments. Production deployments must configure `PersistKeysToFileSystem` or a cloud key store (Azure Key Vault, AWS Secrets Manager). A TODO is recorded below.

---

## Decisions Made

1. **Data Protection over symmetric encryption**: ASP.NET Core Data Protection is the idiomatic choice for protecting secrets at rest in ASP.NET Core. It handles key rotation, key ring management, and is audited by Microsoft.

2. **Base64 fallback**: Keeps Phase 10W-5C-2 stored values valid without a migration. Fallback is explicitly tested.

3. **SmtpEmailSender always registered as IEmailSender**: Removed the conditional `DisabledEmailSender` / `SmtpEmailSender` swap at DI time. `SmtpEmailSender` now skips gracefully at send time when config is missing or disabled. Simpler DI, fewer conditional branches.

4. **Resolver is Scoped, not Singleton**: `NotificationChannelConfigResolver` reads from a Scoped `LinguaCoachDbContext`. Registered as Scoped to match.

---

## AskUserQuestion Answers

No questions were asked during this phase.

---

## Tests Added

| Test file | Tests | Coverage |
|-----------|-------|----------|
| `EmailSenderTests.cs` (updated) | 11 | SmtpEmailSender skips when disabled, no host, no from-address |
| `SecretProtectorTests.cs` (new) | 7 | Round-trip, ciphertext != plaintext, unique per call, null input, Base64 fallback, invalid value, empty string throws |

---

## Implementation Tasks Produced

None. Phase is complete.

---

## Risks / Unresolved Questions

| Risk | Severity | Mitigation |
|------|----------|------------|
| Key ring not persisted in production | High | Must configure key persistence before production deploy. Document in deployment runbook. |
| Base64 fallback path remains active until admin re-saves config | Low | Self-resolving; acceptable during migration window. Remove fallback in a future cleanup sprint. |
| No architecture test enforcing that `ResolvedEmailConfig.PlaintextSecret` never reaches Application handlers | Low | Convention-based for now. |

---

## Gates Passed

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS |
| `dotnet test --configuration Release` (2276 tests) | PASS — 0 failed |
| `ng test --watch=false --browsers=ChromeHeadless` (1004 tests) | PASS — 0 failed |
| `ng build --configuration production` | PASS |

---

## Final Verdict

Phase 10W-5C-3 is complete. Secrets are now protected with ASP.NET Core Data Protection. The runtime resolver decouples `SmtpEmailSender` from static configuration. All gates pass. The only outstanding item is production key persistence (infrastructure concern, not a code change).

---

## Next Recommended Action

1. Update `TODOS.md` — mark `TODO-10W-5C-2-ENCRYPTION` and `TODO-10W-EMAIL-SENDER-RUNTIME` complete; add TODO for key persistence.
2. Update `docs/handoffs/current-product-state.md`.
3. Commit.
