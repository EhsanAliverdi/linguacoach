# Phase 12B — Mastery Signal Validation and Review Scaffold Controlled Rollout

**Date:** 2026-06-27
**Related sprint:** Phase 12B
**Review type:** Engineering implementation review

---

## Files Reviewed (Part A — Audit)

- `src/LinguaCoach.Infrastructure/Mastery/StudentMasteryEvaluationService.cs`
- `src/LinguaCoach.Application/Mastery/MasteryOptions.cs`
- `src/LinguaCoach.Application/Mastery/ObjectiveMasterySignal.cs`
- `src/LinguaCoach.Application/Mastery/StudentMasteryReport.cs`
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs`
- `src/LinguaCoach.Application/ReadinessPool/PoolHealthSummary.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`
- `src/LinguaCoach.Domain/Entities/CurriculumObjective.cs`
- `tests/LinguaCoach.UnitTests/Mastery/StudentMasteryEvaluationServiceTests.cs`

---

## Part A — Mastery Signal Audit Findings

### Evidence inputs per objective

| Input | How used |
|-------|----------|
| `StudentLearningEvent.Outcome` | Classifies success (Mastered/Practised/Reviewed) or failure (Failed/NeedsReview) |
| `StudentLearningEvent.Score` (0–100, optional) | Averaged across all events for the objective key |
| `StudentLearningEvent.PatternKey` | Primary objective key proxy (via `CurriculumObjectiveKey()` extension) |
| `StudentLearningEvent.PrimarySkill` | Fallback when PatternKey is null |

Evidence window: last 200 events per student from `IStudentLearningLedger.GetRecentAsync`.

### Classification rules (`ClassifyStatus`)

| Status | Condition |
|--------|-----------|
| `InsufficientEvidence` | < 3 events |
| `AtRisk` | ≥ 2 consecutive failures OR avg score < 30 |
| `Mastered` | ≥ 5 events AND ≥ 3 consecutive successes AND avg score ≥ 80 |
| `NeedsReview` | ≥ 1 consecutive success AND avg 50–79 |
| `NeedsPractice` | avg 30–79 (catch-all) |

Thresholds come from `MasteryOptions` (appsettings `"Mastery"` section, defaults shown above).

### Demotion decisions (`DecideDemotionAsync`)

| Decision | Trigger |
|----------|---------|
| `NoChange` | Item in terminal state (Consumed/Expired/Failed/Skipped) |
| `Expire` | Item age > StaleDaysThreshold (90 days) AND never consumed |
| `MarkStale` | Student CEFR advanced > 1 level beyond item's target CEFR |
| `ConvertToReviewOnly` | Objective mastered AND item is review-eligible (IsLowerLevelContent OR RoutingReason.Review) |
| `Skip` | Objective mastered AND item is NOT review-eligible |
| `KeepReady` | AtRisk, NeedsPractice, NeedsReview, or no objective key |

### `EnableReviewScaffoldGeneration` usage (in `FillShortfallAsync`)

When `false` (default):
- `allowReviewOrScaffold = false`
- `routingMode = RoutingMode.NewLearning`
- `allowReviewOfMastered = false`
- Mastered objectives are excluded from new-learning routing

When `true`:
- Calls `GetWeakEventsAsync(limit: 5)` per student
- If weak events exist: `allowReviewOrScaffold = true`, `routingMode = RoutingMode.Review`
- If no weak events: behaves same as `false`

### Gaps found

1. **`SkippedCount` and `MarkedReviewOnlyCount` in `StudentMasteryReport` always return 0.** Known gap noted in service comments. Demotion breakdown is logged but not aggregated at report level. This is acceptable for current phase.

2. **`ReviewOnly` items correctly excluded from `ShortfallCount`.** `ShortfallCount = max(0, Target - Ready - QueuedOrGenerating)` — confirmed safe.

3. **No dry-run mode existed.** Added in this phase.

4. **No admin diagnostic endpoint for mastery quality.** Added in this phase.

5. **Suspicious patterns were undetected.** Added warning logic to validation summary endpoint.

### Assessment: mastery signal is sound

The classification algorithm is deterministic, rule-based, and fully unit-tested. Thresholds are conservative (5 events, 3 consecutive successes, 80% avg). The evidence window of 200 events is generous. No AI involved. Safe to proceed with controlled review scaffold rollout validation.

---

## Part B — Mastery Validation Diagnostic

### New files

- `src/LinguaCoach.Application/Mastery/MasteryValidationSummary.cs`

### New endpoint

`GET /api/admin/mastery/validation-summary`

Added to `AdminReadinessPoolController`. Admin-only. Read-only.

**Response fields:**

| Field | Description |
|-------|-------------|
| `totalStudentsEvaluated` | Active students (CourseReady+, onboarding complete) |
| `totalObjectivesEvaluated` | Total objective keys across all students |
| `countInsufficientEvidence` | Objectives with < 3 events |
| `countMastered` | Mastered objective count |
| `countNeedsReview` | NeedsReview + NeedsPractice combined |
| `countAtRisk` | AtRisk objective count |
| `masteredExcludedFromNewLearning` | Mastered count (excluded from new-learning routing) |
| `warnings` | Suspicious patterns (early mastery, all at-risk) |
| `generatedAt` | UTC timestamp |

**Suspicious patterns detected:**

- Student with mastered objectives but < 3 total events → "too many early masteries"
- Student with all objectives AtRisk and no mastered/weak objectives → "all at risk"

---

## Part C — Dry-Run Review Scaffold Simulation

### New files

- `src/LinguaCoach.Application/ReadinessPool/ReviewScaffoldDryRunSummary.cs`

### New endpoint

`GET /api/admin/readiness-pool/review-scaffold/dry-run`

Added to `AdminReadinessPoolController`. Admin-only. Read-only — no DB writes.

**Simulation logic (same gates as real generation):**

1. Load all active students.
2. Per student: call `GetWeakEventsAsync(limit: 5)` — same check as `FillShortfallAsync`.
3. For eligible students: run `EvaluateStudentAsync` to get mastered objectives.
4. Find Ready/Reserved items whose PatternKey/CurriculumObjectiveKey matches a mastered objective.
5. Check for duplicate ReviewOnly (same student + key) → count as `blockedDuplicates`.
6. Check CurriculumObjective.IsActive → count as `blockedInactiveObjectives`.
7. Report net new items = `estimatedReviewOnlyConversions - blockedDuplicates`.

**`status` field mapping:**

| Config | Status |
|--------|--------|
| `EnableReviewScaffoldGeneration=false` | `Disabled` |
| `EnableReviewScaffoldGeneration=true, DryRunOnly=true` | `DryRun` |
| `EnableReviewScaffoldGeneration=true, DryRunOnly=false` | `Enabled` |

---

## Part D — Controlled Rollout Config

### Modified

- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs`

### Change

Added `DryRunOnly` property:

```csharp
public bool DryRunOnly { get; set; } = false;
```

**Config table:**

| Setting | Default | Purpose |
|---------|---------|---------|
| `EnableReviewScaffoldGeneration` | `false` | Master on/off for review routing in replenishment |
| `DryRunOnly` | `false` | When true + enabled: simulate but don't write (for production dry-run) |

Reversible: both flags can be set/unset via appsettings without code change.

---

## Part E — Admin Visibility

### Modified Angular files

- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `ReviewScaffoldDryRunSummary`, `MasteryValidationSummary` interfaces
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — added `getReviewScaffoldDryRun()`, `getMasteryValidationSummary()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts` — added `scaffoldDryRun` signal, `loadScaffoldDryRun()`, `refreshScaffoldDryRun()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.html` — added "Review scaffold generation" card
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.spec.ts` — added `getReviewScaffoldDryRun` to mock
- `src/LinguaCoach.Web/src/app/features/admin/create-student/create-student.component.spec.ts` — updated spec to match Phase 12A HTML change

### UI behavior

The "Review scaffold generation" card on `/admin/lessons`:
- Loads on page init via `loadScaffoldDryRun()`
- Displays: status badge (Disabled/DryRun/Enabled), students considered, students eligible, estimated net new items, blocked counts
- Shows any warnings from backend
- Refresh button calls the dry-run endpoint again (read-only)
- No enable/disable button — config-only via appsettings

---

## Part F — Tests Added

### Unit tests

**New file:** `tests/LinguaCoach.UnitTests/Mastery/StudentMasteryClassificationEdgeCaseTests.cs` (10 tests)

| Test | What it validates |
|------|-------------------|
| `Exactly3Events_IsNotInsufficientEvidence` | Boundary: 3 events clears InsufficientEvidence |
| `TwoEvents_IsInsufficientEvidence` | Boundary: 2 events does not clear it |
| `ExactlyTwoConsecutiveFailures_IsAtRisk` | Boundary: exactly 2 consecutive failures = AtRisk |
| `AvgScoreExactly30_IsNotAtRisk` | Boundary: avg=30 is NOT AtRisk (< 30 required) |
| `AvgScoreJustBelow80_IsNotMastered` | Boundary: avg<80 prevents Mastered |
| `ExactThresholds_IsMastered` | All three mastery conditions verified simultaneously |
| `Mastered_RequiresAllThreeConditions` | Confirms composite gate |
| `NoEvents_IsInsufficientEvidence` | Zero-event edge case |
| `ReviewOnlyItems_ExcludedFromShortfallCount` | Pool shortfall correctness |
| `MasteredNonReviewEligibleItem_GetsSkipDecision` | Non-review-eligible mastered item → Skip |
| `SingleFailure_IsNotAtRisk` | Single failure not enough for AtRisk |

### Integration tests

**New file:** `tests/LinguaCoach.IntegrationTests/Api/ReviewScaffoldDryRunTests.cs` (11 tests)

| Test | What it validates |
|------|-------------------|
| `DryRun_Unauthenticated_Returns401` | Auth guard |
| `DryRun_AsStudent_Returns403` | Role guard |
| `MasteryValidation_Unauthenticated_Returns401` | Auth guard |
| `MasteryValidation_AsStudent_Returns403` | Role guard |
| `DryRun_AsAdmin_Returns200WithExpectedShape` | Response shape |
| `DryRun_FlagDisabled_ReportsDisabledStatus` | Status = Disabled when flag off |
| `DryRun_DoesNotMutateDatabase` | Read-only guarantee |
| `MasteryValidation_AsAdmin_Returns200WithExpectedShape` | Response shape |
| `MasteryValidation_DoesNotMutateDatabase` | Read-only guarantee |
| `MasteryValidation_AllCountsAreNonNegative` | Data integrity |
| `DryRun_AllCountsAreNonNegative` | Data integrity |

---

## Build and Test Results (Part G)

```
dotnet build  → succeeded, 0 errors
dotnet test   → 2532 passed, 0 failed
  - LinguaCoach.UnitTests:          1387 passed
  - LinguaCoach.IntegrationTests:   1142 passed
  - LinguaCoach.ArchitectureTests:     3 passed

npm run build → Application bundle generation complete
npm test      → 1384 passed, 0 failed
```

---

## Decisions Made

1. **Dry-run is read-only by construction** — no flags needed, no DB scope used with tracking.
2. **Suspicious pattern warnings are non-blocking** — advisory only, no action taken.
3. **No "Enable" button on admin UI** — enabling is config-only (appsettings), requiring deliberate deployment action.
4. **`DryRunOnly` defaults to false** — enabling the flag without dry-run mode is a production commitment; the explicit `DryRunOnly=true` step allows safe validation first.
5. **Mastery validation iterates per-student** — acceptable for admin-only diagnostic; not called on hot paths.

---

## Risks and Unresolved Questions

1. **`SkippedCount`/`MarkedReviewOnlyCount` in `StudentMasteryReport` always return 0.** Accepted known gap. Fix in a future phase if detailed per-item demotion breakdown is needed at aggregate level.

2. **`PatternKey` as objective key proxy is fragile.** Different patterns targeting the same learning objective will be tracked as separate keys. Acceptable for current data volume; revisit if curriculum expands significantly.

3. **Mastery validation endpoint latency** — calls `EvaluateStudentAsync` per active student. For large cohorts (>100 students), this could take several seconds. Acceptable for an on-demand admin diagnostic.

4. **`enableReviewScaffoldGeneration=true` in production is irreversible until config is changed** — mitigated by `DryRunOnly=true` step available before full enable.

---

## Final Verdict

**Mastery signal quality: validated and sound.**

The classification algorithm is deterministic, conservative, and fully tested. All boundary cases behave correctly. `EnableReviewScaffoldGeneration` remains `false` by default.

**Review scaffold generation: not enabled. Safe to keep disabled.**

The dry-run endpoint and mastery validation summary are now available for production-like validation before enabling. Recommend running the dry-run endpoint against production data before changing `EnableReviewScaffoldGeneration=true`.

---

## Recommendation

1. Deploy this phase. Run `GET /api/admin/readiness-pool/review-scaffold/dry-run` against production.
2. If `studentsEligibleForReview > 0` and `estimatedNetNewReviewItems` is reasonable: set `ReadinessPool:DryRunOnly=true` and `ReadinessPool:EnableReviewScaffoldGeneration=true` for one cycle.
3. Observe logs. If no issues, remove `DryRunOnly=true` to enable full review generation.

---

## Documentation Impact

- Docs reviewed: `docs/architecture/README.md`
- Docs updated: `docs/reviews/2026-06-27-phase-12b-mastery-signal-review-scaffold-review.md` (this file)
- Docs intentionally not updated: sprint docs, roadmap (per saved user preference for sessions)
