# Phase 10UI-BACKEND-AGG-1 — Admin Dashboard and Usage Aggregate APIs

**Date:** 2026-06-24
**Sprint / Feature:** Phase 10UI-BACKEND-AGG-1
**Commit message:** `feat: add admin dashboard and usage aggregate APIs`

---

## Endpoints Added

| Route | Description |
|-------|-------------|
| `GET /api/admin/dashboard/activity-trends?period=7d\|30d\|90d` | Daily activity attempt counts, completed, failed |
| `GET /api/admin/dashboard/score-distribution?period=7d\|30d\|90d` | Score buckets (0-39, 40-59, 60-74, 75-89, 90-100) |
| `GET /api/admin/ai-usage/aggregate-trends?period=7d\|30d\|90d` | Daily AI usage: requests, tokens, cost, success/fail |
| `GET /api/admin/ai-usage/by-category?period=7d\|30d\|90d` | Per-feature-key AI usage breakdown |

All endpoints: admin-only (`[Authorize(Roles = "Admin")]`), read-only, no migrations required.

---

## Data Sources

| Endpoint | Table | Key fields |
|----------|-------|-----------|
| activity-trends | `ActivityAttempts` | `CreatedAt`, `DeletedAtUtc`, `Completed`, `Passed`, `Score` |
| score-distribution | `ActivityAttempts` | `Score` (nullable double), `CreatedAt`, `DeletedAtUtc` |
| aggregate-trends | `AiUsageLogs` | `CreatedAt`, `WasSuccessful`, `InputTokens`, `OutputTokens`, `CostUsd` |
| by-category | `AiUsageLogs` | `FeatureKey`, all of above |

No new tables. No migrations.

---

## Files Created

- `src/LinguaCoach.Infrastructure/Admin/AdminDashboardAggregateHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminAggregateController.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AdminAggregateEndpointTests.cs` (15 tests)

## Files Modified

- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — new DTOs + `IAdminDashboardAggregateHandler`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — 8 new TypeScript interfaces
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — 4 new service methods
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts` — signals, computed, ngOnInit, template wiring
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — signals, computed, ngOnInit
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — placeholder replacement

---

## Frontend Placeholders Replaced

| Location | Was | Now |
|----------|-----|-----|
| Dashboard — Activity trends card | `sp-admin-visual-placeholder state="not-available"` | `sp-admin-breakdown-bars` with real data from `/dashboard/activity-trends` |
| Dashboard — Score distribution card | `sp-admin-visual-placeholder state="not-available"` | `sp-admin-breakdown-bars` with real buckets from `/dashboard/score-distribution` |
| AI Usage — trends area | skeleton placeholder | real trend breakdown from `/ai-usage/aggregate-trends` |
| AI Usage — category breakdown | skeleton placeholder | real bars from `/ai-usage/by-category` |

## Placeholders Still Deferred

| Placeholder | Reason |
|-------------|--------|
| Dashboard hero "Activities this week" | Needs week-bounded aggregate; current endpoint returns 30d buckets |
| Dashboard hero "Avg score" | Needs separate endpoint or add to score-distribution response |
| Dashboard "AI cost (7d)" KPI | Needs separate 7d cost sum endpoint |
| Dashboard "Avg session duration" | No session duration data in current schema |
| Dashboard "Streak data" | No streak entity |
| Dashboard "Live events feed" | Would need SignalR or polling; deferred |
| AI Usage heatmap | Requires 2D date×hour grouping; deferred |
| AI Usage student engagement | No student-level AI usage breakdown without join complexity |

---

## Performance / Range Limits

- All queries bounded by period (7/30/90 days). No unbounded full-table scans.
- Activity trends and AI trends load all matching rows into memory then group (LINQ in-memory grouping after bounded WHERE). Acceptable for current scale; can push grouping to DB later if needed.
- Score distribution fetches only `Score` column for bounded date range.
- Category breakdown fetches minimal columns per log row.

---

## Gate Results

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS — 0 errors, 9 warnings (pre-existing) |
| `dotnet test --configuration Release` | PASS — 1310 unit + 3 arch + 1071 integration = 2384 total |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1350/1350 |

Backend test delta: +15 (1056 → 1071 integration tests).

---

## Known Limitations

- `ActivityTrendBucket.Failed` counts attempts where `Completed == true && (Passed == false || Score < 50)`. Attempts not yet evaluated (null Passed, null Score) are not counted as failed.
- Score distribution ignores attempts where `Score` is null (un-evaluated).
- AI usage trends use client-side date grouping (load then group), not DB-side `DATE_TRUNC`. Safe for current data volumes; revisit if AiUsageLogs grows to millions of rows.
- `sp-admin-visual-placeholder` does not have a `loading` state variant; loading states use `state="not-available"` with "Loading..." message text.

---

## Security / Scope Constraints Respected

- No new write endpoints.
- No migrations.
- No business logic changes.
- No student-facing UI touched.
- No fake data.
- No PII beyond what admin pages already expose (no email/name in any aggregate response).

---

## Next Recommended Action

Phase 10UI-BACKEND-AGG-2:
- Add 7d cost sum to score-distribution response or new KPI endpoint to unblock hero "Avg score" and "AI cost (7d)" tiles.
- Consider DB-side date grouping if log volumes grow.
- Live events feed if SignalR is in scope.
