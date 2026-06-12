# Lesson Batch Generation — Duplicate Batches & Stuck "Running" Status

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md`
Triggered by: production log showing `DbUpdateConcurrencyException` inside
`LessonBatchGenerationJob.Execute` after the Quartz `JobDataMap` fix
(`docs/reviews/2026-06-12-quartz-jobdatamap-string-fix-engineering-review.md`),
plus the admin UI showing two duplicate batches stuck in "Running" status with
"0 / 4" sessions for the same student.

## Files reviewed

- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
- `src/LinguaCoach.Api/Controllers/AdminGenerationController.cs`
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` (concurrency token config)
- `src/LinguaCoach.Domain/Entities/GenerationBatch.cs`

## Findings

### Priority 1 — root cause: no guard against concurrent batches per student

`LessonBatchGenerationJob.TriggerAsync` builds a unique Quartz job key per call
(`{JobName}-{studentProfileId:N}-{Guid.NewGuid():N}`). `[DisallowConcurrentExecution]`
only prevents two *instances of the same job key* from running concurrently —
it does nothing to stop two separate `TriggerAsync` calls for the same student
from running in parallel.

The admin "Generate lessons now" button has no debounce and no server-side
check for an existing in-flight batch. Two clicks (or one click plus a
background refill trigger) for the same student scheduled two independent jobs,
both of which:

1. Loaded the student's active `LearningPath` (tracked entity, `xmin`
   concurrency token per `LinguaCoachDbContext.cs:56-61`).
2. Both ran `EnsureGeneratedModuleAsync` → both queried/created the "Generated
   Lessons" `LearningModule` and computed `NextCourseSequenceAsync` from the
   same starting point.
3. The first job to `SaveChangesAsync` won; the second job's tracked
   `LearningPath`/related entities now had a stale `xmin`, so its next
   `SaveChangesAsync` (line 171, inserting the first `LearningSession`) threw
   `DbUpdateConcurrencyException`.

The exception was unhandled inside `Execute`, so the batch was left in
`Running` status forever — explaining the two stuck "Running, 0/4" rows in
"Recent batches".

## Fix implemented

`src/LinguaCoach.Api/Controllers/AdminGenerationController.cs`:

- `GenerateLessons` now checks for an existing `GenerationBatch` for the same
  `StudentProfileId` with status `Queued` or `Running` before scheduling a new
  one, returning `409 Conflict` if found. Prevents the duplicate-trigger race
  at the source.

`src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`:

- Extracted the session-materialization loop into `MaterializeSessionsAsync`,
  wrapped in a `try/catch` in `Execute`. Any unhandled exception during
  materialization (including `DbUpdateConcurrencyException` from a future
  race, or any other unexpected error) now calls `batch.MarkFailed(...)` and
  saves, instead of leaving the batch stuck in `Running`. This is a safety net
  independent of the 409 guard — defends against any other source of
  concurrent/duplicate triggers (e.g. background refill + manual admin
  overlapping).

## Tests

- `dotnet build -c Release`: succeeded, 0 errors.
- `dotnet test -c Release` (full suite): 482/482 unit, 430/430 integration
  passing.
- No new tests added: reproducing the Quartz/Postgres concurrency race requires
  a real `AdoJobStore` + concurrent execution, not practical in the current
  in-memory/SQLite test setup. Flagged as a follow-up (same gap noted in the
  prior Quartz JobDataMap review).

## Decisions made

- Guard at the admin endpoint (409 Conflict) rather than inside the job itself,
  since the job has no cheap way to "cancel" a duplicate once Quartz has
  already scheduled it — better to prevent the second schedule entirely.
- Keep the `try/catch` safety net in the job regardless, since the background
  refill pipeline can also trigger batches without going through the admin
  endpoint's guard.

## Data cleanup needed

The two existing stuck `GenerationBatch` rows (status `Running`, `0/4`) for
student `2283fcb4-b1ea-4cb4-9322-287083456805` will now block the new 409 guard
from ever allowing generation for that student again, since they'll never
transition out of `Running`. These need to be manually marked `Failed` (or
deleted) in production via a one-off SQL update:

```sql
UPDATE "GenerationBatches"
SET "Status" = 'Failed'
WHERE "StudentProfileId" = '2283fcb4-b1ea-4cb4-9322-287083456805'
  AND "Status" = 'Running';
```

(Run by an operator with prod DB access — not executed as part of this change.)

## Risks / unresolved questions

- Same test-coverage gap as the prior Quartz review: no integration test
  exercises real concurrent job execution against `AdoJobStore`.
- The 409 guard only covers the admin endpoint. If the background buffer-refill
  job can also trigger `LessonBatchGenerationJob.TriggerAsync` for a student
  that already has a `Running` batch (e.g. retry logic), that path isn't
  covered by this guard — but is covered by the `try/catch` safety net so it
  can no longer get stuck.

## Final verdict

Root cause fixed: duplicate concurrent batch triggers for the same student are
now rejected (409) at the admin endpoint, and any batch that still fails
mid-materialization is marked `Failed` instead of stuck `Running` forever.
Existing stuck rows for the affected student need a manual data fix (see
above) before "Generate lessons now" will work for that student again.

## Next recommended action

1. Run the SQL data-fix above for student
   `2283fcb4-b1ea-4cb4-9322-287083456805`.
2. Retry "Generate lessons now" for that student and confirm a single batch
   completes successfully end-to-end (including the follow-up
   `ActivityMaterializationJob`).
