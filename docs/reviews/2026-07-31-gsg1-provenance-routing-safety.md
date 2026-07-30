---
title: Phase GSG-1 — Grammar Graph Provenance and Routing Safety
date: 2026-07-31
related: docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md, docs/architecture/adaptive-curriculum-skill-graph.md
---

# Phase GSG-1 — Grammar Graph Provenance and Routing Safety

## Context

The user approved a four-phase sequence (GSG-1 through GSG-4) implementing the 2026-07-30 audit's
recommendations. This phase (GSG-1) delivers the foundational, additive piece: provenance/type
metadata on `SkillGraphNode`, an importer fix so future CEFR fallback resolutions carry confidence
forward instead of discarding it, a backfill of the audit's precisely-computed classification for
all 617 existing grammar nodes, and permanent DB-free validators. Explicitly no mastery/routing
expansion, no edge/relationship typing (GSG-2), no key renaming (GSG-3), no content correction
(GSG-4) — every existing key and relationship preserved.

## Files reviewed / changed

- `src/LinguaCoach.Domain/Enums/CefrConfidence.cs` (new) — Unknown/Fallback/Inherited/Attested/Curated.
- `src/LinguaCoach.Domain/Enums/SkillGraphNodeType.cs` (new) — Topic/Concept/Skill/Variant/BroadReference.
- `src/LinguaCoach.Domain/Entities/SkillGraphNode.cs` — four new properties + `SetProvenanceAndType` mutator.
- `src/LinguaCoach.Persistence/Configurations/SkillGraphNodeConfiguration.cs` — column mappings + two new indexes.
- `src/LinguaCoach.Persistence/Migrations/20260730205115_AddSkillGraphNodeProvenanceAndType.cs` (generated via `dotnet ef migrations add`).
- `src/LinguaCoach.Application/SkillGraph/CefrJGrammarImportContracts.cs` — `Confidence`/`Source` on `CefrJProposedContainer`/`CefrJProposedLeaf`.
- `src/LinguaCoach.Infrastructure/SkillGraph/CefrJGrammarImportService.cs` — `ResolveCefrLevel` returns confidence/source.
- `tests/LinguaCoach.UnitTests/SkillGraph/CefrJGrammarImportServiceTests.cs` — updated + 2 new cases.
- `tools/LinguaCoach.ContentSeeder/Program.cs` — `GrammarSeedContainer`/`GrammarSeedLeaf` gain 4 optional fields; `UpsertLeafAsync`/`UpsertContainersAsync` wire them through via a new provenance overload; `ParseCefrConfidence`/`ParseNodeType` helpers.
- `tests/LinguaCoach.UnitTests/SkillGraph/GrammarSeedIntegrityTests.cs` (new) — 11 tests.
- `data/seed-json/grammar-seed.json` — backfilled, bumped to `version: 4`.
- `docs/architecture/adaptive-curriculum-skill-graph.md` — addendum.

A temporary Node.js backfill script (`gsg1_backfill.js`, scratchpad, computed the classification and
wrote it into `grammar-seed.json`) was deleted after use — same discipline as this session's earlier
title-shortening/Adverbs scripts.

## What shipped

**Domain**: `CefrConfidence` (Unknown/Fallback/Inherited/Attested/Curated) and `SkillGraphNodeType`
(Topic/Concept/Skill/Variant/BroadReference) enums; `SkillGraphNode.CefrConfidence`/`CefrSource`/
`NodeType`/`RoutingEligible` properties, set via the new ungated `SetProvenanceAndType` mutator
(same non-approval-gated convention as `UpdateTags` — supplementary classification metadata, not
re-reviewable core content).

**Migration**: purely additive (`dotnet ef migrations add AddSkillGraphNodeProvenanceAndType`,
applied via `dotnet ef database update`) — four new columns, two new indexes
(`routing_eligible`, `node_type`), no existing column touched, no data loss.

**Importer fix**: `CefrJGrammarImportService.ResolveCefrLevel` now returns
`(Level, Confidence, Source)` instead of `(Level, WasDefaulted)`. The review-screen warning list
behavior is preserved exactly (only the true last-resort "no usable column anywhere" case warns;
resolving via a real Core Inventory/EGP/GSELO fallback column is still genuine evidence and doesn't
warn) — but now *every* resolution's confidence is persisted, not just flagged transiently.

**Backfill**: reimplemented the audit's exact CSV-column-resolution algorithm in a scratch script,
applied to all 617 grammar nodes:

| Confidence | Count | Notes |
|---|---|---|
| Attested | 217 | CEFR-J Level column itself |
| Fallback | 371 | Core Inventory/EGP/GSELO, or fully defaulted (distinguished by `cefrSource`) |
| Inherited | 29 | 5 Adverbs containers (`hand_authored`) + 24 Adverbs item leaves (`inherited_from_category`) |

| NodeType | Count |
|---|---|
| Topic | 1 (`grammar.topic_adverbs.a1`) |
| Concept | 96 |
| Skill | 161 |
| Variant | 330 |
| BroadReference | 29 |

**146 of 617 nodes (23.7%) are routing-eligible** — exactly the Skill/Variant + Attested/Curated
set, verified directly in Postgres (`77 Attested Variant + 69 Attested Skill = 146`). Every
`BroadReference` node (including `PREPOSITIONS`, even though it's `Attested`) is correctly excluded.
Every `Inherited` node (including the real, well-written Adverbs pilot content) is correctly
excluded — conservative by design, matching the audit's "unknown must not be treated as equivalent
to attested."

**Validators**: `GrammarSeedIntegrityTests.cs`, 11 tests — 6 hard-failure structural checks
(duplicate keys/edges, missing parent/edge references, self-loops, cycles — the last reusing the
real production `SkillGraphValidationService` DFS detector rather than a second hand-rolled
implementation), 2 provenance-shape hard failures (every node classified; every routing-eligible
node meets the Skill/Variant + Attested/Curated bar), 3 ratchet-style regression guards (mixed-level
containers ≤56, backward-CEFR edges ≤120, transitively-redundant edges ≤274 — the audit's own
baselines, so a future change can't silently make these worse without a deliberate baseline update).

## Verification performed

1. `dotnet build` on Domain/Persistence/Application/Infrastructure/Api/ContentSeeder — all compile,
   only pre-existing warnings.
2. `dotnet ef database update` against the local Docker Postgres — confirmed all 4 new columns + 2
   new indexes exist via `psql \d skill_graph_nodes`.
3. Ran `ContentSeeder -- grammar data/seed-json/grammar-seed.json` — node metadata upserted every
   run regardless of checkpoint (per the existing 2026-07-30 fix), confirmed via direct `psql`
   queries: a known Attested/Variant node (`grammar.cefrj_md_can_aff.a1`), a known BroadReference
   node (`grammar.cefrj_in_prep_general.a1`, correctly NOT routing-eligible despite Attested), an
   Inherited Adverbs item (`grammar.cefrj_rb_frq_always.a1`), and the mislabeled-title-but-correctly-
   Attested past-simple family (`grammar.cefrj_family_ta_past_do_aff.a1`, `grammar.cefrj_ta_past_do_aff.a1`)
   all show exactly the expected classification.
4. `dotnet test tests/LinguaCoach.UnitTests` — 2594 passed (2582 baseline + 12 CefrJGrammarImportServiceTests + 11 new GrammarSeedIntegrityTests, minus overlap; full run green). One run showed 18 unrelated `ResourceImport.ImportPackagePlan*` failures; reproduced in isolation (both alone and as a group) with 0 failures, confirming pre-existing test-parallelism flakiness unrelated to this phase — a clean re-run of the full suite confirmed 2594/2594 passing.
5. `dotnet test tests/LinguaCoach.IntegrationTests` — 1410 passed, 0 failed.
6. No frontend changes — nothing to verify in the browser, confirming the "no mastery/routing
   expansion" scope was honored (no `AdminSkillGraphController` or Angular file touched).

## Findings during implementation

- **7 grammar nodes in the DB are not present in the current `grammar-seed.json`** — they show
  `cefr_confidence = Unknown`, `node_type = NULL` post-migration since the backfill only reaches
  nodes referenced in the JSON. This matches an earlier-noticed discrepancy this session (DB showed
  624 grammar nodes vs. the seed file's 617) that wasn't chased down at the time. Not a GSG-1
  regression — these are pre-existing orphaned rows. They correctly remain `routing_eligible: false`
  by the migration's own default, so the "no unclassified node is routing eligible" invariant holds
  even for them. **Flagged for a future cleanup pass** (identify and either re-attach them to a real
  seed entry or deactivate them) — not resolved here, out of GSG-1's additive-only scope.

## Risks / unresolved questions

- The ratchet-style regression guards use the audit's exact baseline numbers (56/120/274) hardcoded
  in the test file. If GSG-4 curation work legitimately changes these (hopefully downward), the
  baselines need a deliberate update alongside that work — flagged in the test file's own doc
  comments so this isn't a surprise.
- `NodeType` is nullable at the domain level (to represent "not yet classified") but the backfill
  leaves zero seed-JSON nodes unclassified — the nullable design exists for forward-compatibility
  (e.g. a future admin-created node before an explicit type is chosen), enforced by
  `EveryNodeHasANodeTypeClassification`.

## Final verdict

GSG-1 shipped as scoped: additive metadata, importer fix, full backfill, permanent validators. All
tests green (2594 unit + 1410 integration). No mastery, routing, edge-typing, key-migration, or
content-correction work was done — those remain GSG-2/3/4, each requiring its own scoped plan per
the original audit's own caution about bundling risk.

## Update (same day) — admin graph context menu + Details panel

Follow-up UI request: a right-click (or long-press) context menu per node/container in the admin
Graph tab, offering View/Edit/Details. Per this app's existing 2026-07-23 decision (View/Edit moved
from slide-overs to full routed pages, confirmed with the user before building), View and Edit
navigate to the existing `/admin/skill-graph/nodes/:id[/edit]` pages — no duplicate UI. Details is a
new slide-over (`SpAdminSkillGraphNodeDetailsComponent`, reusing `sp-admin-slide-over`) scoped to
"peek without losing your place in the graph." It's the first admin-visible surface for this
phase's `CefrConfidence`/`CefrSource`/`NodeType`/`RoutingEligible` fields — `GetNode` was extended to
return them. Verified live: right-click on both a container ("Adverbs") and a leaf ("so as not to
DO") opens the menu correctly; Details correctly showed the leaf's real `fallback`/`defaulted`
confidence and `Routing eligible: No` — a concrete demonstration of GSG-1 data working end to end,
not just backfilled and unused. One implementation bug caught and fixed during live testing:
`sp-admin-button-group` renders from an `actions` array input, not projected `<sp-admin-button>`
children — the footer buttons silently didn't render until switched to the correct usage.

## Next recommended action

Scope **GSG-2 (Typed Grammar Relationships)** as its own plan: add relationship types/strength to
`SkillGraphPrerequisiteEdge`, reclassify the 231 "within-family" edges as `variant_of`, reclassify
the synonym/formality-order edges (including the Adverbs pilot's own frequently/often-style edges,
flagged in the audit §16), enforce CEFR monotonicity only for `hard_prerequisite`-typed edges, and
render the transitive reduction by default in the admin graph viz (274 redundant edges currently
always rendered).
