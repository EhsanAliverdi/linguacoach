---
status: superseded
lastUpdated: 2026-07-17 (Delivery Health rehaul)
owner: architecture
supersedes:
supersededBy: docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md
---

# Student Activity Readiness Pool (deleted — historical record)

> **Superseded (2026-07-10, Phase I2C):** everything documented below — `StudentActivityReadinessItem`,
> `IStudentActivityReadinessPoolService`, `ReadinessPoolReplenishmentService`/`Job`,
> `AdminReadinessPoolController` — was **deleted entirely**. Today and Practice Gym are served
> exclusively by the bank-first module pipeline (`ITodayPlanModuleSelectionService` /
> `IPracticeGymModuleSelectionService`), with no readiness-pool/AI-generation fallback left. See
> `docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md` and
> `-i2c-readiness-pool-removal-review.md` for the removal, and
> `docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md` for the admin
> "Delivery Health" page that replaced the pool-health surfaces this doc used to describe. Kept
> below as a historical record only — do not treat anything past this notice as current.

## Purpose

The readiness pool is a student-specific queue of pre-generated or in-progress learning items. It decouples content generation from content serving, and preserves the routing/personalisation snapshot at the time of generation so content is never mis-matched if the student's profile changes later.

> **Forward reference (Plan-Sync-G0, 2026-07-09, docs-only):** this pool is **kept, not deleted**. The bank-first migration (Resource Banks/Candidates/Activity Templates) is now the primary content model, so this doc's own framing — and any admin UI ("Pool Health"/"Lesson readiness" pages) built around it — will eventually be reframed from "AI-generated activity cache" language to **"Student Activity Assignment / Delivery Queue"** language. The entity, lifecycle, and concurrency model documented below are unchanged by this decision.
>
> **Phase G0 audit done (2026-07-09, docs/audit-only):** the audit confirmed this lifecycle is **load-bearing** — `ReadinessPoolReplenishmentJob`, `LessonBufferRefillJob`, `PracticeGymBufferRefillJob`, `IStudentActivityReadinessPoolService`, the student Practice Gym suggestions surface, and the admin readiness/repair tooling all depend on it — and classified `StudentActivityReadinessItem` **keep, reframe as Student Activity Assignment / Delivery Queue** (never delete). The concrete rename/rework of this doc's admin surfaces (esp. the `/admin/lessons` "readiness pool health" page, P0) and any code-name changes are deferred to the implementation phases G1 (admin IA) / G2 (backend) / G3 (diagnostics). See `docs/architecture/bank-first-admin-backend-surface-audit.md` for the full classification and `docs/roadmap/road-map.md` §19a / Decision Log for the phase order.
>
> **Phase G1 done (2026-07-09):** the **admin labels** for this lifecycle were reframed (no backend/entity/lifecycle change). `/admin/lessons` is now "Today Delivery Health" (readiness/pool → delivery-queue/assignment wording, manual generation reframed as AI **fallback** generation, plus an info banner pointing admins to the Content Banks); the student-detail readiness panel reads "Assignment / Delivery Queue health"; the AI Operations card was relabeled. The `/admin/lessons` route is kept (full route split deferred to G2). The entity name `StudentActivityReadinessItem`, the jobs/services, and `PracticeActivityCache` are **all unchanged** — G1 touched labels/nav only.

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
| `ActivitySubmitHandler.TryWriteUsageLogAsync` (Phase B, 2026-07-08) | Reads the Reserved/consumed readiness item for a completed activity to snapshot `SourceTemplateId`/`SourceBankItemId`/`Subskill`/`RoutingReason`/context tags onto the new `StudentActivityUsageLog` row — read-only, does not mutate the readiness item. See docs/architecture/repetition-and-novelty.md. |

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
| `MinimumReadyThreshold` | 3 | Alert threshold: students with Ready < this value are flagged in `AggregatePoolHealthSummary.StudentsBelowMinimumThreshold`. |
| `MaxBufferCount` | 20 | Hard cap on active items per student per source (Queued + Generating + Ready + Reserved). Prevents unbounded over-fill. Must be ≥ TodayLessonPoolTargetCount. |
| `MaxGenerationAttempts` | 3 | Max attempts per pool item before abandoning. |
| `ReadyItemExpiryDays` | 14 | Days before a ready item is expired. |
| `ReservedItemExpiryHours` | 2 | Hours before a stuck reserved item is expired. |
| `GeneratingTimeoutMinutes` | 30 | Minutes before an orphaned generating item is failed. |
| `FailedRetryDelayMinutes` | 60 | Minutes a failed item must wait before retry. |
| `MaxItemsGeneratedPerRun` | 50 | Cap on new items queued per replenishment run. |
| `EnableReviewScaffoldGeneration` | false | Master switch. Allows lower-level review/scaffold items when ledger shows weakness. |
| `DryRunOnly` | true (Phase 19A) | When `EnableReviewScaffoldGeneration=true`, gating logic runs but no item is written. A second explicit step (`DryRunOnly=false`) is required before generation goes live. |
| `RequireAdminReview` | true | Generated scaffold items are stamped `RequiresAdminReview=true` and excluded from Practice Gym suggestions until an admin clears the flag globally. Not a per-item approval workflow (see Part below). |
| `MaxScaffoldItemsPerStudentPerDay` | 3 | Per-student daily cap on scaffold-routed item generation (UTC calendar day). |
| `ScaffoldAllowedSources` | `["PracticeGym"]` | Readiness pool sources eligible for scaffold generation. |
| `AllowTodayLessonInsertion` | false | Extra explicit override required (in addition to `ScaffoldAllowedSources` containing `"TodayLesson"`) before Today lesson pool items may be scaffold-routed. |
| `MinimumConfidenceForReviewNeed` | `"Medium"` | Minimum `ReviewNeedConfidence` band (Low/Medium/High) required before a weak-event signal triggers generation. See confidence banding below. |
| `PracticeGymPilotEnabled` | false (Phase 19C) | Master student-visibility switch for the Practice Gym pilot. When false, approved scaffold items are excluded from all suggestion buckets regardless of `AdminReviewStatus`. Instantly reversible, no data deletion. |
| `PracticeGymPilotLabel` | `"Review"` | Student-facing `CallToAction` override for scaffold-origin items while the pilot is active. |
| `PracticeGymPilotReason` | `"This helps you practise a skill you are building."` | Student-facing `Explanation` override for scaffold-origin items while the pilot is active. |
| `MaxStudentVisibleScaffoldSuggestions` | 2 | Cap on approved scaffold items shown per Practice Gym response, independent of the general review-bucket page cap (4). |

### Pool Health

`PoolHealthSummary` counts:
- `ReadyCount` — items in `Ready` status. Counts toward target.
- `ReservedCount` — items in `Reserved` status. Counted separately from Ready.
- `QueuedOrGeneratingCount` — in-flight items. Count toward target to prevent over-generation.
- `ShortfallCount = max(0, Target - Ready - QueuedOrGenerating)`.
- `ReviewOnly`, `Stale`, `Expired`, `Failed` — do not reduce shortfall.

`AggregatePoolHealthSummary` (system-wide, Phase 12A/12C) adds:
- `StudentsBelowMinimumThreshold` — count of students with `ReadyCount < MinimumReadyThreshold` (including students with zero ready items). Used for admin alerting.
- `AverageReadyPerStudent` — `totalReady / totalStudentsWithItems` (0.0 when no students). Rounded to 1 decimal in admin UI.

`ReplenishmentRunSummary` (Phase 12C) adds:
- `SkippedAtMaxBuffer` — items not queued because the student already had ≥ `MaxBufferCount` active items.
- `ElapsedMs` — computed from `CompletedAt - StartedAt` in milliseconds.
- `GenerationSuccessRate` — `ItemsQueued / (ItemsQueued + SkippedDuplicates + SkippedAtMaxBuffer)`. Returns 1.0 when nothing attempted.

Replenishment completion log line includes `elapsedMs` and `successRate` fields for per-run observability.

### Replenishment cycle responsibilities

1. **Sweep expired ready items** — past `ReadyItemExpiryDays` → `Expired`.
2. **Sweep expired reserved items** — past `ReservedItemExpiryHours` → `Expired`.
3. **Recover orphaned generating** — past `GeneratingTimeoutMinutes` → `Failed` (retryable if under attempt limit).
4. **Retry failed items** — `AttemptCount < MaxGenerationAttempts` and past `FailedRetryDelayMinutes` → new `Queued` item with same routing snapshot.
5. **Fill shortfalls** — for each active student × source below target, queue new items up to `MaxItemsGeneratedPerRun`.
6. **Duplicate prevention** — skip if same `(StudentId, Source, CurriculumObjectiveKey, PatternKey, TargetCefrLevel)` already `Queued/Generating/Ready/Reserved`.

### Review / scaffold rule (Phase 19A controlled enablement)

`AllowReviewOrScaffold=true` is passed to routing only when all of the following hold in `FillShortfallAsync`:

1. `EnableReviewScaffoldGeneration=true`.
2. The pool `source` is in `ScaffoldAllowedSources`, and if `source == TodayLesson`, `AllowTodayLessonInsertion=true` as well.
3. `IStudentLearningLedger.GetWeakEventsAsync` returns at least one event for the student.
4. The event is corroborated by mastery classification at or above `MinimumConfidenceForReviewNeed`:
   - `High` — objective appears in `StudentMasteryReport.AtRiskObjectiveKeys` (consistent failures).
   - `Medium` — objective appears in `StudentMasteryReport.WeakObjectiveKeys` (`NeedsReview`).
   - `Low` — only raw ledger weak events exist, no mastery corroboration.
   No new AI/ML signal — deterministic, derived from the existing mastery engine.
5. The student has not reached `MaxScaffoldItemsPerStudentPerDay` for the current UTC calendar day (counts items where `RoutingReason != Normal` and `GeneratedBy` starts with `"ReadinessPoolReplenishment"`, created today).

When all gates pass, the created item is stamped `RequiresAdminReview = RequireAdminReview` (config snapshot at creation time) and `AdminReviewStatus = PendingReview` (Phase 19B; `NotRequired` when `RequiresAdminReview=false`). `PracticeGymSuggestionService` excludes any item with `RequiresAdminReview=true` unless `AdminReviewStatus=Approved` from all three suggestion buckets (Suggested/Continue/Review).

#### Per-item admin approval (Phase 19B)

`AdminReviewStatus` on `StudentActivityReadinessItem`: `NotRequired`, `PendingReview`, `Approved`, `Rejected`. Valid transitions (enforced on the entity, not just the API):

- `PendingReview → Approved` — only when lifecycle `Status` is `Ready`, `ReviewOnly`, or `Reserved` (not `Expired`/`Failed`/`Stale`/`Consumed`/`Skipped`/`Queued`/`Generating`). Idempotent if already `Approved`.
- `PendingReview → Rejected` — requires a non-empty reason. Idempotent if already `Rejected`.
- `Approved → Rejected` — only if the item has not been `Consumed`.
- `Rejected → PendingReview` — explicit reopen only, and only if the item has not been `Consumed`.
- Items with `AdminReviewStatus=NotRequired` cannot be rejected (they never entered the review flow).

None of these transitions touch CEFR, curriculum objective completion, or the Learning Plan — they mutate `AdminReviewStatus` and its audit fields only (`AdminReviewedAtUtc`, `AdminReviewedByUserId`, `AdminReviewReason`, `AdminReviewNotes`).

Admin endpoints (all `[Authorize(Roles = Admin)]`, in `AdminReadinessPoolController`):

- `GET /api/admin/readiness-pool/review-scaffold/pending-review` — up to 50 review scaffold items (`RequiresAdminReview=true`), most recent first, across all admin review statuses (not just pending) so admins can see decision history and reopen rejected items.
- `POST /api/admin/readiness-pool/review-scaffold/{itemId}/approve`
- `POST /api/admin/readiness-pool/review-scaffold/{itemId}/reject` — body `{ reason, notes? }`, reason required.
- `POST /api/admin/readiness-pool/review-scaffold/{itemId}/reopen` — body `{ notes? }` optional.

Each response is a `ReviewScaffoldItemDetailDto` with `isStudentVisible` and `isPracticeGymEligible` computed flags so the admin UI doesn't need to re-derive lifecycle/approval logic. Unknown item IDs return a safe 404; invalid transitions return 409 Conflict; missing reject reason returns 400.

Audit trail: every state-changing action writes an `AdminAuditLog` row (`Action` = `ApproveReviewScaffoldItem` / `RejectReviewScaffoldItem` / `ReopenReviewScaffoldItem`, `EntityType` = `StudentActivityReadinessItem`, old/new `AdminReviewStatus` in `OldValueJson`/`NewValueJson`, reason where applicable). No audit row is written for idempotent no-ops (state didn't change).

The admin lessons page (`admin-lessons.component`) renders a "Review scaffold — approval" table with Approve/Reject/Reopen actions per row, badges for review status and student/Practice-Gym visibility, and a `window.confirm`/`window.prompt` flow for reject reasons (existing admin UI pattern — no new modal component). There is still no global "enable" toggle in the UI; `EnableReviewScaffoldGeneration`, `DryRunOnly`, and `RequireAdminReview` remain server-side config only. See `docs/reviews/2026-07-01-phase-19a-review-scaffold-controlled-enablement-review.md` for the Phase 19A scope decision this phase builds on.

Failed replenishment slots (gate not met) fall back to `RoutingMode.NewLearning` for that batch — no item is left ungenerated, it's just not scaffold-routed. B2 students will never silently receive B1 content as Normal content. Lower-level content is only generated when `RoutingReason != Normal` and `IsLowerLevelContent = true`.

`ReplenishmentRunSummary.SkippedDailyCapReached` counts scaffold slots that fell back to Normal routing because the per-student daily cap was reached.

#### Practice Gym pilot rollout (Phase 19C)

Phase 19B's `AdminReviewStatus=Approved` gate makes an item eligible, but a further `PracticeGymPilotEnabled` gate controls whether approved items actually reach students. `PracticeGymSuggestionService.GetSuggestionsForStudentAsync` applies this to every bucket that could carry a scaffold item (`RequiresAdminReview=true`):

- **SuggestedItems** — defence-in-depth condition added even though scaffold items structurally can't reach this bucket today (`RoutingReason.Review` is always paired with `IsLowerLevelContent=true`, which the Suggested filter already excludes).
- **ContinueItems** — reserved scaffold items are gated too, so `PracticeGymPilotEnabled=false` hides an approved-but-unconsumed reserved item, not just newly-served ones. This is the key rollback guarantee.
- **ReviewItems** — gated, and further capped at `MaxStudentVisibleScaffoldSuggestions` (applied before the shared `MaxReview=4` page cap), and has its `CallToAction`/`Explanation` overridden with `PracticeGymPilotLabel`/`PracticeGymPilotReason` instead of the routing-reason-specific copy (e.g. "Step back to strengthen basics").

`ReviewScaffoldItemDetailDto.IsStudentVisible`/`IsPracticeGymEligible` (the 19B admin-table flags) are **not** coupled to `PracticeGymPilotEnabled` — they keep their 19B meaning (structural eligibility: lifecycle + approval), so the Phase 19B approval test contract (asserting these become `true` immediately after approval, under default config) stays intact. The actual "is this visible to a student right now" signal is the new admin endpoint below.

New read-only admin endpoint: `GET /api/admin/readiness-pool/review-scaffold/pilot-summary` — returns `practiceGymPilotEnabled`/`allowTodayLessonInsertion`/`requireAdminReview`/`maxStudentVisibleScaffoldSuggestions` (config echo) plus `approvedCount`/`studentVisibleCount`/`pendingReviewCount`/`rejectedCount`/`consumedCount`/`skippedOrExpiredCount` and up to 10 recent student-visible/consumed items (skill/objective/status/created-at only, no admin diagnostics). `studentVisibleCount` uses the same conditions as the suggestion service's real gate, so it reflects actual visibility, not just approval status. Rendered on `admin-lessons.component` as a "Practice Gym review scaffold pilot" monitoring card.

`AllowTodayLessonInsertion` and `ScaffoldAllowedSources` are untouched by this phase — Today lesson insertion remains disabled by default and structurally isolated (the suggestion service only ever queries `ReadinessPoolSource.PracticeGym`). See `docs/reviews/2026-07-02-phase-19c-review-scaffold-practice-gym-pilot-rollout-review.md` for the full audit and design rationale.

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
- `GET /api/admin/readiness-pool/health` — Returns `AggregatePoolHealthSummary`: system-wide counts for all statuses, per-status student counts, `StudentsBelowMinimumThreshold`, `AverageReadyPerStudent`, oldest/newest item timestamps. Admin Lessons page displays this as a real-data stat grid.

All endpoints require Admin role. No write endpoints.
