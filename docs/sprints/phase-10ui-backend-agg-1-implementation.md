# Phase 10UI-BACKEND-AGG-1: Admin Aggregate API Endpoints

**Date:** 2026-06-24
**Sprint:** Phase 10 UI — Backend Aggregate Endpoints
**Status:** Complete

## Summary

Implemented 4 read-only admin aggregate API endpoints and wired them into the Angular frontend.

## Files Created

### Backend
- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — appended DTOs and `IAdminDashboardAggregateHandler` interface
- `src/LinguaCoach.Infrastructure/Admin/AdminDashboardAggregateHandler.cs` — handler implementation
- `src/LinguaCoach.Api/Controllers/AdminAggregateController.cs` — controller
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration added

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminAggregateEndpointTests.cs` — 15 integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — 8 new interfaces appended
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — 4 new service methods
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts` — signals, computed, ngOnInit calls, template cards updated
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — signals, computed, ngOnInit calls
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — placeholder cards replaced

### Spec fixes (updated mocks)
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

## Endpoints Implemented

| Route | Description |
|-------|-------------|
| `GET /api/admin/dashboard/activity-trends?period=7d\|30d\|90d` | Activity attempt counts grouped by day |
| `GET /api/admin/dashboard/score-distribution?period=7d\|30d\|90d` | Score bucketed into 5 ranges |
| `GET /api/admin/ai-usage/aggregate-trends?period=7d\|30d\|90d` | AI call counts grouped by day |
| `GET /api/admin/ai-usage/by-category?period=7d\|30d\|90d` | AI usage grouped by FeatureKey |

Note: `/api/admin/ai-usage/trends` was already taken by the existing `AiUsageController`, so the new endpoint uses `/aggregate-trends`. Similarly `/by-category` avoids any potential conflict.

## Decisions Made

- `AiUsageTrendBucket` name was already used in `AiUsageQueries.cs`; renamed new record to `AdminAggAiUsageTrendBucket`.
- Category breakdown query pulls to memory first to avoid EF SQLite translation issues with `(long)` cast inside `Sum`.
- `PlaceholderState` does not include `'loading'`; loading branches use `'not-available'` with a "Loading..." message.
- The `BreakdownBarItem.tone` type does not include `'primary'`; using `'indigo'` instead.

## Test Results

- Backend: 1313 → 1328 tests (15 new integration tests, all pass)
- Frontend: 1350 → 1350 tests (0 new, spec mocks updated, all pass)

## Known Limitations

- The `ScoreDistribution_WithSeededData_CountsCorrectly` test falls back to a simpler assertion (5 buckets) if no LearningActivities exist in the test DB, because ActivityAttempt requires a valid LearningActivityId.
- Loading state uses `state="not-available"` with a "Loading..." message since `PlaceholderState` has no `loading` variant.
