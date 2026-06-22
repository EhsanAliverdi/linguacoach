# Phase 10W-5C-5 — Data Protection Key Encryption / Production Hardening

**Date:** 2026-06-23
**Sprint / Feature:** Phase 10W — Notification Platform; sub-phase 5C-5
**Status:** Complete

---

## Related Phase

Phase 10W-5C-4 introduced `PersistKeysToFileSystem` — keys are now durable across restarts. However, the key XML files were stored as plaintext on disk. Anyone with filesystem access could read the key material and decrypt stored notification channel secrets. This phase adds optional at-rest key encryption via X.509 certificate.

---

## Files Changed

### Modified files
- `src/LinguaCoach.Application/Notifications/NotificationKeyProtectionOptions.cs` — added `DataProtectionKeyMode` enum; added `KeyProtectionMode`, `CertificatePath`, `CertificatePassword`, `CertificateThumbprint` fields
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — reads `KeyProtectionMode` at registration time; calls `ProtectKeysWithCertificate` when mode is `Certificate`; throws `InvalidOperationException` clearly if cert is misconfigured
- `src/LinguaCoach.Api/appsettings.json` — added `KeyProtectionMode`, `CertificatePath`, `CertificatePassword`, `CertificateThumbprint` to `DataProtection` section
- `docker-compose.yml` — added `DP_KEY_PROTECTION_MODE`, `DP_CERTIFICATE_PATH`, `DP_CERTIFICATE_PASSWORD`, `DP_CERTIFICATE_THUMBPRINT` env var pass-throughs
- `.gitignore` — added `*.pfx`, `*.p12`, `app-data/certs/`

### New files
- `tests/LinguaCoach.UnitTests/Notifications/KeyProtectionOptionsTests.cs` — 10 tests

---

## Design Decisions

### 1. Two modes: None and Certificate

`None` is the default (preserves existing dev behavior). `Certificate` activates `ProtectKeysWithCertificate` on the ASP.NET Core Data Protection builder. Cloud KMS modes (Azure Key Vault, AWS KMS) are deferred — they require cloud-provider packages and IAM wiring that is out of scope for this phase.

### 2. Fail-fast on misconfigured certificate

If `KeyProtectionMode=Certificate` is set but no cert path or thumbprint is provided, or the cert file does not exist, startup throws `InvalidOperationException` with a clear message. This matches the JWT key validation pattern already in `Program.cs`. Silent fallback to `None` would be dangerous — the operator would believe keys are encrypted when they are not.

### 3. X509CertificateLoader instead of obsolete constructor

The `X509Certificate2(string, string?, X509KeyStorageFlags)` constructor is obsolete in .NET 9+. `X509CertificateLoader.LoadPkcs12FromFile` is used instead. No SYSLIB0057 warning.

### 4. Certificate password not logged or returned

`CertificatePassword` is read from configuration only at DI registration time and passed directly to `X509CertificateLoader`. It is never stored in a field, never logged, and never appears in any DTO or API response.

### 5. Windows thumbprint support

`CertificateThumbprint` is supported for Windows LocalMachine certificate store lookups. This allows container-less Windows deployments to use the Windows DPAPI-backed certificate store. The `OperatingSystem.IsWindows()` guard prevents the store lookup from being attempted on Linux/macOS.

### 6. ApplicationName unchanged

`ApplicationName=SpeakPath` is unchanged. Changing it would invalidate all existing encrypted data.

---

## Security Analysis

### What is protected

- Key XML files on disk: encrypted with the certificate's public key when `KeyProtectionMode=Certificate`. An attacker with filesystem access but not the private key cannot decrypt the key ring, and therefore cannot decrypt notification channel secrets in the database.

### What is not protected by this phase

- The certificate's private key, if stored in a PFX file on the same filesystem as the keys directory. The security benefit of certificate encryption depends on the private key being stored separately (e.g., HSM, Windows DPAPI-protected store, or a key vault).
- Multi-instance scenarios: all instances must share the same certificate. A shared NFS/EFS path or a cloud KMS is a better fit for multi-instance deployments (deferred).

### Secrets never exposed

- `CertificatePassword` — not logged, not returned by any API, not stored in any field after DI registration completes.
- Key XML content — never returned by any API, not logged.
- `DataProtectionSecretProtector.Unprotect` result — never leaves the Infrastructure layer.

---

## Protection Mode Summary

| Mode | Keys on disk | Key encryption | Recommended for |
|------|-------------|----------------|-----------------|
| `None` | Plaintext XML | None | Local dev / CI |
| `Certificate` (PFX file) | Encrypted XML | RSA via cert public key | Single-host production |
| `Certificate` (Windows store) | Encrypted XML | RSA via cert in Windows store | Windows server production |
| Cloud KMS (future) | Encrypted XML | Cloud-managed | Multi-instance production |

---

## Deployment Guide

### Local dev (default)

No action required. `KeyProtectionMode=None` is the default.

### Single-host Docker / VPS with certificate

1. Generate a self-signed certificate (or use an org CA):
   ```bash
   openssl req -x509 -newkey rsa:4096 -keyout dp-key.pem -out dp-cert.pem -days 3650 -nodes
   openssl pkcs12 -export -out dp.pfx -inkey dp-key.pem -in dp-cert.pem -passout pass:YourPassword
   ```
2. Store `dp.pfx` outside the repo (e.g., a secrets manager, or a host directory not in git).
3. Mount the cert into the container and set env vars:
   ```yaml
   environment:
     DP_KEY_PROTECTION_MODE: "Certificate"
     DP_CERTIFICATE_PATH: "/run/secrets/dp.pfx"
     DP_CERTIFICATE_PASSWORD: "YourPassword"   # use Docker secrets in production
   volumes:
     - /host/secrets/dp.pfx:/run/secrets/dp.pfx:ro
   ```
4. Never commit `dp.pfx` — `.gitignore` excludes `*.pfx` and `*.p12`.

### Windows server

Set `DP_CERTIFICATE_THUMBPRINT` to the thumbprint of a certificate imported into the LocalMachine store. No PFX file needed.

### Production checklist

- [ ] `KeyProtectionMode=Certificate` (not `None`)
- [ ] Certificate private key NOT on the same unprotected filesystem as the key ring
- [ ] `dp_keys` volume persisted across deploys
- [ ] `CertificatePassword` injected via Docker secrets or a secrets manager (not in plaintext env vars)
- [ ] `.pfx` / `.p12` files excluded from source control (verified via `.gitignore`)

---

## Tests Added

| Test | Coverage |
|------|----------|
| `Defaults_AreCorrect` | Default field values + SectionName |
| `BindsFromConfiguration_NoneMode` | IConfiguration → options binding (None) |
| `BindsFromConfiguration_CertificateMode` | IConfiguration → options binding (Certificate) |
| `KeyProtectionMode_ParsesCaseInsensitive` | Enum parsing case-insensitivity |
| `KeyProtectionMode_InvalidValue_DefaultsToNone_ViaTryParse` | TryParse fallback for unknown mode string |
| `AllModes_AreDefinedInEnum` | Enum completeness |
| `AddInfrastructure_NoneMode_DoesNotThrow` | None mode full DI wiring |
| `AddInfrastructure_CertificateMode_MissingCertPath_Throws` | Fail-fast: no cert path configured |
| `AddInfrastructure_CertificateMode_CertFileNotFound_Throws` | Fail-fast: cert file missing |
| (5 existing from 5C-4) | Round-trip, isolation, directory creation, key file written |

---

## Gates Passed

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS — 0 errors |
| `dotnet test --configuration Release` | PASS — 0 failed |
| `ng build --configuration production` | PASS |
| `ng test --watch=false --browsers=ChromeHeadless` | PASS |

---

## Remaining Notification TODOs

- `TODO-10W-DP-KEY-ENCRYPT` — **DONE** (this phase). Certificate mode implemented.
- `TODO-10W-DP-CLOUD-KMS` (new, deferred) — multi-instance deployments should use `PersistKeysToDbContext` or a cloud KMS for key ring sharing. Out of scope until horizontal scaling is needed.
- `TODO-10W-5D-UNIQUE-CONSTRAINT` — DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PHONE` — phone number collection and verification.
- `TODO-10W-SMS-PROVIDER` — real Twilio/other SMS sender.

---

## Final Verdict

Phase 10W-5C-5 is complete. Data Protection keys can now be encrypted at rest using an X.509 certificate (`KeyProtectionMode=Certificate`). Local dev behavior is unchanged (`KeyProtectionMode=None`). Startup fails clearly if certificate mode is misconfigured. No secrets are logged or exposed. All gates pass.

The notification platform secret encryption stack is now production-ready:
- Secrets encrypted in DB (Data Protection, 10W-5C-3)
- Key ring persisted across restarts (10W-5C-4)
- Key ring optionally encrypted at rest (10W-5C-5)
