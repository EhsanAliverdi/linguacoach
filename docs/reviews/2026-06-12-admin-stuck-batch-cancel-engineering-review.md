# Admin Stuck Batch Cancel — Engineering Review

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md`
Triggered by: stuck `GenerationBatch` rows blocking "Generate lessons now" with
`A lesson generation batch is already running for this student.`

## Scope

- Add an admin-safe replacement for the one-off SQL cleanup.
- Keep the database model unchanged.
- Avoid introducing a new `Cancelled` enum value or migration.

## Fix Implemented

`GenerationBatch` now has `MarkCancelledByAdmin()`, which reuses
`GenerationBatchStatus.Failed` and sets the safe failure reason
`Cancelled by admin.`.

`POST /api/admin/generation/batches/{id}/cancel`:

- requires Admin role through the existing controller authorization
- accepts only `Queued` or `Running` batches
- marks the batch failed with the admin cancellation reason
- returns `404` for unknown IDs and `400` for non-active batches

`/admin/integrations`:

- shows a Cancel action for `Queued` and `Running` recent batches
- keeps Retry for `Failed` and `Partial` batches
- uses the existing Failure column to show `Cancelled by admin.`

`LessonBatchGenerationJob` now checks whether its batch was cancelled after AI
planning and before each session materialization step. This cannot interrupt an
in-flight provider call immediately, but it prevents the job from continuing to
write sessions or overwrite the final batch state after admin cancellation.

## Tests

Added:

- unit coverage for `GenerationBatch.MarkCancelledByAdmin()`
- integration coverage for cancelling a running batch through the admin API and
  verifying persisted `Failed` state plus failure reason

## Decision

Reuse `Failed` with a clear failure reason instead of adding a `Cancelled`
status. This avoids a migration and preserves the current retry workflow:
operators can cancel a stuck row, then click "Generate lessons now" or Retry as
appropriate.

## Residual Risk

Cancellation is cooperative. A provider call already in progress may still run
until it returns or times out. The job checks cancellation before writing more
sessions after that point.
