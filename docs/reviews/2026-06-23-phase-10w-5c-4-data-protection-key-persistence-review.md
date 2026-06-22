# Phase 10W-5C-4 — Data Protection Key Persistence

**Date:** 2026-06-23
**Sprint / Feature:** Phase 10W — Notification Platform; sub-phase 5C-4
**Status:** Complete

---

## Related Phase

Phase 10W-5C-3 introduced ASP.NET Core Data Protection for encrypting notification channel secrets. Keys were ephemeral (in-memory) — a container restart would invalidate all stored ciphertext. This phase adds configurable key persistence via `PersistKeysToFileSystem`.

---

## Files Changed

### New files
- `src/LinguaCoach.Application/Notifications/NotificationKeyProtectionOptions.cs` — options class bound from `DataProtection` appsettings section
- `tests/LinguaCoach.UnitTests/Notifications/KeyPersistenceTests.cs` — 5 persistence unit tests

### Modified files
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — reads `DataProtection:KeysPath` + `ApplicationName` at registration time; calls `PersistKeysToFileSystem`; creates directory if missing; logs warning and continues if directory is inaccessible
- `src/LinguaCoach.Api/appsettings.json` — added `DataProtection` section with defaults
- `docker-compose.yml` — added `DataProtection__KeysPath` + `DataProtection__ApplicationName` env vars; added `dp_keys` named volume mounted to `/app/data-protection-keys`
- `.gitignore` — added `app-data/data-protection-keys/` to prevent accidental key commits

---

## Design Decisions

### 1. Options class named `NotificationKeyProtectionOptions`

`Microsoft.AspNetCore.DataProtection` already defines a `DataProtectionOptions` class. Naming our class the same caused an ambiguous reference compile error. `NotificationKeyProtectionOptions` is unambiguous and describes intent clearly.

### 2. Configuration read at registration time (not inside a factory)

`PersistKeysToFileSystem` must be called on the `IDataProtectionBuilder` returned by `AddDataProtection()`, which is called at `IServiceCollection` registration time — before the container is built. Reading `IConfiguration` directly (not via `IOptions<>`) at registration time is the correct pattern here.

### 3. Fail-safe on directory error

If the keys directory cannot be created (permissions, path error), the app logs a warning and continues with ephemeral in-memory keys. This matches the startup resilience pattern used throughout the codebase. The app does not crash — it degrades gracefully. A warning makes the issue detectable in logs.

### 4. Relative vs absolute paths

`Path.IsPathRooted()` is used to distinguish relative paths (resolved from `AppContext.BaseDirectory`) from absolute paths (used as-is). The default `./app-data/data-protection-keys` is relative — safe for local dev. Docker overrides to an absolute container path.

### 5. Docker named volume `dp_keys`

A named Docker volume survives `docker compose down` (without `-v`) and container recreation. The correct production pattern is to use a named volume or a host-bind mount to persistent storage. The volume is declared in the `volumes:` section so `docker compose up` creates it automatically.

### 6. ApplicationName stays `SpeakPath`

The application name scopes the key ring. It must match the value used when secrets were originally encrypted. This value was already `SpeakPath` in the previous phase (set via `SetApplicationName("SpeakPath")`). Changing it would break all existing stored ciphertext.

---

## Findings

### Security

Keys are written to a local filesystem directory. They are not returned by any API or logged. The `.gitignore` entry prevents the local dev key directory from being committed to source control. In Docker, the `dp_keys` named volume is local to the Docker host — no network exposure.

Keys are not encrypted at rest at the filesystem level in this phase. ASP.NET Core Data Protection supports encrypting the key ring itself (e.g., with Windows DPAPI or Azure Key Vault), but that requires OS-specific or cloud-specific configuration. This is deferred as a future TODO.

### Existing encrypted secrets

The `Unprotect` Base64 fallback introduced in 10W-5C-3 remains in place. Secrets stored by the old ephemeral provider (before this phase) can still be decrypted if the same key ring is used. After any restart with ephemeral keys, those secrets would have been unreadable anyway — so this phase only improves the situation.

### Integration tests

The integration test suite (`ApiTestFactory`) overrides DI for SQLite. The Data Protection registration in `AddInfrastructure` now reads `DataProtection:KeysPath` from `IConfiguration`. In the test environment, `configuration` is a real `IConfiguration` built from appsettings — the keys directory defaults to `./app-data/data-protection-keys` relative to the test binary. The directory is created on first use and key files are written there during tests. This is acceptable for the test environment; a future improvement could set a per-test-run temp directory via test configuration.

---

## Tests Added

| Test | Coverage |
|------|----------|
| `Secret_EncryptedWithPersistedKeys_DecryptsAfterRebuildingProvider` | Round-trip survives provider rebuild (simulates restart) |
| `Secret_EncryptedWithOneKeyDir_CannotDecryptWithDifferentKeyDir` | Key isolation between directories |
| `KeysDirectory_CreatedByDI_WhenItDoesNotExist` | Directory auto-creation |
| `NotificationKeyProtectionOptions_DefaultsAreCorrect` | Options defaults and section name |
| `KeyFile_WrittenToDirectory_AfterProtect` | Physical key file written after first protect call |

---

## Deployment Requirements

### Docker / VPS

The `dp_keys` named volume in `docker-compose.yml` satisfies persistence for single-host Docker deployments.

For multi-instance (load-balanced) deployments, all instances must share the same key ring. Options:
- Mount a shared NFS/EFS/Azure Files path
- Migrate to `PersistKeysToAzureBlobStorage` or `PersistKeysToDbContext` (deferred)

### Environment variables

| Variable | Default (docker-compose) | Purpose |
|----------|--------------------------|---------|
| `DataProtection__KeysPath` | `/app/data-protection-keys` | Directory for key XML files |
| `DataProtection__ApplicationName` | `SpeakPath` | Key ring scope — must match across all instances |

### Local dev

Default path `./app-data/data-protection-keys` is created automatically. The directory is gitignored. No action required from developers.

---

## Gates Passed

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS |
| `dotnet test --configuration Release` (2281 tests) | PASS — 0 failed |
| `ng build --configuration production` | PASS |
| `ng test --watch=false --browsers=ChromeHeadless` (1004 tests) | PASS — 0 failed |

---

## Remaining TODOs (notification platform)

- `TODO-10W-5D-UNIQUE-CONSTRAINT` — DB unique index on `(template_key, channel)` for active templates
- `TODO-10W-PHONE` — phone number collection and verification
- `TODO-10W-SMS-PROVIDER` — real Twilio/other SMS sender
- `TODO-10W-DP-KEY-ENCRYPT` (new) — encrypt the key ring itself at rest (Windows DPAPI, Azure KV, or similar) for hardened production deployments

---

## Final Verdict

Phase 10W-5C-4 is complete. Data Protection keys are now persisted to a configurable directory. Docker deployments use a named volume. The app degrades gracefully if the directory is inaccessible. No migration required. All gates pass.

---

## Next Recommended Action

Commit. The notification platform secret encryption stack is now production-ready for single-host Docker deployments.
