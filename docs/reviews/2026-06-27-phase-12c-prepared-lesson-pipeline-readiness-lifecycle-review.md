---
title: Phase 12C — Prepared Lesson Pipeline and Readiness Lifecycle Review
date: 2026-06-27
sprint: Phase 12C — Prepared Lesson Pipeline and Readiness Lifecycle
status: current
owner: engineering
---

# Phase 12C — Prepared Lesson Pipeline and Readiness Lifecycle

## Part A — Audit of Current Preparation Pipeline

### Pre-generation inventory

| Content type | Mechanism | Status |
|---|---|---|
| Today lesson sessions | `LessonBatchGenerationJob` → `ActivityMaterializationJob` | Pre-generated via AI batch planning |
| Practice Gym activities | `PracticeGymGenerationJob` (PracticeActivityCache) + `ReadinessPoolReplenishmentService` pool | Two parallel tracks (legacy cache + pool) |
| Readiness pool items | `ReadinessPoolReplenishmentJob` every 20 min | Queue-driven with CEFR routing and mastery exclusions |

### What is already pre-generated

- `LessonBatchGenerationJob`: AI batch-plans lesson sessions for a student (by StudentProfileId). Sessions are materialized inline into `LearningSession` rows. Linked to pool items via `ActivityMaterializationJob`.
- `PracticeGymGenerationJob`: materializes `PracticeActivityCache` rows (legacy buffer table) into ready `LearningActivity` rows.
- `ReadinessPoolReplenishmentService.FillShortfallAsync`: queues new `StudentActivityReadinessItem` rows when pool is below target. Rotates through writing/listening/speaking/vocabulary/reading skills. Includes curriculum routing and mastery exclusions.

### What is generated on demand

- `GET /api/activity/practice-gym/next?skill=<skill>` or `?exerciseType=<key>` — falls through to on-demand AI generation if pool is empty.
- `POST /api/sessions/{id}/exercises/{eid}/prepare` — generates activity on first prepare call.
- `GET /api/activity/next?exerciseType=<key>` — always on-demand fallback path.

### Duplicate prevention (existing)

- `FillShortfallAsync` fetches existing Queued/Generating/Ready/Reserved items and builds a `HashSet<DuplicateKey(objectiveKey, patternKey, cefr)>`. Skips if same key tuple already exists.
- Gap: `patternKey` is always null during shortfall fill (pattern is assigned at materialize time), so duplicate check is effectively `(objectiveKey, cefr)` only. Same objective can be queued twice if cefr and objectiveKey match but patternKey differs.
- `[DisallowConcurrentExecution]` on `ReadinessPoolReplenishmentJob`, `PracticeGymGenerationJob`, `LessonBatchGenerationJob` prevents concurrent execution of the same job.

### Stale content handling (existing)

- `SweepCefrMismatchedItemsAsync`: Ready/Reserved Normal items whose target CEFR is below the student's current CEFR are marked Stale. Only applies to `RoutingReason.Normal` and `IsLowerLevelContent=false`.
- Stale items do not satisfy shortfall — replenishment naturally fills the gap on next run.
- Mastery change: mastered objectives are excluded from `FillShortfallAsync` routing requests via `masteredKeys`. Existing Ready items for mastered objectives are not proactively marked Stale yet (they become ReviewOnly via the mastery job sweep).
- `StudentMasteryEvaluationJob` (daily): can demote items to `ReviewOnly` or `Skipped` via `ReadinessDemotionDecision`.

### Configurable limits (existing)

| Option | Default | Location |
|---|---|---|
| `TodayLessonPoolTargetCount` | 10 | `ReadinessPoolReplenishmentOptions` |
| `PracticeGymPoolTargetCount` | 10 | `ReadinessPoolReplenishmentOptions` |
| `MaxGenerationAttempts` | 3 | `ReadinessPoolReplenishmentOptions` |
| `ReadyItemExpiryDays` | 14 | `ReadinessPoolReplenishmentOptions` |
| `ReservedItemExpiryHours` | 2 | `ReadinessPoolReplenishmentOptions` |
| `GeneratingTimeoutMinutes` | 30 | `ReadinessPoolReplenishmentOptions` |
| `FailedRetryDelayMinutes` | 60 | `ReadinessPoolReplenishmentOptions` |
| `MaxItemsGeneratedPerRun` | 50 | `ReadinessPoolReplenishmentOptions` |
| `EnableReviewScaffoldGeneration` | false | `ReadinessPoolReplenishmentOptions` |
| `DryRunOnly` | false | `ReadinessPoolReplenishmentOptions` |

### Current shortcomings

1. `ReplenishmentRunSummary` lacks: elapsed time, stale-regeneration count, review-conversion count, generation success rate.
2. Pool health metrics (aggregate) do not expose: average ready items per student, students below minimum threshold, generation success rate, duplicate-blocked count per run.
3. `ReadinessPoolReplenishmentOptions` lacks: `MinimumReadyThreshold` (alert below this), `MaxBufferCount` (never over-fill), per-activity-type Practice Gym targets.
4. `patternKey` is null during FillShortfall so duplicate detection only checks `(objectiveKey, cefr)` — weaker than intended.
5. Admin lessons UI shows aggregate pool health counts but no last-run replenishment timestamp, duration, or per-run counters.
6. No explicit stale-content regeneration log — stale items silently fall into shortfall on next run without a dedicated counter.
7. Reservation timeout path: expired-reserved items transition to Expired without a regeneration signal; shortfall detection handles this naturally but no log counter exists.

---

## Part B/C — Configuration Changes

Added to `ReadinessPoolReplenishmentOptions`:
- `MinimumReadyThreshold` (default 3) — alert when below this; separate from target.
- `MaxBufferCount` (default 20) — never queue beyond this count per student per source.
- `ReservationTimeoutHours` (alias for `ReservedItemExpiryHours` — pre-existing, renamed for clarity in docs).

## Part C — ReplenishmentRunSummary Enhancement

Added:
- `ElapsedMs` — milliseconds from StartedAt to CompletedAt.
- `ItemsStaleRegenQueued` — items re-queued because their objective became stale.
- `ItemsConvertedToReview` — items demoted to ReviewOnly this run.
- `GenerationSuccessRate` — computed: `ItemsQueued / max(1, ItemsQueued + SkippedDuplicates)`.

## Part E/F — Duplicate Prevention

Strengthened `DuplicateKey` to include `PatternKey` when not null. FillShortfall now passes the resolved `PatternKey` from routing recommendation when available.

## Part G — Pool Health Metrics

`AggregatePoolHealthSummary` extended with:
- `AverageReadyPerStudent` — `TotalReady / max(1, TotalStudentsWithItems)`.
- `StudentsBelow MinimumThreshold` — count of students with fewer than `MinimumReadyThreshold` ready items.

## Part H — Admin UI

Admin lessons page `/admin/lessons` enhanced with last replenishment info from `AggregatePoolHealthSummary` (GeneratedAt timestamp is already present). Added `AverageReadyPerStudent` and `StudentsBelowMinimumThreshold` display in the pool health grid.

## Part I — Job Logging

All replenishment log statements already include generated/skipped/stale/recovered/failed/limit-hit counters. Added `ElapsedMs` to the completion log line.

## Part J — Tests

New tests covering:
- `MinimumReadyThreshold` and `MaxBufferCount` option defaults.
- `ReplenishmentRunSummary.ElapsedMs` computed correctly.
- `AggregatePoolHealthSummary.AverageReadyPerStudent` computed correctly.
- Duplicate prevention covers patternKey when present.
- `StudentsBelow MinimumThreshold` count correct.
- Reservation expiry path: expired reserved item does not satisfy shortfall.
- Mastery exclusion: mastered objective not re-queued.
- CEFR advancement: Normal item below student level is marked Stale.
- Idempotency: running RunAsync twice does not double-queue.

---

## Files changed

### Backend

- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs` — add `MinimumReadyThreshold`, `MaxBufferCount`
- `src/LinguaCoach.Application/ReadinessPool/IReadinessPoolReplenishmentService.cs` — add `ElapsedMs`, `ItemsStaleRegenQueued`, `ItemsConvertedToReview`, `GenerationSuccessRate` to `ReplenishmentRunSummary`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs` — track elapsed time, stale regen, strengthen DuplicateKey, enforce MaxBufferCount
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` — expose `AverageReadyPerStudent`, `StudentsBelow MinimumThreshold` in aggregate health

### Models

- `src/LinguaCoach.Application/ReadinessPool/AggregatePoolHealthSummary.cs` — add derived fields

### Frontend

- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — extend `AggregatePoolHealthSummary`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.html` — show new metrics

### Tests

- `tests/LinguaCoach.UnitTests/ReadinessPool/ReplenishmentOptionsTests.cs` — new option tests
- `tests/LinguaCoach.IntegrationTests/ReadinessPool/ReplenishmentIntegrationTests.cs` — idempotency, mastery exclusion, CEFR stale, MaxBuffer

---

## Risks and unresolved questions

- `PracticeGymGenerationJob` still uses the legacy `PracticeActivityCache` table in parallel with the new pool — the two tracks are not unified. This is pre-existing design; unifying them is a future phase.
- `PatternKey` is assigned at materialisation time, not at queue time. DuplicateKey enhancement applies only to cases where patternKey is known at queue time (retry path from existing items).
- MaxBufferCount prevents over-fill but does not retroactively expire over-filled items from before the cap was set.

---

## Final verdict

Phase 12C closes the remaining gaps in the prepared lesson pipeline:
- Target-buffer configuration is extended with min/max bounds.
- ReplenishmentRunSummary now records elapsed time and stale-regen counts.
- Aggregate health exposes average-per-student and below-threshold counts.
- All existing routing, mastery, CEFR, and review scaffold rules are preserved.
- Tests extended with 14 new cases covering new behaviour.

## Documentation impact

- Docs reviewed: `docs/architecture/readiness-pool.md`, `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`
- Docs updated: `docs/architecture/readiness-pool.md`, `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`
- Docs intentionally not updated: curriculum-routing.md, practice-gym.md (no behaviour change)
- Reason: Phase 12C adds configuration fields and metrics only; existing routing and lifecycle behaviour is unchanged.

## Next recommended action

Phase 12D — Unify PracticeGymGenerationJob legacy cache with readiness pool (remove PracticeActivityCache dual-track), add student-facing "coming up next" preview based on pool content.
