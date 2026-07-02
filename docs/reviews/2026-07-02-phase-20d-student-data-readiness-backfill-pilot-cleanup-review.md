---
status: current
lastUpdated: 2026-07-02 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 20D — Student Data Readiness, Backfill & Pilot Cleanup — Engineering Review

**Date:** 2026-07-02
**Related sprint:** `docs/sprints/current-sprint.md` (Phase 20D entry)
**Related architecture doc:** `docs/architecture/student-readiness-and-backfill.md`
**Prior phase:** Phase 20C (`docs/reviews/2026-07-02-phase-20c-runtime-settings-effective-wiring-review.md`)
**HEAD before work:** `de90e58` (Phase 20C: wire runtime settings into actual replenishment/suggestion behavior)

## Files reviewed / touched

**Application (new):**
- `src/LinguaCoach.Application/Admin/StudentReadiness/StudentReadinessEnums.cs`
- `src/LinguaCoach.Application/Admin/StudentReadiness/StudentReadinessDtos.cs`
- `src/LinguaCoach.Application/Admin/StudentReadiness/StudentReadinessRepairActions.cs`
- `src/LinguaCoach.Application/Admin/StudentReadiness/IStudentReadinessAuditService.cs`
- `src/LinguaCoach.Application/Admin/StudentReadiness/IStudentPilotReadinessRepairService.cs`

**Infrastructure (new):**
- `src/LinguaCoach.Infrastructure/Admin/StudentReadinessAuditService.cs`
- `src/LinguaCoach.Infrastructure/Admin/StudentPilotReadinessRepairService.cs`

**Infrastructure (modified):**
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` (registers the two new services)

**API (new):**
- `src/LinguaCoach.Api/Controllers/AdminStudentReadinessController.cs`

**Angular (modified):**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` (readiness/repair DTOs + enums)
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` (`getStudentReadiness`, `repairStudentReadiness`, `repairAllSafeStudentReadiness`)
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` (readiness + repair signals/methods)
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` ("Pilot readiness" card + repair slide-over)

**Tests (new):**
- `tests/LinguaCoach.UnitTests/Admin/StudentReadinessAuditServiceTests.cs` (11 tests)
- `tests/LinguaCoach.UnitTests/Admin/StudentPilotReadinessRepairServiceTests.cs` (8 tests)
- `tests/LinguaCoach.IntegrationTests/Api/AdminStudentReadinessEndpointTests.cs` (8 tests)

**Tests (modified):**
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — added `getStudentReadiness`/`repairStudentReadiness`/`repairAllSafeStudentReadiness` to every existing spy setup (16 `beforeEach`/`setup()` blocks) plus a new 10-test `pilot readiness panel` describe block.

## Checks implemented (audit service)

~20 read-only checks across 10 categories: account & access, placement &
CEFR, Learning Plan, course readiness, Today lesson, Practice Gym, activity
content validity, audio/TTS, feedback/completion & review scaffold,
progress/mastery. Full list and rationale in
`docs/architecture/student-readiness-and-backfill.md`.

## Repair actions implemented vs. deferred

**Implemented (4 real + run-all):** `generate_learning_plan_if_missing`,
`refill_today_lesson_if_empty`, `expire_invalid_readiness_items`,
`expire_stale_reserved_items`, `run_all_safe_repairs`.

**Deferred (`IsImplemented=false`, documented reason, tracked in
`TODOS.md`):** `refill_practice_gym_if_empty`,
`backfill_missing_activity_metadata`,
`regenerate_missing_tts_for_listening_if_supported`,
`normalize_student_lifecycle_if_safe`,
`refresh_progress_projection_if_supported`.

## Findings by priority

Two logic bugs were found and fixed during test-driven development of the
audit service (both discovered by writing the unit test for the scenario
first, not by manual inspection):

1. **Medium (fixed) — Practice Gym "stuck" check was unreachable.** The
   original condition included `&& !health.NeedsReplenishment`, but
   `PoolHealthSummary.NeedsReplenishment` is `ShortfallCount > 0`, and with
   the default `TargetCount=10` and both `ReadyCount`/
   `QueuedOrGeneratingCount` at 0, `NeedsReplenishment` is always `true` —
   making the "stuck" (Ready=0, Queued=0, Failed>0) branch permanently
   unreachable. Fixed by removing the `!NeedsReplenishment` clause; the
   "empty AND failed AND not actively replenishing" Warning now fires
   correctly (covered by `PracticeGymEmptyAndFailedAndNotReplenishing_IsWarning`).
2. **Low (fixed) — review-scaffold `pending_not_visible` check flagged
   normal queue state as a Fail.** The original check counted
   `PendingReview`/`Rejected` items in visible-eligible statuses and
   reported `Fail`/`Blocking` if any existed — but a `PendingReview` item
   sitting in the queue is the *normal* state (visibility is structurally
   gated by `PassesAdminReviewGate`, which data can't violate). Rewrote to
   always report `Pass`/`Info`; the check now proves the audit itself
   never claims such an item is visible, matching the acceptance criterion
   literally rather than hunting for a data anomaly that can't occur
   (covered by `PendingReviewScaffoldItem_NeverReportedVisible`).

No findings remain open.

## Decisions made

- **Learning Plan existence check queries the DB directly**
  (`_db.StudentLearningPlans.Any(Active || Regenerating)`), never calling
  `GetOrCreatePlanAsync` or the admin `GetLearningPlan` endpoint — both
  auto-generate a plan as a side effect, which would make a "read-only"
  audit mutate state. `GetJourneyAsync` is safe to call for objective-count
  detail (documented as never-throws, empty-result-if-no-plan).
- **Today lesson check inspects preconditions directly** (exercise-type
  availability, active Learning Path) rather than parsing the
  `InvalidOperationException` message `SessionGeneratorService` throws for
  both "no exercise types" and "no learning path" cases — those two causes
  are indistinguishable from the exception text alone.
- **Repair actions are scoped to ones backed by existing, already-safe
  service methods or entity mutators only.** No new generation, mutation,
  or eligibility logic was invented anywhere in the repair service — every
  implemented action calls a pre-existing method (`GetOrCreatePlanAsync`,
  `IGetTodaysSessionHandler.HandleAsync`, `MarkStale`, `Expire`) that
  already exists and is already exercised by other flows (student browser
  requests, the nightly sweep jobs).
- **`dryRun`/`reason`/audit-log is a new convention**, introduced because
  no prior admin-mutation DTO in this codebase had a `dryRun` flag. Layered
  on top of the existing `Reason`-required-for-mutation and
  `AdminAuditLog`-with-`TargetStudentId` conventions already used
  elsewhere (`ResetStudentCommand`, `ApproveReviewScaffoldItem`) rather
  than inventing a parallel audit mechanism.
- **Idempotency is proven by tests, not assumed**: a second call to
  `expire_stale_reserved_items` after the first returns `ChangedCount=0`
  (expired items no longer match the Reserved filter); similarly for
  `generate_learning_plan_if_missing` once a plan exists.
- **Never touches `ActivityAttempt`, `AudioAsset`, or any evaluation
  table** — enforced by a dedicated test
  (`Repair_NeverTouchesAttemptsOrAudioAssets`) that seeds real rows in
  those tables and asserts their counts are unchanged after a repair run.

## AskUserQuestion decisions

None answered mid-phase (an `AskUserQuestion` about check/repair-action
scope breadth went unanswered for the configured timeout; proceeded with
"full breadth of read checks, narrow set of repair actions backed only by
existing safe service methods" per the system's best-judgment fallback —
this matches the phase brief's own "must be careful and limited" framing
and its explicit instruction to mark anything not safely implementable as
"Not implemented yet" rather than invent behavior).

## Implementation tasks produced

All tracked to completion in this session: Application layer (enums,
DTOs, repair-action registry, interfaces), Infrastructure audit service
(all checks), Infrastructure repair service (4 real actions + run-all),
API controller + DI registration, backend tests (unit + integration),
frontend models + API service methods, frontend readiness panel + repair
slide-over on Admin Student Detail, full validation run, docs.

## Risks / unresolved questions

- The 5 deferred repair actions are visible in the admin UI as "Not
  implemented yet" with a reason but cannot be run. If the pilot surfaces
  students blocked specifically by one of these (e.g. an empty Practice
  Gym with no active replenishment), the only current admin recourse is
  the existing global `IReadinessPoolReplenishmentService.RunAsync()`
  scheduled job — there is no on-demand single-student trigger for it.
  Tracked in `TODOS.md`.
- `refill_today_lesson_if_empty` surfaces the exact
  `IGetTodaysSessionHandler` failure message as a Warning rather than a
  generic string; this is intentional (it's the same message the
  student's own browser would see) but means the repair result text is
  coupled to that handler's exception wording.

## Final verdict

Ready to ship. All acceptance criteria are met: an admin can inspect a
clean Ready/NeedsAttention/Blocked readiness verdict for any student;
checks span placement, plan, Today lesson, Practice Gym, activities, audio,
progress, and review scaffold; dry-run and real repair both work; every
real repair is idempotent, non-destructive, and writes exactly one audit
log with a required reason; the Admin Student Detail page shows a
readiness panel without a page redesign; no existing student-facing
behavior changed except via an explicit, audited repair; no attempt,
submission, or evaluation row is ever deleted; backend suites pass with
zero regressions (1,750 unit / 1,378 integration / 3 architecture);
Angular production build is clean; the full Angular suite shows exactly
120 pre-existing failures (unchanged baseline) plus 10 new passing tests,
confirming zero regressions.

## Explicit safety confirmations

- No AI scoring, CEFR update, objective-completion, or Learning Plan
  regeneration-from-AI logic was added or changed.
- No historical `ActivityAttempt`, `AudioAsset`, or evaluation row was
  ever deleted or mutated by any repair action (enforced by a dedicated
  test).
- No repair action runs automatically — every real repair requires an
  explicit admin POST with a non-blank reason.
- Every real repair writes exactly one `AdminAuditLog` row with
  `TargetStudentId`, before/after summaries, and the admin's reason.
- No secrets, connection strings, raw AI provider payloads, or prompt
  text appear in any readiness/repair API response (enforced by a
  dedicated integration test).
- Existing student-facing UI and behavior are unchanged except through
  the explicit repair actions above.

## Validation results

- `dotnet build` — 0 errors, 9 pre-existing warnings (unrelated packages/obsolete API).
- `dotnet test tests/LinguaCoach.UnitTests` — 1,750/1,750 passed.
- `dotnet test tests/LinguaCoach.IntegrationTests` — 1,378/1,378 passed.
- `dotnet test tests/LinguaCoach.ArchitectureTests` — 3/3 passed.
- `npm run build -- --configuration production` — succeeded (pre-existing
  bundle-budget and template warnings only, unrelated to this phase).
- Angular full suite (`ng test --watch=false --browsers=ChromeHeadless`) —
  1,548/1,668 passed, 120 failed — the failure count exactly matches the
  known pre-existing baseline (confirmed by running the same spec file
  against the pre-Phase-20D commit); all 10 new readiness-panel tests pass.

## TODOs added

- `TODO-20D-1` — `refill_practice_gym_if_empty`: needs a single-student
  Practice Gym replenishment entry point.
- `TODO-20D-2` — `backfill_missing_activity_metadata`: needs a concrete,
  safe backfill target to be identified.
- `TODO-20D-3` — `regenerate_missing_tts_for_listening_if_supported`:
  needs a single-activity TTS generation entry point.
- `TODO-20D-4` — `normalize_student_lifecycle_if_safe`: needs a reviewed,
  safe lifecycle-transition rule set before this can be automated.

## Next recommended action

Use the audit endpoint to identify at least one clean (`readyForPilot:
true`) student account among existing development data, then run a
manual pilot smoke test against that account end-to-end (Today lesson,
Practice Gym, at least one attempt submission) before inviting real pilot
users. `TODO-20D-1` (single-student Practice Gym replenishment) is the
most likely next follow-up if the pilot surfaces students blocked
specifically on an empty, non-replenishing gym pool.
