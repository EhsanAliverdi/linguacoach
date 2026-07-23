# Skill Graph Rebuild — Phase 6 (Ongoing Admin UX Polish) Sprint Plan

**Date**: 2026-07-23
**Related**: `docs/reviews/2026-07-23-skill-graph-admin-editability-and-connectivity-audit.md` and its approved implementation plan (`melodic-meandering-hickey.md`, section "Phase 6 — Ongoing Admin UX Polish" and "Phase 6a — Graph Change Suggestions Service")

## Context

Phases 1–5 of the Skill Graph rebuild are complete: node/edge CRUD, cross-category AI-draft
edge resolution, the canonical 600-node curriculum, the live reseed, and content cleanup. This
session (2026-07-23) additionally did an unplanned-but-related UX pass on the node View/Edit
pages (reusable multi-select, dedicated read-only View page, layered cytoscape-elk graph
preview with multi-hop expansion + zoom/area-zoom, staged prerequisite/unlock edits, consistent
Save/Cancel placement).

Phase 6 as originally scoped bundles three very differently-sized pieces of work. Per user
decision (2026-07-23), it is broken into ordered sub-phases below and executed one at a time,
matching the same "continue with X" cadence used for Phase 3's per-CEFR-level authoring.

## Sub-phases

### Phase 6.1 — Node search & tag filtering (smallest, do first)

`GET /admin/skill-graph/nodes` currently filters only by CEFR level, skill, and review status.
Add:
- Free-text `search` query param matching node `title`/`description` (case-insensitive `Contains`,
  same convention as other admin list endpoints in this codebase).
- `contextTag`/`focusTag` filter params (array-contains match against the existing
  `ContextTagsJson`/`FocusTagsJson` columns).

Frontend: add a search input + tag filter to the Nodes table's existing `[filters]` toolbar
(`sp-admin-table`'s filter-bar pattern, already used for CEFR/skill/status). This is also a
direct prerequisite for Phase 6.2's picker UX — "reuse whatever node search Phase 6 adds" per
the original plan — so a real server-side search will replace the current client-side
`allNodesForPicker` (loads up to 500 nodes and filters in-memory) used by the Create
panel/Edit page's prerequisite/unlock multi-selects, once it exists.

**Verification**: unit tests for the new query filtering, integration test hitting the endpoint
with `search`/`contextTag`/`focusTag`, frontend Karma spec for the new filter UI, live check
against the real 600-node dev DB.

### Phase 6.2 — Suggest placement (AI)

New Node→Node AI service mirroring `IModuleSkillGraphTaggingService`'s draft-then-validate
shape: given a node id, proposes candidate prerequisite/dependent edges from the full active
graph as reviewable suggestions — **never auto-applied**, matching the archive-not-delete /
batch-approve-not-auto-approve discipline used everywhere else in this codebase.

New endpoint (tentatively `POST /admin/skill-graph/nodes/{id}/suggest-placement`). Frontend: a
"Suggest placement" action on the node View/Edit page that opens the suggestions and lets the
admin add any of them via the *already-staged* add-prerequisite/add-unlock flow built this
session on the Edit page — so a suggestion still isn't committed until the admin clicks Save,
consistent with the staged-edit UX just shipped.

**Verification**: unit tests for the new service (fake AI provider, never real AI — this
repo's standing convention), integration test for the new endpoint, frontend Karma spec, live
check.

### Phase 6.3 — Graph Change Suggestions Service (deterministic, "Phase 6a")

The larger design already worked out in the approved plan. Broken into its own ordered
sub-steps since it's the biggest piece:

- **6.3a** — `IGraphChangeSuggestionService` skeleton + redundant-edge detection (DFS
  reachability): scenarios 1 (insert-between leaves a redundant direct edge), 3 (new edge closes
  an already-covered path), and 4 (on-demand whole-graph transitive-redundancy audit).
- **6.3b** — Reject-triggered reconnect suggestions (scenario 2): wired into `BatchReject`'s
  existing edge-cascade-delete, batch-presented per the plan's decision (one review list per
  `BatchReject` call, not per rejected node).
- **6.3c** — Near-duplicate node detection (scenario 5): same CEFR level + Skill + title
  similarity above a fixed ~0.85 threshold (Jaro-Winkler or equivalent), hardcoded like this
  codebase's other advisory constants (`MaxBatchSize`, `MaxModulesPerRetagSweep`) — not
  admin-tunable, per the plan's decision.
- **6.3d** — Reparenting-on-edit suggestions (scenario 7): if `UpdateCore` moves a node to a
  different CEFR level/Skill, suggest reviewing its existing edges.
- **6.3e** — Admin UI surface: a reviewable accept/dismiss list for all suggestion types,
  wherever they're triggered from (post-mutation inline, or the on-demand "Run graph audit"
  action for 6.3a/6.3c).

**Verification**: per-scenario unit tests (the DFS/similarity logic is pure and deterministic,
easy to test exhaustively), integration tests for each trigger point, frontend Karma specs for
the review UI, live check.

## Order

6.1 → 6.2 → 6.3a → 6.3b → 6.3c → 6.3d → 6.3e, one at a time, each with its own
build/test/verify checkpoint before moving on — matching this session's established discipline
(`tsc --noEmit`, relevant Karma specs, `dotnet test`, live spot-check where the change touches
real data).

## Status

- [x] 6.1 — Node search & tag filtering — done 2026-07-23. `GetNodes` gained `search` (title/description,
      case-insensitive `.ToLower().Contains()` — cross-provider safe for both SQLite tests and
      Postgres prod, matching `AdminNotificationHandler`/`AdminTemplateHandler`'s existing search
      convention) and `contextTag`/`focusTag` (substring match against the quoted tag in
      `ContextTagsJson`/`FocusTagsJson`, since those are plain JSON-string columns, not `jsonb`).
      `GetTaxonomy` now also returns `contextTags`/`focusTags` (the shared 13-value
      `CurriculumContextTagConstants.All` vocabulary) so the frontend can populate real dropdowns.
      Frontend: Nodes table gained a free-text search box (`sp-admin-table`'s existing
      `[searchable]`/`(searchChange)` feature) plus two new tag filter dropdowns, alongside the
      existing CEFR/skill/review-status filters. +4 integration tests
      (`GetNodes_SearchMatchesTitleAndDescription`, `GetNodes_SearchIsCaseInsensitive`,
      `GetNodes_FiltersByContextTagAndFocusTag`, taxonomy assertion extended). Verified: full
      backend suite (30 architecture + 2,475 unit + 1,337 integration) green, frontend `tsc
      --noEmit` clean, 25/25 Skill Graph Karma specs green, and a live check against the real
      600-node reseeded dev DB (rebuilt/redeployed API container) confirmed taxonomy returns 13
      context/focus tags, searching "present continuous" correctly returns exactly 1 node, and
      filtering by `contextTag=social_conversation` correctly returns 101 nodes. This server-side
      search is the direct prerequisite Phase 6.2 needs to replace the current client-side
      `allNodesForPicker` (500-row load-and-filter) multi-select pickers with real search — not
      yet done, tracked as follow-up work when 6.2 starts.
- [x] 6.2 — Suggest placement (AI) — done 2026-07-23. New `INodeGraphPlacementSuggestionService`
      (Infrastructure: `NodeGraphPlacementSuggestionService`), structurally identical to
      `ModuleSkillGraphTaggingService` (bounded AI call, retried once on bad JSON, never throws,
      every proposed key validated against the real candidate list) but Node→Node and returning
      TWO directions in one call (`{"prerequisites": [...], "dependents": [...]}` instead of a
      single `matches` array). New prompt `skill_graph_suggest_placement`. New endpoint
      `POST /admin/skill-graph/nodes/{id}/suggest-placement` — builds candidates from the same
      cross-link shape `Draft()` already uses (same skill any level, OR same CEFR level any
      skill), bounded to 60, excluding the node itself and anything already linked in either
      direction. **Never auto-applied** (unlike Module tagging) — the endpoint only returns a
      reviewable list; accepting a suggestion on the frontend routes through the exact same
      staged add-prerequisite/add-unlock mechanism the Edit page already has (built earlier this
      session), so a suggestion is not written to the graph until an explicit Save, same as any
      manual edit. Frontend: new "Suggest placement (AI)" card on the Edit page with per-suggestion
      confidence % and Accept/Dismiss actions. +9 unit tests (`NodeGraphPlacementSuggestionServiceTests`,
      fake-AI-provider pattern mirroring `ModuleSkillGraphTaggingServiceTests`), +4 integration
      tests (404, no-candidates, graceful-degradation-without-a-real-AI-provider, non-admin
      rejected). **Found and fixed live**: the initial `maxInputTokens: 1700` budget was undersized
      against a real 60-candidate list (observed ~1888 tokens live against the real reseeded dev
      DB) — raised to 2400 with real headroom, matching the Sprint 9/14/Rebuild-Phase-2
      token-budget-fix precedent (never just barely above observed). Verified: full backend suite
      (30 architecture + 2,484 unit + 1,341 integration) green, frontend `tsc --noEmit` clean,
      25/25 Skill Graph Karma specs green, and — after the token-budget fix — a live end-to-end
      check against the real dev DB and its configured AI provider returned genuinely sensible
      suggestions for "Present continuous for actions happening now" (prerequisites: Subject
      pronouns, Verb 'to be' negatives/questions; unlocks: Present simple negatives/questions,
      Describing people and things aloud), and accepting one correctly staged it (dashed amber in
      the graph + list, removed from the suggestion list, unsaved-changes banner appeared) exactly
      like a manually-picked node.
- [x] 6.3a — Redundant-edge detection — done 2026-07-23. New `IGraphChangeSuggestionService`
      (Infrastructure: `GraphChangeSuggestionService`), pure/deterministic (no AI, no DB access):
      `DetectRedundantEdges(edges, restrictToNodeIds?)` runs a BFS per edge, excluding that one
      direct edge, to check whether the dependent node is still reachable from the prerequisite
      via some other path — if so, the direct edge is redundant (a classic transitive-reduction
      check) and gets flagged as a `GraphChangeSuggestion` with `Type: RedundantEdge`. Two ways to
      trigger it: (a) `GET /skill-graph/suggestions/redundant-edges` — the on-demand whole-graph
      audit (scenario 4), and (b) a cheap targeted check wired into `AddPrerequisite` itself
      (scenarios 1/3) — right after a new edge lands, only the two endpoint nodes' edges are
      re-checked (not the whole graph), and any resulting suggestions come back inline on the
      same response. **Never auto-applied** — both paths only return suggestions for the admin to
      review; removing one is a real, separate `DELETE .../prerequisites/{id}` call the admin
      explicitly triggers. Frontend: new "Graph audit" card on the main Skill Graph page — "Run
      graph audit" button, results list with per-suggestion Dismiss/Remove edge actions (Dismiss
      just drops it from the local list; Remove edge calls the existing
      `removeSkillGraphPrerequisite` endpoint, same one Edit's staged-removal flow uses). +7 unit
      tests (`GraphChangeSuggestionServiceTests` — exhaustive since the logic is pure: spanned-edge
      detection, no-alternate-path negative case, 3-hop alternate paths, `restrictToNodeIds`
      scoping, empty/disconnected-graph edge cases), +4 integration tests (whole-graph audit finds
      a seeded redundant edge, `AddPrerequisite` surfaces a suggestion inline when the new edge
      makes an existing one redundant, no-false-positives case, non-admin rejected). Verified:
      full backend suite (30 architecture + 2,491 unit + 1,345 integration) green, frontend `tsc
      --noEmit` clean, 24/24 Skill Graph Karma specs green, and a live check against the real
      600-node reseeded dev DB found **10 genuinely redundant edges already present in the real
      curriculum** (e.g. "Present simple: affirmative for daily routines → First conditional" is
      already implied by a longer real chain) — confirming the detection works correctly against
      real, not just synthetic, data.
- [ ] 6.3b — Reject-triggered reconnect suggestions
- [ ] 6.3c — Near-duplicate node detection
- [ ] 6.3d — Reparenting-on-edit suggestions
- [ ] 6.3e — Admin UI surface for suggestions review
