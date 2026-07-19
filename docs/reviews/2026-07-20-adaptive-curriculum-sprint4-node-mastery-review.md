# Adaptive Curriculum Sprint 4 — Per-Node Mastery (Hard Cutover)

**Date:** 2026-07-20
**Related sprint / feature:** Sprint 4 of the multi-sprint Adaptive Curriculum initiative — see `docs/architecture/adaptive-curriculum-skill-graph.md`, and the Sprint 1/2/3 review docs for the skill graph foundation, content re-tagging, and goal vector this sprint builds on. This is the first sprint in the initiative to retire an existing behavior (the old objective-key mastery grouping used by `EvaluateStudentAsync`) rather than only add new, additive state.

## Context / decision

Per the target architecture, mastery must be tracked per skill-graph node, not per flat `CurriculumObjectiveKey`/`PrimarySkill` string, so that node-level mastery can eventually feed the Sprint 5 AI composer. Two research findings and two user decisions shaped this sprint's scope before any code was touched:

1. **No direct `StudentLearningEvent` → `SkillGraphNode` reference exists.** The real resolution path is `StudentLearningEvent.ActivityId` → `StudentExerciseLaunch.LearningActivityId` → `StudentExerciseLaunch.ModuleId` → `ModuleSkillGraphNodeLink.SkillGraphNodeId` → `SkillGraphNode` (the same H10 bridge Sprint 3 used for goal-vector implicit drift). Events with no `StudentExerciseLaunch` row (legacy-generated activities) cannot resolve to any node.
2. **`ModuleSkillGraphNodeLink` is many-to-many** — one Module can link to several nodes, so there is no single "the node" an event belongs to.
   - Decision (AskUserQuestion, recommended option chosen): **fan out** — one event contributes evidence to every approved, active node its Module links to. Mirrors Sprint 3's goal-tag fan-out for implicit engagement drift exactly; no new schema.
3. **`EvaluateObjectiveMasteryAsync` is a separate method** with exactly one caller, `LearningPlanService.cs:476`, used for `LearningPlanService`'s own per-plan-objective sequencing (a still-load-bearing legacy system not touched this sprint). Confirmed via consumer tracing that this method must stay unchanged.
4. **Downstream consumer chain traced for `EvaluateStudentAsync`'s report**: `AdminMasteryController` (counts only, format-agnostic, safe), `StudentMasteryEvaluationJob` (calls `LearningPlanService.MarkObjectiveMasteredAsync`/`MarkObjectiveCompletedAsync` per key, which already gracefully no-ops via an `"objective_not_in_plan"` result on an unmatched key — confirmed safe, not a crash risk), `LearningPlanService.RegeneratePlanAsync` → `CurriculumRoutingRequest.MasteredObjectiveKeys` → `CurriculumRoutingService.FilterByMastered` (compared against `CurriculumObjective.Key` — becomes a silent no-op once the keys are node keys, not objective keys).
   - Decision (AskUserQuestion, recommended option chosen): **leave the routing/plan-autoadvance gap as a known, documented gap** rather than building a bridge this sprint — not broken, just degraded, and superseded outright when Sprint 5 retires `CurriculumObjective`/`CurriculumRoutingService`.
5. **Thin-content risk**: Sprint 2 found 0/219 approved skill-graph nodes have any linked content (both existing approved Modules are archived). A hard cutover today means node-based mastery will report empty for effectively every student until real content exists.
   - Decision (AskUserQuestion): the recommended safer option was "add alongside, don't replace yet" — **the user explicitly overrode this and chose "hard cutover now anyway,"** then clarified the app has not been launched to any real student yet, so there is no production risk from the interim empty state.

## Files reviewed

`StudentLearningEvent.cs` (`ActivityId`/`SessionId`/`SessionExerciseId`/`ActivityAttemptId` real GUID fields), `StudentExerciseLaunch.cs` (the H10 bridge, its FK constraints — `Module`/`Exercise`/`Lesson`/`LearningActivity` are all real restrict-delete FKs, not lazy references), `ModuleSkillGraphNodeLink.cs`/its configuration (Module-side cascade FK only, node-side is a soft reference), `StudentMasteryEvaluationService.cs` (`EvaluateStudentAsync`'s old inline `GroupBy`, `EvaluateObjectiveMasteryAsync`, `ComputeSignal`/`ClassifyStatus` threshold math), `AdminMasteryController.cs`, `StudentMasteryEvaluationJob.cs`, `LearningPlanService.cs` (`RegeneratePlanAsync`, `EvaluateObjectiveMasteryAsync`'s one caller at line 476), `CurriculumRoutingService.cs` (`FilterByMastered` and the inline Rule 5 mirror in `ResolvePreferredObjective`).

## What was built

### Backend

- **`StudentMasteryEvaluationService.EvaluateStudentAsync`** rewired: replaced the inline `GroupBy(e => e.CurriculumObjectiveKey() ?? e.PrimarySkill!)` with a new `GroupByNodeKeyAsync` helper that batch-resolves every event's `ActivityId` to its linked, approved, active `SkillGraphNode` key(s) in one query (`StudentExerciseLaunches` ⋈ `ModuleSkillGraphNodeLinks` ⋈ `SkillGraphNodes.Where(Approved && IsActive)`), then fans each event out into every matching node's bucket, preserving the ledger's newest-first ordering `ComputeSignal`'s consecutive-streak counting depends on. Events with no resolvable node contribute to no bucket. `ComputeSignal`/`ClassifyStatus` threshold math (evidence-count/consecutive-streak/avg-score rules) is reused completely unchanged — this is a grouping-key substitution, not a redesign, exactly as scoped in the original plan.
- **`EvaluateObjectiveMasteryAsync` left untouched** — still groups by `CurriculumObjectiveKey() ?? PrimarySkill`, still the sole method `LearningPlanService`'s per-plan-objective sequencing depends on.
- **`StudentMasteryEvaluationService` constructor** gained a new required `LinguaCoachDbContext db` parameter to run the resolution query.
- **Known-gap documentation added at both real consumer sites**, not just in this review doc: a doc comment on `CurriculumRoutingService.FilterByMastered` and an inline comment on its Rule-5 mirror in `ResolvePreferredObjective`, plus an inline comment above `StudentMasteryEvaluationJob`'s mark-mastered/mark-completed loop — each explains concretely why the comparison is now a no-op and points at this doc and the Sprint 5 retirement that supersedes it.
- **No new migration** — this sprint changes a query's grouping key against tables Sprint 1/2 already created (`SkillGraphNode`, `ModuleSkillGraphNodeLink`); no new entities or columns.

### Tests

- Fixed 2 existing test files' constructor calls (`StudentMasteryEvaluationServiceTests.cs`, `StudentMasteryClassificationEdgeCaseTests.cs`) for the new `db` constructor parameter.
- Reworked the 2 tests that exercised `EvaluateStudentAsync`'s grouping directly, since their `FakeLedger`-seeded events had no `ActivityId`/`StudentExerciseLaunch` chain to resolve against and now correctly produce empty results under the new hard-cutover semantics:
  - `EvaluateStudent_ReturnsMasteredCount_ForNodeLinkedEvents` — seeds a real approved Module + approved SkillGraphNode + `ModuleSkillGraphNodeLink` + `StudentExerciseLaunch` (with a real Lesson/Exercise/LearningActivity chain to satisfy `StudentExerciseLaunch`'s restrict-delete FKs) and asserts the resulting node key appears in `MasteredObjectiveKeys`.
  - `EvaluateStudent_LegacyEventsWithNoActivityId_ProduceNoMasteredKeys` (new) — confirms the hard-cutover behavior explicitly: events with no `ActivityId` now produce zero mastered keys, not a fallback onto the old skill-string grouping.
  - `EvaluateStudent_FansOutToEveryLinkedNode` (new) — one Module linked to two nodes; confirms a single event set credits both node keys, verifying the fan-out decision directly.
- 30/30 mastery tests passing (27 pre-existing + 3 reworked/new, net +1 versus the pre-sprint count since one old test was split into two).

## Migration

None — no new entities or schema this sprint.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,930 / 3,930 passing, 0 failing** (30 architecture + 2,540 unit + 1,360 integration).
- No frontend files touched this sprint — `npm run build` not re-run (nothing to validate).
- **Live deployment**: forced `docker compose build --no-cache api` (per the standing Sprint 1 lesson) to guarantee the new query logic actually shipped, then `docker compose up -d api`. Confirmed `Healthy` and `GET /health` returns `Healthy`.
- **Live functional verification not performed against real node-linked mastery** — consistent with Sprint 2/3's already-documented finding: there is no real launchable, node-linked content yet (0/219 nodes have linked content, both existing Modules are archived), so there is no real student attempt to submit through the pipeline and observe a node bucket populate. The resolution query, fan-out logic, and hard-cutover behavior are all thoroughly unit-tested with real seeded DB rows (not mocks) exercising the actual FK chain, which is the strongest verification available until Sprint 6 seeds real content.

## Decisions made

1. Fan out to every linked node (not a single pick) — reuses Sprint 3's precedent, no new schema.
2. `EvaluateObjectiveMasteryAsync` unchanged — confirmed via consumer tracing it serves a separate, still-live system (`LearningPlanService`'s own sequencing).
3. `CurriculumRoutingService.FilterByMastered`'s mastered-exclusion left as a known, documented gap rather than bridged — degraded, not broken; superseded by Sprint 5's full retirement of `CurriculumObjective`/`CurriculumRoutingService`.
4. Hard cutover executed now despite thin content, per explicit user override of the recommended safer "add alongside" option — justified by the app not yet being launched to any real student, so there is no production risk from the interim near-empty mastery state.

## Risks / unresolved questions

- Real end-to-end verification (a real student attempt against real node-linked content actually populating a node's mastery bucket) remains blocked on the same content-thinness gap Sprint 2 flagged for Sprint 6 — not a defect in this sprint's code, but still unverified against live data.
- `CurriculumRoutingService`'s mastered-exclusion is now silently inert for both its list-filter and single-candidate-resolution paths. Documented at both code sites and here; will be resolved by deletion, not a fix, in Sprint 5.
- `StudentMasteryEvaluationJob`'s `MarkObjectiveMasteredAsync`/`MarkObjectiveCompletedAsync` calls are now effectively no-ops for every key they receive (node keys never match plan objective keys) — confirmed safe (existing graceful no-op path), but means `LearningPlanService`'s own auto-advance-on-mastery behavior is inert until Sprint 5.

## Final verdict

Sprint 4 complete and verified: 0 backend build errors, 3,930/3,930 tests passing (including 3 tests newly seeding a real FK-validated Module/Node/Launch chain to exercise the actual resolution query rather than mocking it), live deployment confirmed healthy via a forced no-cache rebuild. The hard cutover is real — the old objective-key grouping in `EvaluateStudentAsync` no longer exists — and both downstream degradations it causes (`CurriculumRoutingService`, `StudentMasteryEvaluationJob`'s auto-advance) are documented at the code site, not just in this review.

## Next recommended action

Sprint 5 (AI composer, replacing delivery selection) is the natural next step — it is also the sprint that retires `CurriculumObjective`/`CurriculumRoutingService`/`Module.ObjectiveKey` outright, which resolves (by deletion) both known gaps this sprint documented rather than fixed. Separately, whenever real launchable, node-linked content exists (Sprint 6, or an earlier ad-hoc content pass), do a real end-to-end check: complete a real activity attempt against node-linked content and confirm the corresponding node's mastery bucket actually populates.
