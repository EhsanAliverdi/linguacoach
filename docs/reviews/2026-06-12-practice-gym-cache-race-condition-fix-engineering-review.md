---
status: current
lastUpdated: 2026-06-12 00:00
owner: engineering
supersedes:
supersededBy:
---

# Practice Gym cache race condition fix — engineering review

Date: 2026-06-12
Related sprint: docs/sprints/current-sprint.md (Practice Gym caching, background generation)

## Files reviewed

- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`
- `src/LinguaCoach.Domain/Entities/PracticeActivityCache.cs`
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs`
- `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs`

## Background

A full codebase audit (2026-06-12) verified all 7 claims in `current-sprint.md` against
the code and found them accurate. During that audit, a medium-severity race condition
was found in Practice Gym cache assignment.

## Findings

`ActivityGetHandler.TryAssignReadyPracticeCacheAsync` selected the oldest `Ready`
`PracticeActivityCache` row for a student+pattern, then called `cache.MarkAssigned()`
and `SaveChangesAsync`. `PracticeActivityCache` had no concurrency token (unlike most
other entities, which get an `xmin` shadow property via `LinguaCoachDbContext.OnModelCreating`
— previously only applied to `LearningPath`).

Two concurrent `GET /api/activity/next?pattern=...` requests for the same student and
pattern (e.g. duplicate frontend requests, retry-on-timeout, multiple tabs) could both
read the same `Ready` cache row, both mark it `Assigned`, and both return the same
`LearningActivity` to two different responses — last write wins, no error, silent
duplicate assignment.

## Fix

1. Added `xmin` (Postgres system column) as an EF concurrency token for
   `PracticeActivityCache`, mirroring the existing `LearningPath` configuration in
   `LinguaCoachDbContext.OnModelCreating` (Npgsql-only, gated by provider check).
   Added the corresponding shadow property to `LinguaCoachDbContextModelSnapshot.cs`.
   No migration required — `xmin` is a Postgres system column, not a real schema change
   (same pattern as the existing `LearningPath` xmin token).

2. `TryAssignReadyPracticeCacheAsync` now catches `DbUpdateConcurrencyException` on the
   `MarkAssigned()` save. On conflict, it detaches the stale entity, excludes that cache
   row, and retries against the next-oldest `Ready` row for the same student+pattern.
   If no rows remain, it returns `null` and the caller falls back to on-demand AI
   generation — the existing, already-tested fallback path.

## Decisions

- Retry-with-exclusion rather than returning an error: a concurrency conflict here is
  not a user-facing error condition — it just means another request got the row first.
  Falling through to the next ready row (or to generation) keeps the response path
  working without a 409/500.

## Tests

- `dotnet test tests/LinguaCoach.UnitTests`: 483 passed
- `dotnet test tests/LinguaCoach.IntegrationTests`: 434 passed (98 of these are
  Practice/Activity-focused)
- No new test added for the concurrency race itself — simulating true concurrent
  `SaveChangesAsync` conflicts requires a real Postgres backend (xmin is Npgsql-only;
  the integration test provider does not exercise this path). The fix is defensive and
  was verified not to regress existing single-request behaviour.

## Risks / unresolved questions

- The concurrency guard only activates against Postgres (`Database.ProviderName`
  contains "Npgsql"). Test providers (SQLite/InMemory) do not exercise the conflict
  path, so this fix is unverified by automated tests against a real concurrent
  scenario. Recommend a manual/staging check if this becomes a recurring issue.

## Final verdict

Fix applied, scoped, all existing tests pass. Closes the medium-severity race condition
identified in the 2026-06-12 code/docs audit.

## Next recommended action

None required immediately. If duplicate Practice Gym activity assignments are observed
in production logs, revisit with a Postgres-backed concurrency test.
