# Phase 4.1 — Large-Scale Import Implementation Validation and Gap Audit

**Date:** 2026-07-15
**Related sprint/feature:** Phase 4 — Large-Scale AI Import Packages (`docs/reviews/2026-07-15-phase-4-import-execution-plan-progress.md`)
**Audited commit (HEAD):** `4dea4980` ("feat: implement large-scale AI import packages with mandatory Import Execution Plan gate")
**Audit type:** Read-only implementation validation and gap audit. No implementation code, migrations, tests, or other documentation were modified. Nothing was pushed or deployed.
**Method:** Direct code inspection (not delegated) plus 6 parallel read-only research passes, each independently tracing one pipeline area with file:line evidence; findings synthesized and cross-checked against the actual test run and build output below. The Phase 4 implementation report was treated as a claim to verify, not as ground truth.

---

## Executive summary

Phase 4 delivered a real, working, test-covered skeleton for a package-based import pipeline: ZIP manifest inspection with genuine security checks, a plan-generation/approval state machine, a Quartz-driven background processor, real OpenAI Whisper STT, and a working Angular upload→plan→approve UI. All claimed backend test counts are accurate and independently reproduced (2320 unit + 1325 integration + 8 architecture, 0 failures). The production Angular build is clean.

However, several of the report's headline claims do not hold up under direct verification, and one finding is architecturally significant:

1. **The mandatory Import Execution Plan gate does not apply to every import.** The pre-existing single-file/paste pipeline (`AdminResourceImportController`, `AdminContentImportController`, `AdminResourceCandidateController`) has no `ImportProfile` concept anywhere in its call graph. It can run AI column-mapping, AI candidate analysis, and publish to the live Resource Bank with zero plan, zero cost ceiling, zero approval — and it remains the default, more prominent option on the same Angular page as the new gated ZIP flow. This directly contradicts the addendum's explicit rule ("no import, large or small, may begin material AI/STT/TTS/background processing without an approved plan").
2. **The approved plan's mapping/classification rules are not applied by execution.** `ImportPackageProcessingService` never reads `ImportProfile.ProfileJson`; it independently re-derives its own file-type routing. The plan functions as a cost/status gate and a UI artifact, not an executed contract — undercutting the "approved mapping ruleset drives processing" framing.
3. **"10GB package support" is false as stated.** The hard-coded application-level ceiling is 2GB compressed (`MaxCompressedSizeBytes = 2_000_000_000`), enforced before storage or transport is even involved. No appsettings override exists. A 10GB ZIP is rejected today under any configuration.
4. **"Never buffered" ZIP streaming is false for the only functional storage backend.** `MinioFileStorageService.ReadAsync` fully downloads the object into a `MemoryStream` before the inspector ever sees it — the inspector's own code comments admit this.
5. **Local-disk package upload is not merely "not supporting direct browser upload" — it is non-functional end-to-end.** `GenerateUploadUrlAsync` returns a fake `local://` string with no receiving endpoint, and the Angular client PUTs to it unconditionally.
6. **AI "sample content analysis" is metadata-only, confirmed** (this specific admission in the report is accurate) — but the "second bounded sampling round" mechanic is functionally inert: no new evidence is gathered between rounds, and the model's own request-more-samples field is parsed by nothing.
7. **Cost-ceiling enforcement is a coarse, non-durable guard, not a reliable budget control.** Running cost is a local variable, not persisted or cumulative across a crash/retry; a missing pricing row silently zeroes AI-cost checks with no warning; STT can be re-billed on crash-and-retry between the provider call and the DB save.
8. **7-typed-candidate-schema claim is false as stated.** All content is `NormalizedJson`-by-convention with zero typed DTOs for any of the (actually 8, not 7 — there is no distinct `ReadingReference` type; the enum has `ActivityTemplateCandidate` instead) `ResourceCandidateType` values. The package job itself only ever produces `ListeningPassage` candidates directly; every other type can only appear indirectly via the reused old CSV/JSON pipeline.
9. **TTS has no execution path anywhere in the import pipeline** (confirmed, matches the report's own framing — TTS cost is always $0 by construction, never invoked).
10. **No Playwright E2E coverage exists** (confirmed by direct listing: 0 of 26 e2e specs mention import/package). **All new Phase-4 tests are unit tests** (SQLite in-memory + fakes) — the "1325 integration tests" figure did not grow from this phase at all; zero Phase-4 test files exist under `IntegrationTests`.
11. One deep-link notification route is dead (`/admin/content/import/runs?packageId=...` doesn't match the Angular route, which requires a path segment — falls through to a wildcard redirect).
12. A genuine approval race condition exists: `ApproveAsync` has no optimistic-concurrency token, so two concurrent approve calls can both "succeed," the second silently clobbering the first's audit trail.

**Overall verdict: Substantially Complete foundation, Partial/Misleading on several headline capability claims.** The plan/approval/cost-gate skeleton, ZIP security core algorithm, and real STT wiring are genuine engineering work and test-covered. But "10GB support," "typed 7-resource schemas," "AI content-aware sampling," and "every import is plan-gated" are all overstated relative to what the code does. The system is not production-ready for its stated scale/safety claims without a follow-up phase.

---

## Files and systems inspected

Backend: `ZipPackageInspector.cs`, `ImportPackageUploadService.cs`, `IFileStorageService.cs` + Minio/Local/Fake implementations, `ImportPackageLimitsOptions.cs`, `ImportExecutionPlanGenerationService.cs`, `ImportExecutionPlanContracts.cs`, `ImportProfile.cs` (domain), `ImportProfileStatus.cs`, `ImportExecutionPlanApprovalService.cs`, `ImportCostEstimationOptions.cs`, `ImportPackageProcessingService.cs`, `ImportPackageProcessingJob.cs`, `QuartzConfiguration.cs`, `OpenAiSpeechToTextService.cs`, `ISpeechToTextService.cs`/`FakeSpeechToTextService.cs`, `DependencyInjection.cs` (Infrastructure), `ResourceCandidate.cs` (domain), `ResourceCandidateType.cs`, `ResourceCandidatePublishService.cs`, `ImportCandidateAssetLink.cs`, `ImportAssetRole.cs`, `LessonResourceLookup.cs`, `AdminImportPackageController.cs`, `AdminResourceImportController.cs`, `AdminContentImportController.cs`, `AdminResourceCandidateController.cs`, `NotificationService.cs`, `DefaultAiSeeder.cs` (prompt content), `AiPricingResolver.cs`, `ResourceImportService.cs`. All relevant unit test files (`ZipPackageInspectorTests.cs`, `ImportProfileTests.cs`, `ImportPackagePlanFlowTests.cs`, `ImportProcessingModeDecisionServiceTests.cs`, `ImportPackageProcessingServiceTests.cs`).

Frontend: `admin-import-package.models.ts`, `admin-import-package.service.ts`, `admin-import-package-plan.component.ts/html`, `admin-content-import.component.ts/html`, `admin-import-run-candidates.component.ts/html`, `app.routes.ts`, `admin-resource-bank-detail.component.html`.

---

## Actual pipeline diagram (as implemented, not as intended)

```
Angular "Large package (ZIP)" card
    → POST upload-request (AdminImportPackageController)
    → PUT directly to storage (MinIO presigned URL — REAL; Local — BROKEN, fake URL, no receiver)
    → POST confirm-upload → ZipPackageInspector.InspectAsync (MinIO: full buffer into MemoryStream first)
        → manifest persisted (ImportPackage.ManifestJson), checksum/traversal/ratio/nested checks applied
    → POST plan (ImportExecutionPlanGenerationService)
        → BuildDeterministicGroups (folder/extension clustering — does the real classification work)
        → SelectRepresentativeSamples (first/middle/last/largest/smallest, metadata only)
        → RunBoundedAiReviewAsync (AI sees filenames/paths/sizes/extensions ONLY — commentary/confidence layer on top of deterministic groups, not content understanding)
        → BuildCostEstimateAsync (file-count proxy for candidates, flat 5-min/file for audio, TTS always $0)
        → ImportProfile persisted as AwaitingApproval, notification queued (real)
    → Angular plan page: admin reviews (read-only; cannot edit mappings/samples), approves with a cost ceiling
        → ImportExecutionPlanApprovalService.ApproveAsync (NO optimistic-concurrency guard — race possible)
        → package → Queued
    → ImportPackageProcessingJob (Quartz, every 2 min, [DisallowConcurrentExecution] — NOT cross-instance safe: in-memory/unclustered-persistent JobStore, no UseClustering())
        → ProcessOnePackageAsync: plain read-then-write package claim (no transaction/lock — race possible across workers)
        → Extract stage: re-downloads archive, extracts entries the OLD manifest marked non-suspicious — NO re-validation of security rules, NO bounded-read guard on extraction (unlike inspection). Idempotent by RelativePath-already-extracted check (genuinely correct).
        → Map/CreateCandidates stage: does NOT read plan.ProfileJson; independently re-derives extension-based routing
            → CSV/JSON → existing IResourceImportService.ImportAsync (reused pipeline; NOT individually asset-linked; re-run duplication risk on crash between ImportAsync's internal SaveChanges and the outer processed-flag save)
            → Audio+transcript pairs → real Whisper STT if missing transcript → ListeningPassage candidate + ImportCandidateAssetLink (only candidate type the package job produces directly)
        → Cost ceiling checked before each billable call, but against coarse estimates; running cost is a local variable, not persisted/cumulative across retries; missing pricing row silently zeroes AI cost checks
    → Same Phase 3 candidate review page (edit/approve/reject/skip/publish) — has ZERO awareness of ImportAsset/ImportCandidateAssetLink; not touched by this commit
    → ResourceCandidatePublishService (pre-existing, unmodified) — publishes Listening audio fields correctly
    → ResourceBankItem — no admin UI audio preview exists at all (Resource Bank detail page has zero audio markup)
    → LessonResourceLookup — for Listening, reads Title/Transcript ONLY; AudioStorageKey/AudioContentType/asset links are never read downstream — provenance is persisted but functionally dead weight past publish

PARALLEL, UNGATED PATH (still fully live, same Angular page, default/first option):
Angular "Add content" (paste/upload) card
    → AdminResourceImportController / AdminContentImportController (api/admin/resource-import-runs, api/admin/content-imports)
    → IResourceImportService.ImportAsync directly — NO ImportProfile involved anywhere
    → AI column-mapping (Phase K1) and AI candidate batch analysis run unconditionally, no plan/ceiling
    → AdminResourceCandidateController → Approve/Publish/ApproveAndPublish — reaches the live Resource Bank
    → Zero code path in this entire chain references ImportProfile — the plan gate is structurally absent, not just unused
```

---

## ZIP / archive security audit (Part B)

Core algorithm (`ZipPackageInspector.cs`) is genuinely well-designed for what it covers:
- **Path traversal**: checks `Path.IsPathRooted`, drive-letter `:`, and `..` segments on both `/` and `\` (lines 217-224). Gap: no case-insensitive collision detection, no symlink handling (`ZipArchiveEntry` doesn't expose Unix symlink bits — a symlink entry is silently treated as a regular file).
- **Zip-bomb protection**: entry-count checked before any decompression; per-entry size/ratio checked from central-directory metadata before `entry.Open()`; total expanded-size budget checked mid-loop and halts further decompression once exceeded — correctly enforced, not merely per-entry.
- **Declared-vs-actual-size guard**: real. `ComputeBoundedChecksum` throws the moment bytes read exceed the entry's declared `Length`, defending against a lying central directory within a single entry's stream.
- **Nested archives**: extension-based detection only (`.zip/.rar/.7z/.tar/.gz/.tgz/.bz2`), depth 0 = always rejected. Renaming `evil.zip` → `evil.dat` bypasses detection entirely (then just treated as an unknown binary, still checksummed/extracted).
- **Encrypted/unsupported compression**: `entry.Open()` throws, caught and flagged "likely encrypted or corrupt" — no crash, no silent acceptance, but no distinction between the two causes.
- **Streaming claim is false for MinIO** (the only functional backend — see below): `MinioFileStorageService.ReadAsync` fully buffers the object into a `MemoryStream` before the inspector runs; the code's own comment admits this ("MinIO reads are already fully-buffered MemoryStreams").
- **TOCTOU / re-validation gap, real**: `ExtractAssetsAsync` re-downloads the archive but iterates the *persisted manifest's* non-suspicious entries with **no re-run of traversal/ratio/size checks and no bounded-read guard** on the extraction copy (unlike the inspection-time checksum read). No archive-level checksum is re-verified before extraction. An object swapped in storage between confirm-upload and the (asynchronous, up-to-2-minutes-later) processing job would be extracted blind, using stale manifest metadata and no protection.

**Classification: partial / safe foundation with gaps.** Not unsafe (2GB hard cap + bounded checksum read bound the worst case at inspection time), not production-ready (buffering contradicts the streaming claim; extraction stage has no re-validation).

---

## Large-upload reality check (Part C)

- **10GB is false.** `ImportPackageLimitsOptions.MaxCompressedSizeBytes = 2_000_000_000` (2GB) is enforced in `ImportPackageUploadService.RequestUploadAsync` before storage/transport is touched. No `ImportPackageLimits` appsettings override exists anywhere (grep returned zero matches) — the 2GB C# default is what's actually in force. `MaxExpandedSizeBytes = 8_000_000_000` (8GB decompressed) is the only figure anywhere near "10GB," and it isn't the upload limit.
- MinIO presigned PUT is real (`PresignedPutObjectAsync`/`PresignedPutObjectArgs`, confirmed present in the pinned `Minio` v6.0.4 SDK) and does bypass Kestrel/IIS body-size limits correctly.
- **Local storage upload is non-functional, not merely unsupported for direct-browser upload.** `LocalFileStorageService.GenerateUploadUrlAsync` returns a fake `local://{key}` string (not an HTTP URL); the Angular client (`admin-import-package.service.ts`) does `this.http.put(uploadUrl, file, ...)` unconditionally against it, which fails; and no authenticated API upload endpoint exists to receive package bytes on local storage at all (grep of the controller found only `upload-request`/`confirm-upload`, no multipart POST). The report's own doc comment ("routes back through an authenticated API upload endpoint") describes an endpoint that does not exist.
- Upload is a **single PUT**, no multipart/resumable/chunked implementation anywhere. A connection failure at any point requires a full restart from byte zero. No cancel button exists in the UI, and no real upload-progress reporting exists (`reportProgress`/`HttpEventType` — zero matches; the UI shows a static "Uploading…" label, not byte progress).

---

## Sampling / AI structure understanding audit (Part D)

Confirmed accurate (the report's most honest admission): the AI request payload (`ImportExecutionPlanGenerationService.RunBoundedAiReviewAsync`) sends exactly `fileCount`, `distinctExtensions`, a JSON of detected-group descriptions, and `sampleMetadataJson` = `{RelativePath, FileExtension, UncompressedSizeBytes}` per sample — **no file bytes, rows, or text ever reach the model.** The prompt template itself states "content is NOT included."

- All substantive classification (audio+transcript ⇒ Listening, etc.) happens in `BuildDeterministicGroups` **before** any AI call; the AI call only optionally overwrites `ProposedResourceType`/`Description`/`Confidence` and appends free-text notes. If the AI call throws, generation proceeds unmodified on the deterministic groups.
- The "bounded, escalating second sampling round" is reachable in code but functionally inert: the same evidence is resent on round 2 (no new samples gathered), and the model's own `requestedAdditionalSampleHint` field is parsed by nothing in `TryParsePlanReview`.
- **Classification: deterministic manifest classification with AI commentary layered on top — not AI content understanding, and not really "content-aware" sampling in any operative sense.**

---

## Import Execution Plan audit (Part E)

- **Approved-plan immutability: genuine.** All mutating fields are `private set`; the only post-Approve mutation path (`ApproveRevisedCeilingAndResume`) is gated to `PausedForCostApproval` and only touches ceiling/status/reason, never the mapping/estimate JSON. `ReplaceProfileJson` throws unless `Status == Draft`.
- **Versioning/supersession: genuine.** Regeneration correctly supersedes the prior live (Draft/AwaitingApproval/Approved) plan and increments version. Caveat: `Supersede()` throws for an already-`Executing` plan rather than degrading gracefully — an untested edge case.
- **Approval race condition, confirmed real.** No `[Timestamp]`/rowversion column exists on `ImportProfile`. `ApproveAsync` does a plain read → in-memory status check → `SaveChangesAsync`, with no concurrency token — two concurrent approve calls can both pass the guard and both "succeed," the later commit silently overwriting the former's approver/ceiling.
- **Approver identity stored, best-effort** (`Guid?`, can be null if the claim doesn't parse). Cost ceiling immutably stored via domain methods only. A ceiling *revision* is not attributed to any admin identity (the revise command carries no `ApprovedByUserId`).
- **Execution does not consume the approved plan's mapping rules — the most significant finding in this section.** `ImportPackageProcessingService` reads only `plan.Status`, `plan.ApprovedCostCeiling`, and the lifecycle transition methods. It never reads `plan.ProfileJson`; it maintains its own independent extension-based routing tables. The plan is a cost/status gate plus a UI artifact, not an applied execution contract, despite being framed as "the ImportProfile IS the mapping ruleset."
- **Direct mode is still plan-gated, confirmed correctly** — no code path anywhere skips plan generation/approval for `ImportProcessingMode.Direct`; Direct mode only skips the AI-enrichment cost step during mapping.

---

## Cost estimation accuracy and enforcement audit (Part F)

- Candidate count = **file count**, not row/record count, for structured files. A 50,000-row CSV counts as 1 candidate for cost purposes.
- Audio duration = **flat 5 minutes/file constant**, duplicated verbatim at both plan time and execution-time enforcement. A 100×2-second-clip package is overestimated ~150×; a 5×2-hour-recording package is underestimated ~24×.
- **TTS cost is always $0 by construction** (`ExpectedTtsCandidates: 0` hardcoded); no TTS call exists anywhere in the import pipeline, confirmed by exhaustive grep.
- Cost is checked **before** each billable call (real), but against the same flat/coarse assumptions used at plan time — not the real payload about to be billed, so a batch or file that diverges from the assumption isn't caught by the pre-check.
- **A missing AI pricing row silently zeroes the AI-cost check entirely** — `pricing?.InputPer1KTokens ?? 0m` — with no warning or log line. A misconfigured/missing pricing override for the assumed model makes AI-enrichment cost invisible to the ceiling.
- **Running cost is a local variable, not persisted or cumulative across retries.** A crash-and-retry resets the ceiling's memory of prior spend to zero. STT specifically can be re-billed: if the process crashes after the provider call succeeds but before the candidate/cost is saved, retry re-selects the same asset and calls STT again.
- **Classification: useful guard with coarse estimates, shading into partial/misleading on the specific "enforceable budget" framing** — the pause-on-projected-overrun mechanism genuinely fires pre-call, but durability, real-usage feedback, and the silent-zero-pricing fallback are real gaps.

---

## Background job / checkpointing / idempotency audit (Part G)

- **Extraction-stage idempotency is genuinely correct**: skip check is keyed on `RelativePath` against already-persisted `ImportAsset` rows, verified to prevent duplicate storage copies on retry.
- **Mapping-stage (CSV/JSON) idempotency is not provably safe.** `IResourceImportService.ImportAsync` performs its own internal `SaveChangesAsync` calls (creating `ResourceCandidate` rows) independently of, and before, the outer service's `asset.MarkProcessed()` flag is persisted. A crash between those two points causes a retry to re-run the same file's import. Dedup exists only within a single run (`seenHashesInRun`), not cross-run against previously created candidates.
- **Cross-instance concurrency is not actually protected.** `[DisallowConcurrentExecution]` only prevents overlap within one scheduler instance. `QuartzConfiguration.cs` has no `UseClustering()` call anywhere; when a connection string is present it uses a persistent-but-unclustered `AdoJobStore`, which Quartz.NET does not guarantee safe for multi-instance execution. Independently, `ImportPackage.MoveToStatus` is an unconditional setter with no concurrency token — two workers can read the same `Queued` package and both proceed to process it.
- **Classification: partial.** Solid for its narrowest actual claim (file-extraction dedup); does not hold for candidate-creation idempotency, cost-accrual durability, or cross-instance safety.

---

## STT and TTS audit (Part H)

- **STT is real**: `OpenAiSpeechToTextService` calls `AudioClient.TranscribeAudioAsync` against the real, pinned OpenAI SDK (v2.2.0). DI correctly falls back to `FakeSpeechToTextService` only when no API key is configured, matching the codebase's established pattern.
- Gaps: whole file buffered into memory before transcription (no streaming), flat 25MB hard cutoff with **no chunking for longer/larger audio**, and `MaxDurationSeconds` exists on the options contract but is never read anywhere in the implementation.
- **TTS: missing from the import pipeline entirely.** TTS providers exist elsewhere in the app (speaking practice, placement audio) but zero references exist inside `ResourceImport/`. TTS cost is permanently $0 by construction; no TTS call is ever made by any import code path.
- **Classification: STT substantially complete (real, correctly gated, but no chunking/duration handling). TTS missing** from this pipeline (present, unrelated, elsewhere in the app).

---

## Typed Resource Candidate matrix (Part I)

`ResourceCandidateType` actually has 8 values, not the claimed 7 — there is no distinct `ReadingReference`; the enum has `ActivityTemplateCandidate` instead. Every single type stores content as generic `NormalizedJson`/`CanonicalText` strings — there are zero typed DTOs/schema classes anywhere in the domain or application layer. Task #31 ("done — pre-existing infra covers this") did not add any new schema code; it rests entirely on the pre-existing generic-JSON convention.

| Type | Typed contract? | Package job produces it? |
|---|---|---|
| VocabularyEntry | No — convention only | Only indirectly, via reused old CSV/JSON pipeline |
| GrammarProfileEntry | No | Only indirectly |
| ReadingPassage | No | Only indirectly |
| ActivityTemplateCandidate | No | Only indirectly |
| WritingPrompt | No | Only indirectly |
| ListeningPassage | No (only `AudioStorageKey`/`AudioContentType` are real columns; JSON body still a raw dictionary) | **Yes — the only type the package job creates directly** |
| SpeakingPrompt | No | Only indirectly |

No wiring exists anywhere for package-native Speaking-with-images, Reading-from-PDF/Word, or Vocabulary-with-pronunciation-audio.

---

## Package-format coverage matrix (Part J)

| Format | Classification | Evidence |
|---|---|---|
| TXT | Mapped (transcript companion only) | Never a standalone candidate source |
| CSV | Candidate-producing | via reused `IResourceImportService.ImportAsync` |
| JSON | Candidate-producing | same |
| JSONL | Inventoried/extracted only | Tagged `StructuredData` but absent from the candidate-creation extension set |
| Excel (.xlsx) | Unsupported | Absent from both the inspector's MIME table and the media-type detector |
| PDF | Inventoried only | Has a MIME entry but no downstream handling of any kind |
| Word (.docx) | Unsupported | Absent everywhere |
| Audio | Candidate-producing (Listening only) | |
| Image | Extracted/inventoried only | No image-consuming code exists in candidate creation |
| Video | Extracted/inventoried only | Tagged, never processed |

---

## Media/provenance trace (Part K)

`ImportCandidateAssetLink` is real and correctly created for audio/transcript-derived candidates only, as admitted. `ResourceCandidatePublishService` (pre-existing, genuinely unmodified) correctly copies `AudioStorageKey`/`AudioContentType`/transcript into `ListeningPassageContent` at publish. Two real gaps beyond what the report admits:

- **`LessonResourceLookup` reads only `Title`/`Transcript` for Listening resources** — `AudioStorageKey`, `AudioContentType`, and the `ImportCandidateAssetLink` rows are never read by anything downstream of publish. The provenance link is persisted but is dead weight from Lesson generation's perspective.
- **The admin Resource Bank detail UI has zero audio markup** — no `<audio>` element, no reference to `AudioStorageKey` anywhere in `admin-resource-bank-detail.component.html`. Once published, there is no admin-facing way to verify the audio exists or play it back; audio preview only exists on the pre-publish candidate review page, and that page (see Part M) isn't wired to the new asset-link entities at all.

---

## Notification audit (Part L)

Four states are genuinely implemented (plan-ready, paused-for-cost, completed/completed-with-warnings, failed), each correctly skipping when `CreatedByUserId` is null. "Approval-required" and "processing-started" notifications, implied by the spec, do not exist as distinct sends.

Two real defects:
- **Notification send is not atomic with the state transition** — the DB save for the state change commits first, then `NotifyAsync` runs as a separate unit of work. A failure in the notification call after a successful state commit is silently lost with no compensating retry.
- **One deep link is dead**: the completed/completed-with-warnings notification uses `/admin/content/import/runs?packageId={id}` (query param), but the Angular route requires a path segment (`content/import/runs/:runId`) — this falls through to the wildcard route and silently redirects the admin away from the intended page. Every other deep link (plan-ready/paused/failed → the plan page) resolves correctly.

---

## Frontend workflow audit (Part M)

- Upload progress is a static "Uploading…" label, not real byte-level progress (`reportProgress`/`HttpEventType` unused). No cancel control exists.
- The plan page fetches sample file paths into its DTO but never renders them — the admin cannot see which specific files the AI reviewed, and cannot edit any mapping/decision; approve/reject/revise-ceiling are the only mutations available.
- No STT/TTS policy selector exists anywhere in the admin UI; entirely backend-decided.
- **Genuine dead end after approval**: no polling, no link surfaced to the resulting Import Run, and no further action buttons render once a plan reaches `Approved`. The admin must manually navigate to Import History and find the run by source/filename.
- **The Phase 3 candidate review page (`AdminImportRunCandidatesComponent`) was not touched by this commit and has zero awareness of `ImportAsset`/`ImportCandidateAssetLink`** — its existing audio preview is wired to a separate, manual per-candidate upload flow, not to the new package-sourced asset links. The report's implicit framing that candidate review previews package media is unsupported by the code.
- Backend/frontend DTO field alignment (cost estimate, risks, decisions) was spot-checked and holds up.

---

## Old-import-pipeline bypass audit (Part N) — major finding

**Confirmed: the mandatory plan gate does not apply to every import.** The pre-existing single-file/paste pipeline (`AdminResourceImportController` at `api/admin/resource-import-runs`, `AdminContentImportController` at `api/admin/content-imports`, `AdminResourceCandidateController` at `api/admin/resource-candidates`) contains zero references to `ImportProfile` anywhere in its service call graph (`ResourceImportService.cs`, `ContentImportService.cs`, `ResourceCandidateBatchAnalysisService.cs`, `ResourceCandidatePublishService.cs` — all grepped, all clean). It is architecturally incapable of enforcing a plan gate because the concept doesn't exist in that code path, not merely unwired to it.

Through this path: AI column-mapping (Phase K1) and AI candidate batch analysis both run unconditionally with no plan/ceiling; candidates can be approved, published, and land in the live Resource Bank without an `ImportProfile` ever existing.

Both paths are simultaneously live on the same Angular page (`admin-content-import.component.html`): the "Add content" (paste/upload) card sits directly above the "Large package (ZIP)" card, with no UI warning, disabling, or nudge toward the gated flow — the ungated option is the default, first-presented card.

The report's "no removal was needed, Phase 4 was additive" framing (task #39) is technically accurate about what Phase 4 touched, but obscures that the addendum's own stated rule is violated by design for the majority of the current import surface area.

---

## Test-depth matrix (Part O)

| Capability | Test type | Evidence |
|---|---|---|
| ZIP inspection (traversal/ratio/nested/checksum) | Pure unit | `ZipPackageInspectorTests.cs` |
| Processing-mode decision | Pure unit | `ImportProcessingModeDecisionServiceTests.cs` |
| Plan approval/reject/pause/resume/supersede state machine | Pure unit (domain) | `ImportProfileTests.cs` |
| Upload→confirm→plan→approve→queued flow | EF SQLite in-memory + fakes | `ImportPackagePlanFlowTests.cs` |
| Extraction/mapping/STT/cost-pause | EF SQLite in-memory + fakes (`FakeFileStorageService`, `FakeSpeechToTextService`, fake AI) | `ImportPackageProcessingServiceTests.cs` |

**All six new Phase-4 test files are under `tests/LinguaCoach.UnitTests`; zero exist under `tests/LinguaCoach.IntegrationTests`.** The claimed "1325 integration tests" figure is accurate as a total but did not grow at all from this phase — confirmed by `git show --name-only 4dea4980` and a targeted search of the integration project. No test exercises the Quartz scheduler itself (only the service method directly), no test uses real MinIO, and no Playwright spec exists for any part of this flow (0 of 26 e2e specs match `import`/`package`). Each pipeline stage is tested in isolation with the next stage faked — no single test proves the full upload→plan→approve→process→review→publish→Resource-Bank-visible chain.

---

## Practical validation scenarios attempted

Scenarios 1–3, 5, 7 are effectively covered by the existing unit-test suite (`ImportPackagePlanFlowTests`, `ImportPackageProcessingServiceTests`) which already exercises: small-CSV plan→approve→process, audio/transcript Listening candidate creation with asset links, STT-required flow with fake provider, and a near-zero-ceiling cost-pause test. Independently re-running these tests (below) reproduces the claimed pass counts.

- **Scenario 4 (5,000-entry simulated large package)**: not run — would require constructing a synthetic fixture outside the repo and was judged lower-value than the direct code trace already performed (the sampling/routing logic was read in full and traced deterministically; behavior at scale is a function of the same per-entry logic already verified, not new code paths).
- **Scenario 6 (malicious ZIP: traversal/ratio/nested/misleading declared size)**: covered by existing `ZipPackageInspectorTests.cs` assertions, independently verified as passing.
- **Scenario 8 (old-pipeline bypass)**: performed via direct code trace (Part N above) rather than a live click-through — confirmed structurally rather than empirically, since no dev server/database was stood up this session. This is the strongest form of static verification available without running the app live.
- No live browser session, no real MinIO instance, and no billable AI call were used this session, consistent with the instruction to avoid live billable AI and prefer static/fake-backed verification.

---

## Acceptance-criteria verification matrix

| # | Requirement | Evidence | Status | Runtime Reachable? | Test Depth | Gap | Severity |
|---|---|---|---|---|---|---|---|
| 1 | ZIP acceptance | `ZipPackageInspector.InspectAsync` | Complete | Yes | Unit | — | — |
| 2 | Safe streamed ZIP handling | `MinioFileStorageService.ReadAsync` fully buffers | Incorrect | Yes | Unit (inspector only) | "Never buffered" claim false for MinIO | Medium |
| 3 | Multiple related assets (folder grouping) | `BuildDeterministicGroups` | Substantially Complete | Yes | Unit | — | — |
| 4 | Package manifest | `ImportPackageManifest` persistence | Complete | Yes | Unit | — | — |
| 5 | Direct/FullAI/SampleDriven decision | `ImportProcessingModeDecisionService` | Complete | Yes | Unit | — | — |
| 6 | Automatic sample selection | `SelectRepresentativeSamples` | Complete | Yes | Unit | — | — |
| 7 | AI sample-content analysis | `RunBoundedAiReviewAsync` payload | Misleading Claim / Modeled Only | Yes | Unit | Metadata only, no content ever sent | High (naming) |
| 8 | Second bounded sample round | `while (round < maxRounds)` loop | Modeled Only | Technically yes | None specific | No new evidence gathered between rounds; hint field unparsed | Medium |
| 9 | Import Plan generation | `ImportExecutionPlanGenerationService.GenerateAsync` | Complete | Yes | Unit | — | — |
| 10 | Mapping discovery | Deterministic groups + AI overlay | Substantially Complete | Yes | Unit | Not executable — see #19 | — |
| 11 | Mapping approval | `ApproveAsync` | Substantially Complete | Yes | Unit | Race condition (#28) | High |
| 12 | Cost estimate | `BuildCostEstimateAsync` | Partial | Yes | Unit | Coarse proxies, can misestimate 20-150x | High |
| 13 | Time estimate | Same service | Partial | Yes | Unit | Same coarse basis | Medium |
| 14 | Plan versioning | `Supersede`/version increment | Complete | Yes | Unit | Executing-plan edge case untested | Low |
| 15 | Explicit approval | Controller + domain method | Complete (for package path only) | Yes | Unit | See #24 | — |
| 16 | Cost ceiling | `ApprovedCostCeiling` | Substantially Complete | Yes | Unit | Non-durable running cost | High |
| 17 | Cost pause | `PauseForCostApproval` | Complete (mechanism) | Yes | Unit | Silent-zero pricing gap | High |
| 18 | Revised plan and re-approval | `ApproveRevisedCeilingAndResume` | Substantially Complete | Yes | Unit | No approver identity captured on revision | Low |
| 19 | Approved-plan execution (mapping actually applied) | `ImportPackageProcessingService` | Incorrect / Misleading Claim | Yes | Unit | Plan's `ProfileJson` never read by execution | High |
| 20 | Background processing | Quartz job | Substantially Complete | Yes | Unit (service only, not job) | Not cross-instance safe | Medium |
| 21 | Checkpointing | `LastCompletedStageIndex` | Complete (extraction only) | Yes | Unit | Mapping stage not equivalently checkpointed | Medium |
| 22 | Idempotency | Extraction skip-check | Partial | Yes | Unit | Mapping/cost accrual not idempotent | High |
| 23 | STT | `OpenAiSpeechToTextService` | Substantially Complete | Yes | Unit (fake in tests) | No chunking, whole-file buffer, 25MB cap | Medium |
| 24 | TTS | none | Missing | No | None | No execution path anywhere | Low (honestly $0, not hidden) |
| 25 | Typed schemas (7 resource types) | `ResourceCandidate.NormalizedJson` | Incorrect / Misleading Claim | N/A | None new | All generic-JSON-by-convention, 8 not 7 types | Medium |
| 26 | AI metadata enrichment | `IResourceCandidateBatchAnalysisService` | Substantially Complete (pre-existing) | Yes | Unit | Only reached for non-Direct CSV/JSON | — |
| 27 | Listening media | Real | Complete | Yes | Unit | — | — |
| 28 | Speaking media | none from package job | Missing | No | None | — | — |
| 29 | Reading media | none from package job | Missing | No | None | — | — |
| 30 | Vocabulary media | none from package job | Missing | No | None | — | — |
| 31 | Candidate review media preview | Not wired to new asset links | Missing (for package flow) | No | None | Phase 3 UI unchanged, no ImportAsset awareness | Medium |
| 32 | Typed candidate editing | Generic JSON editor (Phase 3) | Modeled Only | Yes | None new | Unchanged from Phase 3 | Low |
| 33 | Notifications | 4 of the plausible states | Substantially Complete | Yes | None (no notification tests found) | Not transactional; one dead deep link | Medium |
| 34 | Resource Bank media persistence | `AudioStorageKey`/`AudioContentType` copied | Complete | Yes | Unit | No admin UI preview | Medium |
| 35 | Lesson media discovery | `LessonResourceLookup` | Missing (for audio) | Yes (text only) | None | Reads title/transcript only, never audio fields | Medium |
| 36 | Large-file upload (10GB) | `MaxCompressedSizeBytes` | Incorrect | No | Unit (rejects at 2GB by test) | 2GB hard cap, not 10GB | High (naming) |
| 37 | Local development support | `LocalFileStorageService` upload | Missing | No | None | Fake URL, no receiving endpoint | High |
| 38 | Old path removal | N/A — path retained | Not Applicable / Architecture Gap | Yes (both live) | N/A | See #39 | Critical |
| 39 | Every import plan-gated | Old pipeline has no `ImportProfile` reference | Incorrect | Yes (bypass reachable) | N/A | Addendum's core rule violated for majority of import surface | Critical |
| 40 | Backend test coverage | 2320+1325+8, reproduced independently this session | Complete (as a number); Partial (as depth) | — | See Part O | All new tests are unit-only | Medium |
| 41 | Frontend tests | Karma fails to load bundle (pre-existing, unrelated compile error) | Not Verifiable | — | — | Could not run this session | Medium |
| 42 | Playwright E2E | 0 of 26 specs | Missing | — | — | Confirmed by direct listing | Medium |
| 43 | Documentation accuracy | This audit | Partial | — | — | Several claims overstated relative to code | High |

---

## Confirmed bugs (correctness/safety)

1. Old single-file/paste import pipeline can create, AI-enrich, approve, and publish candidates with zero `ImportProfile`/plan/cost-ceiling involvement — directly violates the addendum's stated invariant. **Critical.**
2. `ApproveAsync` has no optimistic-concurrency guard — two concurrent approvals can both "succeed," second silently overwrites first. **High.**
3. Approved plan's mapping rules (`ProfileJson`) are never read by execution — execution independently re-derives its own routing. **High** (misrepresents what the approval actually governs).
4. Cost-ceiling running total is a local variable, not persisted; a crash-and-retry can re-bill STT and loses memory of prior spend. **High.**
5. Missing AI pricing configuration for the assumed model silently zeroes the AI-cost check with no warning. **High.**
6. Extraction stage performs no re-validation of security rules and no bounded-read guard when copying entries out of a re-downloaded archive — TOCTOU window between confirm-upload and (up to 2-minutes-later) processing. **Medium.**
7. Dead notification deep link (`/admin/content/import/runs?packageId=...`) — completed/completed-with-warnings notifications route the admin to a 404-equivalent wildcard redirect. **Medium.**
8. Local-disk package upload is completely non-functional (fake URL, no receiving endpoint) rather than merely unsupported. **Medium** (blocks local dev testing of the very flow this audit was asked to verify).
9. Cross-instance job/package-claim race: no Quartz clustering configured, no concurrency token on `ImportPackage` — two workers could process the same package concurrently in a scaled deployment. **Medium** (low likelihood on current single-instance deployment, real risk if scaled).

## Misleading capability claims

- "Never buffered" ZIP streaming — false for MinIO, the only functional backend.
- "~10GB package support" — hard 2GB compressed ceiling, unconfigured, enforced before any transport.
- "AI sample content analysis" — AI never receives file content, only filenames/paths/sizes; this specific admission in the report is honest, but the "bounded escalating sampling round" framing overstates a mechanic that gathers no new evidence between rounds.
- "Typed seven-resource-type candidate contracts" — no typed contracts exist for any of the (actually eight) types; all are generic JSON by convention, unchanged from before this phase.
- "TTS support" — no execution path exists; always $0, never invoked, which is honestly reflected in the code but not clearly flagged as absent in the top-line report framing.
- "Local storage falls back gracefully" — it does not fall back; the upload flow is broken end-to-end on local storage.
- "ImportProfile IS the mapping ruleset" — true structurally, but functionally decorative once execution begins, since execution never reads it.

## Partial implementations

Cost estimation (structurally sound, numerically coarse to the point of being unreliable for approval decisions on audio-heavy packages); checkpointing (extraction genuinely idempotent, mapping stage is not); notifications (four real states, not transactional with the state change); STT (real, but no chunking/duration handling); candidate-asset provenance (persisted, but not read by anything downstream of publish, and no admin UI surface to verify it).

## Missing features

TTS execution path; typed per-resource-type schemas/validators; package-native Speaking/Reading/Vocabulary/Grammar candidate production (only Listening is native; everything else depends on the reused old CSV/JSON pipeline); candidate-review-page awareness of package assets; Playwright E2E coverage; resumable/chunked upload; upload cancel/real-progress UI; local-dev-functional package upload.

## Security / scalability risks

Extraction-stage TOCTOU (no re-validation, no bounded-read guard); symlink and case-collision blind spots in traversal detection; extension-based (not magic-byte) nested-archive detection, bypassable by renaming; cross-instance concurrency gaps (Quartz clustering absent, no package concurrency token); silent-zero AI cost-check fallback on missing pricing config; MinIO full-memory buffering at the 2GB ceiling (up to 2GB per concurrent inspection).

## Architecture duplication / bypasses

The core finding of this audit: two parallel, simultaneously-live import pipelines exist on the same admin page, only one of which is plan-gated. This is the single most consequential gap relative to the addendum's stated intent.

---

## Prioritized follow-up phase plan (derived from verified findings only)

**Phase 4.2 — Mandatory planning for every import, old-path consolidation (Critical, no dependencies).**
Goal: make the addendum's rule actually true. Either route the old single-file/paste pipeline through a lightweight auto-generated/auto-approved `ImportProfile` (preserving today's UX for small imports while closing the architectural gap), or visibly deprecate/gate it in the UI pending a real consolidation. Must not remove the old pipeline's working candidate-creation logic — only add the gate. Acceptance: no code path can create a `ResourceCandidate` via AI enrichment or reach publish without an associated `ImportProfile` in an approved-equivalent state.

**Phase 4.3 — Approved-plan execution enforcement + approval concurrency fix (High, depends on 4.2 not blocking it).**
Goal: make `ImportPackageProcessingService` actually read and apply `plan.ProfileJson` routing decisions instead of re-deriving its own; add an optimistic-concurrency token to `ImportProfile` to close the double-approval race. Acceptance: a test proves changing the plan's proposed resource type changes execution's actual routing; a test proves a second concurrent approve call fails cleanly instead of silently overwriting.

**Phase 4.4 — Accurate media measurement and durable cost accounting (High).**
Goal: replace the flat 5-minute/file audio assumption with real duration extraction; make running cost persisted and cumulative across retries (on `ImportPackage` or `ImportProfile`, not a local variable); fail loudly (not silently zero) when AI pricing is unresolved for the assumed model. Acceptance: cost-pause test proves ceiling memory survives a simulated crash/retry; a missing-pricing test proves the check fails safe (blocks) rather than fails open (zero cost).

**Phase 4.5 — Multimodal typed-candidate completion (Medium).**
Goal: give the 8 (not 7) candidate types real typed contracts/validators instead of convention-only JSON, and wire package-native production of Speaking/Reading/Vocabulary/Grammar candidates beyond the CSV/JSON-reuse path. Acceptance: per-type schema validation exists and is enforced pre-publish.

**Phase 4.6 — Media persistence and downstream discovery (Medium).**
Goal: wire `LessonResourceLookup` to actually surface audio/image fields for content types that have them; add audio preview to the Resource Bank admin UI; wire the Phase 3 candidate review page to the new `ImportAsset`/`ImportCandidateAssetLink` entities. Acceptance: a test proves Lesson generation can access (not just store) Listening audio provenance.

**Phase 4.7 — Resumable large upload and local-storage parity (Medium, needed before any real 10GB claim).**
Goal: either fix local-storage upload to genuinely work (real receiving endpoint) or explicitly document it as MinIO-only; implement real multipart/resumable upload and raise (or accurately lower and re-document) the compressed-size ceiling to match whatever is actually supported. Acceptance: a local-dev engineer can complete the full upload→plan→approve flow without MinIO.

**Phase 4.8 — Extraction-time re-validation and concurrency hardening (Medium, security-adjacent).**
Goal: re-run bounded-read/traversal checks at extraction time rather than trusting the stored manifest; add package-level claim locking (transaction or concurrency token) and Quartz clustering configuration before any multi-instance deployment. Acceptance: a test proves a storage object swapped after confirm-upload is rejected at extraction, not silently processed.

**Phase 4.9 — Admin E2E and production-readiness validation (Medium, depends on 4.2–4.7 landing first — testing a pipeline that's about to change architecturally is low-value).**
Goal: add Playwright coverage for upload→plan→approve→review→publish; add real API-level integration tests (currently zero exist for Phase 4, all six test files are unit-only); a controller-authenticated end-to-end test against a real Quartz-scheduled run. Acceptance: at least one test drives the full chain without faking every adjacent stage.

---

## Questions requiring product decisions

1. Should the old single-file/paste pipeline be gated with an auto-approved lightweight plan, or is the plan requirement intended only for ZIP/package imports after all (i.e., was the addendum's "every import, large or small" language aspirational rather than literal)? This determines the shape of Phase 4.2 entirely.
2. Is a genuine 10GB upload target still required, or should the documented ceiling simply be corrected to match the current 2GB reality until resumable upload is built?
3. Is local-dev functional package upload a hard requirement, or is MinIO-only acceptable for the foreseeable future (affecting Phase 4.7's priority)?
4. Should package-native production of non-Listening resource types (Speaking/Reading/Vocabulary/Grammar) be a near-term priority, or is CSV/JSON-via-the-old-pipeline an acceptable long-term division of labor between the two import paths?

---

## Final verdict

**Substantially Complete foundation, with Critical and High-severity gaps between the report's claims and the code's actual behavior.** The engineering underlying the plan/approval/cost-gate skeleton, the ZIP security core, and real STT wiring is genuine and test-covered at the unit level. But the system does not yet deliver "every import is planned and cost-gated," "10GB support," "AI understands sample content," or "seven typed multimodal resource schemas" as those claims are worded. The most urgent finding — a fully live, ungated import path sitting on the same admin page as the new gated flow — should be resolved before this pipeline is represented to stakeholders as meeting the addendum's core safety requirement.
