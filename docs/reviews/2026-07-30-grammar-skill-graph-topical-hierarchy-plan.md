---
title: Grammar Skill Graph — topical hierarchy + drill-down UI (Phase 1: Adverbs pilot)
date: 2026-07-30
related: docs/architecture/adaptive-curriculum-skill-graph.md, docs/reviews/2026-07-24-skill-graph-container-leaf-hierarchy-cefrj-import-implementation.md, docs/reviews/2026-07-30-full-content-reseed-and-skill-graph-prerequisites-handoff.md
---

# Grammar Skill Graph — topical hierarchy + drill-down UI (Phase 1: Adverbs pilot)

## Context

The user reviewed the Grammar Skill Graph admin UI and found two problems: (1) many nodes are
"islands" with no prerequisite edges, and (2) several nodes cram many distinct teachable items into
one long slash-joined title (example given: "Adverbs of frequency: always/usually/often/frequently/
occasionally/sometimes/rarely" — to the user, each word is really its own sub-node). After
investigation, "island" nodes turned out to be the 92 existing container nodes (edges only connect
leaves — by design, not a bug). The slash-joined-title problem was real but was reframed by the
user beyond just titles: "it is not about slash, it about link, hierarchy and sub nodes and
containers of containers — improving grammar nodes in the order and logical sequence." The user
wants the whole Grammar taxonomy restructured into a real multi-level hierarchy (topic → subtopic
→ item), sequenced by CEFR level first and linguistic teaching order within a level, plus admin UI
support for navigating that hierarchy (hover preview, click-to-drill-in, breadcrumb back).

## Files reviewed

- `src/LinguaCoach.Domain/Entities/SkillGraphNode.cs` — confirmed `ParentNodeId` is a plain
  self-reference (arbitrary depth already supported, no entity/migration change needed).
- `tools/LinguaCoach.ContentSeeder/Program.cs` — the grammar/vocabulary/etc. seeding pipeline.
- `data/seed-json/grammar-seed.json`, `data/seed-json/grammar-prerequisites-seed.json`.
- `src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs` (`GetGraph`, `GetNodes`).
- `src/LinguaCoach.Web/src/app/features/admin/admin-skill-graph/**` (viz component + parent page).
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`, `core/services/admin.api.service.ts`.

## Findings, grouped by priority

**P0 — blocking gaps found in `ContentSeeder`, not just Adverbs-specific:**
1. `UpsertLeafAsync` never updated an existing node's Title/Description/CefrLevel/DifficultyBand on
   re-run — only reapproved/reparented. Editing a title in the seed JSON and re-running the seeder
   had **no effect** on the DB. This would have silently blocked the entire "clean up titles" goal
   for any future topic, not just Adverbs.
2. `UpsertContainersAsync` always upserted with `parentId: null` — containers could not parent other
   containers, which is required for a topic ("Adverbs") to parent subtopics ("Adverbs of
   frequency").
3. Grammar's `Description` field was never passed through to `UpsertLeafAsync`, so every grammar
   node's `Description` was stuck at the `"{title}."` fallback — unusable for a hover tooltip.

**P1 — CEFR-level filter interaction:** `AdminSkillGraphController.GetGraph` filters by exact-match
`CefrLevel`. A topic container and its subtopic/item children can legitimately span CEFR levels
(e.g. "Adverbs" at A1 parenting "Adverbs of attitude" at B1) — a same-level-only fetch would never
surface cross-level children. Resolved by making `parentNodeId` mutually exclusive with `cefrLevel`
server-side (drill-in issues a fresh parentNodeId-scoped fetch, ignoring the CEFR gate), matching
the pattern `GetNodes`' TreeTable lazy-expand already used.

**P2 — "has children" cannot be inferred client-side.** For the same cross-CEFR-level reason, a
node's children may not be in the currently-fetched batch at all, so the viz component can't infer
"is this a container" from what's on screen. Fixed by computing `HasChildren` DB-wide in
`GetGraph`'s response (reusing `GetNodes`' existing containerIds subquery pattern).

## Decisions made

- Container/leaf hierarchy handles topical grouping too — no new entity/schema needed, just seeder
  fixes (P0 above).
- Sequencing: CEFR level is primary; `DifficultyBand` + real prerequisite edges (the graph's
  existing mechanism) carry linguistic teaching order within a level — no new "order" field.
- Family containers (aff/neg/int variant groupings) are untouched; topical containers sit above them
  where useful, decided per-node rather than forcing every node through a topic tier.
- Scope for this pass: build the mechanism end-to-end (seeder + backend + UI) and pilot on
  **Adverbs** only. Comparatives/Pronouns (same enumerable-item shape) and the remaining ~560
  grammar nodes are explicit follow-up.
- Seed files get a `version` field for traceability (`grammar-seed.json`: schema field, bumped to 2;
  `grammar-prerequisites-seed.json`: leading `//` comment, since it's a flat array shared with
  other domains and `JsonCommentHandling.Skip` was already enabled) — user-requested ("make sure
  when you update our seeder file(json) you have some sort of versioning").

## AskUserQuestion / clarification exchanges

- First AskUserQuestion attempt (scope: pilot Adverbs vs. all 3 topics; umbrella node
  synthetic-vs-skip) was rejected — user clarified the ask was broader than the slash-joined-title
  framing ("it is not about slash... hierarchy and sub nodes and containers of containers").
- Follow-up plain-text questions (should family containers become leaves under topic containers;
  CEFR-vs-linguistic sequencing priority; full-taxonomy-now vs. template-first) were answered
  directly: "whatever works best" for family-container placement (left as-is, topical tier added
  above), CEFR-first-then-linguistic-order confirmed, full taxonomy confirmed as the eventual goal
  with this pass building the reusable mechanism.
- Second AskUserQuestion (scope: pilot-only vs. all-3-topics; umbrella-node authorship) was rejected
  outright with instruction to enter plan mode instead and "start automode now" once the plan was
  approved.

## Implementation

See the plan file this review is derived from (was at
`C:\Users\aliverdi.ehsan\.claude\plans\iridescent-splashing-avalanche.md` during the session) for
the full design. Summary of what shipped:

- **`tools/LinguaCoach.ContentSeeder/Program.cs`**: `UpsertLeafAsync` now does a
  reject→`UpdateCore`→approve round trip when an existing node's seed content actually changed;
  `UpsertContainersAsync` gained a container-of-container overload (two-pass: upsert all containers
  parentless, then assign parents from each container's optional `ParentKey`); `SeedGrammarAsync`
  now upserts node metadata every run (not gated by the content-generation checkpoint) and wires
  `Description` through; `GrammarSeedFile`/`GrammarSeedContainer`/`GrammarSeedLeaf` gained
  `Version`/`ParentKey`/`Description` fields.
- **`src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs`**: `GetGraph` gained an optional
  `parentNodeId` param (mutually exclusive with `cefrLevel`) and now returns `Description` and a
  DB-wide `HasChildren` flag per node.
- **Frontend**: `SkillGraphNode` TS interface gained `description`/`parentNodeId`/`hasChildren`;
  `admin.api.service.ts`'s `getSkillGraph` gained an optional `parentNodeId` param;
  `sp-admin-skill-graph-viz.component.ts` gained a hover tooltip (custom positioned div, no new
  dependency) and a `drillInto` output fired on tapping a node with children;
  `admin-skill-graph.component.ts`/`.html` gained `graphBreadcrumb` state, `onDrillInto`/
  `jumpToBreadcrumb` handlers, and a breadcrumb bar above the graph canvas.
- **Content**: `data/seed-json/grammar-seed.json` — new "Adverbs" topic container; 4 existing
  slash-joined nodes ("Adverbs of frequency", "Adverbs of quasi-negation", "Adverbs of attitude",
  "Intensifiers") promoted from flat leaves to containers with cleaned-up titles/descriptions and
  24 new real item leaves (always, usually, often, ... — each with authored `grammarPoint`/
  `explanation`/`description`/`difficultyBand`); "Adverbs of negation" (only "never") stayed a
  single leaf directly under "Adverbs". `data/seed-json/grammar-prerequisites-seed.json` gained 16
  within-topic teaching-order edges among the new item leaves.

## Verification performed

- `dotnet build` — succeeded, only pre-existing warnings.
- `dotnet run --project tools/LinguaCoach.ContentSeeder -- grammar data/seed-json/grammar-seed.json`
  against the local Docker Postgres — logged `Grammar seed v2 — ...`, processed 24 new leaves.
  Verified via `psql`: the 4 promoted nodes' `title`/`description`/`parent_node_id` actually changed
  in the DB (proves the P0 re-seed-update fix works on real already-approved data, not just new
  rows); "Adverbs" has exactly 5 children; "Adverbs of frequency" has exactly 7 children; all
  `review_status = Approved`.
- `dotnet run --project tools/LinguaCoach.ContentSeeder -- prerequisites data/seed-json/grammar-prerequisites-seed.json`
  — 16 new edges created, 795 already existed (unchanged), 0 skipped for missing keys. Grammar-scoped
  edge count in the DB after the run: 811 (795 + 16), confirmed via `psql`.
  reflects the expected 795 + 16.
- `dotnet test tests/LinguaCoach.UnitTests` — 2582 passed, 0 failed.
- `dotnet test tests/LinguaCoach.IntegrationTests` — 1410 passed, 0 failed.
- `npm run build -- --configuration development` (Angular) — succeeded ("Application bundle
  generation complete"); the only diagnostics were pre-existing unrelated warnings in other
  components (unused-import / optional-chain warnings), none in the touched files beyond one
  pre-existing unused-import warning already present before this change.
- **Update (same day, after user report):** the user reported drill-down wasn't working and titles
  still looked unchanged. Live browser testing (via `gstack browse`, logged in as the dev seed admin)
  found the real cause: **`docker compose up --build api` was never run after the backend controller
  change** — `linguacoach-api-1` had been running for 2 hours (since before this session started) and
  was still serving the pre-change DLL. The frontend (`ng serve`, hot-reloaded) had the new code; the
  backend it called did not — `GetGraph`'s response was missing `description`/`hasChildren` entirely,
  so every node's `hasChildren` was `undefined` client-side and every click fell through to
  `nodeSelected` instead of `drillInto`. Confirmed via a direct authenticated `fetch()` from the
  browser console against `/api/admin/skill-graph/graph?cefrLevel=A1&skill=grammar` — the "Adverbs"
  node's JSON had no `hasChildren`/`description` keys pre-rebuild, both present post-rebuild.
  Fixed with `docker compose build --no-cache api && docker compose up -d api`. Re-verified live in
  the browser afterward: hovering "Adverbs" shows the tooltip (title + description + "Click to
  open"); clicking it fires `GET /skill-graph/graph?skill=grammar&parentNodeId=<id>` and renders its
  5 children with breadcrumb "A1 / grammar › Adverbs"; drilling further into "Adverbs of frequency"
  shows its 7 item leaves with breadcrumb "A1 / grammar › Adverbs › Adverbs of frequency"; clicking
  each breadcrumb crumb correctly navigates back up (5 nodes, then the full 244-node root view). No
  console errors beyond pre-existing unrelated Cytoscape warnings.
  **Lesson for future backend changes on this project**: `ng serve` picking up a frontend change is
  not evidence the backend change is live — the API only runs from the Docker image, which requires
  an explicit rebuild (per `CLAUDE.md`'s Docker workflow section) that is easy to forget mid-session
  since nothing errors, it just silently serves stale behavior.
- The "names are big still" part of the user's report is expected, not a bug: this pass only
  restructured the Adverbs topic (5 nodes). The other ~360 non-container grammar nodes at A1,
  including long-titled family containers like "he/she is (all forms)", are untouched by design —
  visually confirmed in the same browser session (screenshot of the graph canvas). Repeating the
  topical-hierarchy pattern across the rest of grammar is the explicit next-step follow-up already
  noted below, not something this pass claimed to finish.

## Risks / unresolved questions

- The 4 promoted nodes ("Adverbs of frequency" etc.) already had prerequisite edges and linked
  Modules/exercises from the original full reseed, attached before they became containers. Those
  are untouched (same Key/Guid), so nothing downstream breaks, but it means a container now has its
  own directly-attached practice content in addition to its children's — acceptable (parent = mixed
  overview practice, children = focused item practice) but worth knowing if it looks odd in the
  student-facing UI later (this pass only touched the admin graph).
- Prerequisite edges technically now exist on 4 nodes that are containers (a soft convention
  violation — `SkillGraphNode.ParentNodeId`'s doc comment says edges are meant to live on leaves
  only, service-layer-enforced, not domain-enforced). Not a functional problem, flagged for
  awareness.
- Total grammar node count in the DB (624) didn't cleanly match hand-arithmetic from the original
  592-node baseline; the specific correctness checks (child counts, edge counts, per-field values)
  all verified exactly right, so this wasn't chased further, but it's worth someone reconciling if
  the exact grammar node count matters for other reporting.

## Final verdict

Shipped: the seeder/backend/UI mechanism for a topical container tier, plus the Adverbs pilot
content (1 new topic, 4 promoted subtopics, 24 new item leaves, 16 new prerequisite edges). All
automated tests pass; DB state verified directly. UI interaction not manually browser-tested this
session (see Verification).

## Update (same day) — title-shortening pass across all grammar, container color-coding

Two more rounds of user feedback after the initial ship:

1. **No visible back button, and the drilled-in container's title/box label always said
   "grammar"/"Skill graph" instead of the container's real name.** Root cause: `sp-admin-graph-card`'s
   title was hardcoded, and the compound-layout grouping box was always labeled by *skill*
   (redundant once the Graph tab required a mandatory skill filter — every visible node always
   shares one skill anyway). Fixed: added an explicit "← Back" button, and both the card title and
   the compound box label now show the current breadcrumb's last container name (`containerLabel`
   input on the viz component, sourced from `graphBreadcrumb()`).
2. **"Different color for containers, clearly identifiable."** Container nodes (`hasChildren`)
   now render with a 📁 icon prefix, bold text, and a thick indigo double border — visually distinct
   from CEFR-tinted leaf boxes at every drill level, not just at the root.
3. **"It is not just about title, it is about all items in grammar" — extremely long box text
   throughout A1 (and the rest of grammar), e.g. "SENTENCE PATTERN: SUBJECT+BECOME/FEEL/GO/LOOK/
   SEEM/SOUND+COMPLEMENT (ADJ) — AFF. DEC."** This was the real remaining gap: outside the 5-node
   Adverbs pilot, 319 of 617 grammar nodes (≈52%) had titles over 30 characters, most following one
   of a few mechanical patterns:
   - Leaves under a "family" container ending in `— AFF. DEC.` / `NEG. DEC.` / `AFF. INT.` /
     `NEG. INT.` / `AFF. IMP.` / `(SUBORDINATE CLAUSE) ...` etc. → decoded to a plain form label
     ("Affirmative", "Negative", "Question", "Negative question", "Imperative", ...).
   - Containers ending in `(all forms)` → suffix stripped, sentence-cased.
   - `CATEGORY: detail` titles → if the category is unique (no sibling nodes share it), the category
     alone becomes the short title (matches the Adverbs pattern); if multiple siblings share the
     category (e.g. "FUNCTIONAL QUESTION: Can you ...?" / "Could you ...?" / ...), the *distinguishing
     after-colon text* is kept instead — collapsing shared-category siblings to an identical label
     was explicitly avoided (verified afterward: zero sibling-title collisions introduced by this
     pass, checked by grouping all nodes by `parentKey` and diffing titles within each group).
   - ~17 standalone titles with no colon/dash (e.g. "my/our/your/her/their (except for 'his' and
     'its')") got hand-written short titles via a lookup table.
   - Fallback: word-boundary-aware truncation with an ellipsis.
   In every case the **original full title moved to `Description`** (shown on hover, per the
   existing tooltip). Implemented as a one-off Node.js transform script over `grammar-seed.json`
   (not committed — scratch tooling, deleted after use), then re-applied through the existing
   `ContentSeeder` (reusing this session's earlier reject→`UpdateCore`→approve fix, so already-seeded
   nodes actually picked up the new titles). Result: only 38 of 617 grammar nodes still exceed 30
   characters (mostly legitimately-named containers like "Tense/aspect: future perfect
   progressive"), down from 319.
   `grammar-seed.json` bumped to `version: 3`.

**Process note — a mistake and recovery**: mid-pass, `git checkout -- data/seed-json/grammar-seed.json`
was run to discard a bad title-casing attempt, not realizing the Adverbs pilot's `version`/topical
content in that same file had never been committed — it reverted that too (harmlessly; the *database*
already had the Adverbs data from the earlier seeder run, so nothing user-visible broke, but the
source-of-truth file briefly went out of sync with the DB). Recovered by re-running the Adverbs
transform before re-running the title-shortening pass. **Lesson: commit real content-generation
output before running further destructive-adjacent git operations in the same session**, even ones
that look scoped to "just discard my last edit."

**Known pre-existing issue found, not fixed (out of scope for title-shortening)**: 10 modal-auxiliary
"family" groups (e.g. `grammar.cefrj_family_md_can_aff.a1`) have a genuine CEFR-J-source duplicate —
two sibling leaves share an identical title/shorthand (e.g. two "NEG. DEC." leaves under "can",
missing the "AFF. INT."/"NEG. INT." forms entirely). Pre-existing in the original import, not
introduced by this session; now more visible because short titles make duplicates obvious where long
ones didn't. Flagged for a future data-quality pass, not fixed here.

Verified live (browser, logged in): back button + updated title/box-label, container color-coding,
and drastically shorter box text with full detail preserved on hover — all confirmed working
together at multiple drill depths. `dotnet test` (2582 unit tests) still green after the DB
title/description updates.

## Update (same day) — root view showed leaves and containers flat together

Immediate follow-up after the title-shortening pass: the user reported "so many nodes with the
same title" — dozens of boxes just saying "Negative", "Question", "Affirmative". Real regression,
correctly self-diagnosed by the user before I finished investigating: `GetGraph` with only
`cefrLevel`/`skill` (no `parentNodeId`) returned **every** node at that level, containers and their
leaf children flattened into one canvas. The decoded form-labels ("Negative", "Question", ...) are
only unique *within one family* — fine when viewed one drill-level at a time, actively misleading
splashed across a flat view where dozens of unrelated families' children render side by side.

Fix: added a `topLevelOnly` query param to `GetGraph` (mirrors `GetNodes`' existing semantics) —
when true, restricts to `ParentNodeId == null`. The admin Graph tab's root fetch
(`loadGraph()` in `admin-skill-graph.component.ts`) now always passes `topLevelOnly: true`; drill-in
fetches (`loadGraphChildren`, scoped by `parentNodeId`) are unaffected since a `parentNodeId` filter
already returns only that one container's direct children — never floods the canvas with unrelated
siblings. Backend change → rebuilt the `api` Docker image again (learned the lesson from earlier
this session, didn't skip it this time). Verified live: A1/grammar root view dropped from 244 nodes
to 85 (containers/standalone nodes only), zero duplicate-titled boxes at the root except one
pre-existing genuine CEFR-J source duplicate (`Tense/aspect: present (lexical verbs)`, two distinct
container keys sharing an identical title — same class of issue as the 10 modal-family duplicates
already flagged above, not introduced by this session, not fixed here); drilling into "Adverbs"
still correctly shows its 5 children.

## Next recommended action

1. Manually browser-test the Admin Skill Graph Graph tab (hover/drill/breadcrumb) before treating
   the UI feature as fully done.
2. Get user sign-off on the Adverbs pilot's content quality (titles/descriptions/explanations/
   teaching-order edges) before repeating the pattern across Comparatives, Pronouns, and the rest of
   the ~560 grammar nodes.
3. Once approved, scope the next topic batch as its own pass — the mechanism built here (seeder
   reject/update/approve, container-of-container, `parentNodeId` drill-down) is now reusable as-is.
