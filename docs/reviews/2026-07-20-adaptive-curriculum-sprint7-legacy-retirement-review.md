# Adaptive Curriculum Sprint 7 — Skill-Graph Plan Sequencing + Legacy Retirement

**Date:** 2026-07-20
**Related sprint / feature:** Sprint 7 of the multi-sprint Adaptive Curriculum initiative, and its final sprint — see `docs/architecture/adaptive-curriculum-skill-graph.md`, and Sprint 1-6 review docs for the skill graph, content re-tagging, goal vector, node mastery, AI composer, and bulk content seeding this sprint's plan-generation rebuild depends on. This sprint completes the initiative: `CurriculumObjective`/`CurriculumRoutingService`/`AdminCurriculumController`/`Module.ObjectiveKey` — the legacy system every prior sprint traced around but could not delete — are retired outright.

## Context / decision

Sprint 6 research found `LearningPlanService.BuildObjectiveSequenceAsync` still called `CurriculumRoutingService.RecommendAsync` on the hot path of every plan regeneration, and `EvaluateObjectiveMasteryAsync` (driving real-time progress on every activity submit) still grouped by the legacy `CurriculumObjectiveKey()`/`PrimarySkill` chain — both genuinely load-bearing, not dead. Retiring the legacy system required first replacing what it actually did:

1. **`CurriculumRoutingService`'s real logic** (CEFR exact-match → one-level-down fallback, difficulty-band-aware candidate selection, context-tag soft preference) had no skill-graph equivalent — this needed a real rebuild, not a rename.
2. **`EvaluateObjectiveMasteryAsync`** needed the same node-based resolution Sprint 4 already built for `EvaluateStudentAsync`, applied to a single requested key instead of grouping everything.
3. **`Module.ObjectiveKey`** (a free-text field, confirmed in Sprint 5 to be a small, real, working self-directed Practice Gym filter) needed a real replacement via `ModuleSkillGraphNodeLink`, not just deletion.

Only once all three were built and verified did the actual deletion happen — matching this initiative's standing "hard cutover, no long-lived fallback, but never delete load-bearing code before its replacement is real" discipline.

## Files reviewed

`LearningPlanService.cs` (full read — `BuildObjectiveSequenceAsync`'s 3-step sequence: primary new-learning objectives from a skill rotation, review objectives from weak keys, reinforcement from mastered keys; `TryUpdateObjectiveProgressAsync`'s real-time progress trigger from `ActivitySubmitHandler`; every other consumer of `RegeneratePlanAsync`/`GetOrCreatePlanAsync`), `CurriculumRoutingService.cs` (the full algorithm being replaced — `NormalizeCefrLevel`, `GetCandidatesForStudentAsync`'s context-tag soft-filter, `SelectBestCandidate`'s difficulty-band ordering, the CEFR-fallback tier), `CurriculumSyllabusQueryService.cs`, `StudentLearningPlanObjective.cs`/its configuration (confirmed `ObjectiveKey` is a plain unconstrained `varchar(200)` — structurally able to hold a `SkillGraphNode.Key` with zero schema change; `Title` was always persisted `null` in practice, now a real opportunity), `StudentMasteryEvaluationService.cs` (`GroupByNodeKeyAsync`'s existing resolution/fan-out, reused rather than duplicated), `TodayPlanModuleSelectionService.cs`/`PracticeGymModuleSelectionService.cs` (their `ComposerCandidate.ObjectiveKey`/`RequestedObjectiveKey` self-directed narrowing, the last real consumers of `Module.ObjectiveKey`), the full consumer inventory of `CurriculumObjective`/`AdminCurriculumController`/`admin-curriculum` (re-verified against current `HEAD`, confirmed nothing new since Sprint 5).

## What was built

### Backend — `ISkillGraphRoutingService` (replaces `ICurriculumRoutingService`)

New `LinguaCoach.Application.SkillGraph.ISkillGraphRoutingService`/`SkillGraphRoutingService` (`LinguaCoach.Infrastructure.SkillGraph`) — candidates are `SkillGraphNode` rows instead of `CurriculumObjective` rows, with one deliberate improvement over the legacy router: **a node with at least one real, eligible linked Module (`ModuleEligibility.AvailableForNewStudentDeliveryExpr`) is preferred over one without**, so a recommended objective is more likely to be genuinely actionable — the legacy router never checked this at all. CEFR exact-match → one-level-down fallback is preserved; context-tag/focus-area overlap became a soft preference (scored, not a hard filter that can zero out candidates) rather than the legacy hard filter, since a hard filter to zero was itself a source of the legacy fallback-tier complexity. `PreferredObjectiveKey`'s safety-checked "prefer this key if valid" path was **not** carried over — confirmed `LearningPlanService` never set it, and its only other caller (`AdminCurriculumController`'s routing-preview diagnostic) is retired this same sprint.

### Backend — `EvaluateObjectiveMasteryAsync` node migration

`StudentMasteryEvaluationService.EvaluateObjectiveMasteryAsync` now reuses `GroupByNodeKeyAsync` (Sprint 4's event→node resolution/fan-out) and looks up the single requested key, rather than the old `CurriculumObjectiveKey() ?? PrimarySkill` string match. The now-fully-unused `LearningEventExtensions.CurriculumObjectiveKey()` file-scoped extension method was deleted alongside it.

### Backend — `LearningPlanService` rewired

`BuildObjectiveSequenceAsync` now calls `ISkillGraphRoutingService.RecommendAsync`, persists `rec.NodeKey`/`rec.NodeTitle` into `StudentLearningPlanObjective` — **`Title` is now genuinely populated** (`SkillGraphNode.Title`), unlike the legacy path which always persisted `null` in practice. `ObjectiveCandidate.SecondarySkills` was dropped (confirmed its only consumer, `PlannedObjectiveContext`, has zero real callers — `GetNextPlannedObjectiveAsync`/`GetPracticeGymObjectivesAsync` are unused public API surface, left alone since deleting unused-but-harmless interface methods was out of this sprint's "delete proven-load-bearing-now-replaced code" scope).

### Backend — `Module.ObjectiveKey` retirement

The field, its EF column mapping, and every consumer were removed: `PracticeGymModuleSelectionService`'s self-directed `RequestedObjectiveKey` narrowing now joins through `ModuleSkillGraphNodeLink`/`SkillGraphNode` instead of a free-text field match; both selectors' `ComposerCandidate.ObjectiveKey` (descriptive-only context for the AI composer's prompt) now resolves a Module's first real linked node key via a new `ResolvePrimaryNodeKeysAsync` helper, added to both `TodayPlanModuleSelectionService` and `PracticeGymModuleSelectionService`.

### Backend — full retirement

Deleted outright: `CurriculumObjective` (entity, configuration, seeder), `CurriculumRoutingService`, `CurriculumObjectiveWriteService`, `CurriculumSyllabusQueryService`, `CurriculumValidationService`, `CurriculumRoutingRequestFactory` (already-dead code, confirmed zero production callers, cleaned up opportunistically), `AdminCurriculumController`, and their Application-layer contracts (`ICurriculumRoutingService`, `ICurriculumSyllabusQuery`, `ICurriculumValidationService`, `CurriculumRoutingRequest`/`CurriculumRoutingRecommendation`, `AdminCurriculumContracts`, `RoutingMode`). `CurriculumContextMapper`/`ActivityCompatibilityConstants`/`CurriculumContextTagConstants`/`CurriculumSkillConstants` are **kept** — genuinely reused by `SkillGraphRoutingService` and elsewhere, not part of the retired subsystem. `CurriculumObjectiveSeeder`'s call removed from `Program.cs`; `DbSet<CurriculumObjective>` removed from `LinguaCoachDbContext`.

### Migration

`Sprint7_RetireCurriculumObjectiveAndModuleObjectiveKey` (via `dotnet ef migrations add`, never hand-written) — drops the `curriculum_objectives` table and the `modules.objective_key` column. Reviewed for accuracy before applying; no data-loss concern since neither held real production data (the app is not launched to any real student; both were confirmed thin/synthetic in every prior sprint's live DB checks).

### Frontend

Deleted `admin-curriculum` page (component/template/spec) and `curriculum.service.ts`. Removed the `/admin/curriculum` route, both sidebar nav entries (mobile drawer + desktop, matching this codebase's existing dual-nav pattern), and every test assertion referencing them — updated, not just deleted, so `admin-app-layout.component.spec.ts`'s route-completeness and "each section heading appears exactly once" tests now check for `/admin/skill-graph`/"Skill Graph" instead. Removed `objectiveKey` from `ModuleDto`/`CreateModuleRequestBody` TypeScript models. Updated one e2e screenshot spec's dead route reference.

### Tests

Reworked (not just patched) to reflect node-based semantics: `StudentMasteryEvaluationServiceTests.cs`/`StudentMasteryClassificationEdgeCaseTests.cs`'s `EvaluateObjectiveMasteryAsync` tests now seed a real approved Module+Node+`ModuleSkillGraphNodeLink`+`StudentExerciseLaunch` chain (reusing Sprint 4's helper pattern) instead of matching by skill string. Deleted outright (their subject no longer exists, not reworked): `CurriculumRoutingServiceTests.cs`, `CurriculumValidationServiceTests.cs`, `AdminCurriculumObjectiveUnitTests.cs`, `CurriculumObjectiveTests.cs`, and the equivalent integration test files (`CurriculumRoutingIntegrationTests.cs`, `CurriculumSyllabusIntegrationTests.cs`, `CurriculumValidationIntegrationTests.cs`, `AdminCurriculumObjectivesIntegrationTests.cs`). One integration test (`StudentLearningPlanJourneyTests.GetJourney_ResolvesActivePlan_ByUserIdNotProfileId`) needed a real seeded `SkillGraphNode` to keep exercising its actual regression target (userId→profileId resolution) now that plan generation depends on real node content existing, not the legacy system's always-present 34 objectives.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,753 / 3,753 passing, 0 failing** (30 architecture + 2,408 unit + 1,315 integration — the total count dropped from Sprint 6's 3,940 because whole test files dedicated to the retired subsystem were deleted, not because coverage regressed; every surviving/reworked test exercises real behavior).
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries.
- **Live deployment**: Docker was already up this session; forced `docker compose build --no-cache api` per the standing lesson, confirmed `Healthy`. Confirmed directly against Postgres: `curriculum_objectives` table no longer exists, `modules.objective_key` column no longer exists — the migration applied cleanly. API logs post-deploy show no new errors, only the same pre-existing Quartz/DataProtection noise already documented in every prior sprint.
- A full live plan regeneration for a real student against the running Docker deployment was not separately triggered this pass — the exact code path (`GetOrCreatePlanAsync` → `SkillGraphRoutingService.RecommendAsync` → real persisted `StudentLearningPlanObjective` rows → the `/journey` endpoint rendering them) is already exercised end-to-end by 5 real HTTP-request integration tests against a real Postgres-equivalent database, one of which explicitly seeds a real node and asserts `totalObjectives > 0` — the strongest verification available without content depth beyond what Sprint 6 seeded.

## Decisions made

1. Built the real replacement (node-based routing, node-based objective mastery, node-based self-directed narrowing) before deleting anything — no window where the legacy system was deleted but its replacement was unverified.
2. Context-tag/focus-area matching became a soft preference (scoring) rather than the legacy hard filter — a deliberate simplification, not strict behavioral parity, since the legacy hard-filter-to-zero was itself part of what made the old router need so many fallback tiers.
3. Node candidates that have real linked content are preferred over ones that don't — a genuine improvement the legacy system never had, made possible only because `ModuleSkillGraphNodeLink` (Sprint 2) exists.
4. `PreferredObjectiveKey`'s safety-checked routing path was not rebuilt — confirmed unused by the only real caller before deleting its sole other caller.
5. Whole test files for the retired subsystem were deleted outright, not reworked — their subject no longer exists, so there is nothing left to test.

## Risks / unresolved questions

- `GetNextPlannedObjectiveAsync`/`GetPracticeGymObjectivesAsync` (on `ILearningPlanService`) were confirmed to have zero real callers (Practice Gym's actual suggestion path doesn't consume plan objectives at all) — left in place since they still work correctly against the new node-keyed data and deleting unused-but-functioning interface methods was out of this sprint's scope, but they're a candidate for cleanup whenever `LearningPlanService`'s relationship to Today/Practice Gym delivery is revisited.
- Given the content-thinness Sprint 6 already found (only 1/16 seeded Modules has a real skill-graph node link), most real plan-generation calls today will still hit `SkillGraphRoutingReason.Fallback` (no node found) for most skill-rotation slots — an honest, expected consequence of content depth, not a defect in this sprint's routing logic. This resolves itself as content seeding (Sprint 6, extended to more resource types) and skill-graph tagging coverage grow.
- The context-tag soft-preference behavior (vs. the legacy hard filter) is a real behavioral change, not just a refactor — worth watching once real goal-vector-driven students exist, to confirm the scoring approach actually surfaces relevant content rather than just picking arbitrarily among untagged candidates.

## Final verdict

Sprint 7 complete and verified: 0 backend build errors, 3,753/3,753 tests passing, 0 new frontend build errors, a clean EF migration applied live confirming both the `curriculum_objectives` table and `modules.objective_key` column are genuinely gone from the database. This closes the Adaptive Curriculum initiative's core architectural goal, seven sprints after it was scoped: the flat, content-blind `CurriculumObjective` system is fully retired, and skill-graph nodes — real, content-validated, mastery-tracked, goal-vector-aware — are the only curriculum unit left in the codebase.

## Next recommended action

With the architecture now fully migrated, the highest-value remaining work is closing the content-depth gap every sprint since Sprint 2 has flagged: extend Sprint 6's bulk content seeding to Reading/Listening/Speaking/Writing resource types, and consider a finer-grained skill-graph taxonomy pass so node-mastery weakness-matching and node-backed plan routing both have real content to work against at the granularity real lessons are authored. Once that exists, a genuine pilot-student walkthrough (onboarding → placement → plan → Today/Practice Gym → visible mastery progress → a goal-switch scenario) is the natural check-off for the whole initiative's original "beat Duolingo" goal.
