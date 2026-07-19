# Adaptive Curriculum Sprint 2 — Content Re-Tagging

**Date:** 2026-07-20
**Related sprint / feature:** Sprint 2 of the multi-sprint Adaptive Curriculum initiative — see `docs/architecture/adaptive-curriculum-skill-graph.md` for the target architecture and `docs/reviews/2026-07-17-adaptive-curriculum-sprint1-skill-graph-foundation-review.md` for Sprint 1 (skill graph foundation, 219 approved nodes). This sprint links content to that graph; nothing outside the new surface consumes it yet (delivery/mastery still untouched, per plan).

## Context / decision

Sprint 1 built a real, approved 219-node skill graph. Sprint 2's job is to connect it to actual content — which `Module`s teach which nodes — so later sprints (mastery, delivery) have something real to read. Per the master plan, tagging is scoped to **Module granularity only**, not Lesson/Exercise: `TodayPlanModuleSelectionService`/`PracticeGymModuleSelectionService` select at the Module level, and the existing content base is small (2 approved Modules at sprint start) — finer-grained tagging would be premature.

## Files reviewed

`ModuleLessonLink.cs`/`ModuleExerciseLink.cs` (join-table convention, confirmed identical shape reused for the new link), `Module.cs` (constructor/field nullability — `CefrLevel`/`Skill`/`Description` are all nullable, which changed one query filter from what was originally assumed), `SkillGraphDraftingService.cs` (mirrored exactly for the new tagging service), `AdminSkillGraphController.cs` (Sprint 1, extended in place), `CurriculumContextTagConstants.cs` (curated the goal-tag subset here per plan).

## What was built

### Backend

- **`ModuleSkillGraphNodeLink`** — many-to-many join entity (`ModuleId`/`SkillGraphNodeId`/optional `Confidence`), following `ModuleLessonLink`'s exact convention: cascade delete on the `Module` side only, no DB-level FK on the `SkillGraphNode` side (nodes are soft-deactivated via `IsActive`, never hard-deleted), unique composite index.
- **`IModuleSkillGraphTaggingService`/`ModuleSkillGraphTaggingService`** — AI proposes which of a Module's CEFR/skill-matched *approved* nodes it covers. Structurally identical to `SkillGraphDraftingService`: one bounded AI call per Module, retried once on bad JSON, never throws, every proposed node key checked against the real candidate list before being trusted (a hallucinated key is dropped). New prompt `module_skill_graph_propose_coverage` seeded via `DefaultAiSeeder`.
- **`AdminSkillGraphController` extended**: `POST retag-modules` (sweeps up to 20 approved, non-archived, untagged Modules per call, auto-applies every validated match — no per-link approval step, per the earlier "auto-apply, spot-checked via coverage dashboard" decision), `GET content-coverage` (which approved nodes have zero linked Modules — the real Sprint 2 gap, distinct from Sprint 1's node-existence coverage matrix).
- **`CurriculumContextTagConstants.GoalTags`/`IsGoalTag()`** — curated 8-tag subset (`GeneralEnglish`, `DayToDay`, `Travel`, `StudyAcademic`, `MigrationSettlement`, `JobInterviews`, `SocialConversation`, `Workplace`) of the existing 13-tag list, marking which tags represent a genuine student motivation vs. a skill/format descriptor (`Pronunciation`, `ListeningConfidence`, `WritingConfidence`, `ExamInspired`, `Custom`). This is the taxonomy Sprint 3's goal vector will draw from — no new schema, just a curated constant.
- Migration `Sprint2_AddModuleSkillGraphNodeLinks` via `dotnet ef migrations add` — one new table, no existing table touched.

### Frontend

`/admin/skill-graph` extended with a "Content coverage" card: shows nodes-with-content vs. nodes-without-content counts and a table of uncovered nodes, plus a "Re-tag next batch of Modules" trigger button.

### Tests

39 new tests, all passing: 5 `ModuleSkillGraphNodeLink` entity tests, 8 `ModuleSkillGraphTaggingService` unit tests (valid match, hallucinated-key dropped, missing-confidence default, duplicate-key dedup, empty-candidates short-circuit, retry-once, provider-unavailable graceful failure), 6 `CurriculumContextTagConstantsTests` (goal-tag membership both directions, subset-of-All invariant, count), 16 backend integration tests (content-coverage with/without linked Module, retag-modules sweep behavior and graceful AI degradation, non-admin 403 for both new endpoints).

## Migration

`dotnet ef migrations add Sprint2_AddModuleSkillGraphNodeLinks` — adds `module_skill_graph_node_links` only. Verified applied against the real dev Postgres.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,891 / 3,891 passing, 0 failing** (30 architecture + 2,511 unit + 1,350 integration).
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries.
- **Live deployment + real run**: forced `docker compose build --no-cache api` (per the lesson learned in Sprint 1 — a plain `--build` reuses a stale cache and silently skips new source), confirmed the migration applied and the new table exists, confirmed API healthy. Ran `POST retag-modules` against the real dev DB.

## A real finding from the live run, not a bug

`retag-modules` returned `sweptCount: 0`. Investigated directly against the DB: **both of the 2 currently-approved Modules are archived** (`is_archived = true`), and the endpoint correctly excludes archived Modules (they're not eligible for real delivery either way). `content-coverage` confirms: **0 of 219 approved nodes have any linked content** — the content base is not just small, it's currently entirely archived/ineligible. This is an accurate reflection of real system state, not a defect in this sprint's code (confirmed via integration tests that a fresh, non-archived, approved Module *does* get picked up and swept correctly). It's a concrete, measured data point for Sprint 6's "full seeding" scope: there is currently no eligible Module content at all for the skill graph to attach to.

## Decisions made

1. Module-level tagging only (not Lesson/Exercise) — matches delivery's actual selection granularity and the current tiny content base.
2. `retag-modules` only considers Modules with **zero existing links** (not a "re-run and refresh everything" sweep) — keeps the operation idempotent and cheap to call repeatedly; a future sprint can add a force-refresh option if AI tagging quality needs revisiting on already-tagged content.
3. Auto-apply every validated match, no per-link approval step, per the earlier explicit decision — spot-checking happens via the `content-coverage` dashboard, not a review queue.
4. `GoalTags` added as a plain curated constant list on the existing `CurriculumContextTagConstants`, not a new file/table — Sprint 3 will consume this directly.

## Risks / unresolved questions

- **Real re-tagging quality is unverified** — the live run found zero eligible Modules to tag, so the actual AI-matching behavior (as opposed to its unit-tested graceful-degradation behavior) has not yet been exercised against real content. This should be checked as soon as Sprint 6 (or an earlier ad-hoc content pass) produces non-archived approved Modules.
- The whole-suite frontend karma run remains blocked by the same 5 pre-existing broken spec files flagged in prior reviews — still unresolved, still out of this sprint's scope.
- Whether the 2 existing archived Modules should be unarchived, re-approved fresh, or left alone is a content-authoring decision outside this sprint's scope — flagged for whoever picks up Sprint 6.

## Final verdict

Sprint 2 complete and verified: 0 backend build errors, 3,891/3,891 tests passing, 0 new frontend build errors, migration applied cleanly against the real dev database. The Module-to-node linking mechanism, its AI-tagging service, and the content-coverage dashboard all work correctly — confirmed via both automated tests and a live run against the real database, which also surfaced a genuine, useful finding: there is currently no eligible content for the graph to attach to.

## Next recommended action

Either (a) get at least one real, unarchived, approved Module into the system (manually or via existing content-authoring flows) and re-run `retag-modules` to verify real AI-matching quality before Sprint 3, or (b) proceed straight to Sprint 3 (goal vector) since it doesn't depend on content coverage being non-zero, and revisit content coverage before Sprint 5 (the AI composer, which does need real content to select from).
