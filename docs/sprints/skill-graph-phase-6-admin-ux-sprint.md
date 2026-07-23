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
- [x] 6.3b — Reject-triggered reconnect suggestions — done 2026-07-24.
      `IGraphChangeSuggestionService.DetectReconnectsAfterReject(rejectedNodeIds, edgesBeforeRemoval)`
      — for each rejected node, finds its former predecessors and former dependents in the edge
      set captured just before `BatchReject`'s existing cascade-delete runs, and proposes
      reconnecting every predecessor×dependent pair not already directly connected (A→B→C, B
      rejected, suggest A→C). Skips: pairs where either endpoint is also being rejected in the
      same batch, and pairs already directly connected. **Batch-presented per the plan's decision**
      — `BatchReject`'s response now includes `reconnectSuggestions: RejectReconnectGroup[]`, one
      group per rejected node in that same call (not a separate per-node interruption), each with
      `orphanedPredecessors`/`orphanedDependents`/`suggestedReconnects` (id+title resolved
      server-side, same N+1-avoidance discipline as 6.3a's `ToSuggestionDtosAsync`). Never
      auto-applied — the frontend's new "Reconnect suggestions" card (appears only when there's
      something to show, appends across multiple reject calls rather than replacing) lets the
      admin Dismiss (drop from the list) or Reconnect (a real `addSkillGraphPrerequisite` call) each
      suggestion individually. +7 unit tests (`DetectReconnectsAfterReject` — spanned-chain
      reconnect, already-connected no-op, no-predecessor/no-dependent root case, cross-product with
      multiple predecessors/dependents, same-batch-rejected-endpoint exclusion, multi-node batch
      producing one group per node, empty-input edge cases), +2 integration tests. Verified: full
      backend suite (30 architecture + 2,498 unit + 1,347 integration) green, frontend `tsc
      --noEmit` clean, 24/24 Skill Graph Karma specs green, and a full live end-to-end pass against
      the real dev DB — created a disposable A→B→C node chain via the real API (never touching the
      curated 600-node curriculum), rejected B through the actual admin UI (bulk-edit checkbox +
      reject reason + "Reject selected"), and confirmed the "Reconnect suggestions" card correctly
      appeared showing "6.3b verify A2 → 6.3b verify C2" with working Dismiss/Reconnect buttons.
- [x] 6.3c — Near-duplicate node detection — done 2026-07-24.
      `IGraphChangeSuggestionService.DetectNearDuplicateNodes(candidates)` groups active nodes by
      (CefrLevel, Skill) — comparing across levels/skills is meaningless, since the same title at
      different CEFR levels can be a legitimately different node — and flags pairs whose titles
      score ≥ a fixed `NearDuplicateSimilarityThreshold = 0.85` on a from-scratch Jaro-Winkler
      similarity implementation (case-insensitive, trimmed). Threshold is hardcoded per the
      approved plan's explicit "not admin-tunable" decision, matching this codebase's other
      advisory constants (`MaxBatchSize`, `MaxModulesPerRetagSweep`) — revisit only if real usage
      shows it misfires. New `GET /admin/skill-graph/suggestions/near-duplicates` (on-demand
      whole-graph audit, same N+1-avoidance title-resolution pattern as 6.3a/6.3b) plus a new
      explicit action endpoint `POST /admin/skill-graph/nodes/{keepNodeId}/merge/{mergeAwayNodeId}`
      — re-points every edge touching the merge-away node onto the keep node (dropping any edge
      that would become a self-loop or duplicate one that already exists post-repoint), validates
      the resulting edge set against the full active graph via the same `ISkillGraphValidationService`
      cycle check `AddPrerequisite` uses (rejects with 409 if a merge would introduce a cycle), then
      calls `SkillGraphNode.Deactivate()` on the merge-away node — never a hard delete, per this
      codebase's archive-not-delete convention. Detection is advisory-only; merging is a distinct,
      explicit admin action, never triggered automatically by the audit. New "Near-duplicate nodes"
      card on the main Skill Graph page (Run audit / Dismiss / "Keep A" / "Keep B" per pair,
      mirroring the Graph audit / Reconnect suggestions cards' UX). +8 unit tests (near-identical
      titles flagged, identical titles score 1.0, different CEFR levels not compared, different
      skills not compared, genuinely different titles not flagged, 3-node group flags only the
      similar pair, fewer-than-2-nodes edge cases, case/whitespace-only differences still match),
      +8 integration tests (audit finds/excludes pairs correctly, non-admin rejected, merge
      re-points and deactivates, merge drops a would-be self-loop edge, merge rejects
      merging-into-self, merge rejects a would-be cycle, non-admin rejected for merge). Verified:
      full backend suite (30 architecture + 2,506 unit + 1,355 integration) green, frontend `tsc
      --noEmit` clean, 16/16 Skill Graph Karma specs green, and a full live end-to-end pass against
      the real reseeded dev DB — created two disposable near-duplicate nodes ("6.3c verify Present
      simple affirmative" / "...affirmatve") via the real API, confirmed the audit endpoint flagged
      them (similarity ≈0.885), merged one into the other via the real merge endpoint, confirmed the
      merged-away node came back `isActive: false`, then rejected both disposable nodes to clean up
      rather than leaving them in the curated curriculum. The live audit against the real 600-node
      curriculum also surfaced **170 pre-existing near-duplicate pairs already present in the real
      data** — a genuine, expected finding (not a bug): a curriculum this size accumulates enough
      title collisions across CEFR×Skill groups that this is a real backlog for admins to review,
      not synthetic noise.
- [x] 6.3d — Reparenting-on-edit suggestions — done 2026-07-24.
      `IGraphChangeSuggestionService.DetectReparentingReview(nodeId, oldCefrLevel, oldSkill,
      newCefrLevel, newSkill, neighbors)` — pure, deterministic: returns `null` when neither the
      CEFR level nor the Skill actually changed, or when the node has no edges at all (the common
      case on most saves, so this stays a cheap no-op). Otherwise returns every edge touching the
      node for review, with each one flagged `LooksSuspicious` only when it's a genuine CEFR-
      ordering violation under the NEW level — a prerequisite now at a LATER CEFR stage than the
      node, or a dependent now at an EARLIER one. Same-level edges are never flagged, since a
      single CEFR band legitimately orders ~100 nodes internally (this was a deliberate design
      choice over "flag everything," to keep the signal real rather than blanket noise). Wired into
      `UpdateNode`: captures the node's CEFR level/Skill BEFORE `UpdateCore` mutates it, and after
      saving, the response now carries `reparentReview` (null on the common no-change path).
      Frontend: the Edit page's `save()` now stays on the page instead of navigating away when
      `reparentReview` has edges to review, showing a new "Reparenting review" card (`Suspicious`
      badge on flagged edges, `Remove edge` — a real `removeSkillGraphPrerequisite` call — or
      `Dismiss` per edge, `Done reviewing` to finish and navigate on). Advisory only, same
      discipline as 6.3a-c — nothing is ever removed automatically. +7 unit tests (no-change no-op,
      moved-but-no-edges no-op, skill-only change still triggers, prerequisite-at-later-stage
      flagged, dependent-at-earlier-stage flagged, same-level never flagged, all edges returned not
      just suspicious ones), +4 integration tests. Verified: full backend suite (30 architecture +
      2,513 unit + 1,359 integration) green, frontend `tsc --noEmit` clean, 16/16 Skill Graph Karma
      specs green, and a full live end-to-end pass against the real reseeded dev DB — created a
      disposable C1 node with a real B2 prerequisite via the real API, moved it down through the
      actual Edit UI form (CEFR select + Save), confirmed the "Reparenting review" card correctly
      appeared with the B2 prerequisite marked "Suspicious," clicked "Remove edge" and confirmed the
      edge disappeared from both the list and the live graph preview, then rejected the disposable
      nodes to clean up.
- [x] 6.3e — Admin UI surface for suggestions review — done 2026-07-24.
      By the time 6.3a-6.3d shipped, each suggestion type already had its own dedicated review UI
      (Graph audit card, Reconnect suggestions card, Near-duplicate nodes card, Reparenting review
      card) — built incrementally as each sub-phase landed, rather than deferred to a single unified
      list at the end. Auditing what remained of the original "wherever they're triggered from"
      scope found one real, concrete gap: `AddPrerequisite`'s inline redundant-edge check (6.3a
      scenario 1/3) returns `suggestions` in its response, but every frontend caller discarded it —
      the Edit page's staged-commit flow (`commitEdgeChanges`) reduced the response to a bare
      boolean via `settle()`, and the main list page's `acceptReconnectSuggestion` (6.3b's "Reconnect"
      action, itself an `AddPrerequisite` call) did the same. A newly-added prerequisite/unlock, or
      a reconnect accepted after a rejection, could itself make some OTHER edge redundant — and the
      admin never saw it. Fixed by threading the suggestions through instead of discarding them:
      - `addSkillGraphPrerequisite`'s frontend model/service typing changed from an untyped
        `{added: boolean}` to the real `AddSkillGraphPrerequisiteResponse` shape (`added`,
        `suggestions: GraphChangeSuggestion[]`) the backend always returned.
      - Main list page: `acceptReconnectSuggestion` now appends any inline suggestions into the
        existing `redundantEdgeSuggestions` signal (the "Graph audit" card's own list) — no new UI,
        just reusing the existing Dismiss/Remove-edge actions. The card's template gating changed
        from "show the list only after clicking Run graph audit" to "show the list whenever it has
        anything in it," so a suggestion arriving from this different trigger renders immediately.
      - Edit page: `commitEdgeChanges` now carries suggestions through its add-calls (`settleAdd`,
        alongside the existing boolean `settleBool` for remove-calls) instead of discarding them.
        `save()` surfaces any into a new "New redundant-edge suggestion(s)" section (mirrors the
        Reparenting review card's shape/actions) and stays on the page instead of navigating away
        when either that or a reparent review has something to show, with a shared bottom "Done
        reviewing" bar.
      - **Found and fixed a real bug live while verifying this**: staying on the Edit page post-save
        (to show a review card) left the local "pending edit" signals uncleared, so the page kept
        showing the just-committed edge as "X (new — not saved yet)" with an "Undo" button and a
        stale "You have unsaved graph changes" banner — misleading, since the change was already
        saved. Fixed by calling the existing `load()`/`loadFullGraph()` (which already clear pending
        state as a side effect) on the stay-and-review path, not just the failure path.
      Advisory only throughout — nothing is ever removed automatically, same discipline as 6.3a-d.
      +2 Karma tests (inline suggestion appended, no-op when there are none). Verified: `tsc
      --noEmit` clean, 18/18 Skill Graph Karma specs green, full backend suite (30 architecture +
      2,513 unit + 1,359 integration, unchanged — no backend code changed this sub-phase) green, and
      two full live end-to-end passes against the real reseeded dev DB: (1) staged a prerequisite
      add on the Edit page that closed an A→X→B path, confirmed the "New redundant-edge
      suggestion(s)" card appeared, removed it, and confirmed the page came back clean with no
      stale pending-state artifacts; (2) built an A→B→C chain plus an alternate A→D→C path, rejected
      B through the main list's bulk-edit UI, clicked "Reconnect" on the resulting A→C suggestion,
      and confirmed the "Graph audit" card immediately showed "6.3e recon A → 6.3e recon C ... safe
      to remove" without ever clicking "Run graph audit" — proving the inline trigger genuinely
      reaches the UI now.
      **This closes Phase 6.3 (Graph Change Suggestions Service) end to end** — all five scenarios
      from the approved plan (redundant edges, reject-reconnects, near-duplicates, reparenting,
      unified UI surfacing) are implemented, tested, and live-verified.
