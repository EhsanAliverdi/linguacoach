# Phase 10U-9 — AI Usage Trend Summary — Engineering Review

**Date:** 2026-06-21
**Sprint / Feature:** Phase 10U-9 — AI Usage Trend Summary
**Status:** Complete

---

## Files changed

### Backend

- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — added `AiUsageTrendBucket` record; added `GetTrendsAsync` to `IAdminAiUsageHandler`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — implemented `GetTrendsAsync`
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — added `GET /api/admin/ai-usage/trends`
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageTrendTests.cs` — 15 new integration tests

### Frontend

- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — `AiUsageTrendBucket` interface + `getTrends()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — signals, `loadTrends()`, `load()` wired
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — "Usage trend" card
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — 11 trend-specific component tests
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` — added `getTrends` spy to existing test

---

## Findings

### Priority 1 (blocking) — none

### Priority 2 (significant)

**Client-side grouping by `DateOnly`** — logs are materialised before grouping. This is intentional: `DateOnly.FromDateTime()` is not translatable to SQL by EF Core across all providers (SQLite vs PostgreSQL), so grouping client-side avoids provider-specific date-truncation SQL. Acceptable for admin trend queries where row counts are bounded by the date filter.

### Priority 3 (minor)

**Zero-fill gate condition** — zero-fill only activates when `From` is set AND there are >= 2 real buckets. When zero buckets are returned (future date range, no logs for that period), the empty array is returned directly. This avoids generating a multi-day array with no meaningful data. The condition is conservative and correct.

---

## Decisions made

1. Client-side grouping by `DateOnly` — chosen over DB-side to ensure SQLite (tests) and PostgreSQL (production) consistency.
2. Zero-fill only when `From` is set and >= 2 real buckets — avoids unbounded array generation for open-ended queries.
3. No chart library — plain `sp-admin-table` keeps the trend readable and consistent with the rest of the admin UI.
4. Trend reloads on every `load()` call — same lifecycle as summary and recent; no separate trigger needed.

---

## Test results

| Suite | Result |
|-------|--------|
| `LinguaCoach.ArchitectureTests` | 3/3 PASS |
| `LinguaCoach.UnitTests` | 1248/1248 PASS |
| `LinguaCoach.IntegrationTests` | 790/790 PASS (+15 new) |
| Angular Karma (ChromeHeadless) | 858/858 PASS (+11 new) |
| Angular production build | PASS (0 errors) |
| `dotnet build --configuration Release` | PASS (0 errors) |
| `git diff --check` | PASS |

---

## Risks / unresolved questions

- For very wide date ranges with no filters, all logs are materialised before grouping. If the table grows extremely large (millions of rows), this will be slow. Acceptable at current scale; can be revisited with a DB-side `DATE_TRUNC` query when needed.

---

## Final verdict

All gates pass. Feature is complete, tested, and consistent with the existing admin AI Usage page patterns.

## Next recommended action

Proceed to Phase 10U-10 or next backlog item.
