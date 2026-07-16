# Phase 4.6 — Media Review and Downstream Discovery

**Date:** 2026-07-16
**Related sprint/feature:** Import Package → Typed Resource Candidate → Candidate Review → Approve
→ Publish → Resource Bank → Lesson generation pipeline. Direct follow-up to Phase 4.5 (typed
multimodal candidate schemas, `docs/reviews/2026-07-16-phase-4-5-typed-multimodal-candidate-schemas-review.md`)
and Phase 4.4E (real audio duration measurement).
**HEAD before work:** `33c23ae4` (feat: add typed resource candidate schemas)

## Scope

Full brief attempted as a single bounded effort, matching the Phase 4.5/4.4E precedent: thread
`ImportAsset.AudioDurationSeconds` through to `ResourceCandidate` → candidate preview → published
`ListeningPassageContent` → `LessonResourceSnapshot`; add safe media metadata to Candidate Review;
add authenticated audio access + media metadata to the Resource Bank; extend the Lesson resource
lookup with typed media discovery fields (data only — Lesson generation itself stays
text-composition only, per the Phase 4.5 audit finding).

## Files reviewed (read before writing, not re-audited per the brief)

`ImportAsset.cs`, `ImportCandidateAssetLink.cs`, `ResourceCandidate.cs`, `CandidateContentContracts.cs`,
`ResourceBankItemContent.cs`, `ResourceCandidatePublishService.cs`, `ResourceCandidateAudioService.cs`
+ `ResourceCandidateAudioContracts.cs`, `ImportAssetProvenanceGuard.cs`, `IFileStorageService.cs`,
`ImportAssetAudioDurationResolver.cs`, `LessonResourceLookup.cs`, `AdminResourceImportController.cs`,
`AdminResourceBankController.cs`, `ResourceCandidatePreviewService.cs` + `ResourceCandidatePreviewContracts.cs`,
`UnifiedResourceBankContracts.cs`, `ResourceBankQueryContracts.cs`, `ResourceBankQueryService.cs`,
`ImportPackageProcessingService.cs` (Listening-candidate creation path),
`admin-import-run-candidates.component.ts/html`, `admin-resource-bank-detail.component.ts/html`,
`admin-resource-import.models.ts`, `admin-resource-import.service.ts`, `ResourceCandidateConfiguration.cs`.

## Files changed

Backend:
- `src/LinguaCoach.Domain/Entities/ResourceCandidate.cs` — new `AudioDurationSeconds` property +
  `SetAudioDuration` (null-safe, blocked post-publish, mirrors `AttachAudio`'s guard).
- `src/LinguaCoach.Persistence/Configurations/ResourceCandidateConfiguration.cs` — column mapping.
- `src/LinguaCoach.Persistence/Migrations/20260716205025_Phase_4_6_AddResourceCandidateAudioDuration.{cs,Designer.cs}`
  — one nullable `numeric` column, generated via `dotnet ef migrations add` (never hand-written).
- `src/LinguaCoach.Application/ResourceImport/ResourceBankItemContent.cs` — `ListeningPassageContent`
  gained `AudioDurationSeconds` (nullable, optional trailing param — no existing call site broke).
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidatePublishService.cs` — copies
  `candidate.AudioDurationSeconds` into `ListeningPassageContent` at publish time.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageProcessingService.cs` — the audio
  duration is now resolved (best-effort, non-billing) on the supplied-transcript path too (it was
  previously only resolved on the STT path, for cost purposes) and threaded onto the created
  candidate via `SetAudioDuration`.
- `src/LinguaCoach.Infrastructure/Lessons/LessonResourceLookup.cs` — `LessonResourceSnapshot` gained
  `MediaType`/`AudioStorageKey`/`AudioContentType`/`AudioDurationSeconds`/`ImageUrl`, populated for
  Listening/Speaking, null for the rest. Discovery only — no consumer reads these yet.
- `src/LinguaCoach.Application/ResourceImport/ResourceCandidatePreviewContracts.cs` — new
  `ResourceCandidateMediaState` enum (`Ok`/`Missing`/`Invalid`/`Unsupported`/`Unavailable`) and
  `ResourceCandidateMediaMetadataDto` (filename/type/size/duration/provenance — never a raw storage
  key); `ResourceCandidatePreviewDto` gained `Media`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidatePreviewService.cs` — builds `Media`
  for `ListeningPassage` candidates by joining `ImportCandidateAssetLink`→`ImportAsset` (Role=Audio);
  reuses `IResourceCandidateAudioService.IsAllowedMimeType` rather than a second allowlist.
- `src/LinguaCoach.Application/ResourceImport/UnifiedResourceBankContracts.cs` — `UnifiedResourceBankItemDto`
  gained `HasAudio`/`AudioContentType`/`AudioDurationSeconds`/`ImageUrl` (no raw storage key);
  `ResourceBankItemEditDto` gained `AudioStorageKey`/`AudioContentType`/`AudioDurationSeconds`
  (informational, read-only — never accepted back by `UpdateResourceBankItemCommand`).
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceBankQueryService.cs` — `MapListening`/
  `MapSpeaking`/`GetEditDtoAsync` populate the new fields; `MapListening` previously silently
  dropped `AudioStorageKey`/`AudioContentType` entirely.
- `src/LinguaCoach.Application/ResourceImport/ResourceBankMediaContracts.cs` (new) — `IResourceBankMediaService`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceBankMediaService.cs` (new) — mirrors
  `ResourceCandidateAudioService`'s signed-URL/stream-fallback pattern exactly; storage key is
  always read from the row's own `ContentJson`, never client-supplied.
- `src/LinguaCoach.Api/Controllers/AdminResourceBankController.cs` — new
  `GET /api/admin/resource-bank/{id}/audio-url` and `GET /api/admin/resource-bank/{id}/audio`.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registers `IResourceBankMediaService`.

Frontend:
- `src/LinguaCoach.Web/src/app/core/models/admin-resource-import.models.ts` — `ResourceCandidateMediaState`/
  `ResourceCandidateMediaMetadataDto`, `ResourceCandidatePreviewDto.media`, new `UnifiedResourceBankItemDto`/
  `ResourceBankItemEditDto` fields.
- `src/LinguaCoach.Web/src/app/core/services/admin-resource-import.service.ts` — `AdminUnifiedResourceBankService.getAudioUrl`/`getAudioBlobUrl`.
- `admin-import-run-candidates.component.html` — Listening preview now renders a "Media details"
  block (state/filename/type/size/duration/provenance) alongside the existing audio player.
- `admin-resource-bank-detail.component.ts/html` — new "Audio" section (player + format + duration +
  transcript) for Listening items, "Prompt image" section for Speaking items with an `imageUrl`.

Tests (backend): `ResourceCandidateReviewWorkflowTests.cs` (+4 `SetAudioDuration` domain tests),
`ImportPackageProcessingServiceTests.cs` (+1), `ResourceCandidatePublishServiceTests.cs` (+2),
`ResourceCandidatePreviewServiceTests.cs` (+5, new `ResourceCandidateMediaState` coverage),
`UnifiedResourceBankQueryServiceTests.cs` (+2), `ResourceBankMediaServiceTests.cs` (new, 6 tests),
`LessonResourceLookupTests.cs` (new, 4 tests), `AdminResourceBankMediaEndpointTests.cs` (new, 5
integration tests).

Tests (frontend): `e2e/candidate-review-media-review.spec.ts` (new Playwright flow).

## Findings, grouped by priority

**High — fixed:**
- `ResourceBankQueryService.MapListening` silently dropped `AudioStorageKey`/`AudioContentType` for
  every published Listening item's list/detail view — no way for an admin to know a published
  Listening resource had audio at all short of opening the edit form. Fixed.
- No endpoint existed to actually play a published Listening resource's audio from the Resource
  Bank (only the pre-publish candidate had one). Fixed with the same secure pattern.
- `AudioDurationSeconds` was measured (Phase 4.4E) but stranded on `ImportAsset`, never reaching the
  candidate, the published resource, or the Lesson lookup. Fixed end-to-end.

**Medium — judgment calls made (documented, not left silent):**
- `UnifiedResourceBankItemDto` gets `HasAudio`/`AudioContentType`/`AudioDurationSeconds`, not the raw
  `AudioStorageKey` — the brief's literal wording listed `AudioStorageKey` but the established
  security convention in this codebase (candidate audio) never puts a raw storage key in a DTO;
  playback always goes through a signed-url-or-stream endpoint. `ResourceBankItemEditDto` (admin-only,
  informational) does carry `AudioStorageKey` per the brief's explicit parity-with-`ImageUrl` ask,
  but it is read-only — never accepted back by `UpdateResourceBankItemCommand` (replacing published
  audio was explicitly out of scope: "no upload infrastructure").
- `ImportPackageProcessingService`'s supplied-transcript path (no STT) now also attempts a
  best-effort duration measurement, non-blocking on failure (logged and swallowed) — unlike the
  STT path, where a measurement failure blocks (because that path's cost basis depends on it). This
  asymmetry is deliberate: duration threading must never regress the existing STT/cost-ceiling
  behavior that Phase 4.4E built and tested.
- `ResourceCandidateMediaState.Unavailable` is defined but never produced by the preview DTO (which
  never performs a live storage round-trip, to keep preview requests cheap) — reserved for a future
  live-storage-check caller. Documented in the enum's own doc comment rather than left unexplained.

**Low:**
- Speaking's `ImageUrl` now also surfaces on `UnifiedResourceBankItemDto` (list/detail), not only the
  edit DTO — a one-field addition, no new upload plumbing (per the explicit scope boundary).

## Decisions made

1. No new upload infrastructure for images (Speaking's `ImageUrl` stays a plain URL string) — only
   surfaced further downstream, exactly as scoped.
2. Replacing a **published** Resource Bank item's audio file is out of scope — `AudioStorageKey` on
   the edit DTO is informational only.
3. Media metadata computation in Candidate Review preview stays free of live storage I/O (cheap,
   every-request-safe); the Resource Bank's new streaming endpoint does perform a real
   `IFileStorageService.ExistsAsync` check (`ResourceBankMediaService.GetAudioStreamAsync`), matching
   `ResourceCandidateAudioService`'s own precedent.
4. Duration threading for the supplied-transcript (no-STT) Listening path is best-effort/non-blocking
   — chosen over hard-blocking to avoid narrowing an already-working, tested pipeline.

## Implementation tasks produced

None outstanding — the full brief's backend and Angular-detail-view scope was implemented and
tested in this session.

## Risks / unresolved questions

- `TODO-4.5-ZIP-CROSS-PACKAGE-UI`/`TODO-4.5-GENERIC-CSV-STRICT-VALIDATION` (carried over from Phase
  4.5) remain open — unrelated to this phase's media work.
- Angular Karma unit tests remain blocked by the same pre-existing baseline compile errors
  (`feedbackPolicy`/`moduleSuggestions`) every phase this session has hit — confirmed via `tsc
  --noEmit` that this phase's own changed files introduce zero new errors.
- No live ffprobe binary in this environment (carried over from Phase 4.4E) — duration threading was
  verified against the fake probe used by the existing test harness; the real-ffprobe happy path
  remains `TODO-4.4E-FFPROBE-HAPPY-PATH-VERIFICATION`.

## Critical test coverage (mapped to the brief's 14-item list)

| # | Requirement | Test(s) |
|---|---|---|
| 1 | Listening candidate returns its same-package audio asset metadata | `Listening_candidate_with_valid_audio_reports_Ok_media_state_with_duration_and_provenance` |
| 2 | Cross-package asset reference rejected | Pre-existing `ImportAssetProvenanceGuardTests` (confirmed still enforced/passing; guard unchanged) |
| 3 | Wrong media type rejected | Pre-existing `Upload_rejects_an_unsupported_mime_type` (candidate audio endpoint) + new `Listening_candidate_with_unsupported_content_type_reports_Unsupported_media_state` (preview) + new `Listening_candidate_with_a_linked_asset_of_the_wrong_media_type_reports_Invalid_media_state` |
| 4 | Missing required audio blocks approval/publication | Pre-existing `Listening_candidate_without_uploaded_audio_cannot_publish` (unchanged, still passing) |
| 5 | Candidate Review can securely stream permitted audio | Pre-existing `Upload_then_fetch_audio_url_and_stream_round_trips_the_same_bytes` (confirmed still passing) |
| 6 | Unauthorized media access rejected (candidate + Resource Bank) | Pre-existing `Upload_Unauthenticated_Returns401` + new `GetAudioUrl_Unauthenticated_Returns401`/`GetAudio_Unauthenticated_Returns401` |
| 7 | Media endpoint cannot access an arbitrary storage key | New `GetAudioStream_for_one_listening_resource_never_returns_another_resources_audio` + `Two_published_listening_resources_never_cross_resolve_each_others_audio` |
| 8 | Valid Listening candidate publishes audio+duration+transcript provenance | New `Publishing_listening_candidate_preserves_audio_duration_into_ListeningPassageContent` + pre-existing `Publishing_listening_candidate_with_audio_creates_exactly_one_row_with_mapped_fields` |
| 9 | Resource Bank detail returns published audio/duration/transcript | New `Unified_listening_row_surfaces_audio_metadata_but_not_the_raw_storage_key` + `Resource_bank_detail_surfaces_published_audio_and_transcript_metadata` (integration) |
| 10 | Lesson resource lookup returns audio/duration for Listening | New `FindAsync_surfaces_audio_storage_key_content_type_and_duration_for_a_published_listening_resource` |
| 11 | Missing media produces a typed result, not a crash | `Listening_candidate_without_audio_reports_Missing_media_state` + `GetAudioUrl_returns_null_for_a_non_existent_resource`/`_non_listening_resource` |
| 12 | Existing typed candidate validation tests still pass | Full suite: 2422/2422 unit pass (Phase 4.5 baseline unaffected) |
| 13 | Existing plan/cost-accounting/candidate-review/publish tests still pass | Full suite: 1331/1331 integration pass |
| 14 | Resource-to-Exercise media generation absent | Confirmed via existing `ExercisePipelineBoundaryTests` (Phase 2 precedent) — still passing (26/26 architecture tests); no new test needed, per the brief's own instruction not to duplicate an existing guard. |

## Final verdict

The full brief was implemented: duration threading, Candidate Review media metadata, Resource Bank
media/query surfacing with a new secure streaming endpoint, and Lesson-lookup media discovery — all
backed by real tests, zero regressions across 2422 unit / 1331 integration / 26 architecture tests,
one clean additive EF migration, and a passing focused Playwright flow. Angular Karma remains blocked
by a pre-existing, unrelated baseline issue (confirmed via `tsc --noEmit`, not this phase's fault).

## Next recommended action

Pick up `TODO-4.4E-FFPROBE-HAPPY-PATH-VERIFICATION` if/when a real ffprobe binary becomes available
in CI; otherwise this phase is complete. If a future phase wants to let admins replace a published
Listening item's audio, that would need real upload plumbing on `AdminResourceBankController`
(currently intentionally absent).
