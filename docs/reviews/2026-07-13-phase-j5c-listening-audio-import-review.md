# Phase J5c: Listening content-type import (real audio-file upload)

**Date:** 2026-07-13
**Related:** Phase J5 (J5a `docs/reviews/2026-07-13-phase-j5a-writing-content-import-review.md`,
J5b `docs/reviews/2026-07-13-phase-j5b-mixed-content-import-review.md`)
**Type:** Implementation review

## Trigger

Third of the four J5 passes. Unlike J5a/J5b, the user made an explicit scope decision up front
(AskUserQuestion, recorded in the J5a review): Listening import should support **real uploaded
audio files**, not transcript-only text (which would have reused the existing TTS-at-runtime
pattern and been far cheaper). This made J5c the largest of the four passes — it required new
storage columns, a new admin upload/playback flow, and a migration, not just a new enum value.

## Scope audit (before implementation)

Researched what already exists to reuse rather than invent:
- `IFileStorageService` (MinIO in prod, local disk in dev, fake in-memory in tests) is a mature,
  already-used abstraction — `SpeakingAudioService`/`ListeningAudioService`/
  `AdaptivePlacementAudioService` all use it today.
- The real upload precedent is `ActivityController.SubmitSpeakingAttempt` — multipart `IFormFile`,
  mime-type allowlist, size cap, `[RequestSizeLimit]`.
- `ResourceCandidate`/`ResourceRawRecord` had no binary/blob-reference column — a schema change
  was unavoidable.
- No existing multi-file-in-one-request precedent anywhere in this codebase.

**Decision:** lowest-risk shape — stage a Listening candidate as ordinary title/transcript text
first (reusing the existing single-file JSON/CSV import path unchanged), then upload the real
audio file **separately, per-candidate**, via a new endpoint modeled directly on
`ActivityController`'s existing single-file multipart pattern. This needed exactly one schema
change (two nullable columns on `ResourceCandidate`) and zero new multi-file-upload machinery.

## Changes made

### Backend
- **Domain**: `ResourceCandidateType.ListeningPassage`, `PublishedResourceType.Listening`; new
  `ResourceCandidate.AudioStorageKey`/`AudioContentType` columns +
  `AttachAudio(storageKey, contentType)` method (blocked once published — audio is immutable
  post-publish, mirroring `Reject()`'s same `IsPublished` guard).
- **Migration**: `Phase_J5c_AddResourceCandidateAudioColumns` (via `dotnet ef migrations add`, per
  project convention — never hand-written) — two nullable `character varying` columns on
  `resource_candidates`. Applied automatically on `docker compose up --build` startup; confirmed
  present in the running dev database.
- **`ListeningPassageContent`** record (Title/Transcript/AudioStorageKey/AudioContentType/
  AttributionText) in `ResourceBankItemContent.cs`. Named with a `Passage` suffix (not plain
  `ListeningContent`) to avoid a real compile-time collision with an unrelated, already-existing
  `LinguaCoach.Infrastructure.Activity.ListeningContent` class (TTS-generated listening audio for
  student activities — a different, older feature with the same short name).
- **New `IResourceCandidateAudioService`** (`ResourceCandidateAudioService`) — upload/get-signed-
  URL/get-stream, reusing `IFileStorageService` directly (no temp/commit two-phase step needed,
  unlike `SpeakingAudioService`'s STT pipeline — no partial-success flow to roll back here). Same
  mime-type allowlist and a 20 MB cap, matching the speaking-audio precedent.
- **`ResourceImportService`**: recognizes a `transcript` field for staging (forced-type extraction
  and per-row inference), inserted alongside J5a's `prompt` field with no overlap.
- **`ResourceCandidatePublishService`**: new **audio-required publish gate** — a ListeningPassage
  candidate cannot publish until `AudioStorageKey` is set, with a clear, specific error message.
  This is the one genuinely new *behavioral* gate added in J5 so far (J5a/J5b only added new
  content shapes, not new gates).
- **`ResourceCandidatePreviewService`**: new preview case showing title/transcript/`hasAudio`, with
  a preview warning when no audio is attached yet.
- **`ResourceBankQueryService`**: unified Resource Bank mapping for the new type, same pattern as
  every other type.
- **3 new endpoints** on `AdminResourceCandidateController`: `POST {id}/audio` (upload),
  `GET {id}/audio-url` (signed URL, with a `local://`/`fake://` → `/audio` streaming-endpoint
  fallback, same convention `ActivityController.GetAudioUrl` already uses), `GET {id}/audio` (raw
  stream fallback for local storage).
- `AdminContentImportController`: `"listening"` added to the resource-type string map.

### Frontend
- "Listening" added to the Content Import type dropdown, the unified Resource Bank type/skill
  filters, and `RESOURCE_CANDIDATE_TYPES`/`RESOURCE_PUBLISH_SUPPORTED_TYPES`.
  `CONTENT_IMPORT_COMING_SOON_TYPES` now reads just `Speaking`.
- Preview drawer (both `admin-content-import` and `admin-import-run-candidates` — duplicated since
  J4B follow-up) gained a `ListeningPassage` case: title, transcript, an inline `<audio controls>`
  player when audio is attached, a warning when it isn't, and a file input to upload/replace audio
  directly from the drawer.
- **Real bug found and fixed during live QA** (not anticipated in the design): a signed MinIO URL
  can be bound directly to `<audio src>` (it carries its own auth token in the query string), but
  the local-storage streaming-endpoint fallback is a same-origin `/api/...` path behind
  `[Authorize(Roles = "Admin")]` — a plain HTML `<audio src>` cannot send a Bearer token, so it
  401'd. Fixed by adding `AdminResourceCandidateService.getAudioBlobUrl()` (fetches via
  `HttpClient` with `responseType: 'blob'`, authenticated, then `URL.createObjectURL()`), reusing
  the exact pattern this codebase already established for the same problem in
  `ActivityService.getAudioBlobUrl()`/`PlacementService`. The preview component now checks whether
  `audio-url`'s response is an absolute `http(s)` URL (bind directly) or a relative API path (fetch
  as an authenticated blob instead).

## Decisions made

1. **Per-candidate audio upload, not manifest+N-files-in-one-request.** No precedent existed for
   the latter in this codebase; the former reuses `ActivityController`'s exact single-file pattern
   and keeps staging (text) and audio (binary) as two independently-retryable steps.
2. **`ListeningPassageContent`, not `ListeningContent`.** Forced by a genuine C# namespace
   collision with an existing, unrelated class — resolved by renaming rather than aliasing, since
   the new name is also clearer (matches `ReadingPassageContent`'s naming convention).
3. **Audio-required publish gate.** A Listening resource with no audio would be dishonest about
   what was published — matches this codebase's established precedent (the
   `ActivityTemplateCandidate` publish-block reasoning already in
   `ResourceCandidatePublishService`'s class doc comment).
4. **Not wired into Lesson/Exercise/Module generation**, same as Writing (J5a) — `Listening` is
   excluded from `TYPES_SUPPORTING_GENERATION` in the Resource Bank unified page, hiding the
   Generate actions rather than showing ones that would fail server-side.
5. No AskUserQuestion needed mid-implementation — the one true scope decision (real audio vs.
   transcript-only) was already made by the user before this pass started.

## Implementation tasks produced

None outstanding for J5c. J5d (Speaking) remains — expected to be even harder than J5c per the
original audit (no import path exists anywhere for *reference* speaking-prompt audio; `SpeakingTurn`
only scores a *student's* spoken answer).

## Risks or unresolved questions

- **The blob-URL fix only covers the admin candidate-preview player added in this phase.** The
  same underlying risk (native `<audio src>` can't send Bearer tokens against the local-storage
  streaming fallback) likely exists in older, unrelated student-facing audio paths too
  (`ActivityController.GetAudio`) — out of scope to audit/fix here, flagged for awareness only.
  Confirmed via `AudioPlayerComponent`'s plain `[audioUrl]` binding, which relies on the caller
  having already resolved a genuinely fetchable URL — in production (MinIO) this is a non-issue
  since signed URLs work directly; the risk is specifically local-storage dev/self-hosted
  deployments.
- Same pre-existing AI-subskill-classification limitation from J5a/J5b (AI-guessed subskill
  strings don't always exactly match the curriculum taxonomy) surfaced again during live QA on a
  Listening candidate — not new, not fixed here, documented consistently across all three reviews.
- 20 MB audio size cap and the mime-type allowlist are hardcoded constants (matching the speaking-
  audio precedent's magnitude) rather than configurable — acceptable for this phase; revisit if a
  real content team hits the ceiling.

## Verification

- `dotnet build` — clean, 0 errors.
- `dotnet ef migrations add` — used (not hand-written), migration applied automatically on
  `docker compose up --build`; confirmed `audio_storage_key`/`audio_content_type` columns exist in
  the running dev Postgres via `\d resource_candidates`.
- `npx tsc --noEmit` / `npm run build -- --configuration production` — clean; only the pre-existing
  unrelated bundle-budget error remains (confirmed present on `main` before this work, in prior
  J-track reviews).
- `dotnet test` (full suite) — **3,490 passed, 0 failed**: 5 architecture, 2,166 unit (+18 new:
  import-inference, publish-gate success/failure, `ResourceCandidateAudioService` upload/URL/
  stream/mime-type coverage using `FakeFileStorageService`, unified-bank aggregation), 1,319
  integration (+6 new: upload/playback round-trip, 404-when-nothing-uploaded, unsupported-mime-
  type rejection, wrong-candidate-type rejection, and the full publish-blocked-then-succeeds flow
  — plus a J5b test fix, since it had used `"listening"` as its "still unsupported" placeholder,
  now repointed to `"speaking"`).
- Live browser smoke test (gstack `browse`, admin session, API container rebuilt and confirmed
  healthy, migration confirmed applied):
  - "Listening" appears in the Content Import dropdown; staged a real row (title + transcript +
    CEFR) as `ListeningPassage`.
  - Preview drawer correctly showed the "no audio uploaded — cannot publish" warning and a file
    input.
  - Uploaded a real (fake-content) audio file through the drawer — **found and fixed the 401 blob-
    URL bug described above** during this step.
  - Reloaded the page, reopened the preview from Import History → the run's own candidates page —
    audio persisted and the `<audio controls>` player rendered with **zero console errors**.
  - Ran Analyze → Approve & Publish: publish correctly blocked (pre-existing AI-subskill mismatch,
    not the audio gate — the audio gate itself is proven by the automated tests' explicit
    audio-required-then-succeeds assertions).

## Final verdict

J5c (Listening, real audio-file upload) is implemented and verified, including a real bug found
and fixed during live QA that automated tests alone would not have caught (the blob-URL auth
issue only manifests with an actual browser `<audio>` element against the local-storage fallback).
Ready to commit locally.

## Next recommended action

Commit this change locally (no push/deploy). J5d (Speaking) is the last J5 pass — recommend a
fresh scoping pass before starting, since "importing a reference speaking prompt" is a genuinely
different shape of work than everything built so far (J5a-c all import *content the student reads
or listens to*; Speaking would need to define what a *reference* for a task the student *speaks*
even means — likely just a text prompt, closer to Writing's shape than audio-upload's).
