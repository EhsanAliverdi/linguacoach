# Phase 10U-10 — AI Usage Custom Date Range Picker — Engineering Review

**Date:** 2026-06-21
**Sprint / Feature:** Phase 10U-10 — AI Usage Custom Date Range Picker
**Status:** Complete

---

## Files changed

### Frontend only

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
  - `PeriodPreset` union extended with `'custom'`
  - `SpAdminInputComponent` added to imports and `imports[]`
  - `customFrom`, `customTo`, `customRangeError` signals added
  - `customFromValue`, `customToValue` two-way-bind mirrors added
  - `periodOptions` extended with `{ value: 'custom', label: 'Custom range' }`
  - `onPeriodChange` no longer calls `load()` when switching to `'custom'`; clears `customRangeError` on switch away
  - `applyCustomRange()` — validates both required, from <= to; sets error or calls `load()`; resets page to 1
  - `clearCustomRange()` — resets all custom date state
  - `buildRange('custom')` — converts `yyyy-MM-dd` strings to UTC ISO: from = `T00:00:00Z`, to = start of day+1 (exclusive upper bound consistent with backend behavior from 10U-3)

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html`
  - Custom From/To `sp-admin-input type="date"` inputs rendered when `periodPreset() === 'custom'`
  - Apply button triggers `applyCustomRange()`
  - Clear dates button (shown when either field has a value) triggers `clearCustomRange()`
  - `sp-admin-alert variant="error"` rendered below filter bar when `customRangeError()` is set

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts`
  - 13 new custom range tests added

---

## Findings

### Priority 1 (blocking) — none

### Priority 2 (significant) — none

### Priority 3 (minor)

**No backend changes required.** Backend already accepts `from`/`to` on all four endpoints and returns 400 for invalid ranges. Frontend validates before calling.

**Exclusive upper bound** — `buildRange('custom')` adds 1 day to the `To` date to produce the exclusive upper bound. This matches the existing backend behavior documented in 10U-3, where `To` is treated as an exclusive upper bound.

---

## Decisions made

1. `onPeriodChange('custom')` does not immediately call `load()` — requires an explicit Apply so the user can set both dates first.
2. Switching from `'custom'` to any preset immediately reloads with preset range, clearing the custom error.
3. `clearRecentFilters` does not clear the custom date range — it clears only column filters. Custom date is owned by the period preset control.
4. `buildRange('custom')` returns `undefined` when either field is empty — callers (summary, recent, trends) treat `undefined` range as "all time", preventing accidental full-table scans during partial input.

---

## Test results

| Suite | Result |
|-------|--------|
| Angular production build | PASS (0 errors) |
| `git diff --check` | PASS |
| Angular Karma (ChromeHeadless) | 872/872 PASS (+13 new custom range, +1 updated) |

Backend gates not run — no backend changes.

---

## Risks / unresolved questions

None. No timezone selector added (deferred per spec). No chart library added.

---

## Final verdict

All gates pass. Feature is complete, tested, consistent with the admin AI Usage page patterns. No migration, no provider routing change, no usage governance change.

## Next recommended action

Proceed to next AI Usage or admin backlog item.
