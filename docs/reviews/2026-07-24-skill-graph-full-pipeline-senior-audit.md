---
title: Skill Graph Full Pipeline — Senior Audit (Node Generation → Delivery → Admin Ops)
date: 2026-07-24
related: docs/architecture/adaptive-curriculum-skill-graph.md, Sprints 1-8.1 (Adaptive Curriculum), docs/reviews/2026-07-23-skill-graph-admin-editability-and-connectivity-audit.md, docs/reviews/2026-07-24-skill-graph-admin-ux-bugfix-round.md
review_type: engineering-audit + architecture-review (4 parallel sub-audits)
---

# Skill Graph Full Pipeline — Senior Audit

## Trigger

User (product owner) reviewed the real, well-structured **CEFR-J Grammar Profile**
(`cefrj-grammar-profile-20180315.csv`, 500 rows) and observed that our skill-graph
nodes are far more generic than CEFR-J's atomic grammar items — e.g. a single node
"Subject pronouns (I, you, he, she, it, we, they)" where CEFR-J tracks affirmative,
negative, and interrogative forms per pronoun as distinct, independently-leveled
items. Hypothesis: nodes need a **container (generic) ↔ child/leaf (atomic)**
hierarchy. Requested a senior-level audit of the entire skill-graph pipeline —
node generation, content tagging, mastery/delivery, and the admin workflow — for
every bug or scenario that could break a student's consistent daily learning
experience, plus a report on how the admin pipeline works today.

## Method

Four parallel read-only audits (general-purpose agents), each with a scoped file
list and no code changes:

1. **Granularity vs. CEFR-J** — is the flat 219-node graph too generic; schema fix.
2. **Content-tagging/authoring pipeline** — drafting, retagging, near-duplicate/
   placement/repair services.
3. **Mastery + composer + delivery** — is the graph actually driving what a
   student gets day to day.
4. **Admin workflow** — how the admin pipeline works end to end today, plus its
   own bugs/scalability limits.

## Files reviewed

`docs/architecture/adaptive-curriculum-skill-graph.md`,
`src/LinguaCoach.Domain/Entities/SkillGraphNode.cs`,
`SkillGraphPrerequisiteEdge.cs`, `ModuleSkillGraphNodeLink.cs`,
`src/LinguaCoach.Persistence/Seed/SkillGraph/{a1,a2,b1,b2,c1,c2}.json`,
`src/LinguaCoach.Infrastructure/SkillGraph/{SkillGraphDraftingService,
GraphChangeSuggestionService, NearDuplicateConfirmationService,
NodeGraphPlacementSuggestionService, SkillGraphNodeRepairService,
SkillGraphRoutingService, SkillGraphValidationService}.cs`,
`src/LinguaCoach.Infrastructure/ContentSeeding/ContentSeedingService.cs`,
`src/LinguaCoach.Infrastructure/Mastery/StudentMasteryEvaluationService.cs`,
`src/LinguaCoach.Infrastructure/Composer/CurriculumComposerService.cs`,
`src/LinguaCoach.Infrastructure/{TodayPlanModules,PracticeGymModules}/*SelectionService.cs`,
`src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`,
`src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs`,
`src/LinguaCoach.Web/.../admin-skill-graph/**` (list, audit page, node view/edit/
create, graph viz), plus ~12 prior review docs and the CEFR-J CSV
(`c:\Users\aliverdi.ehsan\Downloads\cefrj-grammar-profile-20180315.csv`, 500 rows).

---

## Part 1 — Granularity: the user's hypothesis is confirmed

`SkillGraphNode` is completely flat (Key, Title, Description, CefrLevel, Skill,
Subskill, DifficultyBand, tags) — no `ParentId`/container concept anywhere in
schema, backend, or frontend. The whole graph is **219 nodes**, AI-drafted 2-5 per
CEFR-level×skill combination and bulk-approved without per-node review (Sprint 1).
The drafting prompt (`DefaultAiSeeder.cs:4877`) explicitly targets **lesson
granularity** — "narrow enough to be teachable in one focused lesson... not 'All
present tenses'" — roughly an order of magnitude coarser than CEFR-J's
per-grammar-form-per-sentence-type granularity.

Concrete collapse examples found in the seed data, matching the user's own example:

| Current node (seed JSON) | CEFR-J rows it collapses |
|---|---|
| `grammar.subject_pronouns.a1` + `grammar.verb_to_be_affirmative.a1` + `grammar.verb_to_be_negative_questions.a1` (3 nodes) | 20 items: `PP.I_am` (A1.1) / `PP.I_am_not` / `PP.am_I` / `PP.am_I_not`, repeated for you/he-she/we/they — each independently CEFR-sub-leveled (rows 2-21) |
| `grammar.present_simple_affirmative.a1` + `grammar.present_simple_negative_questions.a1` | 8 items incl. `TA.PRESENT.do.NEG` (A1.2) vs `TA.PRESENT.does.NEG` (**B1.1** — 2 full levels harder; our 2-node model can't express this) (rows 88-95) |
| `grammar.present_continuous.a1` | `TA.PRPRG.AFF` (A1.3) vs `TA.PRPRG.NEG` (**B1.1**) (rows 96-99) |
| `grammar.demonstratives_this_that.a1` | 8 items, `DT.this.that_is` family (rows 24-31) |

This is the default authoring granularity across the whole graph, not an isolated
gap — every node titled "X: negatives and questions" is standing in for a 4-way
AFF/NEG/INT-AFF/INT-NEG split CEFR-J tracks as separate, separately-leveled items.

### Schema recommendation

**Self-referencing `ParentNodeId Guid?` on `SkillGraphNode`** (one nullable FK
column, `Restrict` delete — same convention `SkillGraphPrerequisiteEdge` already
uses), not a separate container entity. A container conceptually *is* a node
(same Title/Description/CefrLevel/Skill/ReviewStatus shape) — CEFR-J's own ID
scheme (`1` vs `1-1`/`1-2`/`1-3`) treats parent and child as the same table with
a hierarchical key, and introducing a second entity type would force
`SkillGraphPrerequisiteEdge` to support two endpoint types.

Design rules that follow from this:
- **Prerequisite edges live on leaves only.** CEFR-J proves prerequisites are
  more precise at leaf granularity (e.g. `TA.PRESENT.do.AFF` A1.1 is a real
  prerequisite of `TA.PRESENT.do.NEG` A1.2 *within* "Present simple") — a
  container-level edge would erase that. Containers can show a *derived*
  "container X depends on Y" (computed: does any leaf of X edge to any leaf of
  Y), never a second independently-maintained edge set.
- **Mastery lives on leaves, rolled up to containers for display.** Favorable
  finding: there is **no persisted per-node mastery table at all** — mastery is
  computed live via `StudentLearningEvent → StudentExerciseLaunch → ModuleId →
  ModuleSkillGraphNodeLink → SkillGraphNode.Key` (`StudentMasteryEvaluationService`).
  So "container mastery" is just a new aggregation query, not new persisted state.
  The rollup rule (average / weakest-child-gates / %-mastered) is an open design
  question, not resolved by this audit.
- **`ModuleSkillGraphNodeLink` should link to leaves only** — a tightening of the
  existing many-to-many content tagging, not a new concept.
- **AI composer reasons over leaves** (real prerequisite-gap signal); **admin/
  student-facing surfaces show containers** (matches the user's own framing).

### Migration path

No destructive migration risk: there is no persisted per-node mastery table to
lose, and Sprint 6/7 already found only 1/16 seeded Modules has a real node link,
so `ModuleSkillGraphNodeLink` is thin enough to be a near-greenfield backfill.
Existing 219 nodes become containers as-is (`ParentNodeId = null`); new leaf rows
get created underneath the confirmed-collapsed ones. Additive, not destructive —
matches this codebase's established "build the replacement before touching the
old" discipline (Sprint 7 legacy retirement, Sprint 8.1 orphan repair).

### Scale estimate

CEFR-J grammar alone → **~500 leaf nodes** (262 unhyphenated "parent" rows,
238 hyphenated AFF/NEG/INT children) — already >2x the entire current 219-node
graph, for grammar alone. Extending equivalent granularity to vocabulary (CEFR-J
has a companion Vocabulary Profile, not yet reviewed) and to functional-language/
subskills (no CEFR-J equivalent — would need a bespoke taxonomy) plausibly puts
the full graph at **1,500-3,000+ leaf nodes**, 7-14x current size.

**`SkillGraphDraftingService` (free-form AI drafting) is not viable at that scale**
for the grammar layer — it caps context at 40 existing titles / 40 cross-link
candidates per call and targets 2-5 nodes/call; forcing 20-sibling grammar
families through that one-at-a-time would waste AI calls and lose CEFR-J's
free, already-correct sub-leveling (e.g. the do/does A1.2-vs-B1.1 gap).
**Recommendation: import the CEFR-J CSV directly as structured seed data for the
grammar leaf layer** (deterministic, CSV hyphenation = the parent/child
structure, zero hallucination risk, real EGP/GSELO cross-references for free).
Reserve AI drafting for what it's good at: proposing **containers** (~260, a
much smaller, well-bounded task) and for vocabulary/functional-language/subskill
nodes with no external structured source.

### Risks / unresolved

- The architecture doc explicitly required "a concrete node-count target before
  Phase 1 starts" — this was never decided; Sprint 1 shipped 219 nodes anyway.
  This audit is effectively that overdue decision point.
- No existing precedent in this codebase for a self-referencing tree on any
  entity — worth a short design pass even though the schema diff is one column.
- Container mastery rollup rule undecided.
- Non-grammar skills have no CEFR-J-equivalent source; their atomic-granularity
  criteria would need to be invented, not imported.
- Review-surface growth: ~500 grammar leaves + ~260 containers is materially
  more than 219 flat rows — the existing "bulk-approve once per batch" model
  (already self-critiqued in Sprint 1 as a rubber stamp) needs to scale via
  per-container batch grouping (approve a container + all its children as one
  unit), which does not exist in the admin UI today (see Part 4).

---

## Part 2 — Content-tagging / authoring pipeline

AI-hallucination defenses (taxonomy-constant validation, candidate-list-only key
resolution, bounded/retried calls) are **solid and consistent** across drafting,
tagging, and placement services. The real defects are **lifecycle consistency**
gaps — graph edits/rejections/merges never cascade to content links:

- **Rejecting an already-Approved node with real content links is allowed** —
  `SkillGraphNode.Reject()` has no `ReviewStatus` guard (unlike `UpdateCore`),
  and `BatchReject` calls it unconditionally. The node vanishes from the
  content-coverage dashboard, but its `ModuleSkillGraphNodeLink` rows are never
  touched — a Module silently "believes" it still teaches a node the graph no
  longer considers valid, no cascade, no admin-visible signal. **High.**
- **`MergeNodes` never touches `ModuleSkillGraphNodeLink`** — prerequisite edges
  are correctly repointed to the keep node, but content links to the merge-away
  node just die when it's deactivated (`IsActive=false` is filtered everywhere).
  A merge that's supposed to consolidate duplicates actually **loses** tagged-
  content signal outright. **High.** Compounds directly with the next finding.
- **Near-duplicate detector is likely to misfire on exactly the CEFR-J-style
  minimal pairs this audit is about** — `BigramDiceSimilarity` blends title
  (0.7) + full description (0.3); minimal pairs like "I am" / "I am not" /
  "Am I?" typically share almost all description text, differing in one clause.
  No existing test covers this shape (only unrelated-topic and typo cases are
  tested). Real risk: as CEFR-J-granularity nodes get drafted, an admin
  following the audit page's "Confirm merge" flow could destructively collapse
  legitimately distinct nodes. **Medium, will become live risk exactly as the
  Part 1 recommendation is implemented — needs a regression test and/or a
  tweaked comparison (e.g. exclude the differentiating final clause) before
  CEFR-J granularity ships.**
- **Modules with null `CefrLevel`/`Skill` are permanently untaggable** — both
  `ContentSeedingService.TagModuleAsync` and the manual re-tag endpoint hard-
  require both fields non-null; no dashboard distinguishes this from "not yet
  swept." **Medium.**
- **`SkillGraphRoutingService` will recommend a node with zero deliverable
  content** as a normal, successful recommendation if every content candidate
  is ineligible (archived/unapproved) — only a fully-empty candidate list
  triggers the documented fallback. This is the literal "dead end the composer
  can never deliver" scenario the architecture doc worries about. **Medium-High.**
- **Single-node AI repair (`POST nodes/{id}/repair`) can 500 on provider
  failure** — unlike every other AI call site in this subsystem, which retries
  once then degrades to `Success:false`. Bulk repair is safe; the single-item
  endpoint is not. **Medium.**
- Module-to-node tagging is scoped to exact same CefrLevel+Skill as the Module
  — the same "island" scoping issue the 2026-07-23 audit found and partially
  fixed for node-to-node edges, not yet applied to Module tagging. **Low-Medium.**

---

## Part 3 — Mastery, composer, and daily delivery

**Is the graph actually wired into delivery?** Partially. Per-node mastery
(Sprint 4) and goal-vector (Sprint 5) are genuinely read by
`TodayPlanModuleSelectionService`/`PracticeGymModuleSelectionService`. But
**prerequisite-edge traversal is never consulted by any delivery/sequencing
path** — its only consumer, anywhere in the codebase, is admin-time cycle
validation. The architecture doc's central promise ("a planner reasoning over
skill-graph gaps in valid prerequisite order") is **unimplemented**. Delivery
today is effectively CEFR+tag filtering with an AI re-rank on top — exactly the
"tags-and-filter shortcut" the architecture doc says this initiative was meant
to move past.

- **No prerequisite-order enforcement anywhere** — a student can be served a
  node whose prerequisites have zero mastery evidence. Masked today by the
  separate CEFR filter, but a same-band ordering violation (B mastery-gated on
  A, both B1) is entirely possible. **High.**
- **`IsBlocked`/`BlockedByObjectiveKey` (the Journey page's "locked" UX) is
  hardcoded false/null at every construction site** — the designed
  prerequisite-lock feature is entirely inert; `BlockedObjectives` counts are
  permanently 0. **High** — silently defeats a designed safety/UX feature.
- **No forgetting-curve/time-decay in mastery** — `ReviewRecencyWindowDays` is
  defined and configured but referenced by zero code. A node mastered a year
  ago and never revisited stays "Mastered" forever. Contradicts both the
  architecture doc's "spacing/forgetting-curve signals" promise and
  `docs/architecture/repetition-and-novelty.md`. **Medium-High.**
- **`ModuleSkillGraphNodeLink.Confidence` is captured at authoring time, never
  read during mastery evaluation** — a weakly-linked Module credits a node's
  mastery exactly as much as a strongly-linked one. **Medium.**
- **Deactivating/rejecting a node orphans a student's real mastery history
  against it** (no remap) — same lifecycle-consistency class of bug as Part 2.
  **Medium.**
- **Goal-vector explicit re-weighting is instant/unbounded** (no smoothing),
  unlike implicit engagement drift (a bounded EMA) — a goal switch can whiplash
  next-day content with zero transition. Arguably intentional per the
  architecture doc's "goal-switching is free" framing, but currently a silent
  side effect rather than a stated product decision. **Low-Medium.**
- **Composer candidate pool hard-capped at 40 via positional `.Take()`** — no
  diversity-aware sampling; moot at current content volume, will matter as
  content grows. **Low today.**
- **Content-thinness, already self-documented by Sprints 4/7**: only 1/16
  seeded Modules is node-linked — the parts of the pipeline that *are* wired
  (mastery, goal-vector) are largely inert today for lack of content to act on.

**Verdict:** mastery classification math itself (`ComputeSignal`/`ClassifyStatus`)
is defensively written (no crashes on 0/1/conflicting events, correct multi-node
fan-out, real seeded-DB tests) — the gap is structural (no prerequisite
enforcement, no decay) and content-thinness, not broken arithmetic. A student
using the product today would experience something closer to a CEFR+tag filter
than the "AI teacher" the architecture doc targets.

---

## Part 4 — Admin pipeline

### How it works today (reference walkthrough)

1. **Draft** — admin picks one CEFR level + one skill (or clicks a coverage-
   heatmap gap cell) → one bounded AI call → nodes land `PendingReview`;
   proposed edges are cycle-validated against the full active graph before
   persisting (dropped edges reported, not silently lost).
2. **Manual create/edit** — hand-authored via a create page, or edit an
   existing node. Edits are fully staged (nothing writes until "Save"). Core-
   field edits are blocked once `Approved` (must reject first, which re-queues
   to `PendingReview`); tag edits are deliberately ungated; edge add/remove is
   also ungated by review status.
3. **Review queue** — the paginated (25/page) Nodes table shows Pending,
   Approved, and Rejected together unless an admin explicitly filters by
   status.
4. **Bulk approve/reject** — "Bulk edit" mode selects rows on the *currently
   loaded page only*; fires immediately with **no confirmation dialog** and
   **no guard against re-approving/re-rejecting an already-reviewed node**.
   Reject cascades: touching prerequisite/dependent edges are deleted
   server-side, with "reconnect" suggestions offered to bridge former
   neighbors.
5. **Audit page** — a dedicated page auto-running 5 checks on load: missing
   tags, isolated nodes, redundant edges, near-duplicates (+ a content-coverage
   heatmap on the main page). Edge-removal and near-duplicate merge are
   *staged* (as of the 2026-07-24 fix round) — nothing applies until an
   explicit "Save"/"Confirm merge". Missing-tags "Fix with AI" is not staged
   (applies directly).
6. **Merge** — repoints prerequisite edges onto the keep node, cycle-validates,
   soft-deactivates the merge-away node. Never touches `ModuleSkillGraphNodeLink`
   (Part 2/4 finding).
7. **Graph viz** — fetches the *entire* active node+edge set in one unpaginated
   call, rendered in Cytoscape.js; also fetched redundantly by the node
   View/Edit pages just to compute a small local neighborhood.

### Admin-pipeline findings

- **Bulk approve/reject: no status guard, no confirmation, no undo.** An
  accidental reject of an already-`Approved`, content-linked node silently
  drops it out of mastery/routing resolution (both filter strictly on
  `Approved && IsActive`). Real, exploitable **today** at 219 nodes, not a
  future risk. **High.**
- **`MergeNodes` orphans `ModuleSkillGraphNodeLink`** (same finding as Part 2,
  confirmed independently from the admin-UX angle). **High.**
- **Prerequisite/unlock picker silently truncates**: frontend requests
  `pageSize: 500`, backend clamps to 200 — already drops ~19 of 219 nodes from
  the picker today with no error and no search fallback. Will be unusable at
  thousands of nodes. **Medium, active today.**
- **Full unpaginated graph-viz fetch, called redundantly 3x** (list page,
  node View, node Edit) just to render small local neighborhoods in 2 of the
  3 cases. Already self-flagged in the component's own comments as borderline
  illegible at 219 nodes. **Medium, compounds directly with node count.**
- **No cross-page bulk selection**, hardcoded 25/page — reviewing a large
  PendingReview backlog (e.g. a CEFR-J import landing hundreds of leaves at
  once) requires many manual batches even though the backend supports up to
  200/call. **Medium (scalability).**
- **Zero hierarchy support anywhere** — schema, models, and UI are all flat.
  Confirms Part 1's schema gap independently. If `ParentNodeId` lands, the
  admin UI needs (none of which exists today): a tree/outline browse view,
  subtree-scoped bulk actions, a parent-picker on create/edit (same
  truncation risk as above, worse at scale), reparenting UX, containment-aware
  graph-viz rendering, and a heatmap/draft-workflow rethink for the exploded
  combination count.

**Verdict:** the core create/review/audit loop works as designed for ~219 flat
nodes and has been iteratively hardened (staged edits, cascade-safe rejection,
cycle validation everywhere, advisory-only AI suggestions). But two safety bugs
are exploitable **today**, independent of any future scale change, and there is
no headroom — schema, UI, or workflow — for either "thousands of nodes" or
"hierarchical structure."

---

## Consolidated bug list (by severity)

**High**
1. **[RESOLVED 2026-07-24]** Bulk approve/reject has no `ReviewStatus` guard and no confirmation —
   `AdminSkillGraphController.cs` `BatchApprove`/`BatchReject`, `SkillGraphNode.Reject()`. Fixed:
   `BatchReject` now returns an impact summary (`requiresConfirmation`, `impactedApprovedCount`,
   `impactedTotalLinkedModules`, `impactedNodes`) without mutating anything when the batch includes
   a currently-Approved node and the caller hasn't set `Confirm = true`; the admin UI shows a
   `sp-admin-modal` confirmation before resubmitting with `confirm: true`. Both `BatchApprove` and
   `BatchReject` now write an `AdminAuditLog` row per node (actor/old status/new status/reason),
   closing the related "no audit trail" gap from Part 4. Covered by 4 new integration tests
   (`AdminSkillGraphEndpointTests.cs`) and 4 new component tests
   (`admin-skill-graph.component.spec.ts`); full backend suite (3,918 tests) and frontend suite
   green after the change. Adversarial review confirmed: no partial-batch mutation, no
   TOCTOU/race window, no crash on a stale-confirm double-reject, sound TypeScript discriminated
   union against the real wire format. Known, accepted scope limit: `confirm: true` is a bare
   request flag, not bound to a prior gated call — this protects against an accidental UI bulk
   action, not a scripted/direct API caller deliberately bypassing it; hardening that further
   would need a real workflow/ticket mechanism, out of scope for this fix.
2. **[RESOLVED 2026-07-24]** `MergeNodes` never repoints/cleans
   `ModuleSkillGraphNodeLink` — silent content-link orphaning on every merge.
   `AdminSkillGraphController.cs:971-1021`. Fixed: `MergeNodes` now repoints
   every `ModuleSkillGraphNodeLink` touching the merge-away node onto the keep
   node (mirroring the method's existing edge-repoint pattern), dropping a link
   as a duplicate instead of violating the unique `(ModuleId, SkillGraphNodeId)`
   index when a Module was already linked to both nodes. Response gains
   `relinkedModuleCount`/`droppedDuplicateModuleLinkCount`; the audit page's
   merge-confirm flow now shows these counts as a success message instead of
   applying silently. An `AdminAuditLog` row is now written per merge (closing
   the related "no audit trail for the highest-blast-radius action" gap from
   Part 4). Covered by 3 new integration tests (relink, dedup-on-overlap,
   zero-content case) and 1 new component test; full backend suite (3,921
   tests) and frontend suite green. Adversarial review found no bugs — the
   unique-index/dedup logic, EF change-tracking, and `Confidence` fidelity all
   held up; one cosmetic wording issue in the merge-status message was fixed.
3. Same as #1 from the content-lifecycle angle: rejecting an Approved node
   leaves its content links stale with zero cascade/signal.
4. **[PARTIALLY RESOLVED 2026-07-24 — soft signal only, hard filter deliberately deferred]**
   No prerequisite-order enforcement anywhere in delivery — schema exists,
   nothing reads it outside admin-time cycle validation. Fixed (scoped, per
   explicit decision this session): `TodayPlanModuleSelectionService` and
   `PracticeGymModuleSelectionService` now compute a real `HasUnmetPrerequisite`
   fact per candidate — true when a candidate's linked skill-graph node has a
   *direct* (one-hop) prerequisite the student is currently `AtRisk` on, never
   for a merely-`Weak` or never-attempted prerequisite (bootstrap-safety:
   otherwise nothing could ever be shown to a brand-new student). This fact is
   surfaced to the AI composer's prompt as a deprioritization signal —
   `CurriculumComposerRankCandidatesContent` now explicitly instructs "prefer a
   candidate without this flag... not a hard rule." **Deliberately not a hard
   pool filter** — given most content still isn't node-linked and Today/Gym
   already fall back to generic content often, a hard filter risked making
   that fallback problem worse before there's content depth to judge the
   impact; the hard-filter question is an explicit, still-open follow-up
   decision, not resolved here. Also deliberately one-hop only (a C→B→A chain
   where only A is AtRisk does not flag C) — transitive prerequisite-chain
   awareness is out of scope for this fix. Covered by 10 new unit tests across
   both selectors (AtRisk-flags-true, never-attempted-doesn't-flag,
   Weak-but-not-AtRisk-doesn't-flag, no-prerequisite-edges, no-linked-node,
   two-hop-chain-not-flagged); full backend suite (3,930 tests) green.
   Adversarial review verified both load-bearing claims by tracing the actual
   code (not assumption): a never-attempted node structurally cannot appear in
   `AtRiskObjectiveKeys` (dead-code path, not "usually excluded"), and the
   one-hop scope is honestly documented, not overclaimed as full
   prerequisite-chain resolution. No bugs found; one coverage gap (the
   two-hop case lacked a regression test) was closed before considering this
   done.
5. **[PARTIALLY RESOLVED 2026-07-24 — creation-time only, no incremental
   re-evaluation]** `IsBlocked`/`BlockedByObjectiveKey` (Journey "locked" UX)
   permanently inert — hardcoded false/null everywhere it's constructed. Fixed:
   `LearningPlanService.BuildObjectiveSequenceAsync` now runs a post-pass
   (`ApplyPrerequisiteBlockingAsync`) after building the objective sequence,
   marking an objective blocked when its skill-graph node has a direct
   `SkillGraphPrerequisiteEdge` prerequisite the student is currently `AtRisk`
   on — a direct structural port of Fix #4's already-verified algorithm and
   threshold (AtRisk-only, one-hop-only, bootstrap-safe) to this call site.
   Unlike Fix #4, this doesn't add a new gate — the `.Where(!o.IsBlocked)`
   filters in `GetProgressAsync`/`GetNextPlannedObjectiveAsync`/
   `GetPracticeGymObjectivesAsync` and the `BlockedObjectives` counters already
   existed and were already correct, just permanently no-op'd on a
   hardcoded-false input; this fix supplies the real data those consumers were
   always waiting for, not a new policy decision — so no fresh AskUserQuestion
   was needed this round. Also discovered and noted: `Unblock()` was equally
   dead (never called anywhere) — the whole block/unblock lifecycle was inert,
   not just the "set true" half. **Deliberately not wired for incremental
   re-evaluation** — blocked status is only recomputed on full plan
   regeneration (placement completion, preference change, CEFR change, mastery
   sweep — this codebase's existing regen triggers, which already supersede
   and rebuild the whole objective set from scratch), not mid-plan as
   mastery changes; an explicit, stated follow-up, not a silent gap. Covered
   by 6 new unit tests (`LearningPlanBlockingTests.cs`) constructing
   `LearningPlanService` directly with a real `SkillGraphRoutingService` and
   the existing `FakeStudentMasteryEvaluationService`; full backend suite
   (3,937 tests) and the existing Learning Plan unit/integration suites green
   unmodified. Adversarial review found one low-severity, non-blocking issue
   (nondeterministic `BlockedByObjectiveKey` attribution when an objective had
   2+ simultaneously-AtRisk direct prerequisites — `IsBlocked` itself was
   always correct regardless) and fixed it before considering this done.

**Medium-High**
6. `SkillGraphRoutingService` can recommend a node with zero deliverable
   content as a normal success — the literal "dead end" scenario.
7. No forgetting-curve/decay in mastery despite config scaffolding
   (`ReviewRecencyWindowDays` unused).

**Medium**
8. Near-duplicate detector likely mis-flags CEFR-J-style minimal-pair nodes
   (untested shape) — risk activates exactly when Part 1's fix ships.
9. `ModuleSkillGraphNodeLink.Confidence` captured but never used in mastery.
10. Deactivated/rejected nodes orphan a student's existing mastery history.
11. Modules with null CefrLevel/Skill are permanently untaggable, invisible
    to coverage tooling.
12. Single-node AI repair endpoint can 500 (only AI call site without
    retry/graceful-degrade in this subsystem).
13. Prerequisite/unlock picker truncates at 200/219 nodes today.
14. Full unpaginated graph fetch, called redundantly 3x per node open.
15. Hardcoded 25/page bulk selection, no cross-page "select all matching".

**Low-Medium**
16. Goal-vector explicit reweighting is instant/unbounded (possibly by
    design, but undocumented as such).
17. Module-to-node tagging scoped to exact CefrLevel+Skill (same "island"
    issue already partially fixed for node-to-node edges).

**Low**
18. Composer 40-candidate cap via positional truncation, no diversity sampling.
19. Mastery classification boundary case (`avgScore == 80`, low evidence)
    falls through to an unlabeled catch-all.
20. Stale doc comment on `StudentGoalWeight` claiming it's unconsumed.

---

## Decisions made this session

None finalized — this audit is explicitly a decision *input*, not a decision.
The user's container/leaf hypothesis is confirmed correct with concrete
evidence; the schema recommendation (self-referencing `ParentNodeId`, edges/
mastery/content-links at leaf granularity, CEFR-J CSV import for grammar
leaves) is proposed but requires explicit user sign-off before implementation,
per this being exactly the kind of foundational, hard-to-reverse call the
original architecture doc flagged as needing "a concrete node-count target
before Phase 1 starts" and never got.

## Implementation tasks produced (candidate backlog, unscoped)

1. Add `ParentNodeId` to `SkillGraphNode` + validation (no linking content/
   edges to a node that has children).
2. Build a CEFR-J CSV → leaf-node structured importer (grammar first).
3. Fix bulk approve/reject: status guard + confirmation step showing counts.
4. Fix `MergeNodes` to repoint `ModuleSkillGraphNodeLink` onto the keep node.
5. Wire prerequisite-edge traversal into `SkillGraphRoutingService`/
   composer selection (the actual sequencing feature).
6. Wire or remove `IsBlocked`/`BlockedByObjectiveKey` — currently dead code
   masquerading as a feature.
7. Implement or remove `ReviewRecencyWindowDays` decay logic.
8. Add near-duplicate regression test for minimal-pair (AFF/NEG/INT) nodes;
   tune `BigramDiceSimilarity` before CEFR-J granularity ships.
9. Admin UI: tree/outline view, subtree bulk actions, parent-picker,
   reparent UX — required before/alongside item 1 shipping to users.
10. Fix picker truncation (server-side search instead of pageSize bump).

None of these are scoped or scheduled — this list is input to a future
sprint-planning pass, not a committed plan.

## Risks / unresolved questions

- Container mastery rollup rule (average vs. weakest-child-gates vs. %-mastered)
  undecided.
- Non-grammar skill granularity (vocabulary, functional-language, subskills)
  has no CEFR-J-equivalent source; needs its own design pass.
- Review-surface growth (~760 rows vs. 219) needs a container-batch review UX
  that doesn't exist yet.
- No prior precedent in this codebase for a self-referencing tree entity.

## Final verdict

The user's hypothesis is correct and evidence-backed: the current skill graph
is generic to roughly lesson-granularity, about an order of magnitude coarser
than CEFR-J's masterable-item granularity, and has no container/leaf structure
at all. Beyond that specific gap, this audit found the graph is not yet
actually driving day-to-day delivery as designed (no prerequisite enforcement,
no decay, thin content links), and the admin tooling has two exploitable
safety bugs today plus zero headroom for either scale or hierarchy. None of
this is "broken beyond repair" — the codebase is consistently honest in its
own prior review docs about exactly these gaps — but the distance between the
architecture doc's promise and current behavior is large enough that it
should be treated as a real project checkpoint, not a routine bug list.

## Next recommended action

1. Get explicit user sign-off on the Part 1 schema recommendation (this is a
   foundational, hard-to-reverse call per the user's own standing framing for
   this initiative).
2. Update `docs/architecture/adaptive-curriculum-skill-graph.md` to record the
   granularity decision once made (its own "Risks" section currently has this
   as an open, unresolved question).
3. Fix the two High-severity admin safety bugs (#1/#2 above) independent of
   the granularity decision — they're live risks today regardless of what
   happens next.
4. Scope a dedicated sprint plan for the CEFR-J import + hierarchy schema
   change before writing any code, per this repo's usual phase discipline.

---

## Documentation impact

- Docs reviewed: `docs/architecture/adaptive-curriculum-skill-graph.md`, all
  Adaptive Curriculum sprint review docs (1, 4, 5, 7, 8.1), the two most recent
  Skill Graph admin review docs (2026-07-23, 2026-07-24).
- Docs updated: this review added under `docs/reviews/`.
- Docs intentionally not updated: `docs/architecture/adaptive-curriculum-skill-graph.md`
  itself — its granularity decision is a proposal pending user sign-off, not
  yet a decision; updating the architecture doc's "locked-in" direction before
  that sign-off would misrepresent the doc's own stated authority.
- Reason: per CLAUDE.md, architecture-direction changes are only made once the
  decision is actually made, not while still proposed.
