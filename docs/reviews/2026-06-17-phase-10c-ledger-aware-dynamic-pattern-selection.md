---
status: current
lastUpdated: 2026-06-17 00:00
owner: engineering
sprint: Phase 10C
---

# Phase 10C — Ledger-Aware Dynamic Pattern Selection

**Date:** 2026-06-17
**Sprint:** Phase 10C
**Related phases:** 10A (DynamicPatternSelector), 10B (StudentLearningEvent ledger)

---

## Summary

Wires the Phase 10B `IStudentLearningLedger` into the dynamic pattern selection system
so automatic session pattern selection uses real student learning history, not only
in-session exercise history and skill profile scores.

---

## Files Changed

| File | Change |
|---|---|
| `src/LinguaCoach.Application/Sessions/DynamicPatternSelection.cs` | Added `LedgerSignals` record; added `Ledger` optional field to `PatternSelectionInput` |
| `src/LinguaCoach.Infrastructure/Sessions/DynamicPatternSelector.cs` | Updated `Score()` with ledger-aware terms; updated `BuildReason()` for ledger annotations |
| `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs` | Injected `IStudentLearningLedger`; added `BuildLedgerSignalsAsync()`; passed signals into `ApplyDynamicPatternSelection()` |
| `tests/LinguaCoach.UnitTests/Sessions/DynamicPatternSelectorTests.cs` | 10 new unit tests covering ledger signal paths |
| `tests/LinguaCoach.IntegrationTests/Sessions/SessionGeneratorServiceTests.cs` | Updated constructor to inject real `StudentLearningLedgerService`; added 3 integration tests |

---

## Ledger Methods Used

| Method | Purpose |
|---|---|
| `GetRecentPatternKeysAsync(studentProfileId, limit: 20)` | Repetition avoidance — replaces ad-hoc `SessionExercise` history for this signal |
| `GetWeakEventsAsync(studentProfileId, limit: 20)` | Identifies `NeedsReview` / `Failed` patterns to boost |
| `GetRecentAsync(studentProfileId, limit: 20)` | Extracts mastered patterns and ledger goal context |

---

## PatternSelectionInput Changes

Added one optional field:

```csharp
LedgerSignals? Ledger = null
```

`LedgerSignals` is a new record:

```csharp
public sealed record LedgerSignals(
    IReadOnlyList<string> RecentPatternKeys,   // newest-first; repetition avoidance
    IReadOnlyList<string> WeakPatternKeys,      // NeedsReview/Failed; boost candidates
    IReadOnlyList<string> MasteredPatternKeys,  // soft deprioritisation
    string? LedgerGoalContext);                 // fallback goal context
```

All existing `PatternSelectionInput` construction sites are unchanged because `Ledger`
is an optional parameter with default `null`.

---

## Scoring / Ranking Rules

### Retained from 10A

| Signal | Points |
|---|---|
| Primary skill matches weakest skill | +20 |
| Not in recent session history | +10 |
| In session history but not in last 3 | +5 |

### Added in 10C (all zero when `Ledger` is null)

| Signal | Points | Rationale |
|---|---|---|
| Pattern in `WeakPatternKeys` (NeedsReview/Failed) | +15 | Surface patterns needing review |
| Pattern in `RecentPatternKeys` last 3 | -8 | Avoid immediate over-repetition from ledger |
| Pattern in `MasteredPatternKeys` | -5 | Soft deprioritisation; does not exclude |

Score range: approximately -13 to +45.

### Tiebreak

Unchanged: stable alphabetical (Ordinal) so tests remain deterministic.

---

## Fallback Behaviour

1. **No ledger data at all** (`Ledger = null`): selector behaves identically to 10A. All ledger score terms are 0.
2. **Ledger exists but empty** (`RecentPatternKeys = []`, etc.): same as no-ledger — all terms evaluate to 0.
3. **`BuildLedgerSignalsAsync` throws**: caught; logs a warning; returns `null`; session generation continues with `Ledger = null`.
4. **Single candidate**: selected regardless of ledger penalty (mastered soft penalty never triggers hard exclusion).

---

## Explicit Override Preservation

`pattern=`, `exerciseType=`, and `type=` query parameters are handled upstream in
`ActivityController` / `SessionController` before `GetOrCreateTodaysSessionAsync` is
called. They bypass `ApplyDynamicPatternSelection` entirely. This phase does not touch
that path — overrides remain fully preserved.

Additionally, `Review` slots in `ApplyDynamicPatternSelection` are skipped unconditionally
(same as 10A):

```csharp
if (step.Kind == ExerciseKind.Review) { steps.Add(step); continue; }
```

---

## Catalog Gate

Unchanged from 10A:

```csharp
catalog.Where(e => e.IsEnabled && e.IsReady && e.SupportsTodayLesson)
```

Ledger signals only affect scoring among already-approved candidates. They cannot
promote a disabled or non-ready pattern into eligibility.

---

## Workplace-Only Assumption Avoidance

- `LedgerGoalContext` is read from the most recent ledger event that had a goal context set.
- It never defaults to `"workplace"` or any other value.
- It is used as a fallback only when the profile-level `LearningGoalContext` is null.
- Profile goal always takes precedence over ledger goal.
- `BuildReason()` emits `goal-context='...'` only when a non-null, non-whitespace goal is present.

---

## Known Limitations

1. **LedgerSignals are fetched fresh per session generation.** There is no caching.
   For high-frequency generation this adds 3 DB round-trips. Acceptable for current scale.

2. **`GetRecentPatternKeysAsync` and the ad-hoc `SessionExercise` query both run.**
   The ledger keys are passed as `Ledger.RecentPatternKeys`; the session-exercise keys
   continue to populate `RecentPatternKeys` on the input. Both influence the score.
   Future phase can consolidate once the ledger has sufficient history depth.

3. **Mastered deprioritisation is a soft penalty (-5) only.** It does not exclude mastered
   patterns. If all candidates are mastered the selector still picks the best-scoring one.

4. **`LearningGoalContext` is not yet back-populated from student profile fields.**
   The field exists on `StudentLearningEvent` but currently relies on the caller (activity
   submit path) to pass it. Population from the profile at session-generation time is
   intentionally deferred to avoid scope creep.

5. **No readiness pool lifecycle (`ready`, `queued`, `generating`, `reserved`, etc.).**
   Out of scope for this phase per the spec. Signal design is compatible with future
   addition of these states.

---

## Recommendation for Next Phase

**Phase 10D options (in priority order):**

1. **Consolidate repetition signals** — replace the ad-hoc `SessionExercise` history
   query with `Ledger.RecentPatternKeys` as the single source of truth, once ledger
   coverage is sufficient.

2. **Populate `LearningGoalContext` at ledger-write time** — thread the student's
   `LearningGoalDescription` through `ActivitySubmitHandler` so goal context accumulates
   automatically in the ledger.

3. **Readiness pool lifecycle** — implement `ready / queued / generating / reserved /
   consumed / expired / failed / stale / review_only` states so prepared activities can
   be validated against current mastery before being shown.

4. **CEFR-aware routing** — use `CefrLevelAtEvent` from the ledger to constrain
   candidate patterns by `MinCefrLevel` / `MaxCefrLevel` once catalog entries carry
   those fields.

---

## Test Results

| Suite | Before | After |
|---|---|---|
| Unit | 941 | 951 (+10) |
| Integration | 531 | 534 (+3) |
| Architecture | 3 | 3 |

All passed. Build: 0 errors, pre-existing warnings only.

---

## Decisions Made

- `LedgerSignals` is a plain record on `PatternSelectionInput`, not a separate service call inside the selector. Keeps the selector pure (no async, no DI).
- `Ledger` is optional (`= null`) on `PatternSelectionInput` to preserve all existing call sites without modification.
- Mastered penalty is soft (-5) not a hard gate, to avoid pathological cases where all candidates are mastered.
- Ledger fetch errors never block session generation (best-effort pattern, same as memory snapshot).
- Profile `LearningGoalContext` always takes precedence over `LedgerSignals.LedgerGoalContext`.

---

## AskUserQuestion Answers

None required. Scope was fully specified in the phase brief.

---

## Risks / Unresolved Questions

- If ledger events are written with no `PatternKey` (nullable), they are silently excluded from all signal lists. This is correct behaviour but means early ledger events (before pattern tracking was solid) contribute nothing.
- The `-8` last-3-ledger penalty and `+15` weak-boost values are reasonable starting points. They may need tuning once real student data accumulates.
