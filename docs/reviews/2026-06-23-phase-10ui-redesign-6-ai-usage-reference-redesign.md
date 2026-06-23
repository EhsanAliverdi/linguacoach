# Engineering Review — Phase 10UI-REDESIGN-6: AI Usage Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-6
**Commit:** 49d9d2e

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-6 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/ai-usage.jsx` — UsageKpi cards, AreaChart (cost over time), BarChart (activities per day), Heatmap (student engagement), date range pills (7d/30d/90d)

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts`

---

## Reference gap analysis

| Reference section | Status | Notes |
|---|---|---|
| UsageKpi card row (Total cost, Total API calls, Avg cost per student) | Upgraded | 4-tile `sp-admin-kpi-card` strip with real data: total requests, total cost, success rate, failed calls. Avg cost per student deferred — no per-student breakdown in current summary API. |
| Date range pills (7d / 30d / 90d) | Added | 3 pill buttons (7d / 30d / All) rendered above filter bar. `onPillClick()` maps to existing `onPeriodChange()`. Full select preserved for custom/today/month presets. |
| Area chart — cost over time | Not implemented — partial | Mini proportional bar chart rendered from real `trendBuckets().callCount` data. No charting library added. Chart view note not needed — bars are clearly visual. |
| Bar chart — activities per day | Not implemented — explicit card | "Backend not available yet" card added. Activity completion by day requires a separate analytics endpoint. |
| Heatmap — student engagement | Not implemented — explicit card | "Backend not available yet" card added. Per-student daily engagement requires a separate analytics endpoint. |
| 8-tile stat-card grid | Replaced | `sp-admin-stat-card` → `sp-admin-kpi-card` (4 tiles). Token detail stats (input/output/total) now available in the trend table and recent calls table instead. |

---

## Changes made

### Component (.ts)

**Replaced:** `SpAdminStatCardComponent` import and decorator entry → `SpAdminKpiCardComponent`.

**Styles updated:**
- `.sp-au-stat-grid` 8-column grid removed.
- `.sp-au-kpi-strip` 4-column grid added.
- `.sp-au-period-pills`, `.sp-au-pill`, `.sp-au-pill--active` added for pill button styling.
- `.sp-au-mini-bars`, `.sp-au-mini-bar` added for the proportional bar chart.
- `.sp-au-not-impl` added for placeholder card text.

**New computed signals:**

```typescript
readonly kpiSummary = computed(() => {
  // totalCalls, totalCostUsd, successRate (0-100 rounded), failedCalls
  // Returns null when summary not loaded
});

readonly trendBars = computed(() => {
  // Proportional bar heights 0-48px from trendBuckets().callCount
  // max bucket gets height=48; others scaled proportionally; min height 2px
});
```

**New helpers:**

```typescript
readonly periodPillOptions = [
  { value: '7d', label: '7d' },
  { value: '30d', label: '30d' },
  { value: 'all', label: 'All' },
];

onPillClick(value: PeriodPreset): void {
  this.periodPresetValue = value;
  this.onPeriodChange(value);
}
```

### Template (.html)

- Period pill buttons rendered before the filter bar. Aria: `aria-label="Quick date range selection"`, each button `[attr.aria-pressed]`.
- KPI strip: 4× `sp-admin-kpi-card` with projected SVG icon (`slot="icon"`) and projected text value. `aria-label="AI usage summary"` on strip container.
- "Usage trend" card renamed to "Calls over time". Mini bar chart (`.sp-au-mini-bars`) rendered above existing trend table when data available. Mini bar: `aria-label` on each bar with date and call count.
- Two placeholder cards in `.sp-au-two-col`: "Activities per day" and "Student engagement" — each with `aria-label="… not implemented"` and "Backend not available yet" text.

### Spec (.spec.ts)

**Updated stale tests (3):**
- `renders stat cards after summary loads` — now checks `sp-admin-kpi-card` count ≥ 4.
- `renders input/output/total token stat card` tests — replaced with `renders total requests/cost/failed calls kpi card` using label attribute check.
- `summary cards still render after filter alignment change` — updated selector to `sp-admin-kpi-card`.

**New tests (15):**
- `summary strip has aria-label "AI usage summary"`
- `kpiSummary computed returns correct totalCalls`
- `kpiSummary computed returns correct totalCostUsd`
- `kpiSummary computed calculates successRate correctly`
- `kpiSummary successRate is 0 when totalCalls is 0`
- `kpiSummary returns null when summary not loaded`
- `period pill buttons render`
- `period pill container has aria-label`
- `onPillClick updates periodPreset and calls load`
- `trendBars returns empty array when no buckets`
- `trendBars returns proportional heights from real data`
- `mini bar chart renders when trend data available`
- `activities per day card renders with "not implemented" text`
- `student engagement card renders with "not implemented" text`
- `no API key or secret text rendered anywhere`

---

## Real data / honest labels

| Section | Source | Notes |
|---|---|---|
| KPI strip — total requests | `summary().totalCalls` | Real |
| KPI strip — total cost | `summary().totalCostUsd` | Real |
| KPI strip — success rate | Derived: `successfulCalls / totalCalls` | Honest derivation |
| KPI strip — failed calls | `summary().failedCalls` | Real |
| Mini bar chart | `trendBuckets().callCount` per day | Real |
| Activities per day card | None | "Backend not available yet" |
| Student engagement card | None | "Backend not available yet" |

No fake data introduced anywhere.

---

## Behaviours preserved

- Period select (all time / today / 7d / 30d / this month / custom) — unchanged
- Custom date range inputs + validation — unchanged
- By-provider and by-feature summary tables — unchanged
- Zero-cost alert — unchanged
- Usage trend table (all columns) — unchanged
- Recent calls table with all column filters — unchanged
- Export CSV with active filters — unchanged
- Student filter — unchanged
- Pagination — unchanged
- Error and loading states — unchanged

---

## Security

No API keys, secrets, bearer tokens, or sensitive values rendered. Confirmed via test: `no API key or secret text rendered anywhere`.

---

## Tests

**Total: 1221/1221 PASS.**
15 new tests added (net after 3 stale tests updated).

---

## Gates passed

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1221/1221 PASS |
| Backend build | Not run (no backend changes) |
| Playwright | Not run (no E2E specs for this page) |

---

## Decisions made

1. Mini bar chart derived from `trendBuckets().callCount` — no charting library added. Proportional heights give a clear visual trend without dependency risk.
2. `kpi-card` value is content-projected text, not a `[value]` input — matches the `SpAdminKpiCardComponent` design (no `value` @Input defined).
3. Period pills use 7d / 30d / All — reference uses 7d / 30d / 90d but we don't have a 90d preset in `PeriodPreset`. "All" maps to the existing `all` preset and is the honest alternative.
4. Activities per day and student engagement kept as "Backend not available yet" — the reference charts use mock data (`window.ADMIN_DATA`). No fake activity counts shown.
5. Token detail (input/output/total) no longer in the KPI strip — these are available in the trend table and recent calls table. Removing them from the KPI strip reduces visual noise and matches the reference more closely.

---

## Deferred gaps

- Avg cost per student KPI tile: requires per-student breakdown in `AiUsageSummary`. Not in current backend contract.
- 90d period pill: requires adding `'90d'` to `PeriodPreset` and `buildRange()`. Deferred — "All" provides equivalent long-range view.
- Area line chart (cost over time): no charting library present. Mini bar chart using call counts is a reasonable approximation from real data.
- Activities per day backend endpoint: out of scope.
- Student engagement backend endpoint: out of scope.

---

## Final verdict

**Complete.** 1221/1221. Build clean. All security constraints satisfied. No fake data. No backend changes. All existing behaviour preserved.

---

## Next recommended action

**10UI-REDESIGN-7** — Notifications and Integrations redesign.

See: `docs/design/admin-reference-alignment.md` — `/admin/notifications` and `/admin/integrations` rows.
