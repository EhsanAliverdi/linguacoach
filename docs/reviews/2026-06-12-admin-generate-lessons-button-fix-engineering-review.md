# Admin "Generate lessons now" Button Does Nothing — Engineering Review

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md`
Triggered by: production report — "this button is doing nothing in admin
https://speakpath.app/admin/integrations Generate lessons now".

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`
- `src/LinguaCoach.Web/src/app/core/services/admin-integrations.service.ts`
- `src/LinguaCoach.Api/Controllers/AdminGenerationController.cs` (lines 180-212)

## Findings

### Priority 1 — root cause: no feedback on click

`AdminIntegrationsComponent.generateLessons()` (old code) called
`this.svc.generateLessons(this.generateStudentId).subscribe({ next: () => {...} })`
with no `error` handler and no success indicator beyond clearing the input and
refreshing the (likely empty) batches table.

The backend `POST /admin/students/{id:guid}/generate-lessons` can:

- return `404` if the student profile ID doesn't exist (typo/invalid GUID — the
  input is freeform text, easy to mistype),
- return `503` if `ISchedulerFactory` isn't registered (background jobs disabled),
- return `202 Accepted` with `{ queued: true, requestedCount }` on success.

In all three cases, the admin UI looked identical: nothing visibly changed. A
successful `202` queues a Quartz job whose effects (new rows in "Ready lesson
buffer per student" / "Recent batches") only appear after `loadBatches()` and may
take time to populate — so even the success path looked like "nothing happened."
A `404`/`503` failure was silently swallowed entirely, same pattern as bug #1
(`docs/reviews/2026-06-12-todays-lesson-button-fix-engineering-review.md`).

### Priority 2 — background jobs / Quartz config

Same as bug #1: `QuartzConfiguration.cs` enables background jobs by default, so
`ISchedulerFactory` should be registered in production. The `503` path is an edge
case, not the primary cause, but is now surfaced if it occurs.

## Fix implemented

`src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`:

- Added `generateStatus` signal.
- `generateLessons()` now clears prior status/error, and on success sets
  `generateStatus` to a confirmation message including the queued session count;
  on error sets `batchesError` with the server message (or a generic fallback).

`src/LinguaCoach.Web/src/app/core/services/admin-integrations.service.ts`:

- `generateLessons()` return type changed from `Observable<unknown>` to
  `Observable<{ queued: boolean; requestedCount: number }>` to match the
  controller's `Accepted(new { queued = true, requestedCount = batchSize })`
  response, so the count can be shown to the admin.

`src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`:

- Added a success message line below the "Generate lessons now" button
  (`generateStatus()`). The existing `batchesError()` line above this section now
  also covers generation failures.

## Tests

- `npm run build`: passed (pre-existing CSS budget/selector warnings only,
  unrelated).
- No backend logic changed; `dotnet test` not re-run (no .NET files touched).

## Decisions made

- Reuse the existing `batchesError` signal for generation errors rather than add
  a separate error signal, since both render in the same "Background Jobs" card
  and a dedicated error slot would duplicate styling for no benefit.
- Surface `requestedCount` from the `202 Accepted` response to confirm to the
  admin that a real batch was queued, addressing the "looks like nothing
  happened" perception for the success path.

## Risks / unresolved questions

- None. This is a frontend-only feedback fix; backend queuing logic was already
  correct.

## Final verdict

Root cause fixed: the button now reports success (with queued count) or failure
(with server error message) instead of silently doing nothing in both cases.

## Next recommended action

None.
