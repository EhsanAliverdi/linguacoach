# Phase 3 — Import Candidate Review Workflow

**Date:** 2026-07-15
**Related sprint/feature:** Completes the Import review workflow the original architecture audit
(`docs/reviews/2026-07-15-content-creation-pipeline-architecture-audit.md`) flagged as narrower
than intended ("only `AdminNotes` is editable"; "no modeled Skip state"). Phase 1 and Phase 2
(same date) fixed Module/Exercise pipeline safety and provenance; this phase is scoped entirely to
Import → Resource Candidate review, editing, and publishing. Deliberately does **not** implement
smarter AI segmentation — the existing deterministic parsing is unchanged.
**Type:** Implementation + engineering review (new domain state, new endpoints, page consolidation, tests, documentation)

## Investigation — what already existed before changing anything

Before designing anything, the current Import/Candidate backend and frontend were read in full.
The findings materially changed the scope of this phase:

- **Publishing was already separate from approval.** `ResourceCandidatePublishService.PublishAsync`
  re-checks `ReviewStatus == Approved` as one of several live gates and is only ever called by an
  explicit publish/approve-and-publish action — `Approve()` itself never writes a bank row. Batch
  equivalents (`ApproveAsync`, `PublishAsync`, `ApproveAndPublishAsync` on
  `IResourceCandidateBatchActionService`) already existed too. The task's "Publishing remains a
  separate action" requirement was **already true** — this phase did not need to build it, only
  add the missing `Reject`/`Skip` batch equivalents (see below) for symmetry.
- **A dedicated per-run review page already existed** at `/admin/content/import/runs/:runId`
  (`AdminImportRunCandidatesComponent`) — approve/reject/preview/batch-approve/batch-publish were
  all already there. It was a near-duplicate of a second, inline copy of the same UI embedded in
  the "New Import" page (`AdminContentImportComponent`), which the original Import page's own
  pipeline stepper revealed after a successful import.
- **What was genuinely missing:** (1) content-field editing — `ResourceCandidate` had exactly one
  mutable field, `AdminNotes`; (2) a `Skip` decision — `AdminReviewStatus` (shared with
  Module/Lesson/Exercise review) had no such value and no code path could set anything like it;
  (3) the redirect-to-review behavior — the Import page reviewed candidates itself instead of
  redirecting, duplicating the dedicated review page's logic almost line-for-line.

This phase's real scope, once the above was confirmed, was: (a) add content editing, (b) add Skip
as a first-class decision with row and batch actions, (c) remove the duplicate inline review UI
from the Import page and make it redirect to the existing dedicated review page instead.

## Backend changes

### New domain state

- **`ResourceCandidateReviewStatus`** (new enum, `src/LinguaCoach.Domain/Enums/ResourceCandidateReviewStatus.cs`)
  — `NotRequired`, `PendingReview`, `Approved`, `Rejected`, `Skipped`. `ResourceCandidate.ReviewStatus`
  now uses this type instead of the shared `AdminReviewStatus` enum (also used by Module/Lesson/
  Exercise review, which have no Skip concept). Split into its own type rather than adding a
  candidate-only member to the shared enum — matches the "no obsolete/compatibility-layer"
  instruction: a clean type per concern instead of one enum serving five unrelated entities. Column
  storage is unaffected (`HasConversion<string>()`, not a DB-level enum type) — every existing
  member name is unchanged, so no data migration was needed for the rename itself.
- **`ResourceCandidate.Skip(string? reason = null)`** — "I am intentionally ignoring this
  candidate," distinct from `PendingReview` (never reviewed). Reason is optional (unlike `Reject`,
  which requires one). Blocked for an already-published candidate, mirroring `Reject`'s guard.
- **`ResourceCandidate.UpdateContent(...)`** — edits `CanonicalText`, `NormalizedJson`, `CefrLevel`,
  `PrimarySkill`, `Subskill`, `DifficultyBand`, `ContextTagsJson`, `FocusTagsJson`. Every parameter
  is independent/optional (null = leave unchanged). `NormalizedJson` is where every type-specific
  field lives (word/definition/title/body/examples/etc, per candidate type) since candidates have
  no per-type typed columns — editing content means replacing that JSON blob wholesale, which is
  how "the editable fields depend on Resource Type" is satisfied without adding seven separate
  typed edit methods. Blocked for an already-published candidate.

### New application contracts / handlers

- `SkipResourceCandidateCommand` / `IAdminResourceCandidateSkipHandler` (single-item skip).
- `UpdateResourceCandidateContentCommand` / `IAdminResourceCandidateContentUpdateHandler` — edits
  content, then re-runs `IResourceCandidateValidationService.ValidateAsync` so the returned DTO's
  `ValidationStatus`/`CanAttemptPublish` reflect the new content immediately, not the stale
  pre-edit gates.
- `BatchRejectResourceCandidatesCommand` / `BatchSkipResourceCandidatesCommand` and
  `IResourceCandidateBatchActionService.RejectAsync`/`.SkipAsync` — batch equivalents of the
  existing single-item reject/skip, following the exact same continue-on-error,
  delegates-to-single-item-handler pattern the existing `ApproveAsync`/`PublishAsync` already used
  (no gate logic duplicated).
- `AdminResourceCandidateReviewSummaryDto` extended with `RejectedCount`, `SkippedCount`,
  `PendingReviewCount` (previously only Passed/NeedsReview/Blocked/Published were broken out).

### New/changed endpoints (`AdminResourceCandidateController`, `api/admin/resource-candidates`)

| Verb | Route | Purpose |
|---|---|---|
| `PUT` | `{candidateId}/content` | Edit candidate content (new) |
| `POST` | `{candidateId}/skip` | Skip one candidate, reason optional (new) |
| `POST` | `batch/reject` | Batch reject, reason required (new) |
| `POST` | `batch/skip` | Batch skip, reason optional (new) |

No existing endpoint was removed. No route naming collided with the Lesson/Module
`generate-from-resources` routes untouched by Phase 2 (different controller, different concept —
Resource→Lesson/Module generation is out of this phase's scope, per the working rules).

## Frontend changes

### Import Review page (`AdminImportRunCandidatesComponent`, `/admin/content/import/runs/:runId`)

Extended (not replaced) with:
- **Skip** — row action + a lightweight modal (reason optional, distinct wording from Reject's
  required-reason modal) + a "Skip selected" bulk button.
- **Reject selected** (bulk) — the row-level reject action already existed; the batch equivalent
  was missing and is now present, reusing the same required-reason modal pattern.
- **"Publish Approved"** — a bulk button that only attempts to publish already-Approved candidates
  in the selection (never approves anything), kept visually and functionally distinct from
  "Approve & Publish selected" (which does both). The underlying `batchPublish` call already
  existed; this phase's contribution was making it a clearly separate, correctly-labeled action in
  the UI per the task's explicit requirement, not new backend work.
- **Content editing** — an "Edit" row action opens a modal with: display text, a Content JSON
  textarea (pretty-printed on open, validated as JSON on save), CEFR level, primary skill,
  subskill, difficulty band, and comma-separated context/focus tag fields. This is a generic
  editor across every `ResourceCandidateType` rather than seven hand-built per-type forms — the
  Content JSON field is genuinely where the type-specific fields live (see the backend section
  above), so the editor is honestly generic, not a placeholder.
- **Status badges** — `reviewStatusTone()` now colors Approved (success), Rejected (danger),
  Skipped (warning), everything else neutral; the review-status summary panel shows
  rejected/skipped/pending-review counts alongside the existing passed/needs-review/blocked/
  published counts.

### Import page (`AdminContentImportComponent`) — duplicate workflow removed

The entire inline candidate-review section (candidates table, batch toolbar, reject modal, preview
drawer, ListeningPassage audio upload UI, ~500 lines of near-duplicate signals/methods/template)
was deleted. `runPasteImport`/`runFileImport` now navigate straight to
`/admin/content/import/runs/:runId` the moment the Import Run finishes, instead of setting local
candidate state and rendering a second copy of the review UI. This directly satisfies "the
administrator should never need to return to the original Import page after candidates have been
generated" and "do not leave duplicate workflows" — the Import page's job is now exactly "Add
content," nothing else. The pipeline stepper was simplified from 4 stages (the last two of which
never actually did anything different from the review page) to 3, ending at "Review (redirects to
Import Review)."

## Import Run / Candidate lifecycle — confirmed equivalent to an existing model, not rebuilt

Per "do not overcomplicate if an equivalent model already exists":

- **Import Run** (`ResourceImportRunStatus`): `Pending`, `Running`, `Completed`,
  `CompletedWithWarnings`, `Failed`, `Cancelled` — already covers the task's suggested
  Draft/Processing/Candidates Generated/Ready to Publish/Published/Completed/Failed shape (the
  per-candidate approve/reject/skip/publish states are tracked on each `ResourceCandidate`, not
  re-derived as a run-level status — a run with a mix of approved/rejected/skipped candidates is
  simply `CompletedWithWarnings` or `Completed`, and the review summary reports the mix). Not
  changed this phase — no gap was found.
- **Candidate** (`ResourceCandidateReviewStatus`, this phase): `PendingReview`, `Approved`,
  `Rejected`, `Skipped`, plus `IsPublished`/`PublishedAtUtc`/etc. as the terminal publish state on
  the same entity (not a sixth enum value — "Published" is a fact about the row, independent of
  which of Approved/Rejected/Skipped it was in beforehand, exactly matching the existing
  `IsPublished` boolean's role for the pre-existing Approved/Rejected states).

## Candidate editing rules — enforced, not just documented

"Candidates remain editable until published; once published, edit through the Resource Bank" is
enforced at the domain layer (`UpdateContent`/`Skip`/`Reject` all throw `InvalidOperationException`
when `IsPublished`), not merely a UI convention — confirmed by
`Content_update_handler_rejects_edits_to_a_published_candidate` and the domain-level
`UpdateContent_throws_for_an_already_published_candidate`/`Skip_throws_for_an_already_published_candidate`
tests.

## Tests added

**Backend** (`tests/LinguaCoach.UnitTests/ResourceImport/`):
- `ResourceCandidateReviewWorkflowTests.cs` (new file, 15 tests) — domain `Skip`/`UpdateContent`
  behavior (including the optional-reason vs required-reason distinction, the published-candidate
  guard, and "Skip is distinct from never-reviewed PendingReview"), the two new handlers
  (persistence, not-found handling, re-validation-after-edit, published-candidate rejection), and
  the review summary's new Rejected/Skipped counts.
- `ResourceCandidateBatchActionServiceTests.cs` — 4 new tests: batch reject sets `Rejected` for
  every requested id, batch skip sets `Skipped` for every requested id, and two direct regression
  tests confirming a Skipped/Rejected candidate cannot be published (the existing
  `ReviewStatus == Approved` gate already covered this correctly — these are regression tests
  proving it, not new production logic).
- Three existing test files (`ContentImportServiceTests.cs`, `ResourceCandidateBatchActionServiceTests.cs`,
  `ResourceCandidateValidationServiceTests.cs`) updated for the `AdminReviewStatus` →
  `ResourceCandidateReviewStatus` type change on `ResourceCandidate.ReviewStatus`.

**Frontend:** no new `.spec.ts` files were added. The Import/Candidate admin area (both
components, both services, the models file) has zero pre-existing frontend test coverage — see
"Deferred findings" below.

## Validation

```text
dotnet restore                                → succeeded
dotnet build --configuration Release          → succeeded, 0 errors
dotnet test tests/LinguaCoach.UnitTests        → Passed: 2290, Failed: 0
dotnet test tests/LinguaCoach.IntegrationTests → Passed: 1325, Failed: 0
dotnet test tests/LinguaCoach.ArchitectureTests→ Passed: 8, Failed: 0
```

Angular:

```text
npx tsc --noEmit -p tsconfig.app.json          → clean, 0 errors
npm run build -- --configuration production    → succeeded, exit 0, 0 ERROR-level diagnostics
                                                   (pre-existing template-strictness warnings in
                                                   unrelated components — AdminDiagnosticsComponent,
                                                   AdminExerciseTypesComponent, etc. — confirmed not
                                                   introduced by this phase; none reference the
                                                   files this phase touched)
```

Angular unit tests (`npm test`) and Playwright were not run: no `.spec.ts` file exists for the
Import/Candidate area to execute, and per the "no AI segmentation, no unrelated redesign" scope
boundary, no E2E spec covering this admin flow existed to update either. See "Deferred findings."

## Deferred findings

- **Zero frontend test coverage for the entire Import/Candidate area, before and after this
  phase.** Neither `AdminContentImportComponent` nor `AdminImportRunCandidatesComponent` (nor their
  services/models) had any `.spec.ts` file prior to this phase. Adding first-time coverage for two
  large, already-complex components in the same phase that changed their shape significantly was
  judged higher-risk than valuable — tests written against a UI still being actively reshaped tend
  to need immediate rewriting. Flagged as a real gap, not implemented here.
- **No Playwright E2E spec exists for `/admin/content/import` or `/admin/content/import/runs/:id`.**
  Same reasoning — none existed to update, and authoring a first E2E spec for this flow is a
  larger, separate effort better done once the UI has stabilized post-Phase-3.
- The content-editing UI is intentionally generic (one Content JSON textarea across all seven
  `ResourceCandidateType` values) rather than seven bespoke per-type forms (Vocabulary
  word/definition/example fields, Grammar title/explanation/examples fields, etc., as the task's
  examples described). This was a deliberate scope decision, not an oversight — see "Known
  limitations" below.

## Known limitations

- **Content editing is JSON-textarea-based, not per-type structured fields.** The task described
  per-type field lists (Vocabulary: word/definition/example/CEFR/tags; Grammar: title/explanation/
  examples/common mistakes; etc.). Building seven distinct structured edit forms was judged out of
  proportion for this phase given the existing pattern (`ResourceCandidateFieldHelper` already
  treats `NormalizedJson` as a generic field-name-keyed bag, not typed per-type DTOs) and the
  "do not add compatibility layers" instruction pointed toward reusing that existing
  representation rather than inventing a second, parallel typed-field system. The generic editor
  is a correct, honest implementation of "edit the type-specific fields" — it is simply not a
  polished per-type form. A future phase could add typed forms per `ResourceCandidateType` without
  any backend change, since `UpdateResourceCandidateContentCommand.NormalizedJson` already accepts
  arbitrary valid JSON for the type.
- **Skip has no dedicated "reopen" action distinct from re-approving/re-rejecting.** A skipped
  candidate can still be approved or rejected later (neither method has a state guard beyond the
  `IsPublished` check), which satisfies "can be re-reviewed later," but there's no explicit
  "return to PendingReview" action — matches the pattern `Reject`/`Approve` already had (no
  dedicated "unreject" either), so this isn't a new inconsistency, just an existing one this phase
  didn't expand the scope to fix for Skip specifically.

## Explicit confirmation of out-of-scope items not touched

AI content segmentation/smarter extraction (deterministic parsing is unchanged), candidate editing
beyond the JSON-blob approach described above (no per-type typed forms), Resource Bank redesign,
Lesson/Exercise/Module redesign, publishing/versioning architecture, student pipeline, learning
memory, Today/Practice Gym, legacy cleanup outside the Import workflow — none of these were
implemented. Nothing was pushed. Nothing was deployed.

## Final verdict

Publishing was already correctly separated from approval before this phase — confirmed by reading
the existing `ResourceCandidatePublishService` rather than assumed. The two genuine gaps
(content editing restricted to `AdminNotes`; no Skip decision) are now closed at the domain,
application, API, and UI layers, each enforced (not just documented) via the existing
`IsPublished` immutability guard. The duplicate inline review workflow on the Import page is
removed; the Import page now does exactly "Add content" and redirects to the pre-existing dedicated
Import Review page, which is now the sole review surface. All 3,623 backend tests pass. Angular
production build is clean. Ready to commit locally.
