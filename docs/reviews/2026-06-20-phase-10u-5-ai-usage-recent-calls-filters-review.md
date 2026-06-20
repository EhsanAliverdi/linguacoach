# Phase 10U-5 — AI Usage Recent Calls Filters — Engineering Review

**Date:** 2026-06-20
**Related sprint:** Phase 10U-5 (current sprint)
**Predecessor:** docs/reviews/2026-06-20-phase-10u-4-ai-usage-recent-calls-pagination-review.md

---

## Files changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — Added `AiUsageRecentFilter` record with `Provider`, `Model`, `FeatureKey`, `Status` fields and `HasInvalidStatus` guard; updated `IAdminAiUsageHandler.GetRecentAsync` signature
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — Added `ApplyRecentFilter` helper; wired into `GetRecentAsync` after date filter, before count/skip/take
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — Added `provider`, `model`, `featureKey`, `status` query params; returns 400 on invalid status
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageRecentFilterTests.cs` — New file, 12 integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — Added `AiUsageRecentCallFilter` interface; `getRecent` accepts optional 4th param
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — Server-side filter signals; `onRecentProviderChange`, `onRecentModelChange`, `onRecentFeatureChange`, `onRecentStatusChange`, `clearRecentFilters`, `hasActiveRecentFilters`; `SpAdminButtonComponent` imported
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — Four-filter bar (provider, model, feature, status) with conditional "Clear filters" button
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — Old client-side filter tests replaced with server-side equivalents; 12 new filter tests added

---

## Status filter semantics

| Status param | EF Core predicate | Meaning |
|---|---|---|
| `success` | `WasSuccessful && !IsFallback` | Clean successful call, no fallback involved |
| `failed` | `!WasSuccessful` | Call failed regardless of fallback |
| `fallback` | `IsFallback` | Fallback was used (may be successful or failed) |

Invalid status (any other string) returns HTTP 400 with `{ error: "..." }`.

---

## Summary totals

Not changed. `GetSummaryAsync` is unchanged. Summary stat cards always reflect the full date-filtered dataset regardless of recent-call column filters. Summary filtering is deferred as future work.

---

## Filter option derivation

- **Provider options:** union of `summary.byProvider` providers + `recentItems` providers — no extra API call needed
- **Model options:** derived from loaded `recentItems` only — models are too dynamic for a static list
- **Feature options:** union of `summary.byFeature` features + `recentItems` features — formatted via `featureLabel()`
- **Status options:** static list (`success`, `failed`, `fallback`)

---

## Decisions made

| Decision | Rationale |
|---|---|
| 400 on invalid status | Consistent with existing controller style (date filter also returns 400 on invalid range) |
| Filters apply before `CountAsync`/`Skip`/`Take` | `totalCount`/`totalPages` must reflect the filtered universe |
| `filteredRecentItems` computed is now a passthrough | Filtering is server-side; client-side slice removed to avoid confusion |
| `clearRecentFilters` does not reset date period | Clear is scoped to column filters only, per spec |
| No separate metadata endpoint | Provider/model/feature options derived from existing summary + loaded items |
| Summary totals not changed | Spec says "keep summary cards stable unless filter intentionally applied to both" — deferred |

---

## Gate results

| Gate | Result |
|---|---|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS — 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | PASS — 1988/1988 (arch 3, unit 1248, integration 737) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 823/823 |

Test count delta vs 10U-4:
- Backend: +12 (recent-call filter integration tests)
- Frontend: +10 (server-side filter component tests; 3 old client-side tests replaced)

---

## Risks / deferred work

- Summary totals do not reflect column filters. Future phase can add `provider`/`model`/`featureKey`/`status` params to the summary endpoint if needed.
- Model options are derived from the current page of recent items only. A dedicated metadata endpoint or a broader "get distinct models" query would improve this.
- Student filter not included in this phase — deferred to 10U-6.

---

## Final verdict

Phase 10U-5 complete. No migration. No provider routing change. No usage governance change. All gates green.
