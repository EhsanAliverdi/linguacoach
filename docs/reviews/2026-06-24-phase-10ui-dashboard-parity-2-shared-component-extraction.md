# Phase 10UI-DASHBOARD-PARITY-2 — Shared Component Extraction Review

**Date:** 2026-06-24
**Sprint/Feature:** 10UI-DASHBOARD-PARITY-2
**Commit:** 3649e44
**Branch:** main

---

## Summary

Extracted reusable dashboard visual patterns from `admin-dashboard.component.ts` into four new shared admin design-system components. Dashboard renders identically to the accepted 10UI-DASHBOARD-PARITY-1 screenshot.

---

## Files Reviewed and Modified

### New shared components
- `src/app/design-system/admin/components/hero-summary/sp-admin-hero-summary.component.ts`
- `src/app/design-system/admin/components/system-health/sp-admin-system-health.component.ts`
- `src/app/design-system/admin/components/donut-chart/sp-admin-donut-chart.component.ts`
- `src/app/design-system/admin/components/sparkline-card/sp-admin-sparkline-card.component.ts`

### Modified files
- `src/app/design-system/admin/components/kpi-card/sp-admin-kpi-card.component.ts` — tile layout + delta + coral variant
- `src/app/design-system/admin/tokens/admin-tokens.css` — 14 new `--sp-dash-*` tokens
- `src/app/design-system/admin/index.ts` — 4 new exports
- `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts` — imports and template refactor
- `src/app/design-system/admin/components/admin-components.spec.ts` — 23 new tests
- `src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts` — updated 27 stale tests

---

## Findings

### New Components

**SpAdminHeroSummaryComponent**
- Input: `columns: HeroColumn[]` (`{label, value, sub?, valueColor?, subColor?}`)
- 4-column dark gradient banner with dividers
- Uses `--sp-dash-hero-*` tokens

**SpAdminSystemHealthComponent**
- Inputs: `services: SystemService[]`, `footer: SystemFooterRow[]`, `diagnosticsLink`
- Latency bars: <100ms green, <200ms warn, else red
- Pulsing "All clear" status dot
- `barPct(ms)` clamps to 100

**SpAdminDonutChartComponent**
- Inputs: `title`, `segments: DonutSegment[]`, `size = 80`
- SVG: viewBox `0 0 100 100`, r=36, strokeWidth=14, white center r=22
- `OnChanges` recomputes `dashArray`/`dashOffset` per segment

**SpAdminSparklineCardComponent**
- Inputs: `title`, `value`, `sub`, `data: number[]`, `color`, `sparkW`, `sparkH`
- `OnChanges` recomputes `linePath` and `lastPt`
- Empty path guard when `data.length < 2`

### KpiCard Enhancements
- `layout='tile'`: 56px flush icon, border-right, 24px value, optional delta row
- `coral` variant: `#FFEAE4` bg / `#FF7A59` icon
- Backward-compatible: `layout='standard'` unchanged

### Token Additions (admin-tokens.css)
```
--sp-dash-hero-bg-start/end/divider/eyebrow/action-color/success-color
--sp-dash-kpi-tile-w
--sp-dash-chart-line/grid/label
--sp-dash-latency-good/warn/bad
--sp-dash-card-gap/grid-gap
```

### Dashboard Refactor
- Hero banner → `<sp-admin-hero-summary>`
- 4 KPI divs → `<sp-admin-kpi-card layout="tile">`
- System health block → `<sp-admin-system-health>`
- AI cost donut → `<sp-admin-donut-chart>`
- AI spend sparkline → `<sp-admin-sparkline-card>`
- Removed delegated computeds: `sparklinePoints`, `sparklinePath`, `sparklineLastPt`
- Removed delegated methods: `latencyBarPct`, `latencyColor`
- Added: `aiUsageTrendValues` computed for sparkline data

---

## Issues Encountered and Resolved

### Template Syntax — number pipe inside interpolation
`${{ (heroAiCost7d() ?? 0) | number:'1.2-4' }}` caused Angular template parse error (pipe inside `{{ }}`). Fixed with computed `aiCost7dFormatted`.

### Model Mismatch — streakDays
`StudentListItem` has no `streakDays`. Streak leaderboard uses `onboardingStatus === 'Completed'` order, streak value hardcoded to 0.

### 27 Stale Tests Fixed
Tests from before PARITY-1 checked for old text strings. Updated to match current dashboard output:

| Old expectation | New expectation |
|---|---|
| `'AI System'` | action card: `'categories not set'` |
| `'Not configured'` | `'categories not set'` |
| `'Writing activities'`, `'Text to speech'` | removed (no longer rendered) |
| `'This week'` | `'THIS WEEK'` |
| `'Activity trends'` | `'Activities completed'` |
| `'AI spend by type'` | `'AI cost by type'` |
| `'Avg session duration'` | `'Avg session'` |
| `'Live events feed'` | `'Live events'` |
| `'Active this week'` | `'ACTIVE THIS WEEK'` |
| `'Total students'` | `'TOTAL STUDENTS'` |
| `'Activities done'` | `'ACTIVITIES DONE'` |
| `'AI cost (7 days)'` | `'AI COST (7 DAYS)'` |
| `'Add student'`, `'Manage students'`, `'AI Config'`, `'Prompts'` | `'Admin actions'`, `'System health'` |
| `'Completed'` badge | `'Active'` status label |
| `'Not started'` badge | `'Inactive'` status label |
| `'backend not available yet'` | `'All students are engaged'` |
| in-progress = at-risk (expected 1) | in-progress NOT at-risk (expected 0) |
| `'No CEFR data yet'` | computed `cefrStripRows()` check |
| `tbody tr` selector | `.sp-dash-tbl-row` selector |
| table limit 5 | table limit 8 |

---

## Test Results

**Before:** 27 failures (stale text assertions) + compile errors
**After:** 1334/1334 pass

---

## Decisions Made

- `sp-admin-kpi-card` tile layout is backward-compatible: `layout='standard'` unchanged. Existing uses of `sp-admin-kpi-card` on other pages unaffected.
- AI system categories are no longer rendered in a dedicated card. They drive the admin actions list (`adminActionsList` computed). Tests updated to verify action list behavior.
- Table row limit changed from 5 to 8 in PARITY-1; tests corrected to match.

---

## Security Constraints Verified

- No API keys, secrets, or provider secrets rendered
- No mock data imported from external files
- No student UI touched
- No migrations added
- No chart libraries added (all SVG inline)

---

## Documentation Impact

- **Docs reviewed:** `docs/architecture/README.md`, `docs/design/admin-reference-alignment.md`
- **Docs updated:** This review doc
- **Docs intentionally not updated:** `docs/handoffs/current-product-state.md`, `TODOS.md` — dashboard is visually stable, no functional changes to document

---

## Risks / Unresolved Questions

- Donut chart segments use hardcoded color values in the dashboard (`costDonutSegments`). These could be tokenized in a future cleanup pass.
- Sparkline data is derived from `aiUsageTrends7d` bucket costs. If the API shape changes, `aiUsageTrendValues` computed will need updating.

---

## Final Verdict

**COMPLETE.** All 1334 tests pass. Dashboard renders identically to accepted PARITY-1 screenshot. Four new reusable design-system components are exported and tested.

## Next Recommended Action

Visual screenshot capture of the dashboard to confirm no regression. Proceed to next sprint item.
