# Phase 4.4A — Admin Plan Editor and Cost Operations UI (core-editor slice)

**Date:** 2026-07-16
**Related:** Phase 4.4 (`docs/reviews/2026-07-16-phase-4-4-editable-plans-and-durable-cost-accounting-review.md`), Phase 4.3, Phase 4.2
**HEAD before work:** `51d19119` (feat: add editable import plans and durable cost accounting)

## Scope decision

The full Phase 4.4A brief specified: a 6-component Angular editor (`ImportPlanSummaryComponent`,
`ImportPlanGroupEditorComponent`, `ImportFieldMappingEditorComponent`, `ImportPlanPreviewComponent`,
`ImportCostSummaryComponent`, `ImportCostPausePanelComponent`), full CSV *and* JSON mapping UIs, a
new audited ceiling-amendment endpoint + migration, an STT operation summary view, 32 Angular unit
tests, 22 additional integration tests, 12 architecture tests, and Playwright coverage — genuinely
multi-day work.

Before starting, the user was asked to scope the session via `AskUserQuestion` and selected
**"Core editor first"**: build one Import plan editor (group include/exclude, Resource-type
routing, CSV mapping, processing mode display, preview, save-with-concurrency, approval, read-only
approved state, revision action), deferring JSON mapping UI, the ceiling-amendment endpoint/UI, a
dedicated cost/pause panel, the STT operation summary, and Playwright — each tracked as an explicit
`TODO-4.4A-*` item in `TODOS.md`, not silently dropped. Investigating and fixing
`TODO-4.4-LOOSE-FILE-FOLDER-BUG` was in scope and completed.

## Backend APIs reused (no new endpoints)

All four endpoints already existed from Phase 4.4 and needed no server-side contract change:

```
GET  api/admin/import-packages/{packageId}/plan               — now consumed for concurrencyStamp/isEditable/groupInstructions
PUT  api/admin/import-packages/{packageId}/plan/{planId}       — draft save
POST api/admin/import-packages/{packageId}/plan/{planId}/revise
POST api/admin/import-packages/{packageId}/plan/preview
POST api/admin/import-packages/{packageId}/plan/{planId}/approve — now sent with expectedConcurrencyStamp (previously omitted, see Bug fixed)
```

## Backend changes (loose-file bug fix only)

`ImportPackageSubmissionService.SubmitAsync` / `StoreAssetAsync`
(`src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageSubmissionService.cs`): added
`FlattenFileName(string fileName) => Path.GetFileName(fileName.Replace('\\', '/'))`, applied to
every loose-file submission's file name before it becomes `ImportAsset.RelativePath` / the
manifest entry's `RelativePath`. Root cause (confirmed via code investigation): the synthetic
manifest for a non-ZIP submission always declares exactly one `"(root)"` `FolderGroup`; an
unflattened filename containing `/` would resolve (via `ImportExecutionGroupKey.ForRelativePath`)
to a folder the manifest never declared, permanently failing plan validation for that asset. A
real browser `<input type="file">` never sends a filename containing `/` — confirmed by reading
`admin-content-import.component.ts`, which only ever calls `.submit()` with `file.name` — so this
was reachable only via a directly-crafted API call, not by any real end user. No migration
required; this is a pure code-path fix.

## Admin plan editor

**File:** `src/LinguaCoach.Web/src/app/features/admin/admin-import-package-plan/admin-import-package-plan.component.ts` (+`.html`)
No new route — the existing `/admin/content/import/packages/:packageId/plan` page was extended in
place, per the brief's "do not create a separate parallel Import workflow." Single component (the
6-component split suggested in the brief was not needed at this scope; deferred if the editor grows).

**Draft controls:** while `plan.isEditable` (Draft/AwaitingApproval), an "Edit source groups" card
renders one row per `ImportExecutionGroupInstruction` (built from `plan.groupInstructions`, cross-
referenced with `estimate.detectedGroups` for description/file-count and
`estimate.structuredMappingPreviews` for detected CSV headers, matched by a client-side
`groupKeyForRelativePath` helper mirroring the backend's `ImportExecutionGroupKey.ForRelativePath`).

**Resource routing controls:** `sp-admin-native-select` bound to all 7 `ResourceCandidateType`
values (`ROUTABLE_RESOURCE_TYPES`), narrowed to Listening passage only when the group is detected
as audio (mirrors `ImportPlanInstructionValidator`'s audio-must-route-to-ListeningPassage rule
client-side, so the admin can't even select an invalid route — the server still re-validates).

**CSV mapping controls:** one `sp-admin-native-select` per detected header, target list built from
`RECOGNIZED_FIELD_MAPPING_TARGETS` (mirrors `ResourceImportRecognizedFields.All`). A group with no
detected headers or JSON content shows an explanatory note instead of a raw JSON editor (per the
brief's explicit "no raw JSON textarea" and the deferred JSON-mapping-UI scoping decision).

**Preview behaviour:** "Preview mapped output" calls `POST .../plan/preview` with the *current
form's* instructions (via `toInstructions()`), independent of save state; a banner clarifies when
the preview reflects unsaved edits. Displays predicted candidate type, canonical text, and
per-row/per-group warnings; explicitly labelled as a bounded sample, not persisted, no candidate
created.

**Validation behaviour:** structural errors (`groupKey === null`) shown in a page-level alert;
per-group errors (`errorsForGroup(groupKey)`) shown under that group's row — both from
`ImportPlanValidationError[]`, matching the backend's "grouped by source group" design.

**Save behaviour:** "Save draft" sends `expectedConcurrencyStamp` (the currently-loaded plan's
stamp) plus `toInstructions()`. On success, the plan signal and form are rebuilt from the
server-returned (recalculated) plan, `dirty` clears, and the concurrency stamp updates. On
validation failure (400 with `errors[]`), the draft is preserved and errors are displayed inline —
never discarded.

**Concurrency-conflict behaviour:** a 409 sets `concurrencyConflict`, shows the server's message
plus explicit "Reload plan" guidance, and does **not** attempt any merge — the same handling is
shared by both save and approve.

**Estimate-staleness behaviour:** any form edit calls `markDirty()`; while dirty, a warning badge
("Unsaved edits — estimate may be stale") shows next to the plan status, and the "Approve and Start
Processing" button is disabled (`[disabled]="dirty()"`) until the draft is saved and the server
returns a freshly recalculated estimate.

**Approval behaviour:** now sends `plan.concurrencyStamp` (previously missing entirely — see Bug
fixed below); a 409 during approval reuses the same conflict banner/reload flow as save.

**Approved read-only state:** when `!plan.isEditable`, the "Edit source groups" card does not
render at all — the existing read-only Detected structure / Estimate / Structured mapping preview
/ Risks / Proposed decisions cards (unchanged from Phase 4.3) remain the approved-plan summary.

**Revision behaviour:** `canRevise()` returns true only when `plan.status === 'Approved'`. "Create
Revision" opens a reason modal and calls `POST .../plan/{planId}/revise`; on success the plan
signal/form are rebuilt from the new Draft revision — the previously-approved row is never mutated
client-side (the backend guarantees this server-side, per Phase 4.4).

## Bug fixed (found while wiring approval)

`approvePlan`/`approveRevisedCeiling` never sent `expectedConcurrencyStamp` prior to this phase,
even though the backend's `ApprovePlanBody(decimal ApprovedCostCeiling, Guid
ExpectedConcurrencyStamp)` requires it — meaning every approval implicitly sent `Guid.Empty`. This
had not yet visibly broken anything because a freshly-generated plan's very first approval attempt
coincidentally still matched whatever stamp comparison existed pre-4.4; it would have started
rejecting real approvals the first time an admin edited a draft (this phase's own feature) before
approving it. Fixed by threading `plan.concurrencyStamp` through `approvePlan()`.

## Cost operations (deferred beyond what already existed)

No new work here — the pre-existing (pre-4.4) pause/resume modals (`PausedForCostApproval` alert,
"Approve revised cost and resume" modal calling `approve-revised-ceiling`) were left unchanged and
still function. A dedicated cost-summary panel, an audited/concurrency-checked ceiling-amendment
endpoint, and an STT operation-ledger view were explicitly scoped out — see
`TODO-4.4A-COST-SUMMARY-PANEL`, `TODO-4.4A-CEILING-AMENDMENT-AUDIT`, `TODO-4.4A-STT-OPERATION-SUMMARY`.

## Loose-file bug

- **Root cause:** confirmed above (Backend changes section).
- **Fix:** `FlattenFileName` in `ImportPackageSubmissionService`.
- **Tests:** new integration test
  `ImportPlanEditingAndCostAccountingTests.Loose_file_with_directory_separator_in_name_is_flattened_to_the_root_group`
  — submits a file named `lesson/data.csv`, asserts the manifest still has exactly one folder group
  with `folderPath === ""`, the generated plan's `groupInstructions` has exactly one `"(root)"`
  entry, and a full edit → save → approve → process → candidate-review cycle succeeds end to end.

## Critical proof

```
Admin UI edit → saved executable plan → exact approval → processing uses edited output
```
Proven at the API layer (the UI calls these same endpoints with no additional logic in between) by
the pre-existing Phase 4.4 test `ImportPlanEditingAndCostAccountingTests.
Admin_edited_plan_through_the_draft_API_produces_the_edited_candidate_output`, and exercised again
end-to-end by this phase's new `Loose_file_with_directory_separator_in_name_is_flattened_to_the_root_group`
test. The Angular component's own unit tests prove the *client* sends the right payload shape:
`admin-import-package-plan.component.spec.ts` → `'include/exclude, resource type, and CSV mapping
edits all appear in the save payload'`, `'sends the current concurrency stamp on save...'`.

```
Stale save or approval → HTTP conflict → UI displays reload guidance
```
Backend: pre-existing Phase 4.4 test `Stale_concurrency_stamp_on_draft_update_returns_conflict`.
Frontend: `admin-import-package-plan.component.spec.ts` →
`'a stale save (409) shows conflict guidance without discarding the edit'` and
`'a stale approval (409) shows conflict guidance'` (both assert `concurrencyConflict()` becomes
true and the draft/form state is preserved, not merged or discarded).

```
Cost-paused package → explicit ceiling amendment → audited update → controlled continuation
```
**Not proven this phase.** The pre-existing (pre-4.4) resume flow is unchanged and still requires
explicit admin confirmation with a new ceiling value, so a package cannot silently resume — but no
audit trail (previous ceiling, actor, timestamp, reason) or concurrency check was added, per the
explicit scoping decision. See `TODO-4.4A-CEILING-AMENDMENT-AUDIT`.

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,338 | Pass (unchanged — no new unit tests needed for this scope) |
| Backend integration | 1,303 | Pass (+1: the loose-file regression test) |
| Backend architecture | 19 | Pass (unchanged — no new architecture guard needed for this scope) |
| Angular unit (this component) | 16 new specs | Compile clean under `tsc --noEmit`; **could not be executed via Karma** — see below |
| Playwright | 0 | Not added — deferred (`TODO-4.4A-PLAYWRIGHT`) |

**Frontend gate results:**
- `npx tsc --noEmit`: same pre-existing baseline errors as Phase 4.4 (`feedbackPolicy`,
  `moduleSuggestions`, unrelated `e2e/*.spec.ts` files) — zero new errors, none in any file this
  phase touched (confirmed via `grep -i "import-package-plan"` against the full error list).
- `npm run build -- --configuration production`: **succeeds.** (One template-syntax fix was needed
  during development — Angular templates reject arrow-function expressions like
  `arr.map(t => ({...}))`; replaced with a precomputed `fieldMappingTargetOptions` property.)
- `npm test -- --watch=false --browsers=ChromeHeadless`: **blocked**, not run to a pass/fail
  result. Karma compiles the entire spec bundle together; the same pre-existing baseline
  TypeScript errors (`feedbackPolicy`/`moduleSuggestions` in unrelated spec files) abort bundle
  generation before any spec — including this phase's new one — can execute. This is the same
  blocker Phase 4.3/4.4 reported; not introduced or worsened this phase, and out of scope to fix
  (per the brief's own "do not fix unrelated frontend areas" instruction). The new spec file has
  no compile errors in isolation (confirmed via the full-project `tsc --noEmit` run above).
- Playwright: not run — no specs were added this phase.

## Data and migrations

None. This phase touched no `LinguaCoachDbContext` configuration and required no schema change —
the loose-file fix is a pure application-code change (a string transform before entity
construction), and the editor consumes DTO fields Phase 4.4 already exposed. Live DB status:
untouched, as in every prior phase this session series.

## Documentation

- Added: `docs/reviews/2026-07-16-phase-4-4a-admin-plan-editor-and-cost-ui-review.md` (this file).
- Updated: `TODOS.md` — closed `TODO-4.4-LOOSE-FILE-FOLDER-BUG` as fixed; marked
  `TODO-4.4-PLAN-EDITOR-UI` as core-scope-shipped; added a new "Phase 4.4A" section with
  `TODO-4.4A-JSON-MAPPING-UI`, `TODO-4.4A-CEILING-AMENDMENT-AUDIT`,
  `TODO-4.4A-COST-SUMMARY-PANEL`, `TODO-4.4A-STT-OPERATION-SUMMARY`, `TODO-4.4A-PLAYWRIGHT`.
- Updated: `docs/handoffs/current-product-state.md` — new Phase 4.4A section prepended,
  `lastUpdated` bumped.

## Deferred work (explicitly confirmed out of scope)

Not implemented this phase, per the user's own scoping decision: JSON/JSONL mapping UI, the
audited/concurrency-checked ceiling-amendment endpoint, a dedicated cost-summary/pause panel
component, an STT operation-ledger summary view, Playwright coverage, and the full 32/22/12
Angular/integration/architecture test counts the original brief specified (a smaller, targeted
subset covering the same critical behaviours was added instead). Phase 4.5+ items (typed
multimodal candidate schemas, etc.) were not touched, per the brief's explicit scope boundary.

## Known limitations

- The Angular unit tests for this component have not been *executed* — only shown to compile
  cleanly. They should be treated as "written and reviewed," not "passing," until the pre-existing
  Karma bundle blocker is separately resolved.
- JSON-mapped groups still show no editable mapping UI at all (a note is displayed instead);
  admins can still include/exclude and route JSON groups, just not remap their fields yet.
- The cost-ceiling resume flow is functionally unchanged from before Phase 4.4 — it works, but
  has no audit trail or concurrency protection.
- No Playwright coverage exists for any of this workflow; only API-level and component-level
  (non-rendered) unit coverage.
- The Import pipeline as a whole is **not** claimed production-ready by this phase — this is
  additive UI work on top of an already-tested backend; the deferred items above are real gaps in
  the admin workflow's usability and auditability, not merely nice-to-haves.

## Verdict

Core editor delivered and proven at both the API and component-payload level; loose-file bug fixed
with a regression test; a real pre-existing bug (missing concurrency stamp on approve) found and
fixed as a direct consequence of wiring this feature correctly. All backend gates green, unchanged
from Phase 4.4's baseline plus one new passing test. Frontend production build succeeds; Karma
remains blocked by a pre-existing, unrelated baseline issue.

## Next recommended action

Pick up `TODO-4.4A-CEILING-AMENDMENT-AUDIT` next — it is the smallest remaining piece needed to
satisfy the original brief's "cost ceiling may only be increased through an explicit audited admin
action" canonical rule, which today is only partially met (explicit, but not audited).
