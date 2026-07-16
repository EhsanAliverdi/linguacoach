---
status: current
lastUpdated: 2026-07-17 20:10
owner: engineering
supersedes:
supersededBy:
---

# Phase 4.8 — Security, Concurrency and Idempotency Review

**Date:** 2026-07-17
**Related sprint/feature:** Import Package pipeline hardening, following Phase 4.7 (reliable large
uploads). Not tracked in `docs/sprints/current-sprint.md` (that document is stale, still describing
Phase H9B/H10 admin-IA work) — see `docs/handoffs/current-product-state.md` for the authoritative
Import Package phase sequence.

## Goal

Harden Import Package processing so retries, crashes, concurrent workers, and unsafe archives
cannot create duplicate work or corrupt package state.

## Files reviewed / touched

**Domain**
- `src/LinguaCoach.Domain/Entities/ImportPackage.cs` — claim/lease fields + methods, archive
  checksum setter.
- `src/LinguaCoach.Domain/Entities/ImportUploadSession.cs` — concurrency stamp.

**Infrastructure**
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageProcessingService.cs` — atomic claim
  loop, lease renewal, extraction-time checksum + safety revalidation, duplicate-path rejection.
- `src/LinguaCoach.Infrastructure/ResourceImport/ZipEntrySafetyValidator.cs` — new; shared hardened
  per-entry validator (extracted from `ZipPackageInspector`).
- `src/LinguaCoach.Infrastructure/ResourceImport/ZipPackageInspector.cs` — now delegates per-entry
  checks to the shared validator.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageInspectionRunner.cs` — now computes
  and persists the whole-archive checksum at inspection time for the legacy single-shot upload
  path (previously only the Phase 4.7 chunked-upload path recorded one).
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportUploadSessionService.cs` — concurrency-safe
  `CompleteAsync`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageLimitsOptions.cs` —
  `ClaimLeaseDurationMinutes`.

**Persistence**
- `src/LinguaCoach.Persistence/Configurations/ImportPackageConfiguration.cs`,
  `ImportUploadSessionConfiguration.cs` — new columns, EF concurrency tokens.
- Migration `20260716233239_Phase_4_8_ImportPackageClaimAndConcurrency` (additive only).

**Tests**
- `tests/LinguaCoach.UnitTests/Domain/ImportPackageClaimTests.cs` — new, entity-level claim rules.
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportPackageClaimConcurrencyTests.cs` — new,
  DB-backed claim exclusivity/recovery proofs.
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportPackageProcessingServiceTests.cs` — +4 tests
  (checksum mismatch, duplicate path, retry-no-duplication, checkpoint resume).
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportUploadSessionServiceTests.cs` — +2 tests
  (expired-session rejection, concurrent-completion conflict).
- `tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs` — +4 guards.

## Package claim design

`ImportPackage` gained `ClaimedByWorkerId`, `ClaimedAtUtc`, `ClaimExpiresAtUtc`, and
`ConcurrencyStamp` (a `Guid`, real EF concurrency token via `IsConcurrencyToken()` — portable
across SQLite (test provider) and Postgres, unlike the codebase's one Postgres-only `xmin`
precedent on `LearningPath`, and unlike `ImportProfile.ConcurrencyStamp`, which is only
application-compared. `ImportPackage`'s token is DB-enforced because the actual race here is
cross-process/cross-connection, not just cross-request-on-one-connection).

`ImportPackage.Claim(workerId, now, leaseDuration)` sets the three claim fields and regenerates
`ConcurrencyStamp`. `ProcessPendingAsync`'s `TryClaimAsync` calls `Claim` then `SaveChangesAsync`;
EF includes `ConcurrencyStamp` in the `UPDATE ... WHERE` clause, so a second worker's concurrent
claim attempt against the same stale row affects zero rows and throws
`DbUpdateConcurrencyException` — caught, the row is reloaded, and that package is skipped for this
pass (not retried in a loop; the next scheduled job run picks it up if still eligible). This
replaces `[DisallowConcurrentExecution]` as the actual correctness guarantee — that Quartz
attribute remains in place but is now defense-in-depth, not the only protection, closing the gap
noted in the Phase 4.4 review ("this codebase still assumes a single active package-processing
worker at a time").

`RenewClaim` is called once between the Extract and CreateCandidates stages so a slow package does
not run past its own lease and invite a second worker to steal it mid-pass. `ReleaseClaim` runs in
a `finally` after every processing pass (success, pause, or failure) so a package does not sit out
the full lease duration before it can be reclaimed — important for `AwaitingMappingApproval`
(cost-pause) packages, which leave the claimable-status set entirely, and for a package that failed
early (e.g. no approved plan) with no real work done.

**Recovery:** `IsClaimable(now)` returns true when unclaimed or when `now > ClaimExpiresAtUtc` — a
crashed worker's claim expires and is picked up automatically; no explicit release is required for
recovery. `ClaimLeaseDurationMinutes` (default 10) is configurable via `ImportPackageLimits`.

## Checkpoint strategy

Unchanged from Phase 4 — `LastCompletedStageIndex` (`StageExtract=0`, `StageMapAndCreateCandidates
=1`) remains the durable resume marker, plus `ExtractAssetsAsync`'s own belt-and-suspenders
already-extracted-path check. This phase did not add new stages (the brief's example list —
Uploaded/Inspected/Extracted/Mapped/Transcribed/CandidatesCreated/CandidatesEnriched/Completed —
maps onto the existing `ImportPackageStatus` enum, most of whose values already exist but are
unused by the current two-stage service; expanding to finer-grained stages was judged out of scope
for this phase, which is about closing concurrency/security gaps in the existing checkpoint
mechanism, not redesigning it). What changed: the claim now wraps every stage transition, so a
resumed pass is guaranteed exclusive, not just idempotent.

## Archive revalidation changes

Two gaps closed:

1. **No archive checksum was ever recorded for the legacy single-shot upload path.**
   `ImportPackageInspectionRunner.InspectAndPersistAsync` (shared by both upload paths) now
   computes and persists a whole-archive SHA-256 at inspection time whenever the package doesn't
   already have one (the Phase 4.7 chunked-upload path already computes one while assembling
   parts). Without this, extraction-time checksum revalidation would have been a silent no-op for
   the majority of ZIP imports.
2. **Extraction trusted the stored manifest unconditionally.** `ExtractAssetsAsync` now: (a)
   recomputes the whole-archive checksum and compares it against `package.ArchiveChecksum` before
   opening the ZIP, aborting extraction on mismatch; (b) re-checks the live entry count against
   `MaxEntryCount`; (c) re-runs the full per-entry safety validator (`ZipEntrySafetyValidator`,
   extracted from `ZipPackageInspector` so both call sites share one implementation — path
   traversal, per-file size, compression ratio, nested-archive rejection, bounded zip-bomb-guarded
   checksum) against the live `ZipArchiveEntry`, not the manifest's cached `IsSuspicious` flag; (d)
   rejects a duplicate normalized path within the same extraction pass (previously, two manifest
   entries sharing a normalized path — a legitimate scenario the inspector never guarded against,
   since it only flags duplicate *checksums* — would both attempt to extract, racing to create two
   `ImportAsset` rows for the same logical path).

## Upload-session hardening

`ImportUploadSession.ConcurrencyStamp` (real EF concurrency token, same mechanism as
`ImportPackage`). `CompleteAsync` now catches `DbUpdateConcurrencyException` around its own
`SaveChangesAsync`: two concurrent completion calls for the same session (both having loaded it
before either committed — the exact race the pre-4.8 code allowed) can both pass every validation
check, but only the first's write succeeds; the second detaches its unsaved `ImportPackage`,
reloads the session, and falls back to the same idempotent already-completed path a normal
duplicate call takes. Other 4.7 lifecycle rules (expired/aborted/completed rejection, foreign-user
403, per-part replace semantics, best-effort part cleanup) were reviewed and already correct —
covered by pre-existing tests plus two new ones (`Expired_session_cannot_accept_a_part_or_complete`,
`A_stale_concurrent_write_to_a_just_completed_session_is_rejected_at_the_database_level`).

**Known limitation:** the assembled archive's storage object (`FinalStorageKey`, deterministic per
session) can still be written twice by two racing completions before the DB-level conflict is
detected — the loser's storage write is wasted but harmless (the DB row that survives points at
whichever write landed last; the checksum verification inside `CompleteAsync` still validates
against what the *winner's own* assembly produced, since each caller streams and checksums its own
assembly independently before either SaveChanges). A true fix would require a storage-level lease,
which is out of this phase's scope ("no resumable-upload redesign beyond security fixes").

## Idempotency constraints (existing, reconfirmed)

STT and AI-enrichment ledgers (`ImportSttOperation`/`ImportAiEnrichmentOperation`) already enforce
exactly-once-per-logical-operation via a unique DB index on `LogicalOperationKey`, with an
insert-and-catch-`DbUpdateException` pattern for the unique-index race. This phase added no changes
here — it's the same pattern the new package claim reuses at a different granularity (row-level
optimistic concurrency instead of insert-uniqueness, since a claim is a mutation of an existing row,
not a new logical-key row). Reviewed and confirmed unchanged/correct: cost-ceiling checks still
compare against `package.AccruedCost` (the durable running total), which cannot now be corrupted by
concurrent workers since only one worker can hold a package's claim at a time.

## Recovery behaviour

| Event | Outcome |
|---|---|
| Worker crash mid-stage | Claim lease expires after `ClaimLeaseDurationMinutes`; next job run reclaims and resumes from `LastCompletedStageIndex`. |
| Lease expiry while worker still alive (slow package) | Mitigated by `RenewClaim` between stages; a genuinely stuck pass beyond the renewed lease is reclaimable by another worker — the original worker's next `SaveChangesAsync` (e.g. on the next stage) would itself throw `DbUpdateConcurrencyException` and abort cleanly rather than silently continuing after being superseded. |
| Application restart | Same as worker crash — no in-memory state is load-bearing; `LastCompletedStageIndex` and `AccruedCost` are the only required durable state, both already persisted per-stage. |
| Temporary storage failure | Unchanged from Phase 4 — an exception during extraction/read fails the package (`MarkFailed`) with the error message surfaced via `ErrorSummary`; a retry re-attempts from the last checkpoint. |
| Provider (STT) failure | Unchanged — `MarkFailedAsync` marks the ledger row `Failed`, no cost accrued; retry via `BeginRetry` reuses the row. |
| Malformed/tampered archive | New: checksum mismatch or safety-revalidation failure at extraction now fails the package clearly instead of extracting corrupted/unsafe content. |
| Cancelled package | Unchanged — `Cancel()` moves to `Cancelled`, outside the claimable-status set. |
| Retry after partial candidate creation | Unchanged — per-asset `MarkProcessed`/checkpoint guards mean already-created candidates are not duplicated. |

Admin-visible failure details (`ImportPackage.ErrorSummary`) now include the checksum-mismatch and
entry-count-exceeded messages verbatim — actionable without exposing internals.

## Exact test counts

- Unit: **2,454** passed (was 2,436 in the prior handoff; +18 this phase — 9 domain claim tests, 4
  DB-backed claim-concurrency tests, 4 processing-service tests, 2 upload-session tests, minus 1 net
  from consolidating the originally-planned "concurrent completion" test into a more direct
  DB-conflict proof after the first version proved flaky against storage cleanup ordering).
- Integration: **1,331** passed (unchanged — no integration-level changes this phase; the new
  coverage lives in unit tests using `FakeFileStorageService`/SQLite in-memory, consistent with the
  existing Import pipeline test convention).
- Architecture: **30** passed (was 26; +4 — concurrency-token guards for both entities, claim-method
  presence guard, shared-validator-entry-point guard).

## Migration and live DB status

One additive migration, `Phase_4_8_ImportPackageClaimAndConcurrency`: adds
`claimed_by_worker_id`/`claimed_at_utc`/`claim_expires_at_utc`/`concurrency_stamp` to
`import_packages` (+ an index on `claim_expires_at_utc`) and `concurrency_stamp` to
`import_upload_sessions`. No column removed, no existing column type changed. Not applied to any
live database this session — migrations run automatically on container startup per
`docker compose up` per existing convention; no live DB was available in this environment.

## Known limitations

- Upload-session completion's storage-write race (see "Upload-session hardening" above) — DB rows
  are protected, the redundant storage write is wasted but harmless.
- `LastCompletedStageIndex` still only distinguishes two stages (Extract, Map+CreateCandidates);
  the finer `ImportPackageStatus` values (`Transcribing`/`Enriching`/`Validating`) remain declared
  but unused, as they were before this phase — expanding checkpoint granularity was judged a
  separate, larger change (would touch `MapAndCreateCandidatesAsync`'s internal STT/AI-enrichment
  loop structure) and out of this phase's "harden the existing mechanism" scope.
- `ImportPackage` has no dead-letter/max-attempt cutoff — a package that fails repeatedly stays
  `Failed` until an admin/operator intervenes (pre-existing behavior, not changed this phase; noted
  in the Phase 4.4 review as a gap and still open).
- No live MinIO or Postgres verification this session (same limitation noted in the Phase 4.7
  review) — all proofs are against SQLite in-memory + `FakeFileStorageService`.

## Final verdict

Ready to merge. All stated critical-test scenarios have direct coverage except #6 ("completed
STT/AI operations reused after package retry" — already covered by pre-existing Phase 4.4 tests,
unchanged and re-verified passing this phase) and #7 ("concurrent cost operations cannot exceed
ceiling" — covered by the pre-existing ledger unique-index race test plus this phase's
architecture-test reconfirmation that the unique index still exists; no new runtime behavior was
needed here since the existing ledger claim pattern was already correct under concurrency). #15/#16
(full regression + no-Resource-to-Exercise-path) confirmed via the full existing suite passing
unchanged.

## Next recommended action

Consider `docs/sprints/current-sprint.md` refresh — it is stale (last describes Phase H9B/H10) and
does not reflect the Import Package Phase 4.x work sequence at all; a future session should either
update it or explicitly mark it superseded by `docs/handoffs/current-product-state.md` for this
area, per the Documentation Freshness rule in `AGENTS.md`.
