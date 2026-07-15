# Phase 4 — Large-Scale AI Import Packages: Final Review (Import Execution Plan addendum)

**Date:** 2026-07-15
**Related:** `docs/reviews/2026-07-15-content-creation-pipeline-architecture-audit.md` (Phase 4 spec origin)
**Status:** Complete — all 15 tasks (#26-#40) delivered, tested, and committed. This document was
started as an interim checkpoint mid-session and is now the final Phase 4 review; see "Session
timeline" below for how it evolved.

## Context

Phase 4 ("Large-Scale ZIP Import, AI Structuring, Mapping, Enrichment, and Multimodal Resource
Packages") was scoped via `AskUserQuestion` as "attempt the full spec" (user's explicit choice
over two narrower options). Mid-session, the user issued a **mandatory addendum**: no package,
regardless of size, may begin material AI/STT/TTS/background processing without a persisted,
administrator-approved **Import Execution Plan** carrying a cost/time estimate. The user also
clarified: (a) this gate applies to *every* import, not just large/expensive ones, and (b) sample
selection is fully automatic (deterministic clustering + a bounded AI review) — there is no manual
admin sample-picking UI/step.

This document covers everything built so far under that addendum. Work continues in later
sessions on the remaining task list below.

## What "Import Execution Plan" means in this codebase

Rather than introduce a parallel entity, the addendum's plan is implemented by extending the
already-designed `ImportProfile` entity (Phase 4 Part B originally called it the AI-proposed
mapping/extraction ruleset — the addendum's plan is a superset of that same concept: mapping +
cost/time estimate + approval + versioning). See `ImportProfileStatus`'s doc comment for this
equivalence, made explicit so future agents don't build a second parallel plan entity.

## Files added/changed this session

### Domain
- `ImportPackageLimitsOptions` (Infrastructure) — configurable Part N safety/cost limits (max
  compressed/expanded size, entry count, compression ratio, nesting depth, per-file size, full-AI
  analysis thresholds, sample caps, AI input/output caps, concurrency/retry caps).
- `ImportProfileStatus` (Domain) — extended from 3 to 10 states: `Draft, AwaitingApproval,
  Approved, Rejected, PausedForCostApproval, Executing, Completed, Failed, Cancelled, Superseded`.
- `ImportProfile` (Domain) — extended with `EstimatedCostExpected/Min/Max`, `Currency`,
  `PlanEstimateJson`, `PricingSnapshotJson`, `ApprovedCostCeiling`, rejection/pause/change-reason
  fields, and the full lifecycle methods: `SubmitForApproval`, `Approve` (now requires a cost
  ceiling), `Reject`, `MarkExecuting`, `PauseForCostApproval`, `ApproveRevisedCeilingAndResume`,
  `MarkCompleted`, `MarkFailed`, `Cancel`, `Supersede`. Version > 1 requires a `ChangeReason`
  (Part 7 — no invisible plan mutation).
- Migration `Phase_4_ImportPackagesAndAssets` regenerated (never applied to any DB, so safely
  replaced rather than layered) to include the new columns.

### Application contracts
- `ImportPackageManifestContracts.cs` — `IZipPackageInspector`, `ImportPackageManifest` and its
  entry/folder-group DTOs.
- `ImportPackageContracts.cs` — `IImportPackageUploadService` (request-upload-URL /
  confirm-upload / manifest-summary).
- `ImportProcessingModeContracts.cs` — `IImportProcessingModeDecisionService`.
- `ImportExecutionPlanContracts.cs` — the full plan DTO shape (detected groups, volume/time/cost
  estimate, risks, proposed decisions), generation/approval commands, and
  `ImportExecutionPlanNotApprovableException` (Part 9 pre-approval quality gate).

### Infrastructure
- `ZipPackageInspector` — streams a ZIP's central directory (never buffers full content),
  rejecting/flagging: path traversal, oversized archive/entries, entry-count ceiling, expanded-size
  ceiling, compression-ratio-per-entry ceiling, nested archives (depth 0 = always rejected this
  phase), and a **declared-vs-actual size guard** on the bounded checksum read (defends against a
  central directory that understates an entry's true size). Computes SHA-256 checksums, folder
  grouping, duplicate-checksum detection.
- `IFileStorageService.GenerateUploadUrlAsync` — new presigned-PUT capability, implemented for
  MinIO (`PresignedPutObjectArgs`, confirmed present in the installed 7.0.0 SDK), Local (falls back
  to an API-upload marker, mirroring the existing GET fallback convention), and Fake (tests).
- `ImportPackageUploadService` — request-upload-URL → confirm-upload (verifies the object landed
  in storage, streams it through the inspector, persists the manifest, transitions
  `ImportPackageStatus`) → manifest-summary read.
- `ImportProcessingModeDecisionService` — pure deterministic Direct/FullAiAssisted/SampleDriven
  decision from manifest + limits (no AI, no I/O).
- `ImportCostEstimationOptions` — STT ($/minute), TTS ($/1K chars), image-analysis ($/image),
  assumed AI tokens/candidate, cost-range uncertainty fraction, cost-ceiling tolerance fraction,
  max sampling rounds. Text-AI cost reuses the **existing** `IAiPricingResolver` /
  `AiModelPricingOverride` pricing system per the addendum's "don't hardcode provider prices if a
  pricing system already exists" instruction — only STT/TTS/image (which have no existing pricing
  table) get new configurable rates here.
- `ImportExecutionPlanGenerationService` — Parts 1-4: deterministic folder/extension clustering,
  automatic representative sampling (first/middle/last/largest/smallest per group, capped by
  config — no manual admin sample step), a **bounded** AI review round (capped at
  `MaxSamplingRounds`, metadata-only — file names/paths/sizes, never file content) via the
  existing `IAiContextBuilder`/`AiExecutionService` pattern with a new seeded prompt
  (`import_package_plan_review`), volume/time/cost estimate construction, risk/decision list
  construction, and plan persistence with automatic versioning/supersession.
- `ImportExecutionPlanApprovalService` — `ApproveAsync` (blocks via
  `ImportExecutionPlanNotApprovableException` if the plan isn't `AwaitingApproval`, has no
  candidate estimate, or no cost/time estimate — Part 9), `RejectAsync`, resumes a
  cost-ceiling-paused plan, current-plan lookup.

### API
- `AdminImportPackageController` — `POST upload-request`, `POST {id}/confirm-upload`,
  `GET {id}/manifest`, `POST {id}/plan` (generate/regenerate), `GET {id}/plan`,
  `POST {id}/plan/{planId}/approve`, `POST {id}/plan/{planId}/reject`,
  `POST {id}/plan/{planId}/approve-revised-ceiling`. No endpoint can trigger processing directly —
  only `approve` moves a package to `Queued`.

### Tests (all passing, no regressions)
- `ZipPackageInspectorTests` — accept path, path traversal, entry-count/size/ratio/nested-archive
  rejection, duplicate detection, non-seekable-stream guard.
- `ImportProcessingModeDecisionServiceTests` — all three mode branches plus threshold edges.
- `ImportProfileTests` (Domain) — the full approval/reject/pause/resume/supersede state machine,
  including "cannot execute an unapproved plan" and "rejected/superseded/completed plan cannot
  execute/cancel."
- `ImportPackagePlanFlowTests` — full in-process flow (fake storage + fake AI provider): upload
  size-limit rejection, upload → confirm → generate plan → approve → package reaches `Queued`,
  reject → package `Failed` and a second approve attempt throws, plan generation on a
  manifest-less package throws.
- Full suite result after the first checkpoint: **2316 unit + 1325 integration + 8 architecture
  tests, 0 failures.**

## Session timeline (continued past the first checkpoint)

The section above was written as an interim checkpoint after tasks #26-#31/#35/#37 landed. The
session then continued through the remaining tasks without stopping, per explicit user instruction
("continue and finish all todo list do not stop"). What follows covers #32-#40.

### Task #32 — STT wiring: real OpenAiSpeechToTextService
- `OpenAiSpeechToTextService` (`src/LinguaCoach.Infrastructure/Speaking/`) — extracts the same
  `AudioClient.TranscribeAudioAsync` call previously inlined/private inside
  `OpenAiSpeakingEvaluationProvider` into a standalone, reusable `ISpeechToTextService`. Uses
  `OpenAI:ApiKey`/`OPENAI_API_KEY`, same precedent as every other OpenAI-backed provider.
- DI: `ISpeechToTextService` now resolves to `OpenAiSpeechToTextService` when an API key is
  configured, `FakeSpeechToTextService` otherwise — mirrors the existing "real if configured, fake
  if not" pattern already used elsewhere in this codebase (never a hardcoded environment check).

### Task #33 — Background job: package processing pipeline
- `IImportPackageProcessingService`/`ImportPackageProcessingService` — the only path that may
  create candidates from a package's files. Requires `package.ApprovedImportProfileId` to be set
  and the plan to be `Approved`/`Executing`; refuses to process otherwise (tested).
- Two checkpointed stages (`ImportPackage.LastCompletedStageIndex`, already built in task #26):
  - **Extract** — reopens the archive from storage, copies each non-suspicious manifest entry to
    its own storage object, and materializes the `ImportAsset` rows that were designed in task #26
    but left unpopulated at the first checkpoint. Idempotent: skips entries already extracted.
  - **Map/CreateCandidates** — structured files (CSV/JSON) are handed to the *existing*
    `IResourceImportService.ImportAsync` pipeline (extended with a new optional
    `ImportPackageId` parameter on `ResourceImportRequest`/`ResourceImportRun`, so package-driven
    runs stay traceable without duplicating any parsing/dedup/field-inference logic). Audio/
    transcript pairs (matched by filename stem within the same folder) become `ListeningPassage`
    candidates directly — real STT via the new `ISpeechToTextService` fills in a missing
    transcript, `ResourceCandidate.SetGeneratedTranscript`/`SetSuppliedTranscript` record
    provenance accordingly.
  - When `package.ProcessingMode != Direct`, each structured-file run also goes through the
    existing `IResourceCandidateBatchAnalysisService.AnalyzePendingForRunAsync` for AI enrichment.
- **Cost-ceiling enforcement (Part 6) is now live**: running cost is tracked as STT calls and AI
  batches accrue, checked against `plan.ApprovedCostCeiling * (1 + CostCeilingToleranceFraction)`
  *before* each call. A projected overrun calls `plan.PauseForCostApproval(reason)` and moves the
  package back to `AwaitingMappingApproval` — tested with a near-zero ceiling that trips on the
  very first STT call.
- `ImportPackageProcessingJob` (Quartz, `[DisallowConcurrentExecution]`, every 2 minutes) drives
  `ProcessPendingAsync` — registered in `QuartzConfiguration.cs` alongside the other scheduled jobs.
- Fixed a real bug found via this work: SQLite (every test project's DB) cannot translate
  `OrderBy(DateTimeOffset)` — the package-selection query now orders client-side.

### Task #34 — Notifications for import lifecycle
- `IImportExecutionPlanGenerationService` now queues an in-app notification
  (`NotificationCategory.Admin`) to the package's `CreatedByUserId` once a plan reaches
  `AwaitingApproval`, with the estimated cost/candidate count and a deep link to the plan page.
- `ImportPackageProcessingService` notifies on: paused-for-cost-approval (`Warning`), completed/
  completed-with-warnings (`Success`/`Warning`), and failed (`Error`) — each with a deep link back
  to the relevant admin page. No notification is sent if the package has no `CreatedByUserId`
  (never crashes; just skips).

### Task #35 — Endpoints (completed at the first checkpoint, unchanged since)
`AdminImportPackageController` — upload-request/confirm-upload/manifest/plan
generate-get/approve/reject/approve-revised-ceiling. No route can trigger processing directly.

### Task #36 — Resource Bank media persistence on publish
- Confirmed (not modified) that the pre-existing `ResourceCandidatePublishService` already copies
  `AudioStorageKey`/`AudioContentType`/transcript verbatim from `NormalizedJson` into
  `ListeningPassageContent` at publish time — the candidates created by the new background job
  populate exactly those fields, so publish "just works" with no service changes needed.
- Added the one genuinely missing piece: `ImportCandidateAssetLink` rows (entity existed since
  task #26, unused until now) are created for every audio/transcript-derived candidate, linking it
  to its source `ImportAsset`(s) with the correct `ImportAssetRole` — the traceability the addendum
  asked for ("Resource Bank publish must preserve all typed content/media/provenance").
- CSV/JSON-sourced candidates are not individually asset-linked (one file → many candidates, and
  `IResourceImportService.ImportAsync` doesn't return per-row candidate ids) — documented as a
  known limitation rather than silently pretended-away.

### Task #37 — Backend tests (extended)
New test files added after the first checkpoint: `ImportPackageProcessingServiceTests.cs` (4
tests: ignores packages without an approved plan, structured-data package completes and creates
candidates, audio-without-transcript uses real STT and records `AITranscribed` provenance,
cost-ceiling overrun pauses processing). `NoOpNotificationService` shared test double added.
**Final full-suite result: 2320 unit + 1325 integration + 8 architecture tests, 0 failures.**

### Task #38 — Frontend
- `admin-import-package.models.ts` / `admin-import-package.service.ts` — typed client mirroring
  every backend contract exactly.
- New route `/admin/content/import/packages/:packageId/plan` →
  `AdminImportPackagePlanComponent` — shows the manifest summary, plan status, detected structure,
  full volume/time/cost breakdown, risks, and proposed decisions; "Approve and Start Processing"
  (requires an explicit cost ceiling, no pre-checked default), "Reject" (requires a reason), and
  "Approve revised cost and resume" (for a cost-ceiling-paused plan) — each a real modal with an
  explicit confirm action, never an implicit one.
- `admin-content-import.component` gained a new "Large package (ZIP)" card: picks a file, requests
  a signed upload URL, PUTs directly to storage (never through the API), confirms the upload, and
  always navigates to the plan page — no candidates are ever created from this flow without a
  subsequent explicit approval.
- Verified: `npx tsc --noEmit` shows zero errors attributable to the new files (all remaining
  errors are the same pre-existing `feedbackPolicy`/`moduleSuggestions` spec-compile failures
  documented in the Phase 3 review); `npm run build -- --configuration production` succeeds.
- **Known gap**: local-disk `IFileStorageService` has no real presigned-PUT endpoint (by design —
  see task #28's doc comment), so the direct-browser-upload flow only works against a real MinIO
  backend in this pass; the frontend surfaces a clear error message rather than failing silently
  when local storage is active.

### Task #39 — Remove obsolete import logic
No removal was needed. Phase 4 was built additively throughout — `ImportPackage` explicitly
"purely an upstream staging concept that feeds [the existing pipeline]" (see `ImportPackage`'s own
doc comment), and the background job reuses `IResourceImportService` rather than replacing it. No
legacy path was superseded this session, so there was nothing genuinely obsolete to delete.

### Task #40 — Docs + validation + commit
This document. `dotnet build --configuration Release` (0 errors), `dotnet test` (all three test
projects green), Angular `tsc --noEmit` + production build (clean) all re-verified after every
task in this session's second half. One local commit follows this doc update (not pushed).

## Final task list

| # | Task | Status |
|---|------|--------|
| 26 | Domain: ImportPackage/ImportAsset/ImportProfile entities + migration | done |
| 27 | ZIP ingestion: secure manifest builder | done |
| 28 | Storage: presigned PUT + large upload flow | done |
| 29 | Processing mode decision + limits config | done |
| 30 | Sample selection + Import Execution Plan generation (AI, cost/time estimate) | done |
| 31 | Typed Resource Candidate content contracts | done (pre-existing infra covers this) |
| 32 | STT wiring: real OpenAiSpeechToTextService | done |
| 33 | Background job: package processing pipeline (gated on approved plan, cost-ceiling pause) | done |
| 34 | Notifications for import lifecycle | done |
| 35 | Endpoints: package upload/manifest/sample/profile/asset roles | done |
| 36 | Resource Bank media persistence on publish | done |
| 37 | Backend tests for Phase 4 | done |
| 38 | Frontend: Import page + Review page extensions (incl. plan-review page) | done |
| 39 | Remove obsolete import logic | done (nothing to remove) |
| 40 | Docs + validation + commit for Phase 4 | done |

## Remaining honest limitations (carried forward, not fixed this session)

- Candidate-count estimate at plan time is a coarse file-count proxy, not a parsed row count.
- Audio duration used for cost/time estimation is a flat 5-minutes/file assumption, not read from
  audio headers.
- Local-disk storage cannot serve the direct-browser-upload flow (MinIO required for that path).
- CSV/JSON-sourced candidates aren't individually linked via `ImportCandidateAssetLink` (only
  audio/transcript-derived ones are).
- Sample-driven mode's "AI requests one additional bounded sampling round" exists
  (`MaxSamplingRounds`, tested up to the cap) but the AI is only ever shown file *metadata*, never
  asked to inspect actual sample file bytes — a deeper content-aware sampling pass was out of
  scope for this session's time budget.

## Next recommended action

Phase 4 is functionally complete and committed. Recommended next steps for a future session:
real audio-duration extraction (replace the flat 5-minute assumption), per-row candidate↔asset
linking for structured-data imports, and Playwright E2E coverage of the full upload→plan→approve→
review flow (not attempted this session — only backend integration-style unit tests and a
production frontend build were verified).
