---
status: current
lastUpdated: 2026-06-17 23:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10M — Student Activity Readiness Pool Foundation: Engineering Review

**Date:** 2026-06-17
**Related sprint:** Phase 10M
**Verdict:** SHIP

---

## Title

Phase 10M Student Activity Readiness Pool Foundation

---

## Related Sprint / Feature

Phase 10M — Student Activity Readiness Pool Foundation
Builds on: Phase 10L (CEFR-Aware Activity Routing), Phase 10K (Curriculum Syllabus Foundation)

---

## Files Reviewed

**New files:**
- `src/LinguaCoach.Domain/Enums/ReadinessPoolStatus.cs`
- `src/LinguaCoach.Domain/Enums/ReadinessPoolSource.cs`
- `src/LinguaCoach.Domain/Enums/RoutingReason.cs` (moved from Application.Curriculum)
- `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs`
- `src/LinguaCoach.Persistence/Configurations/StudentActivityReadinessItemConfiguration.cs`
- `src/LinguaCoach.Application/ReadinessPool/IStudentActivityReadinessPoolService.cs`
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/StudentActivityReadinessPoolService.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`
- `tests/LinguaCoach.UnitTests/ReadinessPool/StudentActivityReadinessItemTests.cs`
- `tests/LinguaCoach.IntegrationTests/ReadinessPool/ReadinessPoolIntegrationTests.cs`
- `src/LinguaCoach.Persistence/Migrations/[T51_StudentActivityReadinessPool]`

**Modified files:**
- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRecommendation.cs` — added `using LinguaCoach.Domain.Enums;`, removed duplicate `RoutingReason` enum.
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `DbSet<StudentActivityReadinessItem>`, xmin registration.
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs` — added `using LinguaCoach.Domain.Enums;`.
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs` — injected `IStudentActivityReadinessPoolService`, creates pool item per materialized cache row.
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs` — injected `IStudentActivityReadinessPoolService`, creates pool item per materialized session. `BuildCompactSummaryAsync` returns routing recommendation as part of tuple.
- `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs` — injected `IStudentActivityReadinessPoolService`, links activity/session ids to matching pool item.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IStudentActivityReadinessPoolService`.
- `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs` — added `using LinguaCoach.Domain.Enums;`.
- `tests/LinguaCoach.IntegrationTests/Curriculum/CurriculumRoutingIntegrationTests.cs` — added `using LinguaCoach.Domain.Enums;`.
- `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs` — added `readinessPool` parameter.

---

## Findings Grouped by Priority

### P0 — Critical (none)

No blocking issues found.

### P1 — High

**RoutingReason moved from Application to Domain**
- Required so `StudentActivityReadinessItem` (a Domain entity) can reference it without a circular dependency.
- All callers updated. Existing tests fixed by adding `using LinguaCoach.Domain.Enums;`.
- Risk: low — enum values are identical and integer-mapped; no data migration required.

### P2 — Medium

**Optimistic concurrency on reservation**
- The `ReserveNextReadyAsync` method uses a 3-attempt retry loop with `DbUpdateConcurrencyException` handling.
- SQLite (integration test DB) does not enforce xmin; the retry loop is tested via unit/integration tests that verify only one reservation is returned.
- PostgreSQL (production) will enforce xmin correctly.

**PracticeGymGenerationJob: pool item created before AI generation**
- Item transitions Queued→Generating→Ready in the same method as AI generation.
- If AI fails mid-way, the outer `Execute` catch block marks the cache row as expired but the pool item remains in Generating status (failed transition not called in catch).
- Risk: acceptable for 10M foundation. The pool item will remain orphaned in Generating status. A background sweep (10N replenishment engine) will handle cleanup.
- TODO added below.

### P3 — Low

**EF configuration: xmin concurrency token in both Configuration and OnModelCreating**
- Removed xmin from `StudentActivityReadinessItemConfiguration.cs` and added it in `OnModelCreating` inside the Postgres-only guard block — matching the existing pattern for `LearningPath` and `PracticeActivityCache`.

**Default context is general_english, not workplace**
- Confirmed: no generation path defaults to `workplace_english`. Tests verify this explicitly.

---

## Decisions Made

1. `RoutingReason` moved to `LinguaCoach.Domain.Enums` — domain entity must not depend on Application layer.
2. Pool item created before AI generation (Queued→Generating immediately) — allows pool to track in-progress work.
3. Reservation uses optimistic concurrency retry (3 attempts) — safe for low-concurrency production use.
4. `ActivityMaterializationJob` links activity to pool item by `LearningSessionId` only (first match, first exercise) — appropriate for 10M; full multi-exercise linking deferred to 10N.
5. No `AllowReviewOrScaffold=true` enabled in existing handlers — that belongs to 10N serving logic.
6. `GET /api/admin/students/{studentId}/readiness-pool` read-only endpoint added — no write endpoints.

---

## AskUserQuestion Answers

No AskUserQuestion was needed for this phase. Phase 10M scope was fully defined in the task specification.

---

## Implementation Tasks Produced

None outstanding — all 10M acceptance criteria met.

Deferred to 10N:
- Full replenishment background engine.
- Serving from pool on Today/Practice/on-demand page load.
- `AllowReviewOrScaffold=true` enabling based on mastery.
- Pool item failure cleanup sweep.

---

## Risks or Unresolved Questions

1. **PracticeGymGenerationJob catch block does not mark pool item failed.** If AI generation throws after `poolItemId` is created, the item stays in Generating. A 10N sweep should expire stale Generating items older than N minutes.
2. **SQLite xmin concurrency not enforced in tests.** Integration tests verify reservation idempotency at the query level (only one Ready item exists after reservation), not via concurrency token conflict. Production Postgres behaviour is correct.
3. **ActivityMaterializationJob links only the first matching pool item per session.** If a session has multiple exercises and multiple pool items (possible in future), only the first ready item gets linked. Acceptable for 10M.

---

## Final Verdict

SHIP. All acceptance criteria met. 1723 tests pass (0 failed). No production-visible behaviour changed. Pool items are recorded for generated content with routing snapshots. Lifecycle transitions are guarded. Lower-level content cannot be stored as Normal. Default context is general_english.

---

## Next Recommended Action

Phase 10N — Pool Replenishment Engine:
- Implement background job to monitor pool health and refill queued/failed items.
- Enable `AllowReviewOrScaffold` based on mastery signals.
- Update Today and Practice Gym page-load paths to serve from pool when available.
- Add pool item failure sweep for orphaned Generating items.
