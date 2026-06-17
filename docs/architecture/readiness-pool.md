---
status: current
lastUpdated: 2026-06-17 23:00
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

## Serving from Pool (Deferred to Phase 10N)

Phase 10M records pool items but does not yet change user-facing serving behaviour. Today lessons and Practice Gym still generate on-demand if the pool is empty. Phase 10N will:
- Implement background replenishment to keep the pool filled.
- Update Today and Practice Gym page-load paths to serve from pool when a ready item is available.
- Enable `AllowReviewOrScaffold=true` based on mastery signals.
- Add stale/failed item sweep.

## Admin Endpoint

`GET /api/admin/students/{studentId}/readiness-pool` — requires Admin role. Returns `ReadinessPoolSummary` with counts by status and item list. No write endpoints in Phase 10M.
