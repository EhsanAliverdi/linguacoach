# Phase 10U-4 — AI Usage Recent Calls Pagination — Engineering Review

**Date:** 2026-06-20
**Related sprint:** Phase 10U-4 (current sprint)
**Predecessor:** docs/reviews/2026-06-20-phase-10u-3-ai-usage-date-filtering-review.md

---

## Files changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — Added `AiUsagePagedResult` record; replaced `GetRecentAsync(limit, filter)` with `GetRecentAsync(page, pageSize, filter)` in `IAdminAiUsageHandler`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — `GetRecentAsync` now uses `CountAsync` + `Skip`/`Take`; clamps page (min 1, clamped to totalPages) and pageSize (1–100)
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — `/recent` endpoint: replaced `limit` param with `page`/`pageSize`; response shape updated to `{ items, totalCount, page, pageSize, totalPages }`
- `tests/LinguaCoach.IntegrationTests/Api/AiUsagePaginationTests.cs` — New file, 10 integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — `AiUsageRecentResponse` updated to paged shape; `getRecent(page, pageSize, range?)` signature
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — Removed client-side `recentTotalPages`/`pagedRecentItems` computed; added `recentTotalCount`/`recentTotalPages` signals; `loadRecent()` extracted; `onRecentPageChange()` added
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — Table iterates `filteredRecentItems()` (current page from server); pagination wired to `onRecentPageChange`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — All `{ total, items }` mock shapes updated to paged shape; 8 new pagination tests added

---

## Findings

### P0 — None

### P1 — None

### P2 — Resolved

**Client-side pagination removed cleanly.** The old component held all 100 fetched items and sliced them client-side. The new design fetches only the current page (25 items by default) from the server. Client-side provider/status filters still operate on the current page's items — acceptable for phase scope.

### P3 — Design notes

**`AiUsagePagedResult` record** added to Application layer rather than Infrastructure to keep the DTO public and testable without referencing infrastructure internals.

**`page` clamping in handler:** if `page > totalPages` (e.g., results shrank due to a date filter), the handler clamps `page` to `totalPages` rather than returning 400. This avoids stale-page 400s when the admin narrows the date range.

**`pageSize` clamped 1–100** server-side. Frontend always sends 25 (the `recentPageSize` constant). The 100-cap prevents runaway queries if the param is manipulated.

**Summary totals are unaffected.** `GetSummaryAsync` was not changed — it still loads all logs matching the date filter before aggregating. Verified by `Summary_TotalCalls_NotLimitedByRecentPagination` integration test.

**Date filter + pagination compose correctly.** `ApplyDateFilter` runs before `CountAsync`/`Skip`/`Take`, so `totalCount`/`totalPages` reflect the filtered universe, not the full table.

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| Server-side pagination instead of client-side slice | Enterprise logs can be millions of rows; fetching all 100 just to show 25 was wasteful |
| `pageSize` default 25 | Matches `recentPageSize` constant already in the component |
| `page` clamp to `totalPages` (not 400) | Prevents broken page state when date filter is tightened |
| `loadRecent()` extracted from `load()` | Allows page-change to reload only recent calls without re-fetching summary |
| Client-side provider/status filter kept for phase scope | Server-side filtering of those dimensions deferred to a future phase |

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS — 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | PASS — 1977/1977 (arch 3, unit 1248, integration 726) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 813/813 |

Test count delta vs 10U-3:
- Backend: +10 (pagination integration tests)
- Frontend: +8 (pagination component tests)

---

## Risks / unresolved questions

- Client-side provider/status filters apply only to the current page's 25 items. A future phase should add server-side provider/status query params to make those filters accurate across all pages.
- No export yet. Enterprise admins may need CSV export of filtered/paged results — deferred.

---

## Final verdict

Phase 10U-4 complete. No migration. No provider routing change. No usage governance change. All gates green.

## Next recommended action

Commit and push as `phase 10u ai usage recent calls pagination`.
