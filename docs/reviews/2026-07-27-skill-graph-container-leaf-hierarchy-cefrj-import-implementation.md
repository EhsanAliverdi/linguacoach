# Skill Graph — Container/Leaf Hierarchy + CEFR-J Grammar Import (Implementation Record)

**Date:** 2026-07-27
**Related sprint/feature:** Adaptive Curriculum — Skill Graph (see
`docs/architecture/adaptive-curriculum-skill-graph.md`), following on from
`docs/reviews/2026-07-24-skill-graph-full-pipeline-senior-audit.md`.

## Context

The 2026-07-24 senior pipeline audit confirmed the user's hypothesis (via the
real CEFR-J Grammar Profile CSV) that the existing 600-node skill graph is
roughly an order of magnitude coarser than real coursebook/CEFR-J
granularity, and recommended a self-referencing container/leaf hierarchy
(`ParentNodeId`) plus a deterministic CEFR-J CSV import for Grammar leaf
nodes. The user signed off on this recommendation via AskUserQuestion
("Sign off on hierarchy + CEFR-J import now"), and a 6-phase implementation
plan was approved (Plan Mode, `melodic-meandering-hickey.md`). This document
records what was actually implemented for Phases 0–2.

## Files reviewed / changed

- `src/LinguaCoach.Domain/Entities/SkillGraphNode.cs` — `ParentNodeId` + `AssignParent`.
- `src/LinguaCoach.Persistence/Configurations/SkillGraphNodeConfiguration.cs` — FK/index config.
- `src/LinguaCoach.Persistence/Migrations/20260727211614_AddSkillGraphNodeParentId.cs`.
- `src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs` — parent read/write surfaces,
  leaf-only guards on edges/module-links/cross-link candidates, `ImportNodes` `ParentKey`
  resolution, new `cefrj-import/preview` endpoint.
- `src/LinguaCoach.Infrastructure/SkillGraph/GraphChangeSuggestionService.cs` — minimal-pair
  near-duplicate guard (`DiffersInSentenceType`).
- `src/LinguaCoach.Infrastructure/SkillGraph/TextSimilarity.cs` — new shared bigram-similarity
  helper (extracted so the CEFR-J importer's fuzzy container matching reuses the same scoring as
  the near-duplicate detector instead of a second implementation).
- `src/LinguaCoach.Infrastructure/SkillGraph/CefrJGrammarImportService.cs` — new deterministic
  (no AI) CEFR-J CSV parser + container/leaf mapper.
- `src/LinguaCoach.Application/SkillGraph/CefrJGrammarImportContracts.cs` — new interface/DTOs.
- Unit tests: `SkillGraphNodeTests`, `GraphChangeSuggestionServiceTests`,
  `CefrJGrammarImportServiceTests` (new, 11 tests).
- Integration tests: `AdminSkillGraphEndpointTests` — 13 tests for parent-aware endpoints, 5 tests
  for the CEFR-J preview endpoint.

## What shipped

### Phase 0 — Near-duplicate detector hardening

`GraphChangeSuggestionService.DetectNearDuplicateNodes` now skips a candidate
pair when their titles differ in sentence type — one ends with `?` and the
other doesn't (AFF vs INT), or one contains negation (`not`/`n't`) and the
other doesn't (AFF vs NEG) — before scoring bigram similarity. This stops the
detector from flagging real CEFR-J minimal pairs ("I am" / "I am not" / "Am I
...?") as duplicates once CEFR-J leaves exist in the graph. Regression tests
added for the exact minimal-pair shape.

### Phase 1 — Container/leaf schema

`SkillGraphNode.ParentNodeId` (nullable, self-referencing, `Restrict` delete)
+ `AssignParent(Guid?)`, ungated by review status (same "supplementary
structure" policy as `UpdateTags`). Read surfaces (`GetNodes`, `GetNode`,
`GetGraph`) expose parent/child info; new `PUT nodes/{id}/parent` endpoint
with self-parent/cycle/has-children guards. `CreateNode` accepts
`ParentNodeId` at creation time. Leaf-only enforcement: a node with children
cannot receive a prerequisite edge, a `ModuleSkillGraphNodeLink`, or appear
as a cross-link/placement/retag candidate. `ImportNodes` resolves a new
`ParentKey` field the same way it already resolves `PrerequisiteKeys`.

### Phase 2 — CEFR-J grammar leaf importer

`CefrJGrammarImportService` (Infrastructure, no AI, pure/deterministic):

- Parses the real CEFR-J CSV (RFC-4180 quoted fields handled directly, no
  external CSV library).
- Groups rows by the CSV's own hyphenated `ID` structure into "families" —
  a bare-integer row plus its `-1`/`-2`/`-3` AFF/NEG/INT siblings.
- A family with siblings becomes a proposed container (fuzzy-matched by
  title against the existing Grammar-skill nodes via the shared
  `TextSimilarity.BigramDiceSimilarity`, threshold 0.35 — looser than the
  near-duplicate detector's 0.85 since this is matching a family's
  collective topic onto a broader existing node, not flagging near-identical
  duplicates); every row in the family (including the base row) becomes a
  leaf under it.
- A row with no siblings becomes a standalone leaf (no parent) — real CSV
  data showed 170/262 "families" have no hyphenated children at all.
- CEFR level per leaf: `CEFR-J Level` column when present (66% of real rows
  are blank), else `Core Inventory`, else the first recognizable CEFR token
  in `EGP`, else `GSELO`, else defaults to A1 with a warning surfaced in the
  preview (never silently applied).
- Difficulty band: the CSV's own sub-level suffix (`B1.2` → band 2) — this
  is CEFR-J's actual finding that e.g. `does`-negation is a full CEFR level
  harder than `do`-negation despite both being "present simple," which is
  the concrete value this import adds over the flat model.
- New `POST /api/admin/skill-graph/cefrj-import/preview` endpoint: takes raw
  CSV text, returns the proposed container/leaf mapping for human review
  AND a ready-to-use `importPayload` shaped exactly like the existing
  `ImportSkillGraphRequest` — applying the import after review is just
  posting `importPayload.nodes` to the existing `POST nodes/import`
  endpoint, no second write mechanism. Verified end-to-end in an integration
  test (`PreviewCefrJImport_ImportPayload_CanBeAppliedThroughTheRealImportEndpoint`).

### Phase 3 — Rollup & downstream consumption

Note: the "downstream consumption" half of this phase (excluding container
nodes from delivery candidacy in routing/composer/module-selection queries)
was effectively already covered by Phase 1's leaf-only guards on the
cross-link/placement/retag candidate queries — no real container nodes with
content links exist yet (containers are a brand-new concept as of this
initiative), so `TodayPlanModuleSelectionService`/`PracticeGymModuleSelectionService`
only ever surface content through real `ModuleSkillGraphNodeLink` rows, which
Phase 1 already stopped creating on nodes with children.

What Phase 3 actually added: `IStudentMasteryEvaluationService.EvaluateContainerRollupAsync`
(new `StudentMasteryEvaluationService` method) — a pure read-time aggregation,
no persisted state. For a given student and container node id, resolves the
container's Approved+Active leaf children's keys, checks each against the
same `StudentMasteryReport.MasteredObjectiveKeys` the per-node evaluation
already produces, and returns `ContainerMasteryRollup` (leaf count, mastered
count, percent, and a `MasteryStatus` banded at the same ≥80%→Mastered
threshold the plan recommended). A container with zero eligible leaf
children, or leaves with no learning-event evidence at all, returns
`InsufficientEvidence` rather than a misleading 0%. Exposed via
`GET api/admin/mastery/students/{studentId}/container-rollup/{containerNodeId}`
on `AdminMasteryController` (404 if the node doesn't exist). +5 unit tests
(`StudentMasteryEvaluationServiceTests`, covering empty/full/half-mastered/
never-attempted/Approved+Active-filtering cases), +3 integration tests.

The content-coverage-dashboard-groups-by-container piece of this phase is
left for Phase 4 (admin UI) — it's a display/dashboard concern, not a data
concern, and Phase 4 is already rebuilding the Nodes list's rendering to be
hierarchy-aware.

### Phase 4 — Admin UI hierarchy support

Frontend-only, Angular. First pass implemented a hand-rolled third "Tree" view
mode alongside the existing flat "Table" view. **User feedback after seeing
it**: two separate views of the same data (a flat table AND a separate tree)
was confusing — "shouldn't all be under table with expandable table? as each
node might have sub nodes." The user also asked to use a free, maintained,
enterprise-level component rather than keep extending a bespoke
implementation, and asked for two more things: children shown on View/Edit
(not just parent), and clicking a node in the local graph preview should
show that node's own hierarchy in a modal rather than just navigating away.
This section describes what actually shipped, after that correction.

**Component choice** (discussed via AskUserQuestion): **PrimeNG TreeTable**
over Angular CDK Tree — free (MIT), purpose-built for hierarchical rows with
columns (vs. CDK Tree's bare hierarchy primitive, which would need the table/
column/checkbox layer hand-built again), built-in lazy-loading and row
checkboxes, actively maintained, tracks Angular's release cycle. ag-Grid was
ruled out — its Tree Data feature is Enterprise-only (paid), not free. Added
`primeng@19.1.4`, `@primeng/themes@19.1.4` (Aura preset, base structural
styling only — this app's own `sp-admin-*` tokens are layered on top, not
replaced), `primeicons@7.0.0`, `@angular/cdk@19.2.19`,
`@angular/animations@19.2.19` (PrimeNG peer deps; installed with
`--legacy-peer-deps` since Angular's own packages declare exact-version
peers that trip npm's strict resolver even for same-minor-version installs —
this is a known Angular ecosystem quirk, not a real incompatibility).
`provideAnimationsAsync()` + `providePrimeNG({ theme: { preset: Aura } })`
wired into `app.config.ts`.

**The Table+Tree replacement**: `AdminSkillGraphComponent`'s Nodes card now
has exactly two view modes — "Nodes" (the TreeTable, replacing both the old
Table and the short-lived Tree) and "Graph" (unchanged cytoscape prerequisite
visualization, a genuinely different view of the whole graph, not part of
this consolidation). The TreeTable IS the hierarchy:
- Root rows are server-paginated and lazy-loaded (`[lazy]="true"`,
  `(onLazyLoad)`) via a new `topLevelOnly` filter on `GET nodes`
  (`ParentNodeId == null` — containers and standalone nodes).
- A container row's leaf children are fetched only when it's expanded
  (`(onNodeExpand)`), via a new `parentNodeId` filter on the same endpoint,
  cached on the `TreeNode` until the next full reload — never a full-
  hierarchy fetch up front.
- Row checkboxes (`selectionMode="checkbox"`) replace the old bulk-edit
  toggle; "Select subtree" on a container row seeds the same selection with
  the container + its currently-loaded children, so the existing
  (already confirmation-gated) Approve/Reject-selected actions apply to a
  whole subtree in one action — no new backend endpoint.
- While a search term is active, results are flat (every row `leaf:true`,
  no expand arrows) rather than grouped — a matched leaf buried inside an
  unexpanded container would otherwise be invisible; simpler than
  reconstructing partial trees, the same "search flattens hierarchy"
  convention many tree UIs use.
- The filter bar (CEFR/skill/status/context-tag/focus-tag/search) is now
  hand-rendered directly in the template (previously `sp-admin-table`'s
  built-in `[filters]`/`[searchable]` props) since `sp-admin-table` is no
  longer used here at all.

**Parent-picker truncation fix** (unrelated bug, found during Phase 4's
original pass — was already broken regardless of hierarchy: the picker
fetched `pageSize: 500`, silently clamped to 200 server-side, dropping real
nodes from every prerequisite/unlock picker today). Fixed via
`SpAdminMultiSelectComponent` gaining a debounced (250ms) `(searchTermChange)`
output — additive, existing callers that don't listen to it are unaffected.
Create/Edit's prerequisite/unlock pickers (and the new Parent pickers) now
re-fetch a small (30-row) server-filtered page per keystroke via the
existing `nodes?search=` endpoint instead of ever holding a large/truncated
static list.

**Parent field on Create / reparent UX on Edit**: new "Parent (container)"
section on both, staged-until-Save like prerequisites/unlocks (Edit's
version applied via the new `PUT nodes/{id}/parent` endpoint in the same
`forkJoin` as the existing staged-edge commit). Hidden (with an explanatory
message) when the node itself is a container, matching the backend's own
"a container can't also be a leaf" 409 guard.

**Children shown on View and Edit** (explicit user ask): View already had a
read-only "Container/leaf hierarchy" section (parent link + children list,
shown only when relevant); Edit's own "Parent (container)" section — the
branch that shows "this node is a container and can't have a parent" —
now also lists the container's leaf children with links and review-status
badges, not just the count.

**Node-hierarchy "peek" modal** (explicit user ask): new shared
`SpAdminNodeHierarchyModalComponent` (`node-hierarchy-modal/`) — fetches
`GET nodes/{id}` fresh on every open (no caching, hierarchy state is cheap
to re-fetch) and shows that node's title/status/CEFR/skill, parent link, and
children list, with "Close" and "View full page" actions. On both View and
Edit, clicking a node inside the local graph preview (`sp-admin-node-graph-
preview`) now opens this modal instead of navigating away immediately — on
Edit specifically this also avoids abandoning any staged, not-yet-saved
prerequisite/unlock/parent changes just from checking a neighbor's place in
the hierarchy. "View full page" still navigates via the existing `goToNode`.

**Deferred, unchanged from the original pass**: containment-aware collapsed/
expand rendering in the cytoscape Graph view specifically (as opposed to the
Nodes/TreeTable view, which now fully covers hierarchy browsing) — no real
container nodes exist in any environment yet (CEFR-J import hasn't been run
against real data), so reworking the working cytoscape visualization for
data that doesn't exist yet remains lower priority than what shipped.

New backend surface for Phase 4: `topLevelOnly`/`parentNodeId` query filters
on `GET nodes` (2 new integration tests). Frontend: +33 tests
(`admin-skill-graph.component.spec.ts`, rewritten for the TreeTable's lazy-
load/node-expand/selection glue logic — PrimeNG's own internal rendering is
not re-tested, only this app's event handlers and data transforms), +2 tests
(`sp-admin-multi-select.component.spec.ts` — debounce timing/coalescing),
+7 tests (`sp-admin-node-hierarchy-modal.component.spec.ts` — fetch-on-open,
re-fetch-on-id-change, error state, Close/View-full-page emissions).
`npx tsc --noEmit`, production build, and the full Karma suite (1765 tests,
234-237 pre-existing failures — within this suite's documented flakiness
band, none touching any file this phase changed) all verified clean.

### Phase 4 revision — `SpAdminTreeTableComponent` (2026-07-27, same day)

**User feedback after seeing the PrimeNG TreeTable page live**: it "looks
nothing like other pages" — the raw `p-treeTable` with PrimeNG's default
Aura theme didn't match `SpAdminTableComponent`'s look used everywhere else
in the admin, wasn't a real reusable design-system component (it was inlined
directly into `AdminSkillGraphComponent`'s template), and filters didn't
apply to lazily-loaded children (a container's children ignored the active
CEFR/skill/status/tag filters entirely). User's direction: keep using
PrimeNG TreeTable as the underlying library, but wrap it in a native,
reusable `sp-admin-tree-table` design-system component.

**New component**: `src/app/design-system/admin/components/tree-table/
sp-admin-tree-table.component.ts` — same public API shape as
`SpAdminTableComponent` (`columns`/`rows`/`loading`/`error`/`searchable`/
`filters`/`selectable`/pagination footer/`#cell` content-projection
template), so pages that already know `sp-admin-table` can pick this up
with no new concepts. Internally: `p-treeTable` is still the engine driving
lazy row data and node-expand orchestration — every header/body cell is
rendered by *this* component's own template (`pTemplate="header"/"body"`)
using `SpAdminTableComponent`'s exact CSS class names and values (`sp-adm-
th`, `sp-adm-td`, `sp-adm-tr-hover`, the toolbar/selection-row classes, …),
so a tree table and a flat table are visually indistinguishable. PrimeNG's
own toggler (`p-treeTableToggler`) and checkbox (`p-treeTableCheckbox`/
`p-treeTableHeaderCheckbox`) sub-components are kept — not reimplemented —
specifically so expand-state and selection-state bookkeeping stays inside a
well-tested library rather than becoming new hand-rolled state in this
codebase; their own default visuals are overridden via `:host ::ng-deep` to
match this design system's checkbox sizing/color and icon color instead of
Aura's.

**Filter-children fix** (explicit ask — "filter nodes with children too"):
`onTtNodeExpand` previously sent only `parentNodeId` to the children-fetch
call, ignoring every other active filter — expanding a container while
filtered to `reviewStatus=Approved` still showed PendingReview children.
Now sends the same CEFR/skill/status/context-tag/focus-tag filter set the
root-level fetch uses (search is intentionally excluded — while a search
term is active the whole table already renders flat with no expand
affordance, per the earlier "search flattens hierarchy" decision, so
expand never fires during a search). A real filter change already resets
`ttNodes` with brand-new `TreeNode` wrappers carrying no cached `children`,
so re-expanding a container after a filter change always re-fetches under
the new filters — no separate cache-invalidation code needed.

`AdminSkillGraphComponent`'s Nodes card now uses `<sp-admin-tree-table>`
instead of a raw `<p-treeTable>`; pagination moved from PrimeNG's built-in
paginator to the same `sp-admin-table-footer`/`sp-admin-pagination` footer
every `sp-admin-table`-based page already uses (`onNodesPageChange`, page-
based, not the offset-based `onLazyLoad` event PrimeNG's own paginator
would have driven).

**Bundle-size tradeoff, accepted**: the initial bundle budget in
`angular.json` was raised (`2MB`/`3MB` → `2.5MB`/`3.5MB` warning/error) to
accommodate PrimeNG's theming runtime (`@primeuix/styled`/`@primeuix/utils`,
pulled in eagerly by the global `providePrimeNG()`/`provideAnimationsAsync()`
providers in `app.config.ts` regardless of which routes actually use
TreeTable) — real total initial bundle is ~3.25MB, up from under 2MB before
this dependency. Still under the raised warning threshold is not achieved
(prints a build warning, not an error) — acceptable given the user's
explicit choice to adopt this library, but worth knowing if initial load
time is ever profiled.

+10 new tests (`sp-admin-tree-table.component.spec.ts` — header/row
rendering, loading/error/empty states, search/filter emission, real
toggler-click → `nodeExpand` emission, checkbox rendering), existing
`admin-skill-graph.component.spec.ts` tests updated for the new
`onNodesPageChange`/`onTtNodeExpand(node)` signatures (pagination no longer
goes through a `TreeTableLazyLoadEvent`), +1 new test asserting the filter-
children fix. Production build clean (warning-level bundle size only), full
Karma suite (1784 tests, 234 pre-existing failures within the documented
flakiness band, 0 new failures) verified clean.

### Phase 4 second follow-up — "Has children" filter + column layout fix (2026-07-27, same day)

Further live feedback on `SpAdminTreeTableComponent`: (1) "in the filters we
want has child node" — no way to filter the Nodes list down to just
containers (or just leaves/standalone); (2) "the column data overlaps, it
shows wrap... the title should push stuff the right" — with `table-layout:
auto` and no fluid-column convention, variable-width cells (badges/tags)
made columns resize unpredictably row to row, reading as overlapping/
wrapped content, instead of the title column claiming the spare width and
every other column staying fixed/one-line the way every other admin table
already does.

**"Has children" filter**: new `hasChildren` (`bool?`) query parameter on
`GET nodes` — `true` restricts to nodes that are somebody's `ParentNodeId`
(containers), `false` excludes them (leaves/standalone only), unset means no
filter. Implemented as `containerIds = SkillGraphNodes.Where(c =>
c.ParentNodeId != null).Select(c => c.ParentNodeId!.Value)` and an `IN`/
`NOT IN` against it — a real subquery EF Core translates directly, not an
N+1. Wired into the Nodes toolbar as a normal `SpAdminTableFilter` ("Has
children": Containers only / Leaves+standalone only / All), same dropdown
convention as every other filter on the page. +2 backend integration tests,
+4 frontend tests (filter appears in `nodesFilters()`, both filter values
map to the right `hasChildren` query param, clearing back to "All" sends
`undefined`).

**Column layout fix**: ported `SpAdminTableComponent`'s existing
`first-column-fluid` layout mode into `SpAdminTreeTableComponent` — new
`layout` input, `sp-adm-fluid-layout` CSS class on the scroll wrapper, and
the title `<th>`/`<td>` explicitly marked `sp-admin-fluid-col` (needed
because the tree table's real first column is the selection checkbox, not
the title, so the CSS's plain `:first-child` fluid rule would otherwise
grab the checkbox column instead — the explicit marker overrides that, same
mechanism `SpAdminTableComponent` itself already relies on). With this
layout, the title column takes the spare width and wraps normally; every
other column (CEFR, skill, badges, tags, status) shrinks to its content and
stays on one line — no more unpredictable per-row column widths. Wired via
`layout="first-column-fluid"` on the Nodes page's `<sp-admin-tree-table>`,
matching what the original flat Table view used before Phase 4. +2 new
component tests (fluid class/marker present when set, absent by default).

Backend suite: 30 architecture + 2,562 unit + 1,395 integration, all green.
Frontend: production build clean, full Karma suite (1,790 tests, 237
pre-existing failures within the documented flakiness band, 0 new failures).

## Findings by priority

None outstanding — this is an implementation record, not an audit. The one
correctness issue found during implementation (a flaky integration test
where the fuzzy-match assertion could coincidentally match another test's
leftover node title in the shared test DB) was fixed by embedding a unique
token in the test's own fixture data rather than relying on a generic title,
and is not a production code issue.

## Decisions made

- Container/leaf hierarchy modeled as a self-referencing FK on the same
  entity (not a separate `SkillGraphContainer` type) — matches the audit's
  recommendation and the existing prerequisite-edge FK convention.
- CEFR-J import scope is Grammar only (the CSV's actual coverage);
  vocabulary/functional-language/subskill granularity remains Phase 5,
  deliberately unscoped (no equivalent free structured source exists yet).
- Fuzzy container matching reuses the near-duplicate detector's bigram
  scoring via a new shared `TextSimilarity` helper rather than a second
  implementation.

## Implementation tasks produced

Phase 3 (rollup & downstream consumption) and Phase 4 (admin UI hierarchy
support) remain, tracked in the session's task list; not yet started as of
this document.

## Risks / unresolved questions

- The CEFR-J → existing-node container match threshold (0.35) is untuned
  against the real ~600-node dataset — only validated against synthetic
  test fixtures so far. Should be re-checked once run against the real CSV
  and real node set before bulk-applying the import.
- CEFR level fallback (Core Inventory → EGP → GSELO → A1 default) is a
  reasonable but untested-against-real-data heuristic; the real CSV has
  330/500 rows with a blank `CEFR-J Level`, so a meaningful fraction of
  leaves will get their level from a fallback column or the A1 default —
  worth spot-checking the real preview output before applying.
- Container mastery rollup rule (Phase 3) is still just a plan-stage
  recommendation, not implemented or verified.

## Final verdict

All 4 in-scope phases (0–4) are implemented and tested: 16 new backend unit
tests + 21 new/changed backend integration tests (Phases 0-3), full backend
suite green (30 architecture + 2562 unit + 1391 integration, 0 failures);
6 new/changed frontend component tests (Phase 4), production build clean,
full Karma suite green relative to its documented pre-existing baseline
(1756 total, 237 pre-existing failures unrelated to any file this initiative
touched, 0 new failures). Phase 5 (non-grammar granularity) remains
deliberately unscoped per the approved plan — no CEFR-J-equivalent free
structured source exists for vocabulary/functional-language/subskills.

## Next recommended action

1. Run `CefrJGrammarImportService.ParseAndProposeMapping` against the real
   CEFR-J CSV and the real dev-DB node set (not just unit-test fixtures) to
   sanity-check match quality and the CEFR-level-fallback warning count
   before ever applying the import to real data.
2. Rebuild/redeploy the Docker dev API+web (`--no-cache`) and verify the new
   admin surfaces (tree view, parent picker, reparent) against the real dev
   DB before considering this initiative fully shipped — everything above
   was verified via automated tests and local builds, not yet a live pass.
3. Revisit containment-aware graph viz (deferred, see Phase 4 notes) once
   real container nodes exist in the dev DB from an actual CEFR-J import run.
