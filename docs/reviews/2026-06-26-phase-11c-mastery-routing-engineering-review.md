# Phase 11C Engineering Review: Mastery Routing and Review Scaffold Integration

**Date:** 2026-06-26 (updated with closure hardening findings)
**Sprint:** Phase 11C + Phase 11C-FINAL
**Reviewer:** Claude Code (Sonnet 4.6)
**Related sprint docs:**
- `docs/sprints/2026-06-26-phase-11c-mastery-routing-integration.md`
- `docs/sprints/2026-06-26-phase-11c-final-closure-hardening.md`
**Call-site audit:** `docs/architecture/routing-call-site-audit-phase-11c.md`

## Files Reviewed

- `src/LinguaCoach.Application/Curriculum/RoutingMode.cs`
- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingRequestFactory.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs`
- `src/LinguaCoach.Application/ReadinessPool/PoolHealthSummary.cs` (no change — reviewed for correctness)
- `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs`
- `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs`

## Inventory Findings (Part A)

Seven call sites invoke curriculum routing. Prior to this phase, none passed `MasteredObjectiveKeys` or `AllowReviewOfMastered`, meaning the Phase 11B mastery filtering was implemented but not activated anywhere.

| Call site | Synchronous | Mastered keys before | After |
|-----------|-------------|----------------------|-------|
| ReadinessPoolReplenishmentService | No | None | Fetches via EvaluateStudentAsync |
| LessonBatchGenerationJob | No | None | Fetches via EvaluateStudentAsync |
| PracticeGymGenerationJob | No | None | Fetches via EvaluateStudentAsync |
| ExercisePrepareHandler | Yes | None | Unchanged (page-load latency risk) |
| ActivityGetHandler | Yes | None | Unchanged (page-load latency risk) |
| ActivityMaterializationJob | No | None | Unchanged (future phase) |
| CurriculumObjectiveWriteService (admin preview) | No | None | Unchanged (preview only) |

## Findings by Priority

### P0 — Resolved in this phase

**Mastered keys not wired anywhere (critical gap)**
Phase 11B added `MasteredObjectiveKeys` to the request DTO and filtering logic in the routing service, but no call site populated the field. This phase wires it into the three background generation paths that matter most for pool health.

**Non-runnable objectives not filtered from routing**
Grammar, Pronunciation, Fluency, Confidence objectives existed in the syllabus seed and could be selected by routing. No exercise type can execute them. This would produce corrupt/unusable readiness items. Now filtered before candidate selection.

### P1 — Accepted gaps

**EvaluateStudentAsync called per-student in replenishment loop**
`ReadinessPoolReplenishmentService.FillShortfallAsync` is called per-student per-source. `EvaluateStudentAsync` is called once per student per `FillShortfallAsync` invocation (before the skill rotation loop). However, it also calls `EvaluateAndDemoteReadinessItemsAsync` internally, which reads and writes all readiness items for the student. This creates a write side-effect during what was previously a read-only replenishment path. Acceptable because demotion should happen before replenishment anyway — mastered items should be demoted before new items are queued. Documented.

**ActivityMaterializationJob not wired**
This job materializes exercises for lesson sessions. It calls routing to determine context for AI generation but does not affect which objectives are selected (that decision was already made upstream by LessonBatchGenerationJob). Deferring is safe.

**Synchronous page-load handlers not wired**
`ExercisePrepareHandler` and `ActivityGetHandler` call routing synchronously on user-facing requests. Adding mastery evaluation here would add latency (one extra DB read per request). The mastery-aware pool pre-generates items in the background, so the page-load handlers only hit routing when the pool is empty. Deferring is safe.

### P2 — Future improvements

**Admin preview does not expose RoutingMode**
`CurriculumObjectiveWriteService.PreviewRoutingAsync` constructs a `CurriculumRoutingRequest` manually. It could expose `Mode` as a preview parameter to let admins test new-learning vs review routing. Low priority.

**RoutingMode not stored on readiness item**
The routing mode used when creating an item is not persisted. Diagnostic tooling (admin pool inspection) cannot tell whether an item was created in NewLearning or Review mode. Future enhancement.

**EnableReviewScaffoldGeneration still false**
Review paths are fully implemented but gated behind this flag. Enable after mastery signal quality is validated in production.

## Decisions Made

1. `FilterNonRunnable` returns an empty list (not the original candidates) when all candidates are non-runnable. This forces fallback routing rather than silently generating unusable content.

2. `EvaluateStudentAsync` is called in replenishment before the skill rotation loop (once per student/source, not once per slot). This batches mastery evaluation efficiently.

3. Synchronous page-load handlers are intentionally not changed. The background pool pre-generates mastery-aware items; page-load handlers are a fallback path that should rarely be hit.

4. `RoutingMode.NewLearning` is the default on `CurriculumRoutingRequest`. All existing call sites that don't set `Mode` explicitly get correct behavior with no change.

## AskUserQuestion Answers

Not applicable — no blocking decisions required.

## Implementation Tasks Produced

None — all tasks completed in this phase.

## Risks or Unresolved Questions

- **Mastery evaluation during replenishment writes demotion state.** If replenishment runs frequently and `EvaluateAndDemoteReadinessItemsAsync` is slow for students with large item pools, it could slow the replenishment job. Monitor job duration after enabling in production.
- **Grammar/Pronunciation objectives seeded but never routable.** They appear in validation warnings. A future phase can add seeder logic to mark them with a `IsRunnable=false` flag at the domain level rather than relying on the routing filter.

## Closure Hardening Findings (Phase 11C-FINAL)

**Bug found and fixed:** `FilterNonRunnable` was not applied to lower-level review candidates (Step 4 of `RecommendAsync`). Grammar/pronunciation at B1 could be selected for a B2 student in review mode. Fixed by applying filter before `lowerCandidates.Count > 0` check.

**Admin preview improved:** `AdminRoutingPreviewRequest` now accepts optional `RoutingMode`. Permanent warning added to all preview results noting mastery filtering is not applied in generic preview.

**Call-site audit completed:** All 7 routing call sites documented in `docs/architecture/routing-call-site-audit-phase-11c.md`.

## Final Verdict

Implementation is correct, minimal, and safe. No schema migrations. No student UI changes. 1356 backend unit tests pass. 1381 Angular unit tests pass. Frontend production build clean. Pre-existing integration test failures are unrelated to this phase.

## Next Recommended Action

1. Enable `EnableReviewScaffoldGeneration: true` in appsettings after mastery signal quality is validated in production.
2. Wire `ExercisePrepareHandler` and `ActivityGetHandler` to mastery keys after introducing a cached/snapshot mastery field on `StudentProfile` to avoid per-request DB reads.
3. Fix pre-existing integration test failures (SetLearningTrack obsolete, xUnit2012 warning) in a cleanup phase.
4. Surface `RoutingMode` selector in the admin curriculum preview HTML template when admin UI enhancement is in scope.
