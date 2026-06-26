# Phase 10Z — Mastery Re-evaluation Engine: Engineering Review

**Date:** 2026-06-26
**Sprint:** Phase 10Z
**Author:** Claude Code (Sonnet 4.6)

---

## Title

Phase 10Z Mastery Re-evaluation Engine — Engineering Review

---

## Related sprint or feature

Phase 10Z — Mastery Re-evaluation Engine
Follows: Phase 10Y (Learning Activity Lifecycle Completion)

---

## Files reviewed / created

### New files

| File | Layer | Purpose |
|------|-------|---------|
| `src/LinguaCoach.Domain/Enums/MasteryStatus.cs` | Domain | 5-value mastery classification enum |
| `src/LinguaCoach.Domain/Enums/ReadinessDemotionDecision.cs` | Domain | 6-value demotion decision enum |
| `src/LinguaCoach.Application/Mastery/ObjectiveMasterySignal.cs` | Application | Computed mastery signal record |
| `src/LinguaCoach.Application/Mastery/MasteryEvaluationReason.cs` | Application | Trigger reason enum |
| `src/LinguaCoach.Application/Mastery/StudentMasteryReport.cs` | Application | Full mastery report record |
| `src/LinguaCoach.Application/Mastery/IStudentMasteryEvaluationService.cs` | Application | Service interface |
| `src/LinguaCoach.Application/Mastery/MasteryOptions.cs` | Application | Configuration POCO bound from `"Mastery"` section |
| `src/LinguaCoach.Infrastructure/Mastery/StudentMasteryEvaluationService.cs` | Infrastructure | Full deterministic implementation |
| `src/LinguaCoach.Infrastructure/Jobs/StudentMasteryEvaluationJob.cs` | Infrastructure | Daily Quartz job |
| `tests/LinguaCoach.UnitTests/Mastery/StudentMasteryEvaluationServiceTests.cs` | Tests | 11 unit tests + FakeLedger |

### Modified files

| File | Change |
|------|--------|
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered `IStudentMasteryEvaluationService`, `MasteryOptions`, `StudentMasteryEvaluationJob` |
| `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` | Added daily Quartz trigger for `StudentMasteryEvaluationJob` |
| `src/LinguaCoach.Api/appsettings.json` | Added `"Mastery"` config section with default thresholds |
| `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs` | Added `MasteredCount`, `NeedsReviewCount`, `LastEvaluatedAtUtc` to `ReadinessPoolSummary` |
| `docs/sprints/current-sprint.md` | Added Phase 10Z entry |
| `docs/handoffs/current-product-state.md` | Added mastery engine to product state |

---

## Findings grouped by priority

### P0 — Resolved during implementation

**Sorting stability in tests**: `GetRecentAsync` returns events newest-first per ledger contract. The service originally called `.OrderByDescending(e => e.OccurredAtUtc)` after filtering, which was unstable when test events had identical `OccurredAtUtc` timestamps. Fixed by trusting ledger ordering and removing the redundant sort in `EvaluateObjectiveMasteryAsync`.

**`IReadOnlyList.IndexOf` does not exist**: Initial implementation called `.IndexOf()` on `CefrLevelConstants.All` (an `IReadOnlyList<string>`). Fixed by calling `.ToList().IndexOf()`.

**Terminal state demotion**: `EnsureStatus` throws if lifecycle transitions are called on terminal items. `ApplyDecision` now guards all transitions with appropriate status checks before calling domain methods.

### P1 — Design notes

**Objective key proxy**: `StudentLearningEvent` has `PatternKey` and `PrimarySkill` but no dedicated `CurriculumObjectiveKey` field. The implementation uses `PatternKey` as the objective key proxy (via a file-local extension method), falling back to `PrimarySkill`. This is accurate for practice gym events. Future phases may add a dedicated objective key field to the event if finer-grained grouping is needed.

**Consecutive run logic**: The consecutive success/failure counting iterates events in ledger order (newest first) and breaks on the first event that does not continue the current run. This is intentional and matches the spec — it reflects the student's most recent performance trajectory.

**`EvaluateStudentAsync` demoted count**: The report's `DemotedCount` reflects the total items changed by `EvaluateAndDemoteReadinessItemsAsync`. The `SkippedCount` and `MarkedReviewOnlyCount` fields in `StudentMasteryReport` are set to 0 at report level (granular per-item breakdown is logged, not aggregated). A future phase can add per-decision counters if the admin UI needs them.

### P2 — Remaining gaps / future work

- `StudentMasteryReport.SkippedCount` and `MarkedReviewOnlyCount` are always 0 in the current implementation. The individual decisions are applied and logged but not counted separately at the report level. Tracked for future admin visibility.
- No `LastMasteryEvaluatedAtUtc` field on `StudentProfile` — the job re-evaluates all students with any learning events on every run. A future optimisation would track the last evaluation timestamp per student and skip recently-evaluated ones. This is safe for now given the daily cadence.
- Admin UI for mastery signal is out of scope for Phase 10Z. The `ReadinessPoolSummary` DTO has `MasteredCount`/`NeedsReviewCount` fields added but the admin controller does not yet populate them (pending a future phase that wires mastery queries into pool health responses).

---

## Decisions made

1. **No migration needed.** Mastery is computed on-demand from existing `StudentLearningEvent` data. No new DB columns.
2. **No AI calls.** All classification is rule-based with configurable thresholds.
3. **Trust ledger ordering.** `IStudentLearningLedger.GetRecentAsync` guarantees newest-first; the service does not re-sort.
4. **PatternKey as objective key proxy.** Until `StudentLearningEvent` has a dedicated curriculum objective field, `PatternKey` is the best available proxy.
5. **Job registered as Quartz `IJob`.** Follows the established pattern in `QuartzConfiguration.cs` (daily cadence, same as `AudioCleanupJob`).
6. **Hand-rolled `FakeLedger` in tests.** No mocking library is present in the unit test project; the fake implements `IStudentLearningLedger` directly.

---

## AskUserQuestion answers

None — all design decisions were resolved from the spec and existing codebase patterns.

---

## Implementation tasks produced

None. Phase 10Z is complete.

Suggested follow-up phases:
- Phase 10Z-B: Populate `MasteredCount`/`NeedsReviewCount` in admin pool health endpoint.
- Phase 10Z-C: Add `LastMasteryEvaluatedAtUtc` to `StudentProfile` to enable incremental sweeps.
- Phase 10Z-D: Wire mastery signals into `SessionGeneratorService` to bias lesson content toward weak/at-risk skills.

---

## Risks or unresolved questions

- If a student has very few `StudentLearningEvent` rows (< 3 per skill), most skills will remain `InsufficientEvidence`. This is correct behaviour and by design; the pool demotion sweep will do nothing for those students until evidence accumulates.
- The daily Quartz job evaluates all students with any events. At scale (thousands of students), this may need batching or the `LastMasteryEvaluatedAtUtc` optimisation.

---

## Final verdict

Phase 10Z implemented cleanly. Build passes. All 1329 unit tests pass (11 new mastery tests, 0 regressions). No migration needed. No UI changes. Scope fully satisfied.

---

## Next recommended action

Run integration tests to confirm EF/SQLite mappings are not broken by DTO additions. Then proceed to Phase 10Z-B (populate mastery counts in admin pool health endpoint) or whichever phase is next in the backlog.
