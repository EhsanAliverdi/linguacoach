# Phase K1: AI-assisted import column-mapping — implementation

**Date:** 2026-07-13
**Related:** Scoping doc `docs/reviews/2026-07-13-ai-assisted-import-column-mapping-scoping.md`,
bug fix that triggered this `docs/reviews/2026-07-13-cefrj-import-field-recognition-bugfix-review.md`
**Type:** Implementation review

## Trigger

Following the CEFR-J bugfix, the user asked for (1) AI-based structure detection at import time
and (2) better error messages generally. Scoped as Phase K1 with the user's explicit decisions:
both flows (file-upload and paste-based Content Import), always show the review UI, bundled with
the error-message improvements into one implementation pass.

## What shipped

### Error-message improvements (no AI)
- Gate 3 ("no recognizable content field") rejection now lists the row's actual column names
  alongside the full recognized set and a concrete suggestion, instead of a fixed generic list.
- Within-run duplicate rejection now names which earlier row it matches (a content preview), not
  just "duplicate row."

### `headword`/`CEFR` recognition (the original bug's direct fix)
Already covered in the prior bugfix review — included here for completeness since it shipped in
the same session as the groundwork for K1's AI feature.

### AI-assisted column-mapping proposal
Follows the scoping doc's design exactly — mirrors `ResourceCandidateAnalysisService` (Phase E2):
AI proposes, the admin always confirms, the underlying deterministic pipeline never changes.

- **New `IResourceImportColumnMappingService`** (`ResourceImportColumnMappingService`) — one
  bounded AI call (header row + up to 5 sample rows, each field truncated to 200 chars), retried
  once on bad JSON, never throws. Every suggested field name is checked against
  `ResourceImportRecognizedFields.All` (a new shared list, kept in sync with the actual recognized
  field set in `ResourceImportService`) — an AI-hallucinated field name outside that list is
  dropped to null, never trusted.
- **New prompt** `resource_import_propose_column_mapping`, DB-seeded via `DefaultAiSeeder`
  (versioned/content-hash-addressed, same as every other prompt), category `llm.evaluation`
  (matches `resource_candidate_analyze`'s classification/judgment categorization).
- **`ResourceImportRequest`/`ContentImportRequest` gained `ColumnRenames`** (an optional
  `IReadOnlyDictionary<string,string>`) — applied as a pure header rewrite immediately after
  parsing, before Gate 1-3 or any candidate-type inference runs. This means the AI-assisted path
  and the pre-K1 path both funnel into the *exact same, unmodified* deterministic pipeline — zero
  changes to `InferCandidateType`, `ExtractCanonicalTextForType`, or any publish/preview logic.
- **New `ParseSample` method** on `IResourceImportService` — parses just the header + a bounded
  row sample, no DB writes, reusing the exact same `ParseRows`/`ParseCsvRows`/`ParseJsonRows`/
  `ParseJsonlRows` the real import uses (never a second, potentially-divergent parser).
- **New endpoints**:
  - `POST /api/admin/resource-import-runs/propose-mapping` (multipart file) — file-upload flow.
  - `POST /api/admin/content-imports/propose-mapping` (JSON body) — paste-based flow; returns a
    trivial success with no suggestions for `pasted_text` mode without an AI call, since that mode
    is synthetic single-column `{"text": line}` rows with no real header to map.
  - `POST /api/admin/resource-import-runs` and `POST /api/admin/content-imports` both gained an
    optional `columnRenames`/`columnRenamesJson` parameter, forwarded verbatim.
- **Frontend**: `AdminContentImportComponent`'s `submitPaste()`/`submitFile()` now call
  `proposeMapping()` first (skipped only for `pasted_text`), open a review modal showing every
  file column with an editable "maps to" dropdown (defaulting to the AI's suggestion, or "Leave
  as-is"), and only run the real import after "Confirm and Import." Both the file-upload and
  paste-CSV/JSON flows share this one modal, since both already funnel through this same
  component.

## Decisions made (mirroring the approved scoping doc)

1. Both flows get the review step — confirmed by the user.
2. Always shown, never auto-skipped for a "trivially correct" mapping — confirmed by the user.
   The one exception (`pasted_text` mode) isn't a UX shortcut, it's structural: that mode has no
   header row at all, so there is nothing to review.
3. AI proposes, deterministic pipeline decides — no code path exists where an AI suggestion is
   applied without the admin clicking "Confirm and Import."
4. `headword`/`cefr` (the specific bug) are already-recognized field names as of the earlier fix,
   so this session's own re-test of the exact reported file shows the AI correctly suggesting
   "Leave as-is" for every column — proving the two fixes compose correctly (the AI doesn't need
   to suggest a rename for a column that's already recognized).

## Risks / follow-up not done here

- The AI mapping service is untested against a real provider in this session beyond the one live
  smoke-test run (which returned "Leave as-is" for all 6 CEFR-J columns — a safe, correct, if
  unexciting result to observe live, since the interesting case — an *unrecognized* column getting
  a real suggested rename — wasn't directly observed with a live AI call in this session; it is
  covered by unit tests using a scripted fake provider).
- Gate 1 (English-only rejection) was not given the same "list the actual value" treatment as
  Gate 3 and the duplicate gate — its existing messages were judged already reasonably specific
  (they include the detected language/script reason) and left alone to keep this pass scoped.
- The recognized-field dropdown in the review modal is a flat list of ~20 field names with no
  grouping/search — acceptable for the current field count, would need revisiting if the
  recognized-field set grows substantially.

## Verification

- `dotnet build` — clean, 0 errors.
- `dotnet test` (full suite) — **3,511 passed, 0 failed**: 5 architecture, 2,180 unit (+13 new:
  `ResourceImportColumnMappingServiceTests` covering valid/unrecognized-field/retry/unavailable/
  empty-columns cases using the existing `SwappableFakeAiProvider` test infrastructure, plus
  `ColumnRenames` end-to-end tests in `ResourceImportServiceTests`), 1,326 integration (+6 new:
  `ResourceImportColumnMappingEndpointTests` covering both propose-mapping endpoints' auth/
  graceful-degrade/bad-file/pasted-text-skip behavior, plus a `columnRenames`-round-trip test).
- `npx tsc --noEmit` / `npm run build -- --configuration production` — clean; only the
  pre-existing unrelated bundle-budget error remains.
- Live browser smoke test (gstack `browse`, admin session, API container rebuilt and confirmed
  healthy, disk space verified before rebuild): re-imported the exact CEFR-J file that originally
  reported the bug via file-upload — the mapping-review modal correctly opened showing all 6 real
  file columns (`headword`, `pos`, `CEFR`, `CoreInventory 1`, `CoreInventory 2`, `Threshold`),
  defaulting to "Leave as-is" for each (correct, since `headword`/`CEFR` are already recognized
  post-bugfix). Confirmed and imported — **7,798/7,799 staged**, identical to the direct-import
  result from the earlier bugfix verification, proving the new review step doesn't regress the
  underlying pipeline. Zero console errors throughout.

## Final verdict

Phase K1 implemented and verified: both the error-message improvements and the AI-assisted
column-mapping feature ship together as scoped and approved. The core design goal — AI proposes,
deterministic system decides, nothing about the actual import pipeline changes — held throughout
implementation with no compromises.

## Next recommended action

Commit locally. No further planned work from this thread — the CEFR-J import bug that started
this session is fixed, and the AI-assisted mapping feature the user asked for is shipped end to
end across both import flows.
