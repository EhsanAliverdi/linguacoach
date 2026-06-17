---
status: current
lastUpdated: 2026-06-18 02:00
owner: architecture
supersedes:
supersededBy:
---

# Student Activity Readiness Pool

## Purpose

The readiness pool is a student-specific queue of pre-generated or in-progress learning items. It decouples content generation from content serving, and preserves the routing/personalisation snapshot at the time of generation so content is never mis-matched if the student's profile changes later.

## Entity: StudentActivityReadinessItem

Table: `student_activity_readiness_items`

Each row represents one content unit (a Practice Gym activity, a lesson batch session, or a future Today lesson) prepared or being prepared for a student.

### Key fields

| Field | Purpose |
|---|---|
| `StudentId` | Owner of this pool item. |
| `Source` | Where the content came from: `TodayLesson`, `PracticeGym`, `LessonBatch`, `Review`, `Remediation`, `OnDemand`. |
| `Status` | Current lifecycle status (see below). |
| `TargetCefrLevel` | CEFR band used at generation time (normalized, never B2+). |
| `OriginalCefrLevelSnapshot` | Student's profile CEFR at snapshot time (may differ from TargetCefrLevel for review/scaffold content). |
| `IsLowerLevelContent` | True when content is below the student's current level. |
| `RoutingReason` | Normal / Review / Scaffold / Remediation / Fallback. |
| `RoutingExplanation` | Human-readable explanation for logs and diagnostics. |
| `CurriculumObjectiveKey/Title` | Objective from Phase 10K routing, snapshotted at generation. |
| `ContextTagsJson` | Learner context at generation time (e.g. `["general_english"]`). |
| `LearningSessionId` | Linked session (set when materialized). |
| `LearningActivityId` | Linked activity (set when materialized). |
| `AttemptCount` | How many generation attempts have been made. |

### Routing snapshot rule

Context tags, CEFR level, objective key, and preference fields are stored as a snapshot. Do not re-read `StudentProfile` at serving time to recover these values — the profile may have changed.

### Lower-level content rule

`IsLowerLevelContent = true` is only valid when `RoutingReason != Normal`. The domain entity enforces this at construction time. B2 students cannot silently receive B1 content marked as Normal.

### Default context rule

The default context is `general_english`, not `workplace_english`. Workplace context is only set when the student explicitly selects a workplace learning goal.

## Lifecycle

```
queued ──────► generating ──────► ready ──────► reserved ──────► consumed  (terminal)
                    │                │                │
                    ▼                ▼                ▼
                  failed           stale           expired           (terminal / near-terminal)
                                  review_only
```

Valid transitions:
1. `queued` → `generating` (MarkGenerating — increments AttemptCount)
2. `generating` → `ready` (MarkReady — links entity ids)
3. `generating` → `failed` (MarkFailed — stores error code/message)
4. `ready` → `reserved` (Reserve — sets ReservedAt)
5. `reserved` → `consumed` (MarkConsumed — terminal)
6. `ready` or `reserved` → `expired` (Expire)
7. `ready` or `reserved` → `stale` (MarkStale — do not serve as normal)
8. `ready` or `reserved` → `review_only` (MarkReviewOnly — serve only for review queries)
9. `failed` may be retried by creating a new item (10N replenishment engine)

`consumed` and `expired` are terminal. Do not attempt further transitions.

`stale` and `review_only` items are excluded from `GetReadyForStudentAsync`. `review_only` items are included in `IsServableAsReview`.

## Concurrency / Safety

Reservation uses PostgreSQL xmin optimistic concurrency token. `ReserveNextReadyAsync` retries up to 3 times on `DbUpdateConcurrencyException`. Only one caller can reserve a given item.

SQLite (test environment) does not enforce xmin. Integration tests verify idempotency at the query level.

## Service: IStudentActivityReadinessPoolService

Application layer interface in `LinguaCoach.Application.ReadinessPool`.
Implementation: `StudentActivityReadinessPoolService` in `LinguaCoach.Infrastructure.ReadinessPool`.
Registered as `AddScoped`.

Key methods:
- `CreateQueuedAsync(request)` — creates a new item with routing snapshot.
- `MarkGeneratingAsync`, `MarkReadyAsync`, `MarkFailedAsync` — generation lifecycle.
- `ReserveNextReadyAsync` — safe reservation with concurrency retry.
- `MarkConsumedAsync`, `ExpireAsync`, `MarkStaleAsync`, `MarkReviewOnlyAsync` — post-generation lifecycle.
- `LinkMaterializedIdsAsync` — links session/activity ids to existing item.
- `GetReadyForStudentAsync` — returns ready items (excludes stale/review_only/expired/failed).
- `GetPoolSummaryAsync` — summary with counts by status for admin inspection.

## Integration Points (Phase 10M)

| Integration | What it does |
|---|---|
| `PracticeGymGenerationJob` | Creates Queued→Generating→Ready pool item per cache row, with routing snapshot. |
| `LessonBatchGenerationJob` | Creates Queued→Generating→Ready pool item per session, with routing snapshot from batch routing recommendation. |
| `ActivityMaterializationJob` | Links `LearningActivityId` and `SessionExerciseId` to matching pool item by `LearningSessionId`. |
| `GET /api/admin/students/{id}/readiness-pool` | Read-only admin inspection endpoint. |

## Replenishment Engine (Phase 10N)

### Service: IReadinessPoolReplenishmentService

Application layer interface: `LinguaCoach.Application.ReadinessPool.IReadinessPoolReplenishmentService`.
Implementation: `LinguaCoach.Infrastructure.ReadinessPool.ReadinessPoolReplenishmentService`.
Registered as `AddScoped`.

Key methods:
- `RunAsync()` — full maintenance cycle for all active students. Returns `ReplenishmentRunSummary`.
- `GetHealthAsync(studentId, source)` — calculates `PoolHealthSummary` without side effects.

### Job: ReadinessPoolReplenishmentJob

Quartz job (`LinguaCoach.Infrastructure.Jobs.ReadinessPoolReplenishmentJob`).
Trigger: every 20 minutes. `[DisallowConcurrentExecution]`.
Delegates all logic to `IReadinessPoolReplenishmentService.RunAsync()`.

### Configuration: ReadinessPoolReplenishmentOptions

Bound from `appsettings.json` under `"ReadinessPool"`. Defaults:

| Option | Default | Purpose |
|---|---|---|
| `TodayLessonPoolTargetCount` | 10 | Target ready items per student for Today lessons. |
| `PracticeGymPoolTargetCount` | 10 | Target ready items per student for Practice Gym. |
| `MaxGenerationAttempts` | 3 | Max attempts per pool item before abandoning. |
| `ReadyItemExpiryDays` | 14 | Days before a ready item is expired. |
| `ReservedItemExpiryHours` | 2 | Hours before a stuck reserved item is expired. |
| `GeneratingTimeoutMinutes` | 30 | Minutes before an orphaned generating item is failed. |
| `FailedRetryDelayMinutes` | 60 | Minutes a failed item must wait before retry. |
| `MaxItemsGeneratedPerRun` | 50 | Cap on new items queued per replenishment run. |
| `EnableReviewScaffoldGeneration` | false | Allows lower-level review/scaffold items when ledger shows weakness. Conservative default. TODO: enable after mastery/weakness engine validated. |

### Pool Health

`PoolHealthSummary` counts:
- `ReadyCount` — items in `Ready` status. Counts toward target.
- `QueuedOrGeneratingCount` — in-flight items. Count toward target to prevent over-generation.
- `ShortfallCount = max(0, Target - Ready - QueuedOrGenerating)`.
- `ReviewOnly`, `Stale`, `Expired`, `Failed` — do not reduce shortfall.

### Replenishment cycle responsibilities

1. **Sweep expired ready items** — past `ReadyItemExpiryDays` → `Expired`.
2. **Sweep expired reserved items** — past `ReservedItemExpiryHours` → `Expired`.
3. **Recover orphaned generating** — past `GeneratingTimeoutMinutes` → `Failed` (retryable if under attempt limit).
4. **Retry failed items** — `AttemptCount < MaxGenerationAttempts` and past `FailedRetryDelayMinutes` → new `Queued` item with same routing snapshot.
5. **Fill shortfalls** — for each active student × source below target, queue new items up to `MaxItemsGeneratedPerRun`.
6. **Duplicate prevention** — skip if same `(StudentId, Source, CurriculumObjectiveKey, PatternKey, TargetCefrLevel)` already `Queued/Generating/Ready/Reserved`.

### Review / scaffold rule

`AllowReviewOrScaffold=true` is passed to routing only when `EnableReviewScaffoldGeneration=true` AND `IStudentLearningLedger.GetWeakEventsAsync` returns at least one event. Default is `false`. B2 students will never silently receive B1 content as Normal content. Lower-level content is only generated when `RoutingReason != Normal` and `IsLowerLevelContent = true`.

### Active students

Replenishment targets students with `LifecycleStage >= CourseReady` and `OnboardingStatus = Complete` and not `Archived`.

### Serving from pool (Phase 10O)

Phase 10O adds student-facing serving for Practice Gym pool items via `IPracticeGymSuggestionService` / `PracticeGymSuggestionService`.

**Selection** — items are filtered by student + source + active statuses (excludes Consumed, Expired, Failed, Stale, Queued, Generating), then partitioned:

- **SuggestedItems** — `Ready`, not lower-level review content. Ranked by focus-area match → goal context match → priority → expiry urgency → FIFO.
- **ContinueItems** — `Reserved`, not past `ExpiresAt`.
- **ReviewItems** — `ReviewOnly` status, or `Ready` + `IsLowerLevelContent=true` + non-Normal routing reason.

**Reservation** — `StartSuggestionAsync` calls `item.Reserve()` with optimistic concurrency retry. Idempotent: already-reserved items return `AlreadyReserved=true` and the same navigation target.

**Consumption** — `TryMarkConsumedAsync` is best-effort (swallows errors) so completion callbacks never break the calling flow. Must be called from `ActivitySubmitHandler` or a similar completion path (TODO — deferred to a later phase).

**Replenishment signal** — `IsReplenishmentRecommended` in the DTO flags a below-target pool. The background `ReadinessPoolReplenishmentJob` (every 20 min) also handles this automatically.

## Student-Facing API (Phase 10O)

| Method | Route | Description |
|---|---|---|
| GET | `/api/practice-gym/suggestions` | Personalised suggestion cards (Suggested, Continue, Review sections) |
| POST | `/api/practice-gym/suggestions/{id}/start` | Reserve item, return navigation target (LearningActivityId etc.) |
| POST | `/api/practice-gym/suggestions/{id}/complete` | Best-effort mark item consumed |

The existing `GET /api/activity/practice-gym/next` (by skill / by exercise type) remains unchanged.

## Admin Endpoints

- `GET /api/admin/students/{studentId}/readiness-pool` — Returns `ReadinessPoolSummary` with counts by status and item list.
- `GET /api/admin/students/{studentId}/readiness-pool/health` — Returns `PoolHealthSummary` for `TodayLesson` and `PracticeGym` pools, including target, ready, in-flight, shortfall, and `needsReplenishment` flag.

Both endpoints require Admin role. No write endpoints.
