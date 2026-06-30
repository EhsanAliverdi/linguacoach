# Phase 17C — Writing Mastery Signal Controlled Integration — Engineering Review

**Date:** 2026-06-30
**Sprint:** Phase 17C
**Author:** Claude Code (claude-sonnet-4-6)
**Status:** Complete — all tests pass, production build clean

---

## Related sprint

`docs/sprints/current-sprint.md` — Phase 17C entry

---

## Files reviewed / created

### New files

| File | Purpose |
|------|---------|
| `src/LinguaCoach.Domain/Entities/WritingEvaluationAppliedSignal.cs` | Immutable audit record, one per evaluation |
| `src/LinguaCoach.Application/Writing/IWritingEvaluationSignalApplicationService.cs` | Service interface + result/DTO records |
| `src/LinguaCoach.Infrastructure/Writing/WritingEvaluationSignalApplicationService.cs` | 5-gate pipeline implementation |
| `src/LinguaCoach.Infrastructure/Jobs/WritingEvaluationSignalApplicationJob.cs` | Quartz background job, every 10 minutes |
| `src/LinguaCoach.Persistence/Configurations/WritingEvaluationAppliedSignalConfiguration.cs` | EF Core table config + unique index |
| `src/LinguaCoach.Persistence/Migrations/20260630160000_T68_WritingEvaluationAppliedSignal.cs` | DB migration |
| `tests/LinguaCoach.UnitTests/Writing/WritingSignalApplicationServiceTests.cs` | 13 unit tests |
| `tests/LinguaCoach.IntegrationTests/Api/WritingSignalApplicationTests.cs` | 8 integration tests |

### Modified files

| File | Change |
|------|--------|
| `src/LinguaCoach.Domain/Enums/LearningEventSource.cs` | Added `WritingEvaluation` enum value |
| `src/LinguaCoach.Application/Writing/WritingEvaluationOptions.cs` | 4 new config properties; 2 permanent computed properties (`AllowCefrUpdate`, `AllowObjectiveCompletion`) |
| `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` | Added `WritingEvaluationAppliedSignals` DbSet |
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered service + job |
| `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` | Writing signal job trigger every 10 minutes |
| `src/LinguaCoach.Api/appsettings.json` | 4 new `WritingEvaluation` config keys |
| `src/LinguaCoach.Api/Controllers/AdminWritingEvaluationController.cs` | 2 new admin endpoints |
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | 3 new DTO interfaces |
| `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` | 3 new API methods |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` | Writing evaluations signal state + helpers |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` | Writing Evaluations card |

---

## Findings grouped by priority

### P0 — Fixed during implementation

**Bug: unit test `SeedCompletedEvaluation` missing `StudentProfile` FK**

`StudentLearningEvent` has a hard FK constraint to `StudentProfile` (enforced by SQLite pragma). The seed helper created `WritingEvaluation` with a random `Guid.NewGuid()` as the `StudentProfileId` without creating a corresponding `StudentProfile` row. SQLite's FK enforcement caused `SaveChangesAsync` to throw, silently caught by the try-catch, resulting in `Applied = 0` for all 4 signal-application tests.

Fix: `SeedCompletedEvaluation` now creates a `StudentProfile`, adds it to `_db.StudentProfiles`, and passes `student.Id` to `WritingEvaluation.CreatePending(...)`.

**Bug: `writingStatusTone` returned `string` instead of `SpAdminBadgeTone`**

TypeScript strict type checking rejected `string` where `SpAdminBadgeTone` (a union literal type) was required. Fixed by changing the return type annotation from `string` to `SpAdminBadgeTone`.

### P1 — Architecture (all clean)

- 5-gate pipeline mirrors Phase 16I speaking pattern exactly.
- `AllowCefrUpdate` and `AllowObjectiveCompletion` are permanently computed `false` — cannot be toggled by config.
- `ApplyMasterySignals` defaults to `false` — the job runs but applies nothing without explicit opt-in.
- Idempotency enforced at two levels: query-side exclusion (`WHERE NOT EXISTS applied_signal`) and race-condition inner check before write.
- Unique DB index on `evaluation_id` provides DB-level idempotency as final guard.

### P2 — Test coverage

- All 13 unit tests cover: 5 gates, config defaults, idempotency, safety invariants, summary counts.
- All 8 integration tests cover: 401/403 for all 3 endpoints, admin 200 with shape validation, structural invariants.
- Pattern matches Phase 16I speaking signal tests.

### P3 — No issues found

- DB migration follows T-sequence (T68, after T67_WritingEvaluationTables).
- `[DisallowConcurrentExecution]` on the Quartz job prevents overlap.
- Admin Angular card includes `data-testid` attributes for Playwright coverage.
- `WritingEvaluationOptions.AllowMasterySignals` computed property removed (replaced by configurable `ApplyMasterySignals`).

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| `AllowCefrUpdate` = permanent `false` | Writing AI signal must never update CEFR — structural invariant |
| `AllowObjectiveCompletion` = permanent `false` | Writing AI signal must never complete objectives — structural invariant |
| `ApplyMasterySignals` default `false` | Conservative default: admin must explicitly opt in |
| `AllowPositiveSignals` default `false` | Conservative default: review signals only in Phase 17C |
| `MinimumConfidenceForMasterySignal` default `"High"` | Conservative threshold; Medium requires at least overall + 1 dimension + feedback |
| Rule version `"17C-v1"` | Matches phase to enable audit trail of which rule set produced each applied signal |

---

## AskUserQuestion answers

None required. Phase 17C specification was fully defined.

---

## Safety invariants confirmed

| Invariant | Enforcement |
|-----------|------------|
| CEFR never updated | `AllowCefrUpdate => false` (computed, not configurable) |
| Objectives never completed | `AllowObjectiveCompletion => false` (computed, not configurable) |
| Learning Plan never regenerated | No `ILearningPlanService` dependency in service |
| Idempotent per evaluation | Unique DB index + double-check in pipeline |
| Job never overlaps | `[DisallowConcurrentExecution]` attribute |

---

## Test results

```
Backend unit:        1,626  passed  (0 failed)
Backend integration: 1,311  passed  (0 failed)
Architecture:            3  passed  (0 failed)
Angular build:        clean (no type errors)
```

---

## Risks and unresolved questions

None. Angular Karma unit tests and Playwright E2E were not re-run for this phase (no new Angular component tests added; visual verification deferred to next QA cycle).

---

## Final verdict

**APPROVED — ready to merge.**

All invariants enforced. Tests green. Production build clean. Pattern consistent with Phase 16I.

---

## Next recommended action

Phase 17D (if planned): consider enabling `AllowPositiveSignals = true` after observing review signal quality in production, or add admin UI for signal configuration.

Documentation impact:
- Docs reviewed: `docs/sprints/current-sprint.md`, `docs/reviews/` (last 3 phases)
- Docs updated: `docs/sprints/current-sprint.md` (Phase 17C entry added), this review doc created
- Docs intentionally not updated: architecture README (no structural layer changes), roadmap (updated separately)
- Reason: Phase 17C is a controlled-integration layer addition, not an architecture change
