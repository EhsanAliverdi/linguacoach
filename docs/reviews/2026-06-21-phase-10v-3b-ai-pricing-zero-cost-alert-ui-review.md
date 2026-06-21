# Phase 10V-3B — AI Pricing Zero-Cost Alert UI — Engineering Review

**Date:** 2026-06-21
**Sprint / Feature:** Phase 10V-3B — AI Pricing Zero-Cost Alert UI
**Status:** Complete

---

## Related sprint

`docs/sprints/current-sprint.md` — Phase 10V-3B

---

## Files changed

### Backend

| File | Change |
|------|--------|
| `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` | Added `ZeroCostCallCount` and `ZeroCostTotalTokens` to `AiUsageSummaryDto` |
| `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` | Computes zero-cost fields in `GetSummaryAsync` after applying all filters |
| `src/LinguaCoach.Api/Controllers/AiUsageController.cs` | Exposes `zeroCostCallCount` and `zeroCostTotalTokens` in summary JSON response |

### Frontend

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` | Added `zeroCostCallCount` and `zeroCostTotalTokens` to `AiUsageSummary` interface |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` | Added `sp-admin-alert variant="warning"` block when `zeroCostCallCount > 0` |

### Tests

| File | Change |
|------|--------|
| `tests/LinguaCoach.UnitTests/Admin/AiUsageSummaryTests.cs` | Refactored to `MakeDto` helper; added 2 tests for zero-cost fields |
| `tests/LinguaCoach.IntegrationTests/Api/AiUsageSummaryFilterTests.cs` | Added 5 integration tests for zero-cost summary fields and filter behaviour |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` | Updated `makeSummary` factory; added 5 Karma tests for zero-cost alert |

---

## Zero-cost definition used

A zero-cost call is one where:

- `CostUsd == 0m`
- AND `(InputTokens + OutputTokens) > 0`

Zero-token calls (e.g. failures before any tokens were consumed) are excluded. This avoids alerting on expected zero-token failed calls.

---

## Backend summary fields added

- `ZeroCostCallCount` — count of zero-cost calls matching active filters
- `ZeroCostTotalTokens` — sum of `InputTokens + OutputTokens` across those calls

Both fields are computed in `AiUsageHandler.GetSummaryAsync` client-side after the DB query, using the same filtered `logs` list already fetched for other aggregations. No additional DB round-trip.

---

## Filters respected

Yes. Zero-cost computation runs on the already-filtered log set. All filters apply:

- `from` / `to` date range
- `provider`
- `model`
- `featureKey`
- `status`
- `studentId`

---

## Historical AiUsageLog recalculation

No. Zero-cost fields reflect logged `CostUsd` values as written. Old rows with `CostUsd = 0` because pricing was missing are counted; rows with a price at log time are not recounted even if the price was later changed.

---

## Frontend alert

Added below the stat grid in `admin-ai-usage.component.html`:

```html
@if (summary()!.zeroCostCallCount > 0) {
  <sp-admin-alert variant="warning">
    N AI call(s) in this filtered range were logged with $0 cost
    (X tokens). This usually means pricing was missing at the time of the call.
  </sp-admin-alert>
}
```

- Uses existing `sp-admin-alert` component.
- Alert text is clear and non-alarming.
- Alert disappears when `zeroCostCallCount` is 0.
- Alert updates whenever filters or date range change (bound to `summary()` signal).

---

## Findings by priority

### P0 — Blocking

None.

### P1 — Correctness

None found. The zero-cost filter correctly excludes zero-token calls and non-zero-cost calls.

### P2 — Notes

- The `AiUsageController` summary endpoint builds an anonymous object manually. The two new fields were added there to match the existing pattern. No separate response DTO was introduced to avoid unnecessary abstraction.
- `makeSummary` test factory was updated with `zeroCostCallCount: 0` and `zeroCostTotalTokens: 0` defaults to keep all 891 existing tests compiling without changes.

---

## Decisions made

- Zero-cost definition: cost == 0 AND tokens > 0. Excludes zero-token failures.
- No historical recalculation. Alert is based on logged rows only.
- No link to AI Config pricing panel deferred (routing not trivially available from the usage page).
- No schema migration required.

---

## Risks / unresolved questions

- TODO-10V-UNIQUE-CONSTRAINT remains deferred: optional unique index on `(ProviderName, ModelName, EffectiveFromUtc)` for `ai_model_pricing_overrides`.

---

## Implementation tasks produced

None. All work delivered in this phase.

---

## Final verdict

Complete. All gates pass.

---

## Next recommended action

Commit and close 10V-3B. Remaining AI pricing TODO: `TODO-10V-UNIQUE-CONSTRAINT` (optional, deferred).

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors, 7 pre-existing warnings) |
| `dotnet test --configuration Release` | PASS (2080/2080 — 3 arch + 1262 unit + 815 integration) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (896/896) |

---

## Documentation impact

- Docs reviewed: `docs/sprints/current-sprint.md`, `TODOS.md`
- Docs updated: `docs/sprints/current-sprint.md`, `TODOS.md`, this review
- Docs intentionally not updated: `docs/handoffs/current-product-state.md` — minor summary-field addition does not change product state description materially
- Reason: No API contract change visible to end users; no new page or feature; no architecture change.
