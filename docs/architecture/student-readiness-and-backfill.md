---
status: current
lastUpdated: 2026-07-03 (20H)
owner: architecture
supersedes:
supersededBy:
---

# Student Data Readiness, Backfill & Pilot Cleanup (Phase 20D)

## Resolved production issues (history)

Two distinct production 500 incidents have hit this audit endpoint since Phase 20D shipped, both
now resolved:

- **Phase 20E/20F (`TODO-20E-1`):** systemic — 6 migrations missing their `Designer.cs`, causing
  every student's audit call to 500. Fixed in Phase 20F (migration files restored); confirmed live
  2026-07-02 (`TODO-20F-1`). See `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.
- **Phase 20G/20H (`TODO-20G-3`):** isolated to one student's specific data shape (a large batch of
  duplicate Practice Gym readiness rows for one objective, compounded by the `TODO-20G-1` dedup bug
  below). Root cause: 4 of the audit's 10 check-category methods
  (`AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`, `AddAudioTtsChecksAsync`,
  `AddFeedbackAndReviewScaffoldChecksAsync`) had no exception handling, so any unexpected data shape
  in those queries crashed the whole audit instead of degrading to a structured `Warning` check like
  the other six categories already did. Fixed in Phase 20H by wrapping all four in try/catch,
  matching the existing `AddLearningPlanChecksAsync`/`AddProgressChecksAsync` pattern — the audit
  now guarantees it never 500s on valid student data, regardless of check-specific data anomalies.
  See `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.

If a future audit call 500s again, it means a **new** exception path exists outside these ten
methods (e.g. in `GetReadinessAsync` itself, before/after the ten calls) — check there first.

## Problem this solves

The app accumulated many features (placement, Learning Plan, Today lesson,
Practice Gym, readiness pool, review scaffold, audio/TTS, evaluation,
mastery ledger, runtime settings) across 20 phases. Development students
created before a given feature existed can have missing/stale/incomplete
data, making the product look broken for a pilot even though the features
themselves work. There was no way for an admin to answer "can this student
safely use the app end-to-end today?" without manually inspecting several
pages and tables.

This phase adds a read-only per-student audit plus a small set of explicit,
idempotent, audited repair actions. It does **not** add any new learning
feature, AI scoring behavior, CEFR update logic, objective-completion logic,
or Learning Plan regeneration logic.

## Model

`LinguaCoach.Application.Admin.StudentReadiness`:

- Enums: `ReadinessCheckStatus` (Pass/Warning/Fail/NotApplicable/
  NotImplemented), `ReadinessCheckSeverity` (Info/Warning/Blocking),
  `ReadinessOverallStatus` (Ready/NeedsAttention/Blocked/NotStarted),
  `ReadinessRepairRiskLevel` (Low/Medium/High).
- DTOs: `StudentReadinessCheckDto`, `StudentReadinessSummaryDto`,
  `StudentReadinessRepairActionDefinitionDto`,
  `StudentReadinessRepairRequestDto` (`ActionKey`, `Reason?`, `DryRun=true`),
  `StudentReadinessRepairResultDto` (`ChangedCount`, `SkippedCount`,
  `Warnings`, `Errors`, `BeforeSummary?`, `AfterSummary?`, `AuditLogId?`).
- `StudentReadinessRepairActions` — static registry of 10 action keys, each
  with `IsImplemented`, `Category`, `RiskLevel`, `Description`. 5 are
  implemented; 5 are registered with `IsImplemented=false` and a
  documented reason (see "Deferred repair actions" below) rather than
  invented behavior.
- `IStudentReadinessAuditService.GetReadinessAsync(studentProfileId, ct)` —
  returns `null` if the student profile doesn't exist (controller maps to
  404). Never mutates.
- `IStudentPilotReadinessRepairService.RepairAsync(...)` /
  `.RunAllSafeRepairsAsync(...)`.

## Audit service (`Infrastructure/Admin/StudentReadinessAuditService.cs`)

Runs ~20 checks grouped into 10 categories, each a small targeted query:

1. **Account & access** — user row exists, role is Student, not archived,
   password-change state.
2. **Placement & CEFR** — status, CEFR set/valid, per-skill estimates.
3. **Learning Plan** — exists (direct `_db.StudentLearningPlans` query,
   **never** calls `GetOrCreatePlanAsync`, which auto-generates as a side
   effect), has objectives (via `GetJourneyAsync`, documented read-only).
4. **Course readiness** — lifecycle/onboarding/placement consistency.
5. **Today lesson** — exercise types available
   (`IExerciseTypeRegistry.GetForTodayAsync`), a ready/reusable session or
   on-demand generation available, no session stuck generating.
6. **Practice Gym** — `GetHealthAsync`. Empty-but-actively-replenishing is
   healthy (Pass); empty AND failed AND not replenishing is a Warning.
7. **Activity content validity** — readiness items whose `PatternKey` no
   longer resolves, materialized activities with empty content JSON.
8. **Audio/TTS** — missing audio for listening activities is only flagged
   when `EnableTtsGeneration` (Phase 20C effective setting) is on;
   informational otherwise.
9. **Feedback/completion & review scaffold** — stale/expired-eligible
   Reserved items past effective `ReservedItemExpiryHours`; the
   pending/rejected-scaffold-visibility check always reports Pass/Info
   (a `PendingReview` item sitting in the queue is normal — visibility is
   structurally gated by `PassesAdminReviewGate`, which data can't violate;
   the check exists to prove the audit itself never claims such an item is
   visible, not to hunt for a data anomaly).
10. **Progress/mastery** — progress summary and ledger calls complete
    without throwing; an empty ledger is a Pass, not a Warning.

## Repair service (`Infrastructure/Admin/StudentPilotReadinessRepairService.cs`)

Every call: validates the student exists (404), the action key is known
and implemented (404/400), and — for `DryRun=false` — that `Reason` is
non-blank (400). A real (non-dry-run) call writes exactly one
`AdminAuditLog` row (`Action="RepairStudentReadiness"`,
`EntityType="StudentReadinessRepair"`, `EntityId=actionKey`,
`TargetStudentId`, old/new value JSON, `Reason`) and is idempotent — a
second run with nothing left to fix returns `ChangedCount=0`.

**Implemented repair actions** (all reuse existing, already-safe service
methods or entity mutators — no new generation/mutation logic invented):

| Action key | What it does |
|---|---|
| `generate_learning_plan_if_missing` | Calls `ILearningPlanService.GetOrCreatePlanAsync` (already a no-op if a plan exists). |
| `refill_today_lesson_if_empty` | Calls `IGetTodaysSessionHandler.HandleAsync` — the same path a student's browser triggers. Reports the exact failure reason (e.g. no enabled exercise types) as a Warning rather than an error. |
| `expire_invalid_readiness_items` | Calls `StudentActivityReadinessItem.MarkStale(...)` on Ready/Reserved items whose `TargetCefrLevel` no longer matches the student's current CEFR — the same rule as `SweepCefrMismatchedItemsAsync`, scoped to one student. |
| `expire_stale_reserved_items` | Calls `StudentActivityReadinessItem.Expire(...)` on Reserved items older than the effective `ReservedItemExpiryHours` — the same rule as half of `SweepExpiredItemsAsync`, scoped to one student. |
| `run_all_safe_repairs` | Runs the four actions above in sequence, aggregating results. |

**Never touches** `ActivityAttempt`, `AudioAsset`, or any evaluation table
— verified by a dedicated unit test. No historical attempt, submission, or
evaluation row is ever deleted or mutated by any repair action.

**Deferred repair actions** (`IsImplemented=false`, shown in the admin UI
as "Not implemented yet" with the reason below, tracked in `TODOS.md`):

| Action key | Why deferred |
|---|---|
| `refill_practice_gym_if_empty` | No single-student-scoped Practice Gym replenishment entry point exists — `IReadinessPoolReplenishmentService.RunAsync()` processes all active students in one call; adding a single-student overload is new service surface, not a safe redirect. |
| `backfill_missing_activity_metadata` | No concrete, safe, existing target was identified for "which metadata" during the survey. |
| `regenerate_missing_tts_for_listening_if_supported` | No single-activity/single-student TTS generation entry point exists; `TtsAudioGenerationJob` only operates batch-wide on a schedule. |
| `normalize_student_lifecycle_if_safe` | Lifecycle transitions are normally driven by dedicated flows (placement completion, onboarding). Forcing a stage jump risks bypassing invariants not fully covered by this survey. |
| `refresh_progress_projection_if_supported` | Not Applicable — there is no stored progress/mastery projection; progress is always computed live from the ledger. |

## API

`Api/Controllers/AdminStudentReadinessController.cs`, admin-only
(`[Authorize(Roles = nameof(UserRole.Admin))]`):

- `GET /api/admin/students/{studentId}/readiness` — 404 if student not
  found.
- `POST /api/admin/students/{studentId}/readiness/repair` — body
  `{ actionKey, reason?, dryRun }`. 404 unknown/unimplemented action, 400
  missing reason for a real repair.
- `POST /api/admin/students/{studentId}/readiness/repair-safe-all` — body
  `{ reason?, dryRun }`.

No response ever includes secrets, connection strings, raw AI provider
payloads, or prompt text (verified by a dedicated integration test).

## Frontend

- `admin.models.ts` — `StudentReadinessSummary`, `StudentReadinessCheck`,
  `StudentReadinessRepairRequest`, `StudentReadinessRepairResult` plus
  matching string-literal-union types for the backend enums.
- `admin.api.service.ts` — `getStudentReadiness`, `repairStudentReadiness`,
  `repairAllSafeStudentReadiness`.
- `admin-student-detail.component.ts/.html` — a "Pilot readiness"
  `sp-admin-card` inserted right after "Readiness pool health", following
  the page's existing `signal` + `loading` + `error` + `loadX()`
  per-section pattern. Shows a Ready/Needs attention/Blocked badge,
  blocking/warning/info counts, an expandable checklist, and a
  recommended-actions list. Repairs reuse the page's existing
  `sp-admin-slide-over` reason-required confirm flow (the same pattern as
  "Reset student data").

No existing student-facing UI or behavior changed except through an
explicit, audited repair action.

## Explicitly out of scope this phase

New AI scoring behavior, new CEFR update logic, new objective-completion
logic, new Learning Plan regeneration-from-AI logic, new activity types,
student UI redesign, billing/account/org model, teacher/cohort workflow.
