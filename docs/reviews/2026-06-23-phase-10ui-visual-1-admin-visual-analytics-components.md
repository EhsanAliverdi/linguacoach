# Phase 10UI-VISUAL-1 — Admin Visual Analytics Components

**Date:** 2026-06-23
**Sprint / Feature:** 10UI-VISUAL-1
**Verdict:** Complete — all gates passed

---

## Summary

Added a reusable admin visual analytics/widget component layer to make the admin UI visually match the SpeakPath reference design. The reference uses rich visual dashboard widgets (graphs, ring charts, progress bars, timeline feeds) instead of plain cards and tables.

---

## New Components

All created under `src/LinguaCoach.Web/src/app/design-system/admin/components/`.

### sp-admin-mini-bar-chart
CSS/SVG mini bar chart. Inputs: `items: MiniBarItem[]`, `tone`, `height`, `showLabels`, `title`, `ariaLabel`. Normalises bars to 100% of max. Handles empty state. No external chart library.

### sp-admin-breakdown-bars
Horizontal progress rows. `BreakdownBarItem` has `label`, `value`, `pct`, `tone?`, `badge?`. ARIA `role="progressbar"` with `aria-valuenow/min/max`. Tone fill classes cover indigo/green/violet/amber/teal/slate/danger.

### sp-admin-ring-metric
SVG donut/ring chart via `stroke-dasharray`/`stroke-dashoffset`. Inputs: `pct`, `label`, `sub`, `tone`, `size` (default 72), `displayValue`, `ariaLabel`. `ngOnChanges()` auto-derives `displayValue` from `pct` if not provided.

### sp-admin-event-feed
Timeline event list with dot-and-line design. `EventFeedItem` maps `level`, `category`, `message`, `timestamp`, `correlationId`. Error/Warning show badges; Information does not. Loading and empty states supported.

### sp-admin-visual-placeholder
Honest "not available yet" placeholder. States: `not-available`, `not-implemented`, `foundation-only`, `coming-later`, `deferred`. Dashed border, icon SVG varies by state. Replaces inline badge-based placeholders.

---

## Pages Updated

| Page | Changes |
|------|---------|
| Admin AI Usage (`/admin/usage`) | Replaced inline `.sp-au-mini-bar` loop with `<sp-admin-mini-bar-chart>`. Added `trendItems` computed from real `trendBuckets()` signal. |
| Admin Dashboard (`/admin`) | Replaced CEFR distribution custom bar with `sp-admin-breakdown-bars`. Replaced onboarding funnel text list with `sp-admin-breakdown-bars`. Replaced four badge-based placeholder cards with `sp-admin-visual-placeholder`. Added `cefrBreakdownItems` and `onboardingFunnelItems` computed signals using real student data. |
| Admin Diagnostics (`/admin/diagnostics`) | Added `sp-admin-event-feed` summary widget showing top 8 real events above the system status card. Added `recentFeedItems` computed from real `events()` signal. |
| Admin Curriculum (`/admin/curriculum`) | Added `sp-admin-ring-metric` for active/total objectives ratio. Added `sp-admin-breakdown-bars` for CEFR distribution. Both use real `allObjectives()` and `coverageSummary()` signals. |
| Admin Exercise Types (`/admin/exercise-types`) | Added `sp-admin-ring-metric` for ready/total exercise types ratio. Added `sp-admin-breakdown-bars` for skill coverage. Both use real `exerciseTypes()` and `typeSummary()` signals. |
| Admin Student Detail (`/admin/students/:id`) | Replaced pool health `dl` tables with `sp-admin-ring-metric` (ready/target) and `sp-admin-breakdown-bars` (ready/queued/shortfall/failed/stale) for both Today Lesson and Practice Gym pools. All data from real `poolHealth()` signal. |

---

## Data Constraints Observed

- No mock data used at any point.
- No new backend APIs added.
- Every widget uses real signals or shows an honest placeholder.
- Score distribution, AI spend by type, session duration, streak leaderboard remain as `sp-admin-visual-placeholder` (state `not-available`) because no backend endpoint exists.

---

## Files Changed

### New files
- `src/LinguaCoach.Web/src/app/design-system/admin/components/mini-bar-chart/sp-admin-mini-bar-chart.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/mini-bar-chart/sp-admin-mini-bar-chart.component.spec.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component.spec.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/ring-metric/sp-admin-ring-metric.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/ring-metric/sp-admin-ring-metric.component.spec.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/event-feed/sp-admin-event-feed.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/event-feed/sp-admin-event-feed.component.spec.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component.spec.ts`

### Modified files
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

---

## Gates

- `git diff --check`: clean
- Production build: passed (warnings only — pre-existing empty sub-selector CSS notices)
- Unit tests: 1309/1309 passed (3 specs updated to match new HTML labels)

---

## Risks / Unresolved

- The four dashboard placeholder cards (score distribution, AI spend by type, session duration, streak leaderboard) remain non-functional pending backend endpoints.
- The diagnostics event feed summary shows top 8 events loaded into the client; it is not a live server-push feed.

---

## Next Recommended Action

Export the five new components from the admin design system barrel (`src/LinguaCoach.Web/src/app/design-system/admin/index.ts`) if other features need to reference them via the barrel import path.
