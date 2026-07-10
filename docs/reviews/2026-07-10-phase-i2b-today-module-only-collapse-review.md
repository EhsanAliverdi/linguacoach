# Phase I2B — Today Module-Only Collapse (Pass B)

**Date:** 2026-07-10
**Related sprint / feature:** I-track (Import pipeline unification → I1, legacy-fallback deletion
→ I2, final nav consolidation → I3). This is Pass B of I2: Today's side. Pass A (commit `3f3769b4`,
`docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md`) did the equivalent for
Practice Gym.

## Context / decision

Today (the student's daily lesson feature) had two content-delivery paths layered on top of each
other: the legacy per-exercise `LearningSession`/`SessionExercise` generation pipeline
(`LessonBatchGenerationJob` → `ActivityMaterializationJob` → `TtsAudioGenerationJob`, plus
on-demand `ExercisePrepareHandler`/`SessionGeneratorService`), and the newer bank-first Daily
Lesson Module pipeline (Phase H6's `IDailyLessonModuleSelectionService`, additive since it shipped
— attached to the same response alongside the legacy content, never replacing it). The user
explicitly directed: **collapse Today to Module-only.** Stop generating legacy session content
entirely; Today now calls only `IDailyLessonModuleSelectionService`, and when it has nothing for
the student, Today honestly reports "nothing available" — never an AI-generation fallback.

## Files reviewed

Backend: `SessionQueryHandler.cs`, `SessionLifecycleHandler.cs`, `SessionsController.cs`,
`SessionHandlers.cs`, `SessionGeneratorCommands.cs`, `StudentDashboardSummaryHandler.cs`,
`StudentDashboardSummaryQuery.cs`, `AdminGenerationController.cs`, `QuartzConfiguration.cs`,
`DependencyInjection.cs`, `LessonBatchGenerationJob.cs`, `ActivityMaterializationJob.cs`,
`LessonBufferRefillJob.cs`, `TtsAudioGenerationJob.cs`, `ExercisePrepareHandler.cs`,
`SessionGeneratorService.cs`, `ReadinessPoolReplenishmentService.cs`, every test file referencing
the deleted types/routes. Frontend: `dashboard.component.ts`/`.html`/`.spec.ts`,
`session.models.ts`, `session.service.ts`, `module-redirect.guard.ts`/`.spec.ts`, `app.routes.ts`,
`admin-lessons.component.ts` (read, not modified), `practice-gym.component.ts` (read, not
modified).

## Findings and decisions, by priority

### P0 — Delete the legacy job pipeline

Deleted entirely:

- `src/LinguaCoach.Infrastructure/Jobs/LessonBufferRefillJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/TtsAudioGenerationJob.cs` — not explicitly listed in the
  task brief, but confirmed orphaned: its only trigger call site was inside
  `ActivityMaterializationJob.Execute` (`await TtsAudioGenerationJob.TriggerAsync(...)`), which is
  now deleted. Grepped the whole repo first — no other caller. Deleted along with its Quartz
  durable-job registration and DI registration.
- `src/LinguaCoach.Infrastructure/Jobs/GenerationHashing.cs` — a small SHA-256 helper used only by
  `TtsAudioGenerationJob`. Orphaned by the same deletion; removed.
- `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs` + `IPrepareExerciseHandler`
  interface (its only implementation).
- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs` + `ISessionGeneratorService`
  interface (its only implementation), and `GetOrCreateTodaysSessionCommand`.

Quartz (`QuartzConfiguration.cs`): removed the `LessonBufferRefillJob` periodic trigger (every 15
min) and the three durable-job registrations for `LessonBatchGenerationJob`,
`ActivityMaterializationJob`, `TtsAudioGenerationJob`. `AudioCleanupJob`,
`ReadinessPoolReplenishmentJob`, `NotificationDispatchJob`, `StudentMasteryEvaluationJob`,
speaking/writing evaluation jobs, and `GenerationValidationFailurePruneJob` are all untouched —
none of them were triggered by or triggered the deleted jobs.

`DependencyInjection.cs`: removed the four job `AddScoped<>()` registrations,
`ISessionGeneratorService`/`SessionGeneratorService`, and
`ExercisePrepareHandler`/`IPrepareExerciseHandler`.

### P0 — `SessionLifecycleHandler.TriggerBufferRefillAsync` removed

`SessionLifecycleHandler` (Start/Complete/CompleteExercise) is otherwise untouched per the task's
explicit scope. Its `CompleteSessionCommand` handler used to call
`TriggerBufferRefillAsync` → `LessonBatchGenerationJob.TriggerAsync` on lesson completion. Deleted
that private method and its call site entirely (not replaced with anything) — Today now re-selects
a Daily Lesson Module live on the next page load via `IDailyLessonModuleSelectionService`, so there
is nothing to pre-generate in the background. The now-unused `ISchedulerFactory?` constructor
parameter and the `Quartz`/`Jobs` usings were removed too.

### P0 — `SessionQueryHandler` rewrite: Today is module-only

`HandleAsync(GetTodaysSessionQuery)` no longer calls `ISessionGeneratorService` at all. It now:

1. Loads the student profile.
2. Calls `IDailyLessonModuleSelectionService.SelectAsync(...)` (unchanged call shape from H6) in a
   try/catch, so a selector failure degrades to "nothing available" rather than propagating.
3. Calls `IDailyLessonModuleAssignmentRecorder.RecordAsync(...)` for the day's assignment
   bookkeeping (unchanged from H6).
4. Returns `new TodaysSessionResult(available, moduleSection)` where `available` is `true` only
   when a `moduleSection` was returned, `FallbackRequired` is `false`, and at least one module was
   selected.

**`TodaysSessionResult` DTO shape decision:** shrunk the record rather than keeping legacy fields
present-but-null. Old shape:
`(SessionId, Title, Topic, SessionGoal, DurationMinutes, FocusSkill, Status, IsResuming, Exercises, ModuleSection)`.
New shape: `(bool Available, DailyLessonModuleSelectionResult? ModuleSection)`. Reasoning: the task
brief itself frames "never a silently-empty legacy shape" as the goal — keeping 8 nulled-out
legacy fields on every response would be exactly that, an invitation for a future reader to treat
`SessionId: null` as "session pending" rather than "there is no session concept anymore." A single
`Available` boolean plus the real `ModuleSection` payload is the more honest shape. The only two
production consumers (`SessionsController.Today`, `StudentDashboardSummaryHandler`) were both
updated in the same change, so there is no orphaned reader of the old field names.

`HandleAsync(GetSessionQuery)` (`GET /api/sessions/{id}` detail) and its private helpers
(`EnsureSessionBelongsToStudentAsync`, `ResolveKind`) were deleted — see the controller section
below for why.

### P0 — `StudentDashboardSummaryHandler` updated to match the new DTO

`StudentDashboardSummaryHandler` calls `IGetTodaysSessionHandler` for its own `todaySession`
summary section and previously read `Status`/`SessionId`/`Title`/`Topic`/`Exercises.Count` — none
of which exist on the new `TodaysSessionResult`. `BuildTodaySession` was rewritten to read
`session.Available` and `session.ModuleSection?.SelectedModules.FirstOrDefault()` instead:

- `!courseActive || failed` → `"NotAvailable"` (unchanged).
- `session is null` → `"Preparing"` (kept for defensive symmetry with `Practice`'s Preparing state,
  though in practice `SessionQueryHandler` always returns a non-null result unless the whole call
  throws, which the dashboard handler already catches as `failed`).
- `!session.Available || selected is null` → `"NotAvailable"` (the honest "nothing available"
  state — previously this branch didn't exist; the old code always had *something* to show because
  the legacy generator never returned null).
- Otherwise → `"Ready"`, with `Title`/`Topic`/`SessionGoal`/`FocusSkill`/`DurationMinutes`/
  `ExerciseCount` repurposed from the selected module's `Title`/`Description`/`Reason`/`Skill`/
  `EstimatedMinutes`/`(LinkedLearnItems.Count + LinkedActivityDefinitions.Count)`. `SessionId` is
  always `null` now — there is no session concept left on this path.

`warnings.MissingTodaySession` changed from `session is null && isCourseActive && !sessionFailed`
to `isCourseActive && !sessionFailed && (session is null || !session.Available)` — it now correctly
flags the "loaded successfully but nothing was available" case, which was previously invisible to
this warning (the old generator never failed to produce *something*).

### P0 — `SessionsController` — deleted `Get` and `PrepareExercise` actions

`GET /api/sessions/{id}` (`Get` action, backed by the now-deleted `IGetSessionHandler`) and
`POST /api/sessions/{id}/exercises/{eid}/prepare` (`PrepareExercise` action, backed by the deleted
`IPrepareExerciseHandler`) were both deleted, along with `_get`/`_prepare` fields and the
`using LinguaCoach.Application.Ai;` import (only needed for the prepare action's
`AiServiceUnavailableException` catch clause).

**Judgment call — why delete rather than "no longer supported":** grepped the whole frontend
(after also removing the lesson-runner page and the `module-redirect.guard.ts` `session-` branch,
see below) and confirmed zero remaining callers of either route. Per the task's explicit
preference ("prefer deletion if there's no live frontend caller"), deleted cleanly rather than
leaving a "410 Gone"-style stub.

`Start`/`Complete`/`CompleteExercise`/`History`/`Reflection` actions are **unchanged** — they still
operate on legitimate `LearningSession`/`SessionExercise` data via `SessionLifecycleHandler`
(explicitly out of scope beyond the buffer-refill trigger removal above). They are currently
unreachable from the live UI (nothing creates a new session for Today anymore, and the
lesson-runner page that was their only caller was deleted), but deleting them was not requested and
they remain correct, idempotent operations on real entities — left in place per the task's
narrower framing of what to touch in `SessionsController`.

### P1 — `AdminGenerationController`: `RetryBatch`/`GenerateLessons` turned into honest no-ops, not deleted

**Judgment call, flagged explicitly (the task offered both options):** `RetryBatch`
(`POST /admin/generation/batches/{id}/retry`) and `GenerateLessons`
(`POST /admin/students/{id}/generate-lessons`) both called `LessonBatchGenerationJob.TriggerAsync`,
now deleted. The task's brief allowed either deleting these actions or turning them into an honest
no-op. **Chose no-op, not deletion**, because the surrounding admin page
(`admin-lessons.component.ts`, nav-labeled "Today Delivery Health") is a 450-line dashboard with
substantial *unrelated* live functionality — readiness pool health, review scaffold dry-run/pending
review/pilot summary, mastery validation — that has nothing to do with the deleted generation
pipeline and would have broken if the whole controller/page were removed. Both actions now return
`409 Conflict` with `{ error: "Lesson batch generation has been retired. Today is module-only
now — there is nothing left to [regenerate|generate]." }`. The `ISchedulerFactory?` constructor
parameter, now unused in this controller, was removed. `GetSettings`/`UpdateSettings`/`GetBatches`/
`CancelBatch`/storage endpoints are all untouched — they read/display historical
`LessonGenerationSettings`/`GenerationBatch` data, not trigger new generation.

Frontend `admin-lessons.component.ts` was **not modified** — its `generateLessons()` method already
surfaces `err?.error?.error` from a failed HTTP call as a status message, so the new 409 response
displays as an honest error inline without any component change needed.

### P1 — Frontend: dashboard collapsed to module-only

`dashboard.component.ts`/`.html`: removed the `todaysSession`/`sessionLoading` signals and the
`applyFromSummary` block that synthesized a fake `TodaysSessionResponse` from the summary's
`todaySession` section (that section's `SessionId`/`Title`/etc. are now repurposed to describe the
*module*, not a session — see the dashboard-summary-handler section above — so synthesizing a fake
session object from it no longer makes sense). Removed `todaySessionState()`/`lessonButtonLabel()`.

The two previously-separate HTML blocks — the legacy "Today's Lesson" gradient card (driven by
`todaysSession()`) and the additive H6 "Today's module" card (driven by `dailyLessonModuleSection()`)
— were merged into **one** `data-testid="dashboard-todays-lesson"` card with three states:
loading skeleton, the selected module (existing H6 rendering, reused verbatim), or a new
`data-testid="today-not-available"` "Nothing available yet" empty state. `dailyLessonModuleSection`
is now populated directly from `GET /api/sessions/today`'s `available`/`moduleSection` fields
(`todaySectionLoading`/`todaySectionLoaded`/`todaySectionAvailable` signals track the fetch
lifecycle so the template can distinguish "still loading" from "loaded, nothing available").

`dashboard.component.spec.ts` was rewritten: fixtures now mock `SessionService.getToday()` directly
(added to the `TestBed` providers) instead of building a `TodaysSessionResponse` from the old shape
via a `sessionToSection()` helper. New/renamed assertions cover the loading/available/not-available
states against the new `data-testid`s.

### P1 — `session.models.ts` / `session.service.ts` DTO + method removal

`TodaysSessionResponse` shrunk to `{ available: boolean; moduleSection: DailyLessonModuleSection | null }`,
mirroring the backend. `SessionExercise`, `SessionDetailResponse`, and `PrepareExerciseResponse`
interfaces removed (each had exactly one remaining consumer — `lesson.component.ts` — deleted
below). `SessionService.getById()`/`.prepareExercise()` removed (their only callers were
`lesson.component.ts` and `module-redirect.guard.ts`'s `session-` branch, both deleted).
`start()`/`complete()`/`completeExercise()`/`getHistory()` are unchanged.

### P1 — Lesson-runner page: deleted

Grepped the whole frontend for any remaining reachable link into `lesson/:sessionId` or
`module/session-{id}-{id}` (the only two ways in). Found exactly two:

1. `dashboard.component.html`'s `routerLink="['/lesson', ...]"` — removed as part of the dashboard
   rewrite above.
2. `module-redirect.guard.ts`'s `session-{sessionId}-{exerciseId}` branch — this branch's only
   entry point was `lesson.component.ts`'s `moduleUrl()` helper (`/module/session-{id}-{id}`
   links rendered inside the lesson-runner page itself). With the lesson-runner page gone, nothing
   generates a `moduleRunId` in that shape anymore.

Deleted `src/app/features/student/lesson/` entirely (`lesson.component.ts`/`.html`).
`app.routes.ts`'s `lesson/:sessionId` route now `redirectTo: () => '/dashboard'`, matching the
existing `RedirectFunction` pattern used for Phase I2A's `activity-templates` redirects.
`module-redirect.guard.ts` had its `session-` branch removed entirely (it called
`sessionService.getById`/`.prepareExercise`, both now deleted) — any leftover/bookmarked
`session-...`-shaped `moduleRunId` now falls through to the guard's existing default
`/dashboard` redirect. `module-redirect.guard.spec.ts` rewritten: the 5 `session-` tests replaced
with 2 tests asserting the fallthrough-to-`/dashboard` behavior for both a session-shaped and a
fully-unrecognized `moduleRunId`.

**Known pre-existing dead code, not touched:** `practice-gym.component.ts`'s `startSuggestion()`
still has a branch that does `this.router.navigate(['/lesson'], { queryParams: { sessionId: ... } })`
when a suggestion resolves to `result.learningSessionId`. This branch was already unreachable
before this pass — Pass A zeroed `PracticeGymSuggestionsDto.SuggestedItems`/`ContinueItems`/
`ReviewItems` to always `[]`, so no suggestion item can ever exist for the UI to click through to
this code path. It also targets `/lesson` (no path segment) which never matched the
`/lesson/:sessionId` route in the first place (this looks like a pre-existing bug independent of
this pass, not something Phase I2B introduced). Left untouched — out of scope, and the route falls
through harmlessly to the `**` wildcard → `/dashboard` redirect if ever reached.

### P2 — Orphaned but not deleted: `LedgerSignals`/`DynamicPatternSelector`

`DynamicPatternSelection.cs` (`LedgerSignals`, `PatternSelectionInput`, `PatternCatalogEntry`) and
`Infrastructure/Sessions/DynamicPatternSelector.cs` were used by exactly one caller —
`SessionGeneratorService`, now deleted. Confirmed via grep: their only remaining references are to
each other and to their own dedicated unit test file (`DynamicPatternSelectorTests.cs`, still
green). **Not deleted** — this is downstream of the task's explicit file list, not on it, and
deleting a selector-plus-its-test-suite is more surface than this pass's brief authorized. Updated
`LedgerSignals`' doc comment to flag it as orphaned for a future cleanup pass. Flagging here per the
"flag anything ambiguous" instruction.

## Readiness pool — confirmation for Pass C

Per item 5 of the task brief: **`LessonBatchGenerationJob`/`ActivityMaterializationJob` are
confirmed gone**, and grepping the whole `src/` tree for `ReadinessPoolSource.TodayLesson` /
`ReadinessPoolSource.LessonBatch` after this pass's deletions shows:

- **`ReadinessPoolSource.LessonBatch`** — now fully orphaned. Its only writer was
  `LessonBatchGenerationJob` (`CreateQueuedAsync`/`MarkGeneratingAsync`/`MarkReadyAsync`), now
  deleted. The one remaining reference (`LearningPlanService.cs:257`) only *reads* it as a filter
  predicate — no code anywhere still creates a `LessonBatch`-sourced readiness item.
- **`ReadinessPoolSource.TodayLesson`** — **still actively written**, but by a completely different,
  unrelated component: `ReadinessPoolReplenishmentService` (a still-running Quartz job untouched by
  this pass) iterates `{ TodayLesson, PracticeGym }` as its two replenishment sources and calls
  `_pool.CreateQueuedAsync(...)` for both, independent of anything Today itself does.
  `SessionQueryHandler` (Today's actual handler, rewritten in this pass) **never calls
  `IStudentActivityReadinessPoolService` at all** — it only calls
  `IDailyLessonModuleSelectionService`/`IDailyLessonModuleAssignmentRecorder`. So `TodayLesson`-sourced
  readiness items are still generated by the background replenishment engine, but have **zero
  consumers on the Today path** after this pass — exactly mirroring Pass A's finding that
  `PracticeGym`-sourced rows are still generated but have zero readers on the Practice Gym side.

**For Pass C:** confirm whether `ReadinessPoolReplenishmentService` should stop generating
`TodayLesson`-sourced rows too (mirroring the `PracticeGym` question left open by Pass A), or
whether both are deliberately left for a future bank-first replenishment mechanism. Either way, no
code on either Today's or Practice Gym's live serving path reads from the readiness pool anymore —
Pass C's interface-narrowing work on `IAiActivityGenerator` can proceed on that basis.

Also confirmed (per the "What NOT to touch" list): `IAiActivityGenerator.GenerateActivityContentAsync`
now has **zero remaining callers** in `src/` — its only two call sites were inside the now-deleted
`ActivityMaterializationJob` and `ExercisePrepareHandler`. `ActivitySubmitHandler.EvaluateAttemptAsync`
(evaluation, a different method on a different concern) is unaffected and untouched.

## Migration

None. No EF-mapped entity's shape changed — this pass deleted jobs/handlers/DTOs, not entities or
their configurations. `LearningSession`/`SessionExercise` domain entities and their EF
configurations are untouched (still needed for the H10 launch bridge and historical/completed
session data, per the task's explicit "what NOT to touch" list).

## Test fixes (grouped)

Deleted entirely (whole file tested only deleted code):
`tests/LinguaCoach.UnitTests/Jobs/LessonBatchPlanValidationTests.cs`,
`tests/LinguaCoach.IntegrationTests/Jobs/ActivityMaterializationJobBankFirstTests.cs`,
`tests/LinguaCoach.IntegrationTests/Sessions/SessionGeneratorServiceTests.cs`,
`tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs`,
`tests/LinguaCoach.IntegrationTests/Sessions/LessonBufferRefillJobTests.cs`,
`tests/LinguaCoach.IntegrationTests/Api/ExercisePrepareEndpointTests.cs`,
`tests/LinguaCoach.IntegrationTests/Sessions/TtsAudioGenerationJobTests.cs`.

Kept, unchanged (read fully first, confirmed no reference to deleted code):
`tests/LinguaCoach.IntegrationTests/Persistence/LearningSessionGenerationStatusPersistenceTests.cs`
— this is a `LearningSession.GenerationStatus` EF round-trip regression test against the entity
itself, not the deleted jobs; its doc comment references `LessonBatchGenerationJob` only as
historical context for *why* the regression it guards against was dangerous, which stays accurate.

Rewritten/trimmed:

- `tests/LinguaCoach.UnitTests/Sessions/ExercisePatternPhase2UnitTests.cs` — removed only the
  `ExercisePrepareHandler.MapKindToActivityType` theory/fact (2 of 10 tests); the rest
  (`SessionDurationTemplates` pattern-key coverage) is unrelated and kept.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase2Tests.cs` — deleted the entire
  `ExercisePatternPhase2EndpointTests` HTTP class (100% built around the deleted `/prepare` action
  and `GetSessionWithExercisesAsync`, which chained Today → GetById, both changed/deleted); kept
  the entire `ExercisePatternPhase2DbTests` class (DB-only pattern/seeder coverage, unrelated).
- `tests/LinguaCoach.IntegrationTests/Api/SessionEndpointTests.cs` — substantially rewritten.
  Today-endpoint tests now assert the new `available`/`moduleSection` shape instead of
  `sessionId`/`exercises`. `GetById_*` tests deleted (action gone). Start/Complete/CompleteExercise/
  Reflection tests kept but no longer obtain a session via `GetTodaysSessionIdAsync` (removed) —
  `SessionTestFactory` gained `CreateCourseReadyStudentWithSessionAsync`, which seeds a
  `LearningSession` + 2 `SessionExercise`s directly via `LinguaCoachDbContext`, mirroring Pass A's
  own pattern of seeding entities directly once the generation endpoint that used to produce them
  is gone.
- `tests/LinguaCoach.IntegrationTests/Api/DailyLessonModulePipelineEndpointTests.cs` — 2 of 9 tests
  updated: `Today_falls_back_when_no_compatible_module_exists` now asserts `available == false`
  (was asserting `exercises.Length > 0`, i.e. the *opposite* of the module-only invariant), with the
  same defensive "other tests in this shared-fixture class may have already seeded a compatible
  module" early-return pattern already used by its sibling
  `Admin_preview_shows_fallback_reason_when_no_module_available` — this was necessary because
  `IClassFixture<SessionTestFactory>` shares one DB across the whole test class, and other tests in
  the same file seed universal (`CefrLevel: null`) modules. `Existing_today_fallback_path_still_works`
  renamed to `Today_endpoint_is_idempotent_across_repeated_calls` and now compares `available`
  across two calls instead of `sessionId`.

Minor doc-comment updates (no behavior change): `LearningActivity.ExercisePatternKey`'s doc comment
("Set by ExercisePrepareHandler") and `LedgerSignals`' doc comment ("Built once per session by
SessionGeneratorService") both updated to past tense + a pointer to this review, since both
referenced classes are now deleted/orphaned.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,640 / 3,640 passing, 0 failing**
  (`ArchitectureTests` 5/5, `UnitTests` 2,237/2,237, `IntegrationTests` 1,398/1,398). Baseline before
  this pass was 3,734/3,734 (Pass A's post-commit number) — the 94-test reduction comes entirely
  from deleted test files/methods that exercised now-removed legacy behavior (listed above), not
  from any coverage loss on surviving functionality.
- `npm run build -- --configuration production`: 0 new TypeScript/Angular errors. The only `[ERROR]`
  is the pre-existing initial-bundle budget overage (2.56 MB vs the 1 MB `maximumError` threshold),
  explicitly called out as acceptable in the task brief and predating this session.
- `npx ng test` (whole-suite karma run): **could not be run to completion** — the spec-file
  compilation unit includes 5 files this pass never touched
  (`activity-feedback-page.component.spec.ts`, `activity-lesson-submission.component.spec.ts`,
  `activity-lesson-vocab.component.spec.ts`, `presenters/test-helpers.ts`,
  `practice-gym.component.spec.ts`) that fail to type-check against the current
  `activity.models.ts`/`practice-gym-suggestions.service.ts` shapes (a missing required
  `feedbackPolicy`/`moduleSuggestions` field). Confirmed via `git status`/`git log` these files have
  no pending changes and were last touched in Phase 18b (2026-07-01) and 19C (2026-07-02) —
  pre-existing, unrelated to this session, not something this pass introduced or can fix within
  scope. Given this, `dashboard.component.spec.ts` and `module-redirect.guard.spec.ts` (both
  rewritten in this pass) were verified by careful manual review against the new DTO/service
  shapes and by the fact that `ng build` (which does type-check all non-spec app source) passes
  clean — but they could not be executed end-to-end via karma in this session. **Flagging this as a
  residual risk**: recommend running `npx ng test --include='**/dashboard*.spec.ts'
  --include='**/module-redirect*.spec.ts'` once the 5 pre-existing broken spec files are fixed (out
  of this pass's scope) to get a real pass/fail signal on the rewritten specs.
- Final repo-wide grep for `LessonBatchGenerationJob`, `ActivityMaterializationJob`,
  `ExercisePrepareHandler`, `LessonBufferRefillJob`, `SessionGeneratorService`,
  `ISessionGeneratorService`, `IPrepareExerciseHandler`: every remaining hit is a doc comment,
  either already updated for accuracy in this pass or legitimately historical (e.g.
  `LearningSessionConfiguration.cs`'s comment explaining why a specific EF convention was
  dangerous, referencing the now-deleted job as the original trigger of that bug).

## Decisions made

1. `TodaysSessionResult` shrunk to `(bool Available, DailyLessonModuleSelectionResult? ModuleSection)`
   rather than kept-with-nulled-legacy-fields — see the DTO-shape section above.
2. `AdminGenerationController.RetryBatch`/`GenerateLessons` turned into honest `409 Conflict`
   no-ops rather than deleted, because the surrounding admin page has substantial unrelated live
   functionality that would have broken if the controller were removed wholesale.
3. `GET /api/sessions/{id}` and the exercise `/prepare` action deleted outright (not "no longer
   supported" stubs) — zero remaining frontend callers confirmed after the lesson-runner page and
   guard branch were also removed.
4. Lesson-runner page (`src/app/features/student/lesson/`) deleted outright; `lesson/:sessionId`
   route redirects to `/dashboard`.
5. `LedgerSignals`/`DynamicPatternSelector` left in place despite being orphaned — out of this
   pass's authorized scope; flagged for a future cleanup pass rather than deleted unilaterally.
6. `Start`/`Complete`/`CompleteExercise`/`History`/`Reflection` session actions and their frontend
   `SessionService` methods left wired up even though currently unreachable from any live UI —
   the task scoped `SessionsController` changes to the `Get`/`prepare` actions specifically, and
   these remaining actions are correct operations on legitimate historical `LearningSession` data.

## Risks / unresolved questions

- The whole-suite frontend karma test run could not be executed due to 5 pre-existing,
  unrelated broken spec files (see Validation above). This is a real gap in end-to-end
  verification for the two frontend spec files this pass rewrote, mitigated by manual review and
  the clean `ng build` type-check, but not a substitute for an actual test run.
- `ReadinessPoolSource.TodayLesson` is still actively written by `ReadinessPoolReplenishmentService`
  with zero remaining consumers on Today's live path — mirrors Pass A's identical finding for
  `PracticeGym`. Left unresolved for Pass C by explicit task scoping.
- `practice-gym.component.ts`'s dead `router.navigate(['/lesson'], ...)` branch (pre-existing,
  already unreachable since Pass A) was left untouched — flagged but not fixed, since it was out of
  this pass's scope and does not affect Today's module-only behavior.
- `AdminGenerationController`'s `GetSettings`/`UpdateSettings` still expose
  `ReadyLessonBufferSize`/`RefillThreshold`/`RefillBatchSize`/`MaxGenerationAttempts`/
  `GenerationTimeoutSeconds`/`TtsTimeoutSeconds`/`MaxConcurrentGenerationJobs`/
  `MaxConcurrentTtsJobs`/`EnableBackgroundGeneration`/`EnableTtsGeneration` fields on
  `LessonGenerationSettings` that no longer have any reader (every job that consulted them is
  deleted). They're harmless (display/edit only, backed by a table this pass doesn't touch) but are
  now entirely inert — worth a follow-up cleanup pass alongside the `PracticeGymReadyExercisesPerType`
  etc. fields Pass A already flagged as similarly inert.

## Final verdict

Pass B complete and verified on the backend (0 build errors, 3,640/3,640 tests passing) and on the
frontend build (0 new TS/Angular errors). Today no longer has any legacy per-exercise
generation/AI-fallback path; it honestly reports "nothing available" when the bank-first Daily
Lesson Module selector has nothing for the student. The one gap is the whole-suite frontend karma
test run, blocked by pre-existing unrelated breakage — flagged clearly above rather than worked
around by touching out-of-scope files.

## Next recommended action

Pass C: narrow `IAiActivityGenerator` (confirmed zero remaining callers of
`GenerateActivityContentAsync`), decide the readiness pool's fate now that both `TodayLesson`- and
`PracticeGym`-sourced replenishment write paths are confirmed to have zero live consumers, and
separately — outside the I-track — someone should fix the 5 pre-existing broken frontend spec files
so the whole-suite `ng test` run is usable again for future passes.
