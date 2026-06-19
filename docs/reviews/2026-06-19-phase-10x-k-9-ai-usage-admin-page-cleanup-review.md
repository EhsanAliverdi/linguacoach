# Phase 10X-K-9 — AI Usage Admin Page Cleanup Review

**Date:** 2026-06-19
**Sprint/Phase:** 10X-K-9
**Author:** Claude (automated, reviewed via gates)

---

## Files Changed

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` | Refactored template — filter bar, improved recent calls table, provider/feature summary improvements |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` | Added filter signals, `FormsModule`, `SpAdminFilterBarComponent`, `SpAdminSelectComponent`, improved `formatDateTime` |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` | Created (new file) — 14 behavioral tests |

---

## Page Improvements Made

### Stat grid

- No change in layout or data — already correct.
- Moved margin/gap into scoped CSS class (`sp-au-stat-grid`) instead of global `sp-admin-stat-grid`.

### By provider summary

- Provider name now rendered as `sp-admin-code-pill` instead of raw capitalized text — consistent with other technical identifier patterns.
- OK and Fallback count cells use `sp-admin-badge` (success / warning tones) for quick visual scanning.
- Numeric columns right-aligned with `sp-au-num` class.
- Cost rendered in monospace font.

### By feature summary

- Feature display name shown via `sp-admin-truncated-text` (36 chars).
- Raw feature key shown underneath as `sp-admin-code-pill` — avoids lossy label-only display.
- OK count uses `sp-admin-badge` for visual consistency with provider table.

### Recent calls table

- **Filter bar added** with provider select (dynamically derived from loaded data) and status select (OK / Failed / Fallback).
- Filters reset page to 1 on change.
- Empty state shown when filters yield no results.
- **Columns reduced from 8 to 8** but reorganised for clarity:
  - `Provider` and `Model` consolidated into one cell (provider pill + model pill below).
  - `Fallback` column removed as standalone — merged into Status cell as a stacked badge.
  - `Tokens in/out` column **added** (was available in data, not previously shown).
  - `Cost` column **added** per-row (was available in data, not previously shown).
- `Status` cell now shows both success/failure badge and fallback badge together, plus failure reason text if present.
- `Time` column now shows `dd-Mon hh:mm:ss` (date + time) instead of time only — useful when data spans multiple days.
- Duration rendered in monospace.
- Correlation ID unchanged — `sp-admin-copyable-text` retained.
- `minWidth` reduced from `1100px` to `1080px` (marginal, but columns are better balanced).

---

## Wrappers / Helpers Used

All previously imported. New additions to this component:

- `sp-admin-filter-bar` — provider + status filter row above recent calls table
- `sp-admin-select` — two filter selects (provider, status)
- `FormsModule` — required for `[(ngModel)]` on select

Retained:
- `sp-admin-page-header`, `sp-admin-page-body`
- `sp-admin-stat-card`, `sp-admin-card`
- `sp-admin-table`, `sp-admin-pagination`
- `sp-admin-badge`, `sp-admin-code-pill`, `sp-admin-truncated-text`, `sp-admin-copyable-text`
- `sp-admin-loading-state`, `sp-admin-error-state`, `sp-admin-empty-state`

---

## Pagination / Filter Changes

- `filteredRecentItems` computed signal applies provider and status filters.
- `recentTotalPages` and `pagedRecentItems` now derive from `filteredRecentItems` (not raw `recentItems`).
- Page resets to 1 on either filter change.
- No server-side filtering added — all local.

---

## Build Result

```
npm run build -- --configuration production
✔ Output location: dist/lingua-coach.web
RESULT: PASS (warnings: pre-existing empty sub-selector CSS warnings only)
```

---

## Angular Test Result

```
npm test -- --watch=false --browsers=ChromeHeadless --include=**/admin-ai-usage/*.spec.ts
TOTAL: 14 SUCCESS
```

Tests created (all new — no prior spec existed):

- `renders page header`
- `renders stat cards after summary loads`
- `renders provider and feature summary tables`
- `renders recent calls table with rows`
- `renders filter bar for recent calls`
- `filters recent calls by provider`
- `filters recent calls by status: failed`
- `filters recent calls by status: fallback`
- `resets page to 1 when provider filter changes`
- `shows error state when summary fails`
- `shows empty state when no recent items`
- `featureLabel formats underscore keys to title case`
- `formatDateTime returns a non-empty string for valid ISO`
- `providerOptions derives unique sorted providers from recent items`

---

## Playwright Result

Not run. No existing Playwright tests exist for the AI Usage admin page. Scope exclusion confirmed.

---

## Remaining AI Usage Issues

The following are out of scope for this phase:

- No date-range filter (would require backend support).
- No token cost breakdown by model/tier.
- No charts or trend visualisation.
- No per-student usage drill-down.
- Cost column in recent calls table shows raw `costUsd` with 4 decimal places — acceptable for now; future work could normalise to milli-dollars for low-cost calls.

---

## Decisions Made

| Decision | Reason |
|----------|--------|
| Merge Provider + Model into one cell | Reduces column count; both are short technical values that read together |
| Merge Fallback badge into Status cell | Fallback is a sub-state of success; grouping reduces visual noise |
| Add Tokens in/out and Cost columns | Data was already in `AiUsageRecentItem` but not displayed; adds diagnostic value |
| Provider filter options from loaded data | Adapts to whatever providers the system actually uses; avoids hardcoding |
| Show date + time in Time column | Time-only display is ambiguous when data spans days |
| Feature key shown as code-pill under label | Avoids lossy label-only display for unknown/new feature keys |

---

## Confirmation

- **No backend changes.** No API contracts altered. No `.NET` files touched.
- **No product behavior changed.** Service calls identical (`getSummary`, `getRecent(100)`).
- **No commit or push performed.**

---

## Next Recommended Action

Continue to next 10X-K cleanup task. No AI Usage issues remain that block further admin page work.
