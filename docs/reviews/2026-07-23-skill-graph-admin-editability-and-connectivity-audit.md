# Skill Graph — Admin Editability & Connectivity Audit

**Date:** 2026-07-23
**Related sprint/feature:** Adaptive Curriculum, Sprint 1 (`docs/architecture/adaptive-curriculum-skill-graph.md`), Sprint 2 (Module re-tagging), Sprint 13 (graph viz), Sprint 14.1–14.8 (repair, filters, heatmap).
**Trigger:** User inspected the Skill Graph visualization (screenshot, 2026-07-23) and observed every category cluster (Writing, Grammar, Pronunciation, Speaking, Reading, Listening, Confidence, Vocabulary) rendered as an isolated island with no cross-cluster edges, and asked for an enterprise-level gap audit of admin editability (create/edit/move/relink/retag/find/manual-create/AI-create) before this feature is considered production-ready.

## Files reviewed

- `docs/architecture/adaptive-curriculum-skill-graph.md`
- `src/LinguaCoach.Domain/Entities/SkillGraphNode.cs`
- `src/LinguaCoach.Domain/Entities/SkillGraphPrerequisiteEdge.cs`
- `src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs`
- `src/LinguaCoach.Infrastructure/SkillGraph/SkillGraphDraftingService.cs`
- `src/LinguaCoach.Infrastructure/SkillGraph/SkillGraphValidationService.cs`
- `src/LinguaCoach.Infrastructure/SkillGraph/SkillGraphNodeRepairService.cs`
- `src/LinguaCoach.Application/SkillGraph/SkillGraphContracts.cs`
- `src/LinguaCoach.Web/src/app/features/admin/admin-skill-graph/admin-skill-graph.component.ts`
- Screenshot: `Screenshot 2026-07-23 065136.png` (skill graph viz, category clusters visibly disconnected)

Not read in full: `sp-admin-skill-graph-viz.component.ts` (viz rendering only, not a data-source issue — the API payload itself has no cross-cluster edges, confirmed below), `admin.api.service.ts`/`admin.models.ts` (thin HTTP wrappers, mirror the controller 1:1 — no hidden endpoints found there).

## Root cause of the isolated-cluster graph (confirmed, not cosmetic)

The screenshot is an accurate rendering of the real data — this is a backend/algorithm defect, not a viz bug.

`AdminSkillGraphController.Draft()` (the *only* node-creation path in the system) is called once per single `(CefrLevel, Skill)` combination. Inside it:

- `existingByTitle` is queried with `.Where(n => n.CefrLevel == request.CefrLevel && n.Skill == request.Skill)` (line ~217).
- `allNodesInScope` — the universe used both to resolve `PrerequisiteTitles` into edges and to cycle-check them — is built from `byTitle`, which is seeded from that same CEFR+Skill-scoped query plus the newly drafted batch.

Consequently a prerequisite edge can **only ever be created between two nodes that share both the same CEFR level and the same Skill category**. There is no code path — AI-driven or manual — that can ever produce a `Writing → Speaking` edge, an `A1 → A2` edge, or any cross-category/cross-level edge. The graph is structurally guaranteed to be a forest of disconnected per-(CEFR, Skill) islands, exactly matching the screenshot's 8 isolated clusters (one per `Skill`, each internally sparse because within-cluster edges depend on the AI happening to echo an exact existing title back as a `prerequisiteTitle`).

This also means `SkillGraphValidationService`'s cycle detection is scoped the same way — it can never see a cycle that would only exist across CEFR levels, which is a secondary correctness gap once cross-level edges are added (see Gap 3 below).

## Gaps, grouped by priority

### P0 — blocks production readiness

1. **No node editing.** `SkillGraphNode` has no update method for `Title`, `Description`, `CefrLevel`, `Skill`, `Subskill`, `DifficultyBand`, or `DescriptionForAi` — every setter is `private` and the constructor is the only writer. `UpdateTags` (tags only) and `Approve`/`Reject` (status only) are the sole mutators. The controller has no `PUT`/`PATCH` on `nodes/{id}`. An admin who spots a wrong CEFR level, a badly worded description, or a wrong skill assignment on an AI-drafted node has **no corrective action** except `Reject` (destructive, requires a reason, does not retry with guidance) and hoping a re-`Draft` call produces something better. There is no way to nudge or correct AI output — only accept or discard it.
2. **No manual node creation.** `Draft()` is 100% AI-authored and scoped to one CEFR×Skill combination; there is no `POST /nodes` for an admin to hand-author a node (e.g. a niche competency the AI keeps missing, or an exam-specific skill the taxonomy doesn't cover well).
3. **No manual prerequisite-edge management.** There is no `POST`/`DELETE` on `SkillGraphPrerequisiteEdge` anywhere. Edges are created *only* as a side effect of `Draft()`, from AI-proposed `PrerequisiteTitles` matched by exact string within the same CEFR+Skill scope. An admin cannot link two existing approved nodes, cannot remove a bad edge, and — combined with Gap "root cause" above — cannot ever manually bridge two categories either, since there is no edge-creation surface at all, manual or otherwise.
4. **Structural inability to form cross-skill or cross-CEFR edges** (see Root Cause section). This is the direct cause of the screenshot. Fixing the viz will not fix this — the API payload (`GET /graph`) itself contains zero such edges because none can ever be persisted.
5. **No node deletion**, only `Deactivate()` (soft-disable via `IsActive`). Reasonable as a general policy, but combined with Gaps 1–3 it means the *only* admin lever over a bad node is binary accept/soft-hide — there is no repair loop.

### P1 — serious data-quality / usability gaps

6. **No search.** `GET /nodes` filters only by `cefrLevel`, `skill`, `reviewStatus` — no free-text title/description search, no filter by `contextTag`/`focusTag` even though both are modeled, returned, and rendered as chips. At production scale (the code already references "219/219 nodes" as a past count) an admin cannot find a specific node without paging through the full filtered list.
7. **No "suggest placement" for a node.** `IModuleSkillGraphTaggingService` links **Module → Node**, never **Node → Node**. After a node is created (drafted or, once Gap 2 is fixed, hand-authored), nothing proposes likely prerequisites/successors for it. The user's ask — "after adding a node, suggest a good place for it" — has no equivalent anywhere in the current service layer.
8. **Approval happens after edges are already live.** The architecture doc's decision is "AI drafts, human approves once per batch" as the safety gate on prerequisite logic. In practice, `Draft()` inserts both nodes *and* edges in the same call, before any human review — `GET /graph` explicitly includes `PendingReview` nodes ("cheap regardless of ReviewStatus so a PendingReview node is visible pre-approval too", per the controller's own comment). So the documented safety gate does not actually gate edge creation; it only gates whether content built on the node (Modules, mastery) can later consume it. An unreviewed, potentially wrong prerequisite structure is real and visible in the graph immediately.
9. **Rejecting a node doesn't clean up its edges.** `SkillGraphNode.Reject()` only changes status/reason fields. Any `SkillGraphPrerequisiteEdge` rows pointing to/from a since-rejected node are left in place — dangling edges into content that should no longer be considered a valid prerequisite chain link.
10. **Silent duplicate/collision handling.** `BuildKey()` is a naive slugify of the title; if the generated `Key` collides with an existing one, the node is silently dropped (`if (await _db.SkillGraphNodes.AnyAsync(n => n.Key == key, ct)) continue;`) with no surfaced warning to the admin — a proposal that should have been a near-duplicate flag instead just vanishes from the batch with no `createdCount` explanation beyond "fewer than expected."

### P2 — robustness / scale for an enterprise bar

11. **No edit/audit history.** Once editing exists (Gap 1), there is no versioning or change-log analogous to `ReviewedByUserId`/`ApprovedAtUtc` for content edits — no way to see who changed a node's CEFR level or when.
12. **Fully manual, one-combination-at-a-time drafting.** `Draft()` requires an admin to pick one CEFR×Skill pair per call (up to 6 CEFR levels × 8 skills = 48 manual triggers to seed the whole taxonomy) with no "draft all gaps" batch action, despite `GetCoverage()` already computing exactly which combinations have zero nodes.
13. **Viz reinforces the disconnection rather than surfacing it as a problem.** Per the screenshot, nodes are laid out/colored per `Skill` cluster with no visual cue that isolation is a defect rather than expected structure (no "N nodes have zero prerequisite edges" warning, no cross-cluster suggestion affordance) — worth fixing once the backend can actually produce cross-cluster edges.

## On the pasted "database content"

The user pasted a long "AI Mode conversation" transcript (an English-course-book web search/chat log) as an example of what skill-graph node data allegedly looks like in the database. That text reads as an accidental clipboard paste (a browser AI-search session about CEFR coursebook series), not actual `SkillGraphNode.Title`/`Description` values — nothing in the domain model, controller, or drafting prompt path could produce or store a blob shaped like that; `Title` is capped at 150 chars (`MaxTitleLength`) and `Description` is a short field per the entity's own doc comment ("shown to admins... not shown to students"). This audit could not verify real node title/description quality directly (no DB query tool available in this session) — the connectivity defect above was confirmed from code paths, not from that pasted text. Recommend pulling 10–15 real node rows via the admin Nodes table export or a DB query to separately audit title/description wording quality (the user's "doesn't follow standard English" concern) as a follow-up, since that is a distinct question from the structural isolation issue this audit confirmed.

## Decisions made

None — this is an audit only, no `AskUserQuestion` was run and no implementation was authorized in this pass.

## Implementation tasks produced (not yet scheduled)

1. Add node editing: entity update method(s) gated the same way `Module.UpdateDraft` gates on `ReviewStatus`, plus `PUT /nodes/{id}` endpoint and admin UI form.
2. Add manual node creation: `POST /nodes` (validated against the same taxonomy constants the constructor already enforces) plus an admin "create manually" form, distinct from the existing AI "Draft" trigger.
3. Add manual edge management: `POST`/`DELETE` on `SkillGraphPrerequisiteEdge`, gated through `ISkillGraphValidationService.Validate` for cycles before commit — reuse the same trial-edge-then-validate pattern `Draft()` already uses.
4. **Redesign drafting/edge-resolution scope so cross-Skill and cross-CEFR-level prerequisite edges are structurally possible** — this is the fix for the screenshot's actual defect, not a viz change. Likely needs `Draft()` to consider the full active/approved node set (or at least adjacent CEFR levels) when resolving `PrerequisiteTitles`, not just same-CEFR+Skill matches.
5. Add "suggest placement" — an AI or heuristic pass that, given a new/existing node, proposes candidate prerequisite/successor edges from the full graph (mirrors `IModuleSkillGraphTaggingService`'s shape but Node→Node instead of Module→Node).
6. Add node search: free-text + tag filters on `GET /nodes`.
7. Add "reject cascades to edges" — either block rejection while active edges reference the node, or explicitly remove/flag them on reject.
8. Add batch "draft all gaps" using the existing `GetCoverage()` gap list.
9. Surface duplicate-key collisions to the admin instead of silently dropping the proposal.
10. Separately audit real node title/description quality against real DB samples (see prior section) once query access is available.

## Risks / unresolved questions

- Gap 4 (cross-cluster edges) is the highest-leverage fix but also the riskiest to scope: widening `Draft()`'s edge-resolution universe changes what "one bounded AI call" means and interacts with the existing cycle-detection scope (Gap in validation noted under Root Cause) — needs its own design pass, not a quick patch.
- Whether node editing (Gap 1) should be blocked once `Approved` (mirroring `Module.UpdateDraft`'s convention) or always open (mirroring `UpdateTags`'s deliberate no-gate) is a product decision, not just an implementation detail — the two existing precedents in this same file disagree, so this needs an explicit call before Task 1 is built.
- Manual node/edge creation (Gaps 2–3) raises the same batch-approval question the architecture doc already answered for AI-drafted nodes ("human approves once per batch, not per node") — does a manually created node still enter `PendingReview`, or is manual admin authorship implicitly self-approved? Not decided anywhere in current docs.

## Final verdict

The Skill Graph is **not production-ready**, confirming the user's assessment. Beyond the missing CRUD/edit surface (which alone would block any real content-ops workflow), the graph has a structural defect, not a display bug: prerequisite edges can only ever form within a single (CEFR level, Skill) pair, so the "graph" is currently 48 potential disconnected islands by construction. The AI-draft-then-batch-approve model from the architecture doc is also not fully honored in the current implementation — edges are persisted before human review, and rejection doesn't retract them.

## Next recommended action

Scope Task 4 (cross-cluster edge resolution redesign) and Task 1/3 (node + edge editing) as a dedicated implementation plan before writing code — these three are prerequisites for every other gap listed above being meaningfully usable, and Task 4 in particular needs its own AskUserQuestion pass on how wide the AI's edge-resolution candidate pool should be (same CEFR ± 1 level? whole graph? admin-configurable?).
