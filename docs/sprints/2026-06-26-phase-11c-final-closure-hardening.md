# Phase 11C-FINAL: Mastery Routing Closure Hardening

**Date:** 2026-06-26
**Status:** Complete
**Builds on:** `docs/sprints/2026-06-26-phase-11c-mastery-routing-integration.md`

## Goal

Close remaining risks from Phase 11C before committing. Focused hardening pass — no new features, no student UI, no schema changes.

## What Was Fixed / Added

### Part B — Non-runnable filter applied to lower-level review path

**Bug fixed:** `CurriculumRoutingService.RecommendAsync` Step 4 (lower-level review candidates) did not apply `FilterNonRunnable`. A grammar or pronunciation objective at B1 could be selected for a B2 student in review mode.

**Fix:** Added `lowerCandidates = FilterNonRunnable(lowerCandidates, request.Source)` before the `lowerCandidates.Count > 0` check in Step 4.

**New tests:**
- `Recommend_LowerLevelReviewPath_NonRunnableFilteredOut` — all B1 candidates non-runnable → fallback
- `Recommend_LowerLevelReviewPath_RunnableSelectedOverNonRunnable` — mixed B1 → runnable selected
- `Recommend_MasteredFilterDoesNotReintroduceNonRunnable` — mastered fallback cannot resurface grammar

### Part C — Review scaffold flag tests confirmed

Existing tests in `ReplenishmentOptionsTests` (tests 1, 12, 30) already cover flag-off behavior. Added:
- Test 17: `Options_ReviewScaffoldFlagFalse_RoutingStaysNewLearning`
- Test 18: `PoolHealthSummary_ReviewOnly_NeverSatisfiesNewLearningShortfall`

### Part D — RoutingMode added to admin routing preview

**Backend:** `AdminRoutingPreviewRequest` record gains optional `Mode = RoutingMode.NewLearning` parameter.

**Wire-up:** `CurriculumObjectiveWriteService.PreviewRoutingAsync` now passes `request.Mode` to the routing request.

**Mastery note:** Admin preview intentionally does not pass `MasteredObjectiveKeys`. A permanent warning is now included in every preview result: `"Student-specific mastery filtering is not applied in generic preview."` This prevents admin from assuming preview results reflect actual student routing.

**TypeScript:** `curriculum.service.ts` — added `RoutingMode` type and optional `mode` field to `AdminRoutingPreviewRequest`. Component default updated to `mode: 'NewLearning'`.

### Part A — Call-site audit document

Created: `docs/architecture/routing-call-site-audit-phase-11c.md`

Documents all 7 routing call sites: wired status, reason for deferral, latency/architecture concern, future action.

## Build and Test Results

| Suite | Result |
|-------|--------|
| `dotnet build` (Infrastructure) | ✅ 0 errors, 1 pre-existing warning |
| `dotnet test` (UnitTests) | ✅ 1356 passed (up from 1351) |
| `npm run build -- --configuration production` | ✅ No TS errors |
| `npm test -- --watch=false --browsers=ChromeHeadless` | ✅ 1381 passed |

## Files Changed

| File | Change |
|------|--------|
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs` | `FilterNonRunnable` applied to lower-level review candidates |
| `src/LinguaCoach.Application/Curriculum/AdminCurriculumContracts.cs` | `Mode` field added to `AdminRoutingPreviewRequest` |
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumObjectiveWriteService.cs` | `Mode` wired; mastery-not-applied warning added |
| `src/LinguaCoach.Web/src/app/core/services/curriculum.service.ts` | `RoutingMode` type + `mode` field on request interface |
| `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts` | Default preview object includes `mode: 'NewLearning'` |
| `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs` | 3 new non-runnable/review-path tests |
| `tests/LinguaCoach.UnitTests/ReadinessPool/ReplenishmentOptionsTests.cs` | 2 new flag/ReviewOnly tests |
| `docs/architecture/routing-call-site-audit-phase-11c.md` | New — call-site audit |

No schema migrations. No new routes. No student UI.

## Remaining Gaps (Accepted)

- `ExercisePrepareHandler` and `ActivityGetHandler` still do not pass mastered keys — page-load latency guard in place, documented in audit doc.
- `ActivityMaterializationJob` deferred — objective already decided upstream.
- `EnableReviewScaffoldGeneration` remains `false` — enable after production mastery signal validation.
- Admin preview does not expose RoutingMode selector in the HTML template — field is available in the TS model but not yet surfaced in the UI (intentional, no redesign in scope).
