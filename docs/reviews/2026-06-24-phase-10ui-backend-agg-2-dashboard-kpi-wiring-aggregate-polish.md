# Phase 10UI-BACKEND-AGG-2 — Dashboard KPI Wiring and Aggregate Polish

**Date:** 2026-06-24
**Sprint / Feature:** Phase 10UI-BACKEND-AGG-2
**Commit message:** `ui: wire dashboard aggregate kpis`

---

## Summary

Used the aggregate endpoints from AGG-1 to wire three previously-placeholder dashboard hero KPIs with real data. Added `AverageScore` to the score distribution backend response as a small additive change. All wiring uses only real backend data — no fake values.

---

## Endpoints / Fields Consumed

| KPI | Endpoint | Field |
|-----|----------|-------|
| Activity attempts (7d) | `GET /api/admin/dashboard/activity-trends?period=7d` | sum of `activityCount` across all buckets |
| Avg score (7d) | `GET /api/admin/dashboard/score-distribution?period=7d` | `averageScore` (new field) |
| AI cost (7d) | `GET /api/admin/ai-usage/aggregate-trends?period=7d` | sum of `cost` across all buckets |

---

## Backend Changes

### `AdminDashboardScoreDistributionResponse` (additive)

Added `double? AverageScore` to the DTO record. Computed in `AdminDashboardAggregateHandler.GetScoreDistributionAsync` as `Math.Round(scores.Average(), 1)`. Returns `null` when no scored attempts exist in the period. Backwards-compatible — clients that don't read the field are unaffected.

### Files modified (backend)

- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — added `AverageScore` to record
- `src/LinguaCoach.Infrastructure/Admin/AdminDashboardAggregateHandler.cs` — compute average
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `averageScore: number | null`
- `tests/LinguaCoach.IntegrationTests/Api/AdminAggregateEndpointTests.cs` — 2 new tests for `averageScore` field

---

## Dashboard KPIs Replaced

| Hero slot | Was | Now |
|-----------|-----|-----|
| "Activities this week" (hero) | `—` / "Backend not available yet" | Real sum of `activityCount` from 7d trend; labelled "Activity attempts (7d)" |
| "Avg score" (hero) | `—` / "Backend not available yet" | Real `averageScore` from 7d score distribution; shows "No scored attempts yet" if null |
| "AI cost (7 d)" (KPI tile) | "Not implemented" | Real sum of `cost` from 7d AI usage trends; format `$0.0000` |

---

## Frontend Changes

- `admin-dashboard.component.ts`:
  - Import `AdminAiUsageTrendResponse`
  - New signal: `aiUsageTrends7d`, `loadingAiUsageTrends7d`, `aiUsageTrends7dError`
  - New computed: `heroActivitiesThisWeek`, `heroAvgScore`, `heroAiCost7d`
  - `ngOnInit`: added `getAiUsageTrends('7d')` call; changed score distribution period to `'7d'`
  - Template: replaced 2 placeholder hero tiles and 1 KPI tile with real-data bindings

---

## Placeholders Still Deferred

| Placeholder | Reason |
|-------------|--------|
| Dashboard "AI spend by type" donut | Needs grouped spend-by-category endpoint; existing category breakdown is per-feature-key not per-category-type |
| Dashboard "Avg session duration" | No session duration data in `LearningSessions` schema |
| Dashboard "Streak leaderboard" | No streak entity |
| Dashboard "Live events feed" | Needs SignalR or polling |
| Student "minutes/week" column | No per-week duration aggregate |
| AI Usage heatmap | Needs 2D date×hour grouping |

---

## Tests Added / Updated

### Backend integration (+2)

- `ScoreDistribution_EmptyDb_AverageScoreIsNull` — verifies `averageScore` is JSON null when no scored attempts
- `ScoreDistribution_ResponseIncludesAverageScoreField` — verifies field always present in response

### Frontend unit (+10 net new, stale tests updated)

**New tests (dashboard.component.spec.ts):**
- `hero KPI — activities this week`: getDashboardActivityTrends called on init, heroActivitiesThisWeek sums correctly, null while loading
- `hero KPI — avg score`: getDashboardScoreDistribution called on init, heroAvgScore returns averageScore, null when no scored attempts
- `hero KPI — AI cost 7d`: getAiUsageTrends called on init, heroAiCost7d sums correctly, null while loading, 0 when empty data

**Updated stale tests:**
- 4 hero/KPI tests updated to match new labels ("Activity attempts (7d)", "Avg score (7d)")
- 5 placeholder card tests updated to remove stale "Backend not available yet" text assertions
- `no fake data` test updated (removed `$` check since AI cost tile now legitimately shows `$0.0000`)
- `admin-wrapper-migration.spec.ts`: 2 dashboard spy objects updated with `getAiUsageTrends` spy

---

## Gate Results

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS — 0 errors |
| `dotnet test --configuration Release` | PASS — 1310 unit + 3 arch + **1073** integration (+2) = 2386 total |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — **1360/1360** (+10 net) |
| Playwright | Not run — no stable admin E2E specs exist |

---

## Security / Scope Constraints Respected

- No migrations.
- No new tables.
- No new write endpoints.
- No business logic changes.
- No student-facing UI touched.
- No fake data — all three hero KPIs show real zero when no data exists.
- `AverageScore` is `null` (not `0`) when there are no scored attempts, to avoid misleading zeros.

---

## Known Limitations

- "Activity attempts (7d)" counts all `ActivityAttempt` rows (including those without a score), not unique learning events. Label accurately reflects this.
- AI cost format shows up to 4 decimal places (`$0.0000`) to avoid rounding small costs to zero.
- `AverageScore` rounds to 1 decimal place server-side.
