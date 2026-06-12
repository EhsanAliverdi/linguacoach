# Today's Lesson Button Not Working — Engineering Review

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md`
Triggered by: production report — "no lesson is generated for student in advance, the
'Start lesson' button doesn't work, the scheduler for created lessons isn't working."

## Files reviewed

- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` (lines 320-366,
  existing lazy LearningPath generation in the legacy activity flow)
- `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Application/LearningPath/LearningPathCommands.cs`
- `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` (background job enable check)
- `tests/LinguaCoach.IntegrationTests/Sessions/SessionGeneratorServiceTests.cs`

## Findings

### Priority 1 — root cause: SessionGeneratorService had no LearningPath fallback

`SessionGeneratorService.GetOrCreateTodaysSessionAsync` calls
`ResolveCurrentModuleAsync`, which returned `null` if the student had no active
`LearningPath` (e.g. `CourseReady` students who completed placement but never hit the
legacy `/activity` flow, which is the only place a `LearningPath` was previously
created). `GetOrCreateTodaysSessionAsync` then called `EnsureFallbackModuleId`, which
unconditionally **threw `InvalidOperationException`**
(`SessionGeneratorService.cs:354-357`, old code).

`SessionsController.Today` caught this and returned `400 BadRequest`
(`SessionsController.cs:60-63`). The frontend's `loadTodaysSession()`
(`dashboard.component.ts:90-96`, old code) caught this error and **silently** called
`this.sessionLoading.set(false)` only — no error surfaced, `todaysSession()` stayed
`null`.

In the template, the "Start today's lesson" button's `routerLink` is
`todaysSession() ? ['/lesson', todaysSession()!.sessionId] : null`
(`dashboard.component.html:130`) — with `todaysSession()` null, the link is `null`,
so the button renders but does nothing when clicked.

This explains all three symptoms: no lesson generated in advance (session creation
never completed for these students), the button doesn't work (no session = no link),
and it looks like "the scheduler isn't working" (the background lesson-buffer jobs
operate on existing sessions/activities — they never get a chance to run for a
student stuck with no `LearningPath` at all).

`ActivityGetHandler.ResolveCurrentModuleAsync` (lines 322-366) already had this exact
lazy-generation fallback — calling `IAdaptivePathGenerator`/`ILearningPathGenerator`
when no path exists — but `SessionGeneratorService` was written later and didn't reuse
it.

### Priority 2 — background jobs / Quartz config

Investigated as a possible contributing cause. `QuartzConfiguration.cs:23-24`:
```csharp
var enabled = config.GetValue<bool?>("BackgroundJobs:Enabled")
    ?? (Environment.GetEnvironmentVariable("BACKGROUND_JOBS_ENABLED") is "true" or null);
```
This means background jobs are **enabled by default** unless explicitly disabled —
correct/intentional, matches `LessonGenerationSettings.EnableBackgroundGeneration`
defaulting to `true`. Not a bug. No change made.

## Fix implemented

`src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs`:

- Constructor now takes `ILearningPathGenerator` (already registered in DI as
  `AiLearningPathGeneratorHandler`, used by the legacy activity flow).
- `ResolveCurrentModuleAsync(Guid userId, Guid studentProfileId, ...)` — when no
  active `LearningPath` exists, calls
  `_pathGenerator.GenerateAsync(new GenerateLearningPathCommand(userId), ct)` (mirrors
  `ActivityGetHandler.cs:331-342`), then re-queries for the path before proceeding.
  `AiLearningPathGeneratorHandler.GenerateAsync` never throws — falls back to
  `DefaultPathFactory` on AI failure, so this always produces a usable path.
- Call site updated to pass `profile.UserId` (loaded earlier in
  `GetOrCreateTodaysSessionAsync`).
- `EnsureFallbackModuleId` retained as a final guard (now effectively unreachable
  in normal operation, since path generation never returns null/throws).

`src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts`:

- `loadTodaysSession()` error handler now sets `this.error` with the server message
  (or a generic fallback), instead of silently swallowing the error — so any future
  failure is visible to the student/support instead of an inert button.

## Tests

- `tests/LinguaCoach.IntegrationTests/Sessions/SessionGeneratorServiceTests.cs`:
  - Added `FakeLearningPathGenerator` (creates a minimal active `LearningPath` +
    one `LearningModule` for the student, standing in for
    `AiLearningPathGeneratorHandler` without invoking AI).
  - Replaced `GetOrCreate_NoLearningPath_Throws` (asserted the old broken behavior)
    with `GetOrCreate_NoLearningPath_GeneratesPathLazilyAndCreatesSession` — asserts
    a session with exercises is created AND an active `LearningPath` now exists for
    the student.
- `dotnet test`: 482/482 unit, 430/430 integration passing.
- `npm run build`: passed (pre-existing CSS warnings only).

## Decisions made

- Reuse the existing `ILearningPathGenerator`/`AiLearningPathGeneratorHandler` (same
  one `ActivityGetHandler` already uses) rather than introducing a new path-generation
  mechanism — keeps the "lazy generation for students without a path" behavior
  consistent across both the session and legacy activity flows.
- Quartz/background-job enable logic left unchanged — investigated, found correct.

## Risks / unresolved questions

- `AiLearningPathGeneratorHandler.GenerateAsync` calls AI (with `DefaultPathFactory`
  fallback) — first `/api/sessions/today` call for an affected student will now be
  slightly slower (one extra path-generation step) the first time only. Acceptable;
  matches existing behavior in the legacy activity flow.
- This fix is reactive (lazy, on first request). A more complete fix would ensure
  `LearningPath` generation happens automatically when `PlacementService` sets
  `CourseReady` (`PlacementService.cs:165`), so no student ever reaches
  `SessionGeneratorService` without a path. Not done here — out of scope for this
  bug fix, flagged as a possible follow-up.

## Final verdict

Root cause fixed: `SessionGeneratorService` now lazily generates a `LearningPath` for
students who reach "Today's Lesson" without one (e.g. fresh `CourseReady` students who
never used the legacy activity flow), matching the existing fallback in
`ActivityGetHandler`. The "Start today's lesson" button will now work for these
students, and any future session-load failure will be shown to the student instead of
silently disabling the button.

## Follow-up implemented (2026-06-12)

`src/LinguaCoach.Infrastructure/Placement/PlacementService.cs`:

- Injected `ILearningPathGenerator`.
- After `profile.SetLifecycleStage(StudentLifecycleStage.CourseReady)`, added
  `GenerateLearningPathAsync(profile.UserId, profile.Id, ct)` — calls
  `_pathGenerator.GenerateAsync(new GenerateLearningPathCommand(userId), ct)`,
  wrapped in try/catch (logs a warning on failure, never blocks placement
  completion — `AiLearningPathGeneratorHandler` itself never throws, falls back to
  `DefaultPathFactory`).

Now every student gets an active `LearningPath` immediately on placement
completion. The lazy-generation fallback in `SessionGeneratorService`/
`ActivityGetHandler` remains in place as a safety net for any pre-existing
`CourseReady` students who don't yet have a path.

Tests: 482 unit + 430 integration passing.

## Next recommended action

None — both the reactive fallback and the proactive generation are in place.
