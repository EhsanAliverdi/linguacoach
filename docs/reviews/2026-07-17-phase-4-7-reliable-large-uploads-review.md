# Phase 4.7 — Reliable Large Uploads: Engineering Review

**Date:** 2026-07-17

**Related sprint/feature:** Phase 4.7 — Reliable Large Uploads for Import Packages. Follows
directly from the prior-art audit at
`docs/reviews/2026-07-15-phase-4-1-large-import-validation-and-gap-audit.md` (Part B "ZIP security
audit" and Part C "Large-upload reality check"), which recommended this phase at line 366-367.

## Summary

Replaced the fragile, single-request ZIP upload for Import Packages with a resumable, chunked,
memory-safe upload workflow. The prior flow (`upload-request` → presigned PUT → `confirm-upload`)
is left in place for API compatibility but is no longer called by the Import UI; a new
`ImportUploadSession` lifecycle is the path every ZIP archive now takes, working identically
whether the configured storage backend is Local or MinIO.

## Files reviewed

- `src/LinguaCoach.Api/Controllers/AdminImportPackageController.cs`
- `src/LinguaCoach.Application/Storage/IFileStorageService.cs`
- `src/LinguaCoach.Infrastructure/Storage/{Minio,Local,Fake}FileStorageService.cs`
- `src/LinguaCoach.Infrastructure/ResourceImport/{ImportPackageUploadService,ZipPackageInspector,ImportPackageLimitsOptions}.cs`
- `src/LinguaCoach.Domain/Entities/ImportPackage.cs`
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/*`
- `src/LinguaCoach.Web/src/app/core/services/admin-import-package.service.ts`
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportPackagePlanFlowTests.cs` (reused pattern, not modified)

## Files changed/added

Backend:
- `src/LinguaCoach.Domain/Enums/ImportUploadSessionStatus.cs` (new)
- `src/LinguaCoach.Domain/Entities/ImportUploadSession.cs` (new)
- `src/LinguaCoach.Domain/Entities/ImportUploadSessionPart.cs` (new)
- `src/LinguaCoach.Application/ResourceImport/ImportUploadSessionContracts.cs` (new)
- `src/LinguaCoach.Application/Storage/IFileStorageService.cs` (added optional `knownSizeBytes` hint to `SaveAsync`)
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportUploadSessionService.cs` (new — the session lifecycle)
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportUploadStreams.cs` (new — `HashingPassthroughStream`, `SequentialPartStream`)
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageInspectionRunner.cs` (new — shared inspect-and-persist step, extracted from `ImportPackageUploadService.ConfirmUploadAsync`)
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageUploadService.cs` (ConfirmUploadAsync now delegates to the shared runner)
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageLimitsOptions.cs` (added `ChunkedUploadPartSizeBytes`, `MaxUploadPartCount`, `UploadSessionExpiryHours`)
- `src/LinguaCoach.Infrastructure/Storage/MinioFileStorageService.cs` (`SaveAsync` streams directly when a size hint is supplied; `ReadAsync` now streams to a temp file instead of a full in-memory `MemoryStream`)
- `src/LinguaCoach.Infrastructure/Storage/LocalFileStorageService.cs` (`GenerateUploadUrlAsync` now throws `NotSupportedException` instead of returning a silently-broken `local://` marker)
- `src/LinguaCoach.Infrastructure/Storage/FakeFileStorageService.cs` (records whether the last `SaveAsync` received a size hint, for test assertions)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` (registers `IImportUploadSessionService`)
- `src/LinguaCoach.Persistence/Configurations/ImportUploadSessionConfiguration.cs` (new)
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` (new `DbSet`s)
- `src/LinguaCoach.Persistence/Migrations/20260716220025_Phase_4_7_ImportUploadSessions.cs` (new migration)
- `src/LinguaCoach.Api/Controllers/AdminImportPackageController.cs` (5 new endpoints under `upload-sessions`)
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportUploadSessionServiceTests.cs` (new — 14 tests)

Frontend:
- `src/LinguaCoach.Web/src/app/core/models/admin-import-package.models.ts` (new session DTOs)
- `src/LinguaCoach.Web/src/app/core/services/admin-import-package.service.ts` (new session methods)
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.ts` (rewrote `submitZip` around the session lifecycle; fixed a pre-existing `computed()` memoization bug — see Findings)
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.html` (progress bar + Cancel button)
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.spec.ts` (3 new Jasmine tests for the chunked flow)
- `src/LinguaCoach.Web/e2e/import-package-chunked-upload.spec.ts` (new Playwright spec, 2 tests)

## Findings grouped by priority

### Critical

- **Broken `local://` presigned-PUT path (confirmed pre-existing, audit item 3).** No HTTP endpoint
  ever existed to receive bytes for MinIO's `GenerateUploadUrlAsync`'s Local-storage fallback.
  Resolved by design: the new chunked session flow does not use presigned URLs at all — every part
  is proxied through the API and written via `IFileStorageService.SaveAsync`, which both Local and
  MinIO already implement correctly. `LocalFileStorageService.GenerateUploadUrlAsync` now throws
  `NotSupportedException` with an actionable message instead of returning the dead-end marker, so
  nothing can silently depend on it going forward.

- **Full in-memory buffering of large objects (audit item 5).** `MinioFileStorageService.ReadAsync`
  copied the entire object into a `MemoryStream` before returning it — a 2GB archive meant a 2GB
  allocation. Fixed: it now streams MinIO's callback data into a temp file and returns a
  `FileOptions.DeleteOnClose` `FileStream` — same seekability `ZipArchive` needs, without holding
  the object fully in memory. `MinioFileStorageService.SaveAsync` similarly used to buffer into a
  `MemoryStream` to learn the object's length for `WithObjectSize`; it now accepts an optional
  `knownSizeBytes` hint and streams directly when supplied (every new large-upload call site
  supplies it — a single 32MB part, or the exact declared archive size for the final assembly).

- **No real multipart/resumable upload existed (audit item 7).** Added `ImportUploadSession` +
  `ImportUploadSessionPart` and the full session lifecycle described below.

### High

- **No ownership check on the old upload endpoints (audit item 10).** The new session endpoints
  compare the requesting user's id against `ImportUploadSession.CreatedByUserId` on every
  session-scoped call (`GetStatusAsync`, `UploadPartAsync`, `CompleteAsync`, `AbortAsync`) and throw
  `ImportUploadSessionForbiddenException` → HTTP 403 on mismatch. The older
  `upload-request`/`confirm-upload` pair was left as-is (out of scope — it's no longer reachable
  from the UI, and widening its own authz behavior risked destabilizing
  `ImportPackagePlanFlowTests`, which exercises it directly).

- **Angular `canSubmit`/`selectedFilesLabel` computed() memoization bug (found during this phase,
  not previously known).** These were Angular `computed()` signals reading plain (non-signal)
  instance fields (`selectedFiles: File[]`, `pastedText: string`, `selectedSourceId: string`). A
  `computed()` with zero signal dependencies computes once and never invalidates — confirmed via a
  real-browser Playwright reproduction that the "Submit for review" button stayed disabled forever
  after the *first* render pass, regardless of later file selection or pasted text, in the actual
  running app (not just unit tests, which happened to only call `canSubmit()` once per component
  instance and so never observed the staleness). Fixed by converting both to plain methods —
  already invoked as functions in the template, so no template changes were needed. This was
  directly blocking the Phase 4.7 e2e coverage and is a real, user-facing defect independent of
  this phase's scope, so it was fixed here rather than deferred.

### Medium

- **No true S3-native multipart upload.** The installed Minio .NET SDK (6.0.4, confirmed via
  reflection over the assembly — no `InitiateMultipartUpload`/`UploadPart`/`CompleteMultipartUpload`
  public surface exists at all) does not expose the low-level S3 multipart primitives a
  client-direct-to-storage per-part presigned-URL design would need. The phase brief explicitly
  sanctioned proxying bounded chunks through the API as an alternative, which was chosen — see
  Decisions below.

### Low

- Per-part and whole-file SHA-256 checksums are optional (client may omit `declaredChecksumSha256`),
  matching "checksum where available" in the brief rather than a hard requirement.
- Orphaned part-storage cleanup (after a successful `Complete`, or after `Abort`) is best-effort —
  a `DeleteAsync` failure is swallowed rather than failing the already-successful operation. A
  future pass could add a sweep job for any part objects left behind by a crash between upload and
  cleanup (rare — parts live under a session-scoped key prefix so this is at worst a small amount of
  orphaned storage, not a correctness issue).

## Decisions made

1. **Storage architecture: API-proxied bounded chunks, not client→storage direct multipart.**
   Chosen because (a) the installed Minio SDK has no public multipart API surface to build the
   alternative on, and (b) proxying through the API works identically for Local and MinIO — no
   separate "local chunked-upload equivalent" had to be built as a parallel implementation; the
   session service only ever calls `IFileStorageService.SaveAsync`/`ReadAsync`/`DeleteAsync`, which
   both backends (and the test fake) already implement. This resolves the "pick one, don't leave a
   broken fallback" requirement by construction — there is only one implementation, and it works on
   both backends.

2. **Local storage is not "MinIO-only for uploads."** Because chunk receipt never depends on a
   presigned PUT, Local storage genuinely supports the full resumable-upload flow now, not just
   MinIO. The previously-broken `local://` presigned-PUT marker is retired (throws instead of lying)
   since nothing needs it anymore.

3. **Kept `ImportPackage` as the eventual artifact; `ImportUploadSession` as the upload-in-progress
   state.** A session transitions into exactly one package on successful `Complete` — the session
   row stores `ImportPackageId` once set, and a repeated `Complete` call is idempotent (checked
   first, before any assembly work), which structurally prevents duplicate `ImportPackage` rows on
   retried completion.

4. **Part size: 32MB, configurable via `ImportPackageLimitsOptions.ChunkedUploadPartSizeBytes`.**
   Small enough to bound memory well below any concerning threshold per part, and small enough that
   retrying one failed part is fast. `MaxUploadPartCount` (128) bounds the number of part rows a
   session may create.

5. **Upload ceiling unchanged: 2GB (`MaxCompressedSizeBytes`), documented as the only proven limit.**
   This phase does not raise it — see "Proven upload limit" below.

6. **Existing `upload-request`/`confirm-upload` endpoints and `IImportPackageUploadService` were
   left in place, not deleted.** `ImportPackagePlanFlowTests` exercises them directly and they still
   represent a valid (if now Angular-unused) API surface; deleting them was not necessary to satisfy
   "don't leave a non-working fallback" once the Local presigned-PUT dead end itself was fixed.

## Implementation tasks produced

All required backend/frontend/test work for this phase was completed in this pass; no follow-up
implementation tasks were generated beyond the "Risks / unresolved questions" below.

## Risks / unresolved questions

- **No real MinIO instance was available to exercise `MinioFileStorageService.ReadAsync`'s new
  temp-file-backed path or `SaveAsync`'s size-hinted path against a live server.** Verified instead
  via: (a) unit tests against `FakeFileStorageService` proving the session service always supplies
  a size hint for the final assembled archive (a proxy for "does not force a full-buffer fallback"),
  and (b) direct reasoning/reflection-confirmed removal of the `MemoryStream` allocation in the
  MinIO code path. A live-MinIO smoke test (upload a multi-hundred-MB archive, confirm process
  memory stays flat) is recommended before this ships to an environment where MinIO is the active
  backend.
- **Part upload is sequential in the Angular client** (one `PUT` at a time, not parallel). This was
  a deliberate simplicity choice for this phase — parallelizing part uploads would improve wall-clock
  time for very large archives but adds complexity (concurrent progress accounting, part-order
  independence, which the server already supports since parts can arrive in any order and are
  validated for contiguity only at `Complete`). Deferred, not required by the brief.
- **Orphaned session/part cleanup on expiry is not proactively swept** — an expired session simply
  can no longer accept parts or complete; its rows and any uploaded part objects remain until an
  explicit `Abort` or a future cleanup job. Not required by this phase's brief but worth a follow-up
  background job if abandoned uploads become common in practice.

## Proven upload limit

**2GB remains the documented, tested ceiling** (`ImportPackageLimitsOptions.MaxCompressedSizeBytes`,
unchanged this phase). This phase did not attempt to raise it because doing so credibly requires
proving memory-safety, reverse-proxy timeout behavior, and storage-backend behavior together at the
higher size — none of which were re-verified against a higher number in this pass. What *is* newly
proven this phase: uploads no longer require the whole archive to transit as a single HTTP request
or be held fully in memory by the API process at any point (part size is bounded at 32MB by
default, and the final assembly streams through `SequentialPartStream` without ever holding more
than one part's bytes in flight). Combined with a real live-MinIO verification (see Risks above),
raising the ceiling would be a reasonable follow-up phase, but is not claimed as proven here.

## Final verdict

**Approved / shipped this phase.** Backend fully implemented and tested (2436 unit / 1331
integration / 26 architecture tests passing, including 14 new upload-session tests). Frontend
implemented with real byte-level progress, resumable/retryable/cancellable upload, and a fixed
pre-existing UI defect that was blocking the feature from working in a real browser at all;
verified via 2 new Playwright tests (isolated run) and `tsc --noEmit`/production build both showing
zero new errors introduced. Full Angular unit-test run (`ng test`) could not execute — see "Known
limitations" below; this is a pre-existing, unrelated repository state, not something this phase
introduced or could safely fix within scope.

## Known limitations / deferred work

- Angular's `npm test -- --watch=false --browsers=ChromeHeadless` currently fails to even start
  (Karma "Found 1 load error") because of **pre-existing** TypeScript compile errors in unrelated
  spec/test-helper files (`activity-feedback-page.component.spec.ts`,
  `activity-lesson-submission.component.spec.ts`, `activity-lesson-vocab.component.spec.ts`,
  `presenters/test-helpers.ts`, `practice-gym.component.spec.ts`, plus two e2e specs) — all missing
  a `feedbackPolicy`/`moduleSuggestions` field added to a DTO by an earlier, unrelated phase.
  Confirmed via `git stash` that these errors exist identically with none of this phase's frontend
  changes applied. Out of scope to fix here (not touched by Phase 4.7, would be uncontrolled scope
  creep per this repo's scope-discipline rule) — flagged for a separate fix.
- Sequential (not parallel) per-part upload in the Angular client (see Risks above).
- No live-MinIO smoke test of the new streaming paths (see Risks above).
- No proactive sweep job for expired/abandoned sessions' part objects.

## Next recommended action

1. Run a live-MinIO smoke test uploading a multi-hundred-MB archive and confirm API process memory
   stays flat, before relying on this in a MinIO-backed environment.
2. Separately fix the pre-existing `feedbackPolicy`/`moduleSuggestions` TypeScript errors blocking
   `ng test` so the full Angular unit suite can run again.
3. Consider a follow-up phase to parallelize part uploads and/or raise the 2GB ceiling once the
   live-MinIO verification above is done.
