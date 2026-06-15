---
status: resolved
lastUpdated: 2026-06-15
owner: architecture
relatedSprint: docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md
relatedArchitecture: docs/architecture/learning-activity-engine.md#practice-gym-pre-generation-pool-foundation
---

# Practice Gym Pre-generation Pool — Foundation Architecture Decision

## Date

2026-06-15

## Related sprint / feature

`docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md`,
item 3 of "Required follow-up architecture" (Practice Gym pre-generation pool).
Scoped as a small, safe foundation PR only.

## Files reviewed

- `src/LinguaCoach.Domain/Enums/PracticeCacheStatus.cs`
- `src/LinguaCoach.Domain/Entities/PracticeActivityCache.cs`
- `src/LinguaCoach.Application/Activity/IExerciseTypeRegistry.cs`
- `src/LinguaCoach.Application/Activity/IGetNextActivityHandler.cs` /
  `ActivityGetHandler` (`TryAssignReadyPracticeCacheAsync`,
  `HandleExerciseTypeKeyedAsync`, `HandlePatternKeyedAsync`)
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs` /
  `PracticeGymBufferRefillJob.cs`
- `src/LinguaCoach.Api/Controllers/ActivityController.cs`

## Findings / options considered

### New `PracticeGymPoolItem` entity vs. extending `PracticeActivityCache`

- A new entity would require a new migration, a new table, and duplicate the
  reservation/expiry logic already implemented in
  `ActivityGetHandler.TryAssignReadyPracticeCacheAsync`.
- `PracticeActivityCache` already models exactly what the pool needs: a
  per-student, per-pattern, status-tracked link to a generated
  `LearningActivity`, with `Ready/Assigned/Completed/Expired` states and
  expiry. `PatternKey` already doubles as the exercise type key for
  pattern-backed exercise types.
- `PracticeGymGenerationJob` / `PracticeGymBufferRefillJob` already populate
  this table using the same `IExerciseTypeRegistry` eligibility rules
  (`IsEnabled && ImplementationStatus == "ready" && SupportsPracticeGym`).

### Reservation state: new `Reserved` status vs. reuse `Assigned`

- `Assigned` already means "claimed by a request, not yet completed" in the
  existing on-demand path. Introducing a separate `Reserved` status would
  create two semantically overlapping states and require both
  `ActivityGetHandler` and the new pool service to check both statuses
  everywhere to avoid double-serving.
- Decision: reuse `Assigned` as the reservation state for pool items too.

## Decisions made

1. No new entity/table/migration. `IPracticeGymPoolService` is a thin lookup
   layer over `PracticeActivityCache`, implemented in
   `PracticeGymPoolService` (Infrastructure).
2. `PracticeCacheStatus.Failed = 5` added (additive `int` enum value, no
   schema migration needed — `Status` is stored via `HasConversion<int>`).
   `PracticeActivityCache.MarkFailed()` added alongside existing
   `MarkAssigned/MarkCompleted/MarkExpired`.
3. `ReserveReadyItemAsync` (private to `PracticeGymPoolService`) mirrors
   `TryAssignReadyPracticeCacheAsync`: selects the oldest non-expired `Ready`
   row for `(StudentProfileId, PatternKey)` with a linked active
   `LearningActivity`, marks it `Assigned`, with a
   `DbUpdateConcurrencyException` retry loop excluding already-tried rows.
4. Both `FindReadyForExerciseTypeAsync` and `FindReadyForSkillAsync` resolve
   eligibility via `IExerciseTypeRegistry` first
   (`GetByKeyAsync` / `GetEligibleExerciseTypesForSkillAsync` with
   `ExerciseTypeSupportContext.PracticeGym`), so disabled or `planned`
   exercise types are never returned from the pool.
5. New endpoint `GET /api/activity/practice-gym/next` is additive. Existing
   `/api/activity/next` (`exerciseType=`, `type=`, `pattern=`) is unchanged
   and remains the underlying on-demand/fallback mechanism.

## Concurrency safety argument

Because `ReserveReadyItemAsync` and `TryAssignReadyPracticeCacheAsync` both
mark the row `Assigned` before returning it (the former via
`PracticeGymPoolService`, the latter via `ActivityGetHandler`'s existing
on-demand path), a given `PracticeActivityCache` row can never be served to
two requests:

- If `PracticeGymPoolService` reserves the only `Ready` row for a
  `(StudentProfileId, PatternKey)`, the subsequent on-demand fallback
  (`HandleExerciseTypeKeyedAsync` → `HandlePatternKeyedAsync` →
  `TryAssignReadyPracticeCacheAsync`) finds no `Ready` rows and proceeds to AI
  generation — correct and safe, just an extra generation call in the rare
  race case.

## Admin-disable behaviour (requirement 7)

Satisfied implicitly via `IExerciseTypeRegistry` filtering — no separate
expiry of existing `Ready`/`Assigned` rows for newly-disabled types was added.
If an exercise type is disabled after pool rows already exist for it, those
rows simply stop being selectable (registry lookup returns null /excludes the
type from skill eligibility); they will eventually expire via the existing
`ExpiresAtUtc` mechanism. This was a deliberate scope decision — actively
expiring rows on disable was considered unnecessary complexity for a
foundation PR.

## Implementation tasks produced

See `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md`,
"PR — Practice Gym pre-generation pool foundation (Phase 4), completed".

## Risks / unresolved questions

- The pool currently only serves pattern-backed exercise types (those with a
  non-null `ExercisePatternKey`), since `PracticeActivityCache.PatternKey` is
  the lookup key. Non-pattern legacy `ActivityType`s (e.g. plain
  `VocabularyPractice` without a pattern key) are not pool-eligible — this
  matches current `PracticeGymGenerationJob` behaviour, so no regression, but
  is a constraint future work should be aware of.
- No new background fill logic was added; pool hit rate depends entirely on
  existing `PracticeGymGenerationJob` / `PracticeGymBufferRefillJob` cadence.

## Final verdict

Approved and implemented as the foundation PR. Matches "keep it small and
safe" scope: reuses existing primitives, additive-only schema change, no
changes to Today, MinIO/audio lifecycle, or PTE renderers/evaluators.

## Next recommended action

Future Practice Gym pool work should focus on: (a) background pool-fill
tuning so `source: "pool"` hit rate is high enough to matter, and (b)
extending pool eligibility to non-pattern-backed exercise types if/when those
are migrated to `module_stage_v1` (see sprint doc item 1).
