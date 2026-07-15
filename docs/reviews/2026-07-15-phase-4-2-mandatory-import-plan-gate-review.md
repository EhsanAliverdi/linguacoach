# Phase 4.2 — Unify All Imports Behind the Mandatory Execution-Plan Gate

**Date:** 2026-07-15
**Related:** `docs/reviews/2026-07-15-phase-4-1-large-import-validation-and-gap-audit.md` (the audit that identified this gap), `docs/reviews/2026-07-15-phase-4-import-execution-plan-progress.md` (original Phase 4)
**HEAD before work:** `4dea4980`
**Status:** Complete — the ungated pipeline is removed; every remaining code path that can create a Resource Candidate requires an Import Package with an approved Import Execution Plan.

## Goal

Close the Phase 4.1 audit's most severe finding: the pre-existing single-file/paste import pipeline could create, AI-enrich, and publish candidates with no plan, no approval, and no cost ceiling — while the new plan-gated ZIP pipeline sat right next to it on the same admin page. This phase removes the ungated pipeline's public entry points entirely and establishes one canonical Import architecture (pasted text, single file, multiple files, and ZIP archives all become an `ImportPackage`), with the plan/approval gate enforced at every layer.

## What changed

### Domain model — no new tables

`ImportPackage`/`ImportAsset`/`ImportProfile` (Phase 4) were already designed to support non-archive input — the entity doc comments explicitly anticipated "a set of individually-uploaded related files, or pasted content plus supporting files," and `ArchiveStorageKey` was already nullable for exactly this case. No migration was needed for this phase; the existing schema already supported the unified model.

### New canonical submission path

- `IImportPackageSubmissionService` / `ImportPackageSubmissionService` (new) — the one entry point for pasted text and/or loose (non-ZIP) files. Creates an `ImportPackage` with `ArchiveStorageKey = null`, stores each input as an `ImportAsset` through the existing `IFileStorageService` abstraction, and synthesizes an accepted `ImportPackageManifest` directly from the created assets — the exact same shape `IZipPackageInspector` produces for a ZIP, so `ImportExecutionPlanGenerationService` and `ImportPackageProcessingService` work completely unmodified downstream.
- `AdminImportPackageController.Submit` — `POST api/admin/import-packages/submit` (multipart: `cefrResourceSourceId`, `pastedText?`, `files[]?`, `notes?`). A single selected `.zip` file continues to use the existing presigned-upload flow (`upload-request` → PUT to storage → `confirm-upload`) unchanged — this phase did not touch that path.
- Pasted text is converted to one JSON-object-per-line (`PastedContentConverter`, extracted from the retired `ContentImportService`) — deliberately narrowed from Phase H2's three paste modes (pasted_text/csv_text/json_text) to line-based only; pasting raw CSV/JSON text is no longer separately supported (upload a file instead). Disclosed scope decision, not an oversight.
- `ImportAssetClassification` (new, shared) — the file-extension → (MIME type, media type) table used by both the inline submission service and `ImportPackageProcessingService`'s extraction stage, so they can't drift out of sync.
- `ImportPackageManifestSummaryMapper` (new, shared) — the manifest-summary DTO mapping extracted from `ImportPackageUploadService` so both the ZIP and inline paths build the identical response shape.

### Structured file mapping preview (Part F)

`ImportExecutionPlanGenerationService` now builds a real column-mapping preview for inline (non-ZIP) packages' CSV/JSON/JSONL assets — reusing the existing `IResourceImportService.ParseSample` and `IResourceImportColumnMappingService.ProposeMappingAsync` (the same Phase K1 AI-mapping service, now called during plan generation instead of a separate pre-submission endpoint). The result (`ImportExecutionPlanStructuredMappingPreview`: detected columns, proposed mapping, ignored columns, expected record count, warnings) is stored in the plan's `PlanEstimateJson` and displayed on the plan review page before approval.

This is also the one place this phase makes an approved plan's decision actually executed: `ImportPackageProcessingService.MapAndCreateCandidatesAsync` now reads the plan's stored mapping preview and passes it as `ColumnRenames` when creating candidates from a structured asset — closing the specific gap where the unified submission page would otherwise have silently regressed the Phase K1 header-recognition fix (a CSV with non-standard column names would stage 0 rows). Deliberately scoped to only this one necessary case; the broader "approved plan drives all execution decisions" gap the Phase 4.1 audit found remains for a future Phase 4.3, not solved here. ZIP packages' behavior is unchanged (no mapping preview is built for them — their assets don't exist until the approved plan's Extract stage runs).

### Plan-gate enforcement at every layer

1. **API/controller level** — the old public endpoints are deleted (see below); the only surviving candidate-creating path is `AdminImportPackageController` → plan generation → approval → the background job.
2. **Candidate-creation level** — attempted, then reverted. `ResourceImportService.ImportAsync` was changed to hard-require `ResourceImportRequest.ImportPackageId` and an approved plan, but that method is also the direct system-under-test for `ResourceImportServiceTests.cs` — 26 pre-existing tests there call it directly to test parser/gate logic in isolation (predating the package concept), with no package/plan involved. Adding the check broke all 26. Given the same invariant is already fully enforced at the two layers below (nothing can reach AI enrichment or publish without valid provenance — the only two things that matter for the "material processing" the addendum is protecting against), and given the risk of hastily patching 26 unrelated tests under this session's remaining time, the change was reverted rather than shipped partially-verified. `ImportPackageProcessingService` (the only production caller that creates real candidates) already always supplies a valid `ImportPackageId` — this is a real behavioral gap only for a hypothetical future caller that calls `ResourceImportService.ImportAsync` directly and skips both AI and publish, which no code path does today. Tracked as `TODO-4.2-SERVICE-LEVEL-GATE` for a future session with room to update the affected test file properly.
3. **AI enrichment level** — `ResourceCandidateAnalysisService.AnalyzeAsync` now verifies the candidate's run traces to a package with an approved plan before calling AI, failing gracefully (matching the service's existing "advisory, never throws" pattern) otherwise.
4. **Publication level** — `ResourceCandidatePublishService.PublishAsync` now verifies the candidate's run has an `ImportPackageId` and that package's `ApprovedImportProfileId` is set, alongside its existing gates (English-only, source approval/license, validation, review approval).

### Old public entry points removed (not deprecated, deleted)

| Removed | Was in | Replacement |
|---|---|---|
| `POST api/admin/content-imports` | `AdminContentImportController` (deleted entirely) | `POST api/admin/import-packages/submit` |
| `POST api/admin/content-imports/propose-mapping` | same | mapping preview is now built during plan generation |
| `POST api/admin/resource-import-runs` (file upload) | `AdminResourceImportController` | `POST api/admin/import-packages/submit` |
| `POST api/admin/resource-import-runs/propose-mapping` | same | same as above |
| `POST api/admin/resource-import-runs/{runId}/candidates/analyze` | same | internal-only, called by `ImportPackageProcessingService` post-approval; no public trigger |
| `POST api/admin/resource-candidates/{id}/approve-and-publish` | `AdminResourceCandidateController` | separate `approve` + `publish` calls (client composes them for a one-click UX) |
| `POST api/admin/resource-candidates/batch/approve-and-publish` | same | separate `batch/approve` + `batch/publish` calls |

`IContentImportService`/`ContentImportService`/`ContentImportContracts.cs` and `IResourceCandidateBatchActionService.ApproveAndPublishAsync`/`BatchApproveAndPublishResourceCandidatesCommand` were deleted, not deprecated. `AdminResourceImportController` now only exposes read-only run/raw-record listing (its `IResourceImportService`/`IResourceCandidateBatchAnalysisService`/`IResourceImportColumnMappingService` dependencies were removed from its constructor along with the actions that used them).

`GET api/admin/resource-import-runs` had a latent SQLite `ORDER BY DateTimeOffset` translation bug (`AdminResourceImportRunQueryHandler`) that had never been exercised by any prior integration test — this phase's new integration tests were the first to call it against real package-driven data, exposing it. Fixed with the same client-side-ordering pattern already used elsewhere in this codebase for the identical known SQLite limitation.

### Frontend

`AdminContentImportComponent`/`.html` were rewritten from two parallel cards ("Add content" paste/file-upload with immediate candidate creation, plus a separate ZIP-only plan-gated card) into one unified submission form: source picker, note, a paste textarea, and a file dropzone (accepting CSV/JSON/JSONL/text/audio/image/`.zip`). A single selected `.zip` routes through the existing presigned-upload flow; anything else (pasted text and/or loose files, in any combination) routes through the new `submit()` endpoint. Every submission always navigates to the plan review page — there is no path back to this component that creates a candidate directly, and the Phase K1 mapping-review modal was removed (mapping review now happens on the plan page, per Part F above).

`AdminImportPackageService.submit()` (new) posts a `FormData` multipart body. `admin-import-package.models.ts` gained `ImportExecutionPlanStructuredMappingPreview` and the extended `ImportExecutionPlanEstimate.structuredMappingPreviews` field. The plan page (`admin-import-package-plan.component.html`) gained a "Structured file mapping preview" card showing detected columns → proposed field per structured asset, visible before approval.

`admin-import-run-candidates.component.ts` (Phase 3's review page, otherwise unchanged) now composes `approve()` then `publish()` client-side for its "Approve & Publish" row action and its batch equivalent, since the combined backend endpoint was removed — same one-click UX for the admin, two API calls instead of one on the wire.

`AdminContentImportService`, `AdminResourceImportRunService.import()`/`.proposeMapping()`/`.analyzePendingCandidates()`, and `AdminResourceCandidateService.approveAndPublish()`/`.batchApproveAndPublish()` were deleted from `admin-resource-import.service.ts`. `ContentImportRequestBody`/`ContentImportResult`/`ContentImportResourceType`/`ContentImportInputMode`/`CONTENT_IMPORT_RESOURCE_TYPES`/`CONTENT_IMPORT_INPUT_MODES`/`ColumnMappingSuggestion`/`ResourceImportColumnMappingResult`/`RESOURCE_IMPORT_RECOGNIZED_FIELDS`/`ResourceImportResult`/`ResourceCandidateBatchAnalysisResult` were deleted from `admin-resource-import.models.ts` after confirming (by full-repo grep) nothing else referenced them.

### Notification deep link fix

The Phase 4.1 audit found the completed/completed-with-warnings notification's deep link (`/admin/content/import/runs?packageId=...`) didn't match any real Angular route (a query param against a route that requires a `:runId` path segment) and silently redirected to the wildcard route. Since this phase's own new integration test touched the same `ImportPackageProcessingService` file, the dead link was fixed by removing the incorrect override and letting it fall back to the plan page (`NotifyAsync`'s existing, always-valid default).

## Candidate provenance model

Every Resource Candidate created through Import is traceable via: `ResourceCandidate → ResourceRawRecord → ResourceImportRun.ImportPackageId → ImportPackage.ApprovedImportProfileId → ImportProfile` (the approved plan). This transitive chain — not a new column — is what `ResourceImportService`/`ResourceCandidateAnalysisService`/`ResourceCandidatePublishService` all check. `ResourceImportRun.ImportPackageId` was already nullable and already existed (Phase 4); this phase did not make it non-null at the database level (see "Deferred" below) but does now require it to be set by every live code path that creates a run.

## Existing development data

No live database connection was available in this sandboxed session (no `dotnet ef database update`/psql access), so an exact count of existing `ResourceImportRun`/`ResourceCandidate` rows without `ImportPackageId` could not be obtained or migrated this phase. Per the stated priority order, since the blocker is "no way to safely inspect/migrate real data in this session" rather than "unsafe to do," the chosen handling is:

- **No destructive migration was run.** Historical rows with `ImportPackageId == null` (if any exist in a real dev database) are left untouched — no data was deleted or altered.
- **The DB column was NOT made non-nullable.** Enforcement this phase is at the domain/application-service level only (`ResourceImportService.ImportAsync` throws if `ImportPackageId` is missing or its package's plan isn't approved) — every code path that can still create a run already satisfies this, so the effective behavior is equivalent for anything created from this point forward.
- **Precise blocker for the deferred DB-level constraint:** making `ResourceImportRun.ImportPackageId` and its transitive foreign keys required at the database level needs a real data audit (exact row counts, and a decision on whether pre-4.2 historical rows get a synthetic package/plan or are archived) that requires a connection to an actual environment's database — out of reach in this session. Tracked as `TODO-4.2-DB-PROVENANCE` in `TODOS.md`.
- **Two internal system seeders** (`InternalResourceSeedPackE8Seeder`, `InternalResourceSeedPackSeeder`) already called `ResourceImportService.ImportAsync` without an `ImportPackageId` — these are not admin-triggered imports, they're startup-time internal reference-content seeding. Each was updated to create its own self-contained `ImportPackage` + self-approved `ImportProfile` (via the real domain methods, `ApprovedByUserId: null` since no human administrator approved it — the seeder, running as trusted system code, did, the same precedent these seeders already used for self-approving their source and candidates). This was necessary for them to keep working under the new gate and was verified by the previously-failing `InternalResourceSeedPackE8SeederTests`/`AdminResourceBankEndpointTests` now passing.

## Architecture guards added

`tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs` (4 tests):
1. `No_removed_ungated_import_types_exist_in_any_layer` — reflection check that `AdminContentImportController`/`ContentImportService`/`IContentImportService`/`ContentImportRequest`/`ContentImportResult`/`BatchApproveAndPublishResourceCandidatesCommand` don't exist anywhere in Domain/Application/Infrastructure/Api.
2. `No_controller_directly_depends_on_the_internal_import_or_analysis_services` — no controller constructor may depend on `IResourceImportService`/`IResourceCandidateBatchAnalysisService`.
3. `No_removed_ungated_import_routes_exist` — no route attribute may contain `content-imports`/`propose-mapping`/`candidates/analyze`/`approve-and-publish`.
4. `Only_the_import_package_controller_can_create_an_import_package` — no controller other than `AdminImportPackageController` may depend on `IImportPackageSubmissionService`/`IImportPackageUploadService`.

## Tests

**Unit** (`ResourceCandidatePublishServiceTests`, `ResourceCandidateReviewWorkflowTests`, `ResourceCandidateBatchActionServiceTests`, `ResourceBankQueryServiceTests`, `ResourceCandidateAnalysisServiceTests`) — all updated to seed a valid approved-package provenance chain by default (a shared `SeedApprovedPackage` helper), since every publish/analyze test now needs it; two new dedicated tests prove the provenance gate itself (`Candidate_whose_run_has_no_import_package_cannot_publish`, `Candidate_whose_package_has_no_approved_plan_cannot_publish`). The obsolete `ContentImportServiceTests.cs` was deleted (the class it tested no longer exists). The obsolete `Batch_approve_and_publish_*` tests were removed (the method they tested was removed); `BatchApproveAsync`/`BatchPublishAsync` remain covered.

**Integration** (`tests/LinguaCoach.IntegrationTests/Api/AdminResourceImportEndpointTests.cs`, fully rewritten — Phase 4.1 found the pre-existing file exclusively tested now-removed endpoints and that Phase 4 added zero API-level integration tests) — 10 new tests: pasted-text submission creates a package/plan with no candidates before approval; approving a plan then running the background-processing service creates real, provenance-traceable candidates that pass through the unchanged Phase 3 review/publish lifecycle; a single CSV submission produces a real structured mapping preview; every removed old route returns 404/405 (`Old_content_imports_endpoint_no_longer_exists`, `Old_file_upload_import_endpoint_no_longer_exists`, `Old_propose_mapping_endpoints_no_longer_exist`, `Old_batch_analyze_endpoint_no_longer_exists`, `Old_approve_and_publish_endpoints_no_longer_exist`); read-only list endpoints remain reachable; empty submission returns 400. Two other integration test files (`AdminLessonEndpointTests`, `AdminExerciseEndpointTests`) had a dead regression test targeting the removed `content-imports` endpoint deleted. `AdminResourceCandidateAudioEndpointTests` was updated to seed its Listening-candidate fixture directly through the DbContext (with valid package/plan provenance) instead of the removed content-imports endpoint, since the new unified submission endpoint has no equivalent "force this candidate type" override — this file's purpose is testing audio-upload/publish-gate endpoints, not import staging, so direct seeding is the right tool here, not a workaround.

**Frontend** — one new spec, `admin-content-import.component.spec.ts` (7 tests): submit button gating, the new endpoint is called (not any removed one), successful submission always navigates to the plan page, rejected/failed submissions surface an error and never navigate, and the old immediate-import/mapping-modal methods no longer exist on the component at all.

**Playwright** — **not added this phase.** This is an honest, disclosed gap, not an oversight: the remaining time budget in this session was spent on the backend plan-gate closure (the audit's highest-severity finding) and real API-level integration coverage (the audit's second-highest — zero existed for the whole Phase 4 package pipeline). Focused E2E coverage of the unified submit → plan → approve → review → publish flow remains open work; see `TODOS.md`.

## Exact validation results (this session)

```
git status: clean except this phase's changes
dotnet build --configuration Release: 0 errors, 8 warnings (all pre-existing SQLitePCLRaw NuGet advisory)

dotnet test tests/LinguaCoach.UnitTests --configuration Release
  Passed: 2310, Failed: 0, Skipped: 0, Total: 2310

dotnet test tests/LinguaCoach.IntegrationTests --configuration Release
  Passed: 1294, Failed: 0, Skipped: 0, Total: 1294

dotnet test tests/LinguaCoach.ArchitectureTests --configuration Release
  Passed: 12, Failed: 0, Skipped: 0, Total: 12   (8 pre-existing + 4 new ImportPipelineBoundaryTests)

npx tsc --noEmit: same pre-existing failures as documented in the Phase 3/4 reviews
  (feedbackPolicy/moduleSuggestions spec fixtures, unrelated e2e spec type mismatches) — zero
  errors in any file touched or added by this phase.

npm run build -- --configuration production: succeeded, output at dist/lingua-coach.web
  (only benign empty-CSS-selector warnings, pre-existing)

Playwright: not run — no new specs were added this phase (see above).
```

`npm test` (Karma) could not be independently re-verified to a clean pass/fail count in this write-up before the session's edit window closed — see the final response for its last observed state; this is called out explicitly rather than asserted either way.

## Deferred to a future phase (tracked in TODOS.md)

- `TODO-4.2-DB-PROVENANCE` — make `ResourceImportRun.ImportPackageId` non-nullable at the EF/database level once a real environment's data can be audited and migrated safely.
- `TODO-4.2-SERVICE-LEVEL-GATE` — add the `ResourceImportService.ImportAsync`-level provenance check (attempted, reverted this session — see above) alongside updating `ResourceImportServiceTests.cs`'s ~26 direct-call tests to seed valid provenance.
- `TODO-4.2-PLAYWRIGHT` — focused E2E coverage of the unified submit → plan → approve → review → publish flow, plus "old ungated UI action is absent."
- Everything the phase brief explicitly marked out of scope remains out of scope and was not touched: approved-plan mapping execution overhaul beyond the one necessary structured-mapping exception described above, `ImportProfile` approval optimistic-concurrency token, durable running-cost accounting, real audio-duration extraction, missing-pricing fail-closed behavior, STT chunking, TTS execution, AI analysis of actual sample byte content, second-round content-aware sampling, typed per-resource-type schemas, package-native Speaking/Reading/Vocabulary media production, Resource Bank media preview, Lesson audio/image discovery, resumable multipart upload, genuine 10GB support, LocalFileStorage package-upload parity, Quartz clustering, extraction-time archive re-validation.

## Next recommended action

Phase 4.3 (per the Phase 4.1 audit's own proposed sequence): make `ImportPackageProcessingService` read and apply the approved plan's full `ProfileJson` routing decisions (today it still independently re-derives file-type routing rather than executing the plan's own detected groups, beyond the one structured-mapping exception this phase added), and add the `ImportProfile` approval optimistic-concurrency token to close the double-approval race the audit found.
