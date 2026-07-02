# Phase 20E — Controlled Student Pilot Smoke QA — Review

- **Date:** 2026-07-02
- **Sprint/feature:** Phase 20E, follows Phase 20D (`docs/reviews/2026-07-02-phase-20d-student-data-readiness-backfill-pilot-cleanup-review.md`)
- **HEAD at start:** `947f9ca`
- **Environment tested:** Production (`https://speakpath.app`), by explicit user decision (see AskUserQuestion decisions below)
- **Files reviewed/touched:** see "Implementation tasks produced"

## Goal

Answer, with evidence: *"Can we give this app to one real student and have
them complete the intended learning flow without developer hand-holding?"*
Use the Phase 20D readiness audit/repair tooling to validate one pilot
student end-to-end in production, fix only P0/P1 blockers found along the
way, and produce a runbook.

## AskUserQuestion decisions

1. **Prod scope.** Asked whether to dry-run only, fully repair a fresh
   student, fully repair an existing student, or both. **Decision: create a
   fresh dedicated pilot student in prod and fully repair it.** Existing
   students were only read (admin dashboard list), never modified.
2. **Root-causing the production DB issue found mid-session.** Asked
   whether to get log/DB access to root-cause a `PostgresException` pattern
   spanning multiple endpoints and background jobs, have the user describe
   the cause, or document it as a blocking finding without root-causing it
   further in this session. **Decision: document as a blocking finding,
   do not root-cause or attempt a production migration fix in this
   session.**

## What was done

### Pilot student

Created via the real admin "Create student" flow
(`/admin/create-student` → `POST /api/admin/students`):
`pilot.student.20e@speakpath.app`, `StudentId = c2a7caff-b46a-4da4-b424-8bd5ca8c0394`.
"Require password change on first login" left checked (the safe/realistic
default). No existing student's data was modified.

### Readiness audit result

`GET /api/admin/students/{id}/readiness` returned **HTTP 500** for this
student, both before and after the student progressed through
onboarding/placement. This is the single biggest finding of the phase —
see "P0 blockers found" below. No dry-run or real repair action could be
exercised because the audit itself never returned a summary to act on.

### Manual pilot walkthrough (in place of the readiness tool)

Logged in as the pilot student and walked the real golden path by hand:

| Step | Route | Result |
|---|---|---|
| Login | `/login` | Pass |
| Forced password change | `/change-password` | Pass — redirected to onboarding |
| Onboarding steps 1–5 | `/onboarding/step-1` … `/onboarding/step-5` | Pass — all 5 steps completed, redirected to `/placement` |
| Placement | `/placement` | **Blocked** — "Start placement" → `POST /api/student/placement/start` → 500 |
| Dashboard | `/dashboard` | Degrades gracefully — shows "Complete your placement assessment" card, no crash |
| Journey | `/journey` | Degrades gracefully — "learning plan is being prepared" empty state |
| Practice | `/practice` | Degrades gracefully — "practice is being prepared" empty state, skill picker still usable |
| Progress | `/progress` | **P0 found and fixed in this session** — was a raw exception message, now fixed (see below) |
| Profile | `/profile` | Pass — loads account/placement/preferences sections |
| Activity player / Feedback | not reached | Blocked by placement failure — a student cannot reach `CourseReady` without completing placement, so no runnable activity or feedback page could be exercised end-to-end in prod this session |

### Admin routes checked

- Admin Students list (`/admin/students`) — pass, pilot student visible,
  filterable, "View profile" row action works.
- Admin Student Detail (`/admin/students/{id}`) — page loads and renders
  the shell, Readiness pool health, Practice Gym, Speaking Submissions,
  Learning Journey, and Mastery evaluation cards correctly with an empty
  pilot student. Four sub-widgets show `[Section] unavailable — Could not
  load [section]` due to the same production issue: **Pilot readiness**,
  **Writing Evaluations**, **Progress Summary**, **Adaptive placement
  assessment**.
- Admin Diagnostics (`/admin/diagnostics`) — used to identify and confirm
  the scope of the P0 finding (see below). Error/warning event log and
  correlation-ID search both work as designed.
- Admin Feature Gates — not modified. No runtime setting was changed in
  this session (dry-run/pilot-visibility settings were left at their
  existing production values throughout).

## P0 blockers found

### P0-1 (documented, not fixed — needs prod DB/log access): widespread `PostgresException` across multiple endpoints and background jobs

**Evidence.** Via `/admin/diagnostics`, filtered to `Error` level and
fetched the raw events JSON
(`GET /api/admin/diagnostics/events?level=Error&limit=100`):

- `GET /api/admin/students/{id}/readiness` → 500, `ExceptionType=PostgresException`
- `GET /api/admin/students/{id}/writing-evaluations` → 500, `PostgresException`
- `GET /api/admin/students/{id}/placement/latest` → 500, `PostgresException`
- `GET /api/placement/status` (student-facing) → 500
- `GET /api/student/placement/current` (student-facing) → 500
- `POST /api/student/placement/start` (student-facing) → 500 — **this is
  the hard pilot blocker**: a brand-new student cannot start placement in
  production today.
- Background job `DEFAULT.writing-evaluation` — `PostgresException`,
  repeating roughly every 5 minutes for at least the several hours visible
  in the event buffer.
- Background job `DEFAULT.writing-signal-application` — same pattern.
- The **same** `readiness`/`writing-evaluations`/`placement/latest` 500s
  were also observed for a pre-existing student
  (`cfcca014-5950-4392-945b-dc668ceb72e1`) at an earlier timestamp,
  confirming this is **not specific to the new pilot student** — it is a
  standing production condition.

**Why this wasn't caught by CI.** Per `CLAUDE.md` / `AGENTS.md`, backend
integration tests run against SQLite in-memory, never against real
PostgreSQL. A Postgres-specific error (missing column/table, a failed or
out-of-order migration, a type/constraint mismatch) would not be
reproduced by the existing 1,750 unit + 1,378 integration tests, both of
which pass cleanly in this session.

**Circumstantial signal.** `src/LinguaCoach.Persistence/Migrations`
contains several migrations whose `T`-number and filename timestamp are
out of the sequence `docs/roadmap/road-map.md` §20 documents ("Migrations
are hand-authored, named T1–T66 in sequence"): `T59_SpeakingEvaluationTables`
is timestamped *after* T63/T65, and `T70_AiPromptContentHash` is
timestamped *after* T71. EF Core applies migrations in filename-timestamp
order, not T-number order, so the actual applied order does not match the
apparent logical order. This is offered as a lead, not a confirmed root
cause — diagnosing the exact failing table/column requires either
production server logs or a database console, neither of which was
available in this session (the diagnostics API deliberately does not leak
raw exception text or SQL to the client, and no DB connection string was
provided).

**Decision:** per the user's explicit AskUserQuestion answer, this is
documented as a blocking finding and not root-caused or fixed in this
session. **This is the reason the golden path could not be walked
end-to-end through placement → activity → feedback.**

**Recommended next step:** an operator with production log/DB access
should pull the actual Postgres error text for one of the failing
requests (correlation IDs are logged, e.g. `89af27f68e52`) and either
(a) run any pending migration that didn't apply, or (b) identify and fix
the specific schema mismatch. This should be treated as a P0 production
incident independent of any future pilot phase.

### P0-2 (fixed): `/progress` leaked a raw internal exception message to the student

**Evidence.** As the pilot student, `/progress` rendered:

> Could not load progress
> A second operation was started on this context instance before a
> previous operation completed. This is usually caused by different
> threads concurrently using the same instance of DbContext.

This is a real, distinct bug from P0-1 (confirmed by exception type —
`InvalidOperationException`, not `PostgresException` — and by the fact
that it reproduces identically against the SQLite-backed local build).

**Root cause.** Both `StudentProgressSummaryHandler.HandleAsync`
(`src/LinguaCoach.Infrastructure/Progress/StudentProgressSummaryHandler.cs`)
and `AdminStudentProgressHandler.HandleAsync`
(`src/LinguaCoach.Infrastructure/Admin/AdminStudentProgressHandler.cs`)
kicked off 4–5 independent loader methods and awaited them together with
`Task.WhenAll(...)`. Every loader shares the same request-scoped
`LinguaCoachDbContext` (including transitively through
`ILearningPlanService.GetProgressAsync`), and EF Core's `DbContext` is not
safe for concurrent use from multiple in-flight async operations. Under
real network latency (Postgres in production; effectively instant in
local SQLite tests) this reliably raced and threw
`InvalidOperationException`, which the student saw as a raw, unstyled
error message — both a broken experience and a minor information
disclosure (internal .NET/EF implementation detail exposed to an
end user).

**Fix.** Changed both handlers to `await` each loader **sequentially**
instead of via `Task.WhenAll`. This is the standard fix for this class of
bug given a single scoped, non-thread-safe `DbContext` (the alternative —
introducing `IDbContextFactory` for true parallel contexts — was judged
out of scope for a QA/hardening phase). Behavior and output DTOs are
unchanged; only the concurrency pattern changed.

**Files changed:**
- `src/LinguaCoach.Infrastructure/Progress/StudentProgressSummaryHandler.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminStudentProgressHandler.cs`

**Verification.** `grep -r "Task.WhenAll" src/` confirms these were the
only two call sites of this pattern in the codebase — not a systemic
anti-pattern elsewhere. All 1,750 unit + 1,378 integration + 3 architecture
tests pass after the change (same counts as before — the fix does not
change any DTO shape or add new branches that need new coverage; a
timing-dependent regression test for a race condition would be flaky and
was judged not worth adding, consistent with the "tests only for stable
smoke coverage" instruction).

## P1 issues found

None beyond what's captured under P0-1's blast radius (the "[Section]
unavailable — Could not load [section]" messages on Admin Student Detail
are the correct, non-scary fallback UI already built in earlier phases —
they degrade gracefully; they are symptoms of P0-1, not a separate P1 UI
bug).

## P2 / cosmetic issues found and fixed (trivial, same bug class as Phase 15H)

Found four more instances of the UTF-8 mojibake bug already fixed once in
Phase 15H (`â€"` / `â€“` / `âœ“` artifacts from a double-encoding mistake),
all in user-visible strings on routes touched by this pilot walkthrough:

- `src/LinguaCoach.Web/src/app/features/student/onboarding/step5-experience/step5-experience.component.ts` —
  "Junior (0–2 years)", "Mid-level (2–5 years)", "Senior (5–10 years)"
  labels, shown on `/onboarding/step-5` (part of the pilot flow walked in
  this session).
- `src/LinguaCoach.Web/src/app/features/student/activity/pattern-evaluation-result/pattern-evaluation-result.component.ts` —
  "Retry recommended — check the corrections below." feedback text.
- `src/LinguaCoach.Web/src/app/features/student/assessment/cefr-assessment/cefr-assessment.component.ts` —
  writing-prompt instruction text ("5–10 sentences").
- `src/LinguaCoach.Web/src/app/features/student/onboarding/onboarding-v2/steps/onboarding-v2-summary.component.ts` —
  checkmark glyph and "— this is a rough guide..." caption text.

Several more instances of the same corrupted byte sequence exist only in
**code comments and test-title strings** (not rendered to any user) — left
untouched as genuinely out of scope (cosmetic, non-functional, and would
be pure churn).

## Backend fixes applied

1. `StudentProgressSummaryHandler.HandleAsync` — sequential loaders
   instead of `Task.WhenAll` on a shared `DbContext` (P0-2).
2. `AdminStudentProgressHandler.HandleAsync` — same fix (P0-2, admin side).

No AI scoring, CEFR update, objective-completion, or Learning Plan
regeneration logic was touched. No readiness-audit or repair-action logic
was changed (P0-1 remains open, undiagnosed in this session).

## Frontend fixes applied

Four mojibake text corrections (see P2 above). No component logic,
routing, guard, or error-state handling was changed — the existing
empty/error-state components (dashboard, journey, practice) already
degrade gracefully and needed no changes.

## Runtime settings

No runtime setting was changed. `PracticeGymPilotEnabled`,
`EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`,
`AllowTodayLessonInsertion`, and the lesson/readiness refill settings were
left exactly as found in production throughout this session.

## Tests

- **Backend:** `dotnet build --configuration Release` — 0 errors.
  `dotnet test tests/LinguaCoach.UnitTests` — **1,750/1,750 pass** (no
  change in count; both touched handlers are exercised by existing
  integration tests). `dotnet test tests/LinguaCoach.IntegrationTests` —
  **1,378/1,378 pass**. `dotnet test tests/LinguaCoach.ArchitectureTests` —
  **3/3 pass**.
- **Frontend:** `npm run build -- --configuration production` — clean.
  `npm test -- --watch=false --browsers=ChromeHeadless` —
  **1,548 pass / 120 fail**, identical to the Phase 20D baseline
  ("120 pre-existing, unrelated failures — unchanged baseline"). **0 new
  regressions** from the 4 text-only edits in this phase.
- **Playwright:** not added/run this session. The one net-new user-facing
  behavior this phase produces is a text fix and a concurrency fix with no
  new branch — neither warrants a new E2E spec, and the golden-path smoke
  Playwright spec described in the phase brief (login → dashboard → today
  → practice → journey → progress → profile) cannot be made to pass
  end-to-end against production right now because of P0-1 (placement
  cannot start). Manual validation notes are captured in the table above
  instead.

## Implementation tasks produced

- `src/LinguaCoach.Infrastructure/Progress/StudentProgressSummaryHandler.cs` (fix)
- `src/LinguaCoach.Infrastructure/Admin/AdminStudentProgressHandler.cs` (fix)
- `src/LinguaCoach.Web/src/app/features/student/onboarding/step5-experience/step5-experience.component.ts` (text fix)
- `src/LinguaCoach.Web/src/app/features/student/activity/pattern-evaluation-result/pattern-evaluation-result.component.ts` (text fix)
- `src/LinguaCoach.Web/src/app/features/student/assessment/cefr-assessment/cefr-assessment.component.ts` (text fix)
- `src/LinguaCoach.Web/src/app/features/student/onboarding/onboarding-v2/steps/onboarding-v2-summary.component.ts` (text fix)
- `docs/pilot/student-pilot-runbook.md` (new)
- `docs/reviews/2026-07-02-phase-20e-controlled-student-pilot-smoke-qa-review.md` (this file)
- `TODOS.md` — new P0 entry for the undiagnosed production DB issue
- `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`,
  `docs/roadmap/road-map.md` — Phase 20E entries added

## Risks / unresolved questions

- **P0-1 is unresolved and is a hard pilot blocker.** A student cannot
  currently complete placement in production. This must be fixed — by
  someone with prod DB/log access — before any real student pilot invite
  goes out.
- Because P0-1 blocked placement, the activity player, activity feedback
  page, and a completed-activity Practice Gym/Journey/Progress state were
  **never reached** in this session. Those parts of the acceptance
  criteria are unverified, not passing.
- The pilot student `pilot.student.20e@speakpath.app`
  (`c2a7caff-b46a-4da4-b424-8bd5ca8c0394`) was left in production in a
  stuck "onboarding complete, placement not started" state. It is real
  seed data for the next attempt, not a bug — no cleanup was performed
  because the task explicitly excludes destructive cleanup.

## Final verdict

**Not ready for a controlled student pilot.** The core blocker
(`POST /api/student/placement/start` returning 500 in production) means a
real student cannot get past placement today. This is pre-existing,
affects all students (new and old), and is outside what this phase's
tooling (Phase 20D's readiness/repair system) can fix, since the
readiness audit endpoint itself is a casualty of the same issue.

Two real, distinct bugs were found and fixed in this session (the
DbContext race on `/progress`, and four cosmetic text bugs on the pilot
path) — both are genuine improvements and should ship — but they do not
unblock the pilot on their own.

## Next recommended action

1. **Immediately:** someone with production log/DB console access pulls
   the actual Postgres error for correlation ID `89af27f68e52` (or any
   fresh `readiness`/`placement/start` 500) and identifies the exact
   missing/mismatched schema object.
2. Fix the schema issue (likely: run a pending migration, or patch a
   migration that partially failed) directly against production, following
   normal production-change safety practice (backup first, apply during a
   low-traffic window, verify with a health check after).
3. Re-run this same manual pilot walkthrough (or, once P0-1 is fixed, the
   Phase 20D readiness audit itself) against `pilot.student.20e@speakpath.app`
   to confirm placement → activity → feedback → Practice Gym now work.
4. Only then schedule Phase 20E's originally-intended Playwright golden-path
   smoke and the "invite one real student" decision.
