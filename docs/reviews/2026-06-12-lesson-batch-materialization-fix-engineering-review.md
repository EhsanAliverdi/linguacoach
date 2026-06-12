---
status: current
lastUpdated: 2026-06-12 12:53
owner: engineering
supersedes:
supersededBy:
---

# Lesson Batch Materialization Fix - Engineering Review

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md`
Triggered by: production logs showing successful `lesson_batch_plan` AI output,
followed by `LessonBatchGenerationJob: materialization failed` for student
`2283fcb4-b1ea-4cb4-9322-287083456805`.

## Symptom

Admin "Generate lessons now" returned `202 Accepted`, and the AI provider call
completed successfully. The batch then failed while saving the first generated
session:

```text
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
The database operation was expected to affect 1 row(s), but actually affected 0 row(s)
```

The error handler attempted to mark the batch failed using the same `DbContext`.
Because the failed save left stale tracked entries in the context, that failure
handler could throw too. Quartz then logged an unhandled job exception, and the
admin Recent batches view could remain misleading.

## Root Cause

The job created later `GenerationJobItem` rows by calling
`GenerationBatch.AddItem(...)` on an already-saved aggregate. That method appends
to a private backing collection. In this path EF did not reliably treat the new
session/activity items as inserts. During the next `SaveChangesAsync`, EF tried
to update a `GenerationJobItem` row that did not exist and raised
`DbUpdateConcurrencyException`.

There was a second risk in the same materialization path. The generated module
lookup loaded path/module entities into the same job context. The production
database uses an `xmin` concurrency token for `LearningPath`, so materialization
should avoid unnecessary tracked path state while creating new sessions.

## Fix

`src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`:

- explicitly adds generated session and activity `GenerationJobItem` rows through
  `_db.GenerationJobItems.Add(...)`
- resolves the generated module as an ID using `AsNoTracking()` path/module
  reads
- detaches newly-created path/module entities after their own save
- clears the failed change tracker before reloading and marking a batch failed
  after materialization errors

This keeps the successful path insert-only for new job items and keeps the
failure path from throwing a second exception.

## Test Coverage

Added `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs`.

The test executes `LessonBatchGenerationJob` directly with:

- a real integration-test `LinguaCoachDbContext`
- a fake lesson-plan AI provider returning two valid session plans
- an in-memory Quartz scheduler

It verifies:

- the generation batch completes
- two ready `LearningSession` rows are created
- course sequence numbers are assigned
- session exercises are persisted

Targeted validation:

```text
dotnet test tests\LinguaCoach.IntegrationTests\LinguaCoach.IntegrationTests.csproj --filter "LessonBatchGenerationJobTests"
Passed: 1
```

## Residual Risk

This does not prove the production scheduler, provider credentials, or deployed
database are healthy. It proves the job can now turn valid AI lesson-plan JSON
into ready sessions without the EF tracking failure shown in production.

After deploy, verify `/admin/integrations` for the affected student:

1. Cancel any old running batch.
2. Click "Generate lessons now".
3. Confirm the new batch reaches `Completed`.
4. Confirm ready lesson buffer count is non-zero.
