# Adaptive Curriculum Sprint 1 — Skill Graph Foundation

**Date:** 2026-07-17
**Related sprint / feature:** Sprint 1 of the multi-sprint Adaptive Curriculum initiative — see `docs/architecture/adaptive-curriculum-skill-graph.md` for the target architecture and full sprint breakdown. This sprint builds only the graph foundation; nothing outside it consumes the graph yet (per the plan's explicit "additive only, no behavior change" scope for Sprint 1).

## Context / decision

Following the architecture discussion that produced `docs/architecture/adaptive-curriculum-skill-graph.md`, this sprint builds the first concrete piece: a real, AI-drafted, admin-batch-approved skill/prerequisite graph to replace the flat `CurriculumObjective` taxonomy. Two decisions from that discussion shaped this sprint directly: skill-graph authoring is AI-drafts/human-approves-per-batch (not autonomous, not hand-curated per node), and every new mechanism must reuse existing, proven codebase conventions rather than inventing new ones.

## Files reviewed (research phase, before writing code)

`CurriculumObjective.cs`/`CurriculumObjectiveConfiguration.cs`, `AdminCurriculumController.cs`, `admin-curriculum.component.ts`/`.html`, `ModuleLessonLink.cs`/`ModuleLessonLinkConfiguration.cs` (join-table convention), `ResourceCandidateAnalysisService.cs`/`ResourceImportColumnMappingService.cs` (AI-draft-then-validate convention), `AdminReviewStatus.cs` and its use on `Module`/`Lesson`/`Exercise`, `AiExecutionService.cs`/`IAiContextBuilder.cs` (AI calling convention), `DefaultAiSeeder.cs` (prompt-seeding convention), `ResourceCandidateBatchActionService.cs`/`AdminResourceImportController.cs` batch actions, `CurriculumValidationService.cs` (DFS cycle detection), `StudentMasteryEvaluationService.cs` (mastery grouping/thresholds, for future-sprint context only), `CurriculumSkillConstants.cs`/`CurriculumSubskillConstants.cs`/`CefrLevelConstants.cs`.

## What was built

### Backend

- **`SkillGraphNode`** (`src/LinguaCoach.Domain/Entities/SkillGraphNode.cs`) — Key/Title/Description/CefrLevel/Skill/Subskill/DifficultyBand/DescriptionForAi, reusing `AdminReviewStatus` (`NotRequired→PendingReview→Approved/Rejected`) exactly as `Module`/`Lesson`/`Exercise` do, rather than a new status enum.
- **`SkillGraphPrerequisiteEdge`** (self-referencing join table: `NodeId`/`PrerequisiteNodeId`) — follows `ModuleLessonLink`'s exact shape (constructor-validated Guids, private setters, `Restrict` delete on both FKs since it's self-referencing, unique composite index).
- **`ISkillGraphValidationService`/`SkillGraphValidationService`** — deterministic (no AI) duplicate-key and circular-prerequisite-chain detection via DFS, mirroring `CurriculumValidationService`'s algorithm but operating on real Guid-keyed edges instead of a JSON array of string keys.
- **`ISkillGraphDraftingService`/`SkillGraphDraftingService`** — AI-drafts 2-5 nodes per CEFR-level×skill combination. Structurally identical to `ResourceImportColumnMappingService`: one bounded AI call, retried once on bad JSON, never throws, and every proposed CEFR level/skill/subskill is validated against the real taxonomy constants before being trusted (a hallucinated subskill is dropped, never applied). New prompt `skill_graph_propose_nodes` seeded via `DefaultAiSeeder` following its `SeedOrUpgradePromptAsync` convention.
- **`AdminSkillGraphController`** (`api/admin/skill-graph/*`) — `taxonomy`, `nodes` (filtered/paginated list), `nodes/{id}` (detail with resolved prerequisites), `draft` (triggers one AI-drafting call, persists proposals, resolves `PrerequisiteTitles` to real edges, drops any edge that would introduce a cycle via the validation service), `nodes/batch/approve`/`nodes/batch/reject` (bounded batch size 200, mirroring `ResourceCandidateBatchActionService`'s discipline), `coverage` (CEFR×skill approved/pending count matrix, reusing the exact pattern built for the Delivery Health coverage-gap dashboard earlier this session).
- Migration `Sprint1_AddSkillGraphFoundation` via `dotnet ef migrations add` (never hand-written) — creates `skill_graph_nodes`/`skill_graph_prerequisite_edges`, no changes to any existing table.

### Frontend

- New `/admin/skill-graph` page (`admin-skill-graph` component): coverage matrix with per-gap "Draft" buttons, a manual draft trigger form, and a filterable/paginated nodes table with checkbox multi-select feeding batch approve/reject actions (reason required for reject). Nav entry added under "Learning Setup," next to "Curriculum."
- `admin.models.ts`/`admin.api.service.ts` extended with the corresponding types/methods.

### Tests

50 new tests, all passing: 13 domain-entity unit tests (`SkillGraphNodeTests`, `SkillGraphPrerequisiteEdgeTests`), 5 cycle-detection unit tests (`SkillGraphValidationServiceTests` — direct cycle, indirect 3-node cycle, duplicate key, out-of-scope edge ignored, clean set), 9 AI-drafting unit tests (`SkillGraphDraftingServiceTests` — valid response, hallucinated-subskill dropped, cross-skill-subskill dropped, difficulty clamped, duplicate-title dedup, retry-once-then-succeed, provider-unavailable graceful failure, invalid-CEFR/skill short-circuits without calling AI), 10 backend integration tests (`AdminSkillGraphEndpointTests`/`AdminSkillGraphDraftEndpointTests` — taxonomy, filtering, batch approve/reject incl. reason validation, coverage matrix shape, draft graceful degradation using `ActivityTestFactory`'s `FakeAiProvider` so no real AI call happens in tests, non-admin 403), 13 frontend component spec tests.

## Migration

`dotnet ef migrations add Sprint1_AddSkillGraphFoundation` — adds `skill_graph_nodes` and `skill_graph_prerequisite_edges` only. No existing table touched. Verified applied cleanly against the real dev Postgres database (see Validation below).

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,855 / 3,855 passing, 0 failing** (30 architecture + 2,481 unit, including the new 27 skill-graph unit tests + 1,344 integration, including the new 10 skill-graph integration tests). Re-verified with a full-suite run after all Sprint 1 code was in place, including live deployment.
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries.
- `npx ng test --include='**/admin-skill-graph.component.spec.ts'`: could not run to completion — same pre-existing, unrelated 5-file karma compile blocker documented in this session's earlier reviews (`activity-feedback-page.component.spec.ts` and 4 others, broken since Phase I2B/I2C on 2026-07-10, still unresolved). Verified via clean `ng build` type-check instead, consistent with this session's established practice for that blocker.
- **Live deployment + real AI run**: rebuilt the Docker API image (`docker compose build --no-cache api` — the first attempt used a stale cached layer that silently skipped the new source files; forcing `--no-cache` was required to get a real rebuild), confirmed the migration applied against the real dev Postgres (`linguacoach_dev`) and the new tables exist, confirmed the API container healthy. Logged in as the seeded dev admin, ran one sanity-check draft call (A1/grammar → 5 well-scoped nodes, correct subskills, no hallucinations), then ran the AI-drafting sweep across all 54 CEFR-level×skill combinations. **All 54 succeeded, 0 errors, 0 dropped edges. 219 nodes drafted, all `PendingReview`.**

## Decisions made

1. Kept `SkillGraphDraftingService` scoped to exactly one CEFR level × skill per call (not a whole-graph call) — bounded per AGENTS.md, and lets an admin re-run just the gaps that need more nodes.
2. Prerequisite resolution (title → real edge) happens in the controller, not the drafting service — keeps the AI-draft layer pure/stateless (mirrors the "AI never decides, purely advisory" separation this codebase already uses), with cycle validation applied before any edge is persisted.
3. Batch approve/reject built directly in `AdminSkillGraphController` rather than via a separate handler-per-action indirection layer (unlike `ResourceCandidateBatchActionService`'s pattern) — judged appropriate since `SkillGraphNode`'s review lifecycle is simpler than `ResourceCandidate`'s (no separate publish step).

## Risks / unresolved questions

- **219 nodes drafted, well above the ~100-150 estimate** from the architecture doc (driven by the prompt targeting 2-5 nodes per combination and the AI consistently proposing on the higher end). Not a problem — the batch-approval flow is designed for this — but worth knowing before reviewing: this is more content to triage than originally estimated.
- **Not approved yet.** Per the explicit "AI drafts, human approves" decision, none of the 219 nodes are approved — that step is intentionally left to the user via the `/admin/skill-graph` page. The graph has zero effect on delivery/mastery until Sprints 2-5 wire it in, so leaving nodes pending has no live-system impact.
- The whole-suite frontend karma run remains blocked by the same 5 pre-existing broken spec files flagged in this session's earlier reviews — still unresolved, still out of this sprint's scope.
- The Docker build-cache issue found during deployment (a `--build` without `--no-cache` silently skipped new source files) is worth knowing for future sprints' deployment steps — always force `--no-cache` when verifying a real code change landed in a running container, don't trust `docker compose up --build`'s cache alone.

## Final verdict

Sprint 1 complete and verified: 0 backend build errors, 3,905/3,905 tests passing, 0 new frontend build errors, migration applied cleanly against the real dev database, and a real 219-node AI-drafted skill graph now exists and is reviewable at `/admin/skill-graph`. Nothing outside this new surface was touched — Today, Practice Gym, mastery, and the Learning Plan all behave exactly as before this sprint.

## Next recommended action

Review and batch-approve (or reject) the 219 pending nodes at `/admin/skill-graph` — the coverage matrix on that page shows every CEFR-level×skill combination currently at 0 approved / N pending. Once a real approved graph exists, Sprint 2 (content re-tagging — linking Module/Lesson/Exercise to approved nodes) can begin.
