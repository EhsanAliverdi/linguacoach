---
status: current
lastUpdated: 2026-06-26
owner: engineering
sprint: Phase 10Y — Learning Activity Lifecycle Completion
---

# Phase 10Y — Learning Activity Lifecycle Completion
## Engineering Review & Implementation Record

**Date:** 2026-06-26
**Related sprint:** Phase 10Y
**Files reviewed:** StudentActivityReadinessItem.cs, ReadinessPoolStatus.cs, ReadinessPoolReplenishmentService.cs, StudentActivityReadinessPoolService.cs, PracticeGymSuggestionService.cs, ActivitySubmitHandler.cs, PoolHealthSummary.cs, ReadinessPoolDtos.cs, IStudentActivityReadinessPoolService.cs, AdminReadinessPoolController.cs, EF configuration and migration.

---

## Part A — Inventory Findings

The lifecycle model was already substantially implemented from Phases 10M–10O. The following were confirmed as **already production-correct**:

| Concern | Status |
|---|---|
| Status enum: Queued/Generating/Ready/Reserved/Consumed/Expired/Failed/Stale/ReviewOnly | All 9 present |
| Domain lifecycle methods: MarkGenerating, MarkReady, MarkFailed, Reserve, MarkConsumed, Expire, MarkStale, MarkReviewOnly | All present |
| Timestamps: ReservedAt, ConsumedAt, ExpiresAt, StaleAt, UpdatedAt | All present |
| Reservation with optimistic concurrency | Implemented in StudentActivityReadinessPoolService (3-attempt retry loop) |
| Consumption on completion | TryConsumeReadinessItemAsync in ActivitySubmitHandler — present for all activity paths |
| Age-based expiry sweep | SweepExpiredItemsAsync in ReadinessPoolReplenishmentService |
| Reserved-item timeout sweep | SweepExpiredItemsAsync covers reserved past ReservedItemExpiryHours |
| Orphan generating recovery | RecoverOrphanedGeneratingAsync |
| Failed item retry | RetryFailedItemsAsync |
| Duplicate prevention | existingKeySet in FillShortfallAsync |
| Practice Gym suggestions: lifecycle-aware filtering | Excludes Consumed/Expired/Failed/Stale/Queued/Generating |
| StartSuggestion → reserve | StartSuggestionAsync with idempotent already-reserved path |
| TryMarkConsumed | Implemented; called from ActivitySubmitHandler.TryConsumeReadinessItemAsync |
| Pool health counts (all statuses) | GetPoolSummaryAsync and GetHealthAsync both cover all statuses |
| Tests: unit + integration | PracticeGymSuggestionServiceTests, ReplenishmentIntegrationTests, ReadinessPoolIntegrationTests, ReadinessConsumptionWiringTests, StudentActivityReadinessItemTests |

**Key architectural finding:** The Today lesson system (`SessionGeneratorService`) uses a **separate lesson buffer** (`LearningSession` pre-generation via `LessonBatchGenerationJob`), not `StudentActivityReadinessItems` directly. The `TodayLesson` source in the readiness pool feeds the upstream content pipeline — not the session generator. Session generator reads from `FindNextReadyBufferedSessionAsync`. This is correct by design: readiness pool is the pre-materialization queue; lesson buffer is the post-materialization ready-to-serve queue.

---

## Real Gaps Identified and Addressed

### 1. `Skipped` status — Added

The phase brief required a `Skipped` terminal state (mastered or irrelevant, not even useful for review). The existing model used `Stale`/`ReviewOnly` for overlapping cases but lacked a true "skip permanently — done with this" terminal state.

**Implementation:**
- `ReadinessPoolStatus.Skipped = 9` added to enum
- `MarkSkipped(reason)` added to `StudentActivityReadinessItem` domain entity
  - Valid from: Ready, Reserved, ReviewOnly (terminal states Consumed/Expired/Skipped throw)
- `MarkSkippedAsync` added to `IStudentActivityReadinessPoolService` interface and `StudentActivityReadinessPoolService` implementation
- `Skipped` excluded from `PracticeGymSuggestionService` query (alongside Consumed/Expired/Failed/Stale)
- `IsServableAsNormalContent` and `IsServableAsReview` return false for Skipped items

### 2. CEFR mismatch stale demotion — Added

The replenishment service only swept age-based expiry. It did not detect when a student's CEFR level had advanced past an item's target level, leaving stale B1 content visible to B2+ students.

**Implementation:** `SweepCefrMismatchedItemsAsync` added to `ReadinessPoolReplenishmentService`:
- Runs after age-based expiry sweep, before orphan recovery
- Loads all active students with known CEFR level
- Finds all Ready/Reserved Normal-routing items for those students
- Calls `IsBelowCurrentLevel(itemCefr, studentCefr)` — CEFR rank comparison (A1 < A2 < B1 < B2 < C1 < C2)
- If item target is strictly below current student level: calls `item.MarkStale(reason)` + `item.RecordEvaluation()`
- Non-matching items still get `RecordEvaluation()` to stamp `LastEvaluatedAtUtc`
- Review/scaffold/remediation lower-level items are intentionally excluded (routing reason check)
- Saves once per batch; logs each demotion at Information level
- `totalStale` counter wired into `ReplenishmentRunSummary.ItemsMarkedStale`

### 3. `ReservedCount` and `SkippedCount` in pool health — Added

`PoolHealthSummary` was missing `ReservedCount` (items actively in use) and `SkippedCount`. Both are now surfaced for admin diagnostics.

**Changes:**
- `PoolHealthSummary`: added `ReservedCount` and `SkippedCount` properties
- `GetHealthAsync` in `ReadinessPoolReplenishmentService`: populates both from DB group-by
- `ReadinessPoolSummary` DTO: added `SkippedCount`
- `GetPoolSummaryAsync`: populates `SkippedCount`
- Admin health endpoint (`AdminReadinessPoolController`): exposes `reservedCount` and `skippedCount` in both `todayLesson` and `practiceGym` response objects

### 4. `LastEvaluatedAtUtc` timestamp — Added

The phase brief asked for a timestamp recording when an item was last checked for staleness. This enables future incremental evaluation (only re-check items not evaluated recently).

**Changes:**
- `StudentActivityReadinessItem.LastEvaluatedAtUtc` nullable `DateTime?` property added
- `RecordEvaluation()` method added — stamps `LastEvaluatedAtUtc` without changing lifecycle state
- Called by `SweepCefrMismatchedItemsAsync` on every evaluated item
- EF configuration: `last_evaluated_at_utc` nullable column added to `StudentActivityReadinessItemConfiguration`
- `ReadinessItemDto.LastEvaluatedAtUtc` added to admin pool summary DTO

### 5. EF Migration — T60

Migration `20260626065357_T60_ReadinessLifecycleSkippedAndLastEvaluated` adds:
- `last_evaluated_at_utc timestamp with time zone nullable` to `student_activity_readiness_items`

No migration needed for `Skipped` status — already stored as string via `HasConversion<string>()`.

Existing rows default `last_evaluated_at_utc` to null — safe. Next replenishment run will stamp them.

---

## Parts B–J Assessment (Already Implemented)

| Part | Assessment |
|---|---|
| B: Status enum | Complete. 9 states present + Skipped added = 10 total |
| C: Reservation | Production-correct. Optimistic concurrency, idempotent, 3-attempt retry |
| D: Consumption | Production-correct. TryConsumeReadinessItemAsync in all 3 activity paths |
| E: Expiry/stale job | Age-based expiry and reserved timeout — implemented. CEFR mismatch added this phase |
| F: ReviewOnly/Skipped | ReviewOnly implemented. Skipped added this phase. Mastery-based auto-demotion deferred (requires mastery engine — see Gaps) |
| G: Today lesson lifecycle | Today lesson uses separate lesson buffer; readiness pool feeds upstream. No change needed |
| H: Practice Gym lifecycle | Production-correct per Phase 10O. Skipped exclusion added this phase |
| I: Replenishment pool health | All statuses counted. Reserved and Skipped added to PoolHealthSummary this phase |
| J: Admin diagnostics | AdminReadinessPoolController exposes all lifecycle counts. reservedCount + skippedCount added |

---

## Tests Added

### Unit tests — StudentActivityReadinessItemTests.cs (8 new)

- `MarkSkipped_FromReady_SetsSkippedStatus` — terminal state, not servable
- `MarkSkipped_FromReserved_SetsSkippedStatus`
- `MarkSkipped_FromReviewOnly_SetsSkippedStatus`
- `MarkSkipped_FromConsumed_Throws` — terminal guard
- `MarkSkipped_AlreadySkipped_Throws` — idempotency guard
- `MarkSkipped_FromExpired_Throws`
- `RecordEvaluation_SetsLastEvaluatedAtUtc`
- `RecordEvaluation_DoesNotChangeStatus`

### Integration tests — ReplenishmentIntegrationTests.cs (5 new)

- `GetHealth_ReservedItem_CountedInReservedCount`
- `GetHealth_SkippedItem_CountedInSkippedCount`
- `MarkSkippedAsync_PersistsSkippedStatus`
- `SkippedItem_NotReturnedByGetReadyForStudent`
- `AdminHealthEndpoint_IncludesReservedAndSkippedCounts`

---

## Build and Test Results

```
dotnet build --configuration Release
  → Build succeeded. 0 errors. 15 pre-existing warnings (NU1903, CS0108, CS0618).

dotnet test tests/LinguaCoach.UnitTests
  → Passed! Failed: 0, Passed: 1318, Skipped: 0

dotnet test tests/LinguaCoach.IntegrationTests --filter ReadinessPool|PracticeGym|ReadinessConsumption
  → Passed! Failed: 0, Passed: 64, Skipped: 0
```

---

## Remaining Lifecycle Gaps (Not in This Phase's Scope)

| Gap | Reason deferred |
|---|---|
| Mastery-based auto-demotion to ReviewOnly/Skipped | Requires mastery engine (StudentSkillProfile threshold evaluation). `EnableReviewScaffoldGeneration=false` guards this path. When enabled, replenishment will need a `SweepMasteredItemsAsync` pass |
| Today lesson readiness pool integration (direct) | By design: Today lesson uses LessonBatchGenerationJob buffer. Readiness pool is upstream content pipeline. Integration is at job level, not SessionGeneratorService |
| CEFR advancement notification | When student advances CEFR, a notification could trigger immediate stale sweep. Currently deferred to next scheduled replenishment run |
| Skipped items auto-detection | Currently requires explicit `MarkSkippedAsync` call. Auto-detection from skill profile not yet wired |

---

## Files Changed

| File | Change |
|---|---|
| `src/LinguaCoach.Domain/Enums/ReadinessPoolStatus.cs` | Added `Skipped = 9` |
| `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs` | Added `LastEvaluatedAtUtc`, `MarkSkipped()`, `RecordEvaluation()`, updated lifecycle comment |
| `src/LinguaCoach.Application/ReadinessPool/IStudentActivityReadinessPoolService.cs` | Added `MarkSkippedAsync` |
| `src/LinguaCoach.Application/ReadinessPool/PoolHealthSummary.cs` | Added `ReservedCount`, `SkippedCount` |
| `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs` | Added `SkippedCount` to summary, `LastEvaluatedAtUtc` to item DTO |
| `src/LinguaCoach.Infrastructure/ReadinessPool/StudentActivityReadinessPoolService.cs` | Added `MarkSkippedAsync`, `SkippedCount` in summary, `LastEvaluatedAtUtc` in DTO |
| `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs` | Added `SweepCefrMismatchedItemsAsync`, `IsBelowCurrentLevel`, updated `GetHealthAsync` with Reserved/Skipped counts |
| `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs` | Added `Skipped` to exclusion filter |
| `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` | Added `reservedCount`, `skippedCount` to health response |
| `src/LinguaCoach.Persistence/Configurations/StudentActivityReadinessItemConfiguration.cs` | Added `last_evaluated_at_utc` EF mapping |
| `src/LinguaCoach.Persistence/Migrations/20260626065357_T60_ReadinessLifecycleSkippedAndLastEvaluated.cs` | New migration |
| `tests/LinguaCoach.UnitTests/ReadinessPool/StudentActivityReadinessItemTests.cs` | 8 new tests |
| `tests/LinguaCoach.IntegrationTests/ReadinessPool/ReplenishmentIntegrationTests.cs` | 5 new tests |

---

## Final Verdict

The lifecycle model was already production-correct for the core happy path (Queued → Generating → Ready → Reserved → Consumed). This phase filled the remaining gaps:

- `Skipped` terminal state for mastered/irrelevant items
- CEFR mismatch stale demotion in the replenishment sweep
- `ReservedCount` and `SkippedCount` surfaced in pool health and admin diagnostics
- `LastEvaluatedAtUtc` for incremental evaluation tracking
- 13 new tests (8 unit + 5 integration), all passing

**Next recommended action:** Enable `EnableReviewScaffoldGeneration = true` once the mastery/weakness signal engine is validated in production, and wire `SweepMasteredItemsAsync` to auto-demote mastered objectives to ReviewOnly or Skipped.
