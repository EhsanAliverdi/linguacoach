# Admin Chart System — Flowbite/ApexCharts Alignment Plan

**Date:** 2026-07-21
**Related sprint/feature:** Admin design system (`docs/architecture/admin-ui-design-system.md`), Sprint 14.x Skill Graph work (recent chart usage in `sp-admin-graph-card`)
**Type:** Implementation plan (from an AskUserQuestion-driven planning session, not yet executed)

## Goal

Replace the admin design system's ad-hoc, hand-rolled SVG chart components with a
single reusable ApexCharts-backed chart family, styled to match Flowbite's chart
block library (https://flowbite.com/docs/plugins/charts/), and extend chart-type
coverage to what Flowbite ships (line, column, horizontal bar, pie, radial) while
keeping donut/bar/area/mini-bar visually equivalent.

## Files reviewed

- `src/app/design-system/admin/components/bar-chart/sp-admin-bar-chart.component.ts` — hand-rolled inline SVG vertical bars, no library.
- `src/app/design-system/admin/components/area-chart/sp-admin-area-chart.component.ts` — hand-rolled inline SVG area/line, manual gridlines/ticks.
- `src/app/design-system/admin/components/donut-chart/sp-admin-donut-chart.component.ts` — hand-rolled `stroke-dasharray` donut + legend.
- `src/app/design-system/admin/components/mini-bar-chart/sp-admin-mini-bar-chart.component.ts` — CSS flexbox bars (sparkline-style), tone-based coloring.
- `src/app/design-system/admin/components/graph-card/sp-admin-graph-card.component.ts` — existing composable card shell: title/subtitle, status badge, action link, footer note, `ng-content` body. Already covers most of Flowbite's "card chrome" (header stat + link + footer) generically.
- `src/app/design-system/admin/components/heatmap/sp-admin-heatmap.component.ts`, `ring-metric`, `sparkline-card`, `kpi-card`, `stat-card` — adjacent visualization components, out of scope for the ApexCharts migration but must keep visually consistent with the new chart tokens.
- `src/app/design-system/admin/tokens/admin-tokens.css` — existing CSS custom property system (`--sp-admin-primary`, `--sp-admin-green`, `--sp-admin-text-muted`, etc.) that must drive chart colors instead of Flowbite's hardcoded Tailwind classes.
- `src/LinguaCoach.Web/package.json` — Angular 19, standalone components + signals, no chart library currently installed (`apexcharts` absent).

## Findings

- No ApexCharts or any chart library is installed. The 4 existing chart components are independent, non-generic SVG/CSS implementations with inconsistent Input APIs (`data: number[]` vs `items: MiniBarItem[]` vs `segments: DonutSegment[]`).
- Flowbite's chart examples are always **card + chart**, where the card chrome (stat number, trend badge, period dropdown, footer link) is already handled by `sp-admin-graph-card` (and `sp-admin-card`/`sp-admin-dropdown`) in this codebase. There's no need to re-invent a monolithic "chart card" — the gap is purely in the chart rendering layer and its visual polish (grid, tooltip, legend, data labels, formatter, sizing) matching Flowbite's ApexCharts defaults.
- Missing chart types vs. Flowbite's library: **line**, **horizontal bar**, **pie**, **radial/progress**. Existing: column (current "bar-chart"), donut, area, and a custom sparkline (mini-bar) with no direct Flowbite equivalent (keep as-is, restyle only).

## Decisions (AskUserQuestion + follow-up)

1. **Chart engine — Adopt ApexCharts via the official `ng-apexcharts` Angular
   wrapper package**, not a hand-written lifecycle wrapper. Rationale: matches
   Flowbite's exact look/behavior (tooltips, animations, radial/pie), and
   `ng-apexcharts` already solves the Angular lifecycle/resize/memory-leak
   concerns a custom wrapper would have to get right from scratch — writing
   our own wrapper around `new ApexCharts()` would just be re-implementing
   what the maintained package already does.
   **Version constraint found during planning**: the latest `ng-apexcharts`
   (2.4.0) requires `@angular/core` ≥20, but this repo is on Angular 19.2.
   Must pin to the **1.14.x–1.15.x line**, which targets Angular 19 and
   `apexcharts` ^4.x — install as `ng-apexcharts@^1.15` (not `latest`), and
   revisit the pin whenever the repo's Angular version is bumped.
   Tradeoff accepted: ~130KB `apexcharts` + wrapper dependency, and the 4
   existing SVG charts get rewritten on top of it rather than patched in place.
2. **Component scope — flexible/composable, not a rigid full card.** User's own
   words: "chart should support everything that is visible in flowbite. we
   should be able to use it wherever we want." Interpreted as: each chart stays
   a **pure, richly-parameterized chart component** (not hard-wired into one
   fixed card layout), so it composes freely inside `sp-admin-graph-card`,
   `sp-admin-card`, a KPI tile, a modal, or standalone — matching how the
   existing 4 charts are already consumed today. The *visual capabilities* Flowbite
   shows (grid, legend, tooltip, data labels, formatter, size, multi-series
   colors) become **Inputs** on the chart component, not separate wrapper
   components.
3. **New chart types to add:** Line, Horizontal bar, Pie, Radial/progress —
   plus, per "any other chart you think is useful", **Combo/mixed
   (bar+line)** for the "spend vs. usage" style admin views already implied by
   `sp-admin-graph-card` (`AI cost by type`, sprint 14 skill graph coverage
   widgets), since Flowbite's ApexCharts base supports it at near-zero extra
   cost once the wrapper exists.
4. **Full replacement, not backward-compatible adapters.** Follow-up decision:
   drop the dual Input surface (`data`/`segments` + new `series`) originally
   proposed to avoid touching call sites. Instead rewrite `sp-admin-area-chart`,
   `sp-admin-bar-chart`, `sp-admin-donut-chart` directly onto the new
   `series`/`categories` contract, and update every call site in the same
   change. Blast radius checked: only 2 feature areas consume these 3
   components today — `features/admin/admin-delivery-health/` and
   `features/admin/admin-ai-usage/` (plus that feature's spec) — small enough
   to migrate in one pass rather than carry legacy Inputs indefinitely.

## Architecture

### 1. Shared chart foundation (new)

- Add `ng-apexcharts@^1.15` (pinned, **not** `latest` — see Decision 1) +
  `apexcharts@^4` to `package.json`. `ng-apexcharts` provides the
  `<apx-chart>` standalone component (owns ApexCharts lifecycle: create,
  `ngOnChanges`-driven updates, destroy) so `sp-admin-chart-base` does not
  need to reimplement that.
- `src/app/design-system/admin/components/chart/chart-theme.util.ts`: reads
  `--sp-admin-*` CSS custom properties at render time (via `getComputedStyle`)
  and produces the `ApexTheme`/`ApexGrid`/`ApexTooltip`/`ApexDataLabels`/
  `ApexXAxis`/`ApexYAxis` fragments so every chart automatically matches the
  design system without each component repeating color literals. Reacts to
  the existing `AdminThemeService` (dark/light) the same way
  `sp-admin-theme-toggle` already does, re-calling the chart's
  `updateOptions()` on theme change.
- `sp-admin-chart-base.component.ts` (internal, not exported from
  `index.ts`): thin composition layer over `<apx-chart>` that merges
  `chart-theme.util` output with each public component's per-type options,
  so per-type components stay declarative (just pass `series`/`categories`
  + type-specific flags).

### 2. Public chart components (one per Flowbite chart type)

Each is a standalone Angular component under
`src/app/design-system/admin/components/*-chart/`, exported from
`design-system/admin/index.ts`, and internally composes the shared wrapper by
building an ApexCharts options object from a **common Input contract**:

```ts
export interface SpAdminChartSeries {
  name: string;
  data: number[];
  color?: string; // falls back to design-system palette by index
}

// Shared inputs across every sp-admin-*-chart component:
@Input() series: SpAdminChartSeries[] = [];      // multi-series always supported
@Input() categories: string[] = [];               // x-axis / labels
@Input() height = 200;                             // px
@Input() width: number | string = '100%';
@Input() colors: string[] = [];                    // overrides palette
@Input() showGrid = true;
@Input() showLegend = false;
@Input() legendPosition: 'top' | 'bottom' = 'bottom';
@Input() showTooltip = true;
@Input() showDataLabels = false;
@Input() showXAxisLabels = true;
@Input() showYAxisLabels = true;
@Input() valuePrefix = '';                          // e.g. '$'
@Input() valueSuffix = '';                          // e.g. '%'
@Input() decimals = 0;
@Input() emptyMessage = 'No data for this period';
@Input() ariaLabel = '';
```

Component list:

| Component | Flowbite type | Status |
|---|---|---|
| `sp-admin-area-chart` | `area` | **Rewrite** on ApexCharts (`series`/`categories` contract). Legacy `data: number[]` Input removed; the 2 consuming call sites updated in the same change (see Decision 4). |
| `sp-admin-bar-chart` (→ column semantics unchanged) | `bar` (`horizontal:false`) | **Rewrite** on ApexCharts. Legacy `data`/`labels` Inputs removed; call sites updated. |
| `sp-admin-donut-chart` | `donut` | **Rewrite** on ApexCharts. Legacy `segments: DonutSegment[]` Input removed in favor of `series`; call sites updated. |
| `sp-admin-line-chart` | `line` | **New.** |
| ~~`sp-admin-horizontal-bar-chart`~~ | `bar` (`horizontal:true`) | **Implementation deviation**: folded into `sp-admin-bar-chart` as a `[horizontal]` boolean Input instead of a separate component — ApexCharts toggles orientation via one `plotOptions.bar.horizontal` flag, so a second component would just duplicate the same options-building code for no behavioral gain. Flagged here since the original plan listed it as a distinct component. |
| `sp-admin-pie-chart` | `pie` | **New.** |
| `sp-admin-radial-chart` | `radialBar` | **New.** Candidate to eventually back `sp-admin-ring-metric`'s visual, but that component stays separate in this plan (no forced migration). |
| `sp-admin-combo-chart` | mixed `bar`+`line` | **New**, stretch/optional — only build if time allows after the 4 requested types ship. |
| `sp-admin-mini-bar-chart` | (no direct Flowbite type — sparkline) | **Restyle only**, stays CSS-based (deliberately lightweight for dense table/grid usage; ApexCharts per-cell instances would be wasteful). |

### 3. Visual chrome (header stat, dropdown, footer link)

No new wrapper card component. Flowbite's stat + trend-badge + period-dropdown
+ footer-link chrome is composed from **existing** components at each call
site:

- `sp-admin-graph-card` — title/subtitle/status/action-link/footer-note (already exists).
- `sp-admin-dropdown` — period selector ("Last 7 days" etc., already exists).
- `sp-admin-badge` / `sp-admin-stat-badge` — trend % pill (already exists).

This satisfies "use it wherever we want": the chart component never assumes
it's inside a card, so it also drops cleanly into `sp-admin-kpi-card`,
`sp-admin-modal`, a table cell, or a bare page section.

### 4. Design tokens → ApexCharts mapping

Extend `admin-tokens.css` only if a genuinely new token is needed (e.g. a
`--sp-admin-chart-grid` line color distinct from `--sp-admin-border` if the
current border token proves too dark against chart backgrounds in testing).
Otherwise reuse: `--sp-admin-primary/green/violet/amber/teal/slate` (+ `-bg`
variants) as the default multi-series palette, in that order, matching the
existing `MiniBarTone` palette so bar/line/donut/pie all draw from one
consistent color sequence.

## Migration approach

1. Add `ng-apexcharts`/`apexcharts` deps; land the shared foundation
   (`chart-theme.util.ts`, `sp-admin-chart-base`) — no visible change yet.
2. Rewrite `area`, `bar`(column), `donut` onto the new `series`/`categories`
   contract (legacy Inputs removed, not adapted) and update both consuming
   call sites in the same change:
   - `features/admin/admin-delivery-health/admin-delivery-health.component.{ts,html}`
   - `features/admin/admin-ai-usage/admin-ai-usage.component.{ts,html,spec.ts}`
   Run existing specs (`admin-components.spec.ts` + the 3 chart specs +
   `admin-ai-usage.component.spec.ts`) to confirm the rewrite renders
   correctly end to end, not just in isolation.
3. Add the 4 new types (line, horizontal-bar, pie, radial) as net-new
   components — zero migration risk since nothing consumes them yet.
4. Restyle `mini-bar-chart` chrome only (spacing/typography/tokens) to sit
   visually consistent next to the new ApexCharts-based charts — no engine change.
5. Update `docs/architecture/admin-ui-design-system.md` with a new "Charts"
   section documenting the shared Input contract and the component table
   above, once implementation lands (not part of this planning pass).

## Risks / unresolved questions

- **Bundle size**: ApexCharts (~130KB min+gzip) is new weight on every admin
  page that renders a chart. Mitigate via Angular's existing lazy-loaded
  admin feature routes (already code-split), so cost is paid once per admin
  session, not per page — acceptable but worth confirming with a bundle-size
  check after the first rewrite lands.
- **Dark mode**: current tokens already support a dark theme
  (`AdminThemeService`); ApexCharts needs its `theme`/`grid`/`tooltip` colors
  refreshed on theme toggle — the theme util must subscribe to theme changes
  and call `chart.updateOptions()`, not just read tokens once at init.
- **`ng-apexcharts` version pin**: must stay on the 1.14.x–1.15.x line while
  the repo is on Angular 19; bumping to `ng-apexcharts` 2.x requires bumping
  Angular to 20 first (peer dep `@angular/core >=20.0.0`) — track this
  coupling so a future Angular upgrade doesn't silently break the chart
  package resolution.
- **Full replacement (no adapters)**: the 2 call sites
  (`admin-delivery-health`, `admin-ai-usage`) must be updated in the same PR
  as the 3 component rewrites — this is a single atomic change, not
  landable incrementally, since the old Inputs are removed rather than kept
  alongside the new ones.
- **Combo chart** scope is explicitly optional/stretch — flag if it should be
  dropped entirely to shrink this into a smaller sprint.

## Implementation tasks (for the eventual sprint doc)

1. ✅ Add `ng-apexcharts` (pinned exact `1.15.0`, not `^1.15` — npm resolves
   the caret to 1.17.1 which requires Angular 20) + `apexcharts@^4`
   dependencies; build `chart-theme.util.ts` (token→ApexCharts option
   bridge) + `chart-common.ts` (`SpAdminChartSeries` type +
   `SpAdminAxisChartBase` shared-Input directive). No separate
   `sp-admin-chart-base` component was needed — `ChartComponent` from
   `ng-apexcharts` is directly importable as a standalone component, so each
   chart imports it straight into its own `imports: []`.
2. ✅ Rewrite area/bar(column, +horizontal)/donut charts onto the new
   `series`/`categories` contract (legacy Inputs removed) and update
   `admin-delivery-health` + `admin-ai-usage` call sites in the same change.
   `admin-components.spec.ts`'s donut tests rewritten for the new
   `apexSeries`/`apexLabels`/`apexColors` fields. Verified: `tsc --noEmit`
   clean, `ng test` green (140/140, 115/115, 17/17 across the 3 affected spec
   files), production build succeeds with no new errors (bundle grew ~10KB
   on an initial chunk already over budget pre-existing this change —
   ApexCharts itself lazy-loads inside the admin route chunks as expected).
   **Not yet done**: live browser verification — Docker wasn't running in
   this session, so the chart rendering hasn't been eyeballed in the actual
   app yet, only unit-tested.
3. Build line, pie, radial chart components. (Horizontal bar is done — see
   deviation note above.)
4. (Stretch) Build combo chart.
5. Restyle mini-bar-chart chrome only.
6. Update `docs/architecture/admin-ui-design-system.md` Charts section.

## Final verdict

Plan approved by user via AskUserQuestion answers (ApexCharts engine, flexible
chart-only components, 4 new chart types + combo as stretch), then refined via
2 follow-up decisions: use the official `ng-apexcharts` package (pinned to the
Angular-19-compatible 1.15.x line, not `latest`) instead of a hand-written
wrapper, and fully replace the 3 existing chart components' call sites instead
of keeping backward-compatible legacy Inputs. Not yet implemented — this
document records the agreed direction before code changes begin.

## Next recommended action

Confirm sprint sizing/sequencing (likely split into "foundation + 3 rewrites"
and "4 new chart types" as two sprints), then begin with task 1 (shared
ApexCharts wrapper) since every other task depends on it.
