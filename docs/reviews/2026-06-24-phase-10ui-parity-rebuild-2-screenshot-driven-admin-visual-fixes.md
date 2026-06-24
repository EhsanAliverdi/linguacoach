# Phase 10UI-PARITY-REBUILD-2A — Screenshot-driven admin visual fixes

- **Date:** 2026-06-24
- **Related sprint/feature:** Admin UI parity (SpeakPath standalone design reference)
- **HEAD before work:** `ec7e2aa` (ignore design files) on `main`

## Method

Production admin screenshots in `src/LinguaCoach.Web/e2e/screenshots/prod/` were inspected
as images against the standalone design source files under
`docs/design/speakpath/admin/pages/`. Note: the prior commit `7456ab1`
(10UI-PARITY-REBUILD-1) changed only docs and `app.routes.ts`; the substantial page
rebuilds happened in earlier parity phases. The prod screenshots were captured at
12:41 (after that commit at 12:28), so they reflect the actual current deployed state.

## Screenshots inspected

All 15 files in `src/LinguaCoach.Web/e2e/screenshots/prod/` (01-dashboard.png through
15-security.png). Priority pages read in detail: 06-ai-usage, 10-curriculum,
11-exercise-types, 14-diagnostics.

## Design files inspected

`ai-usage-detail.jsx`, `curriculum.jsx`, `exercise-types.jsx`, `diagnostics.jsx`
(plus reference scan of dashboard, students, ai-config, prompts).

## Visual gap matrix

| Page | Screenshot observation | Design intent | Match? | Mismatches | Fixed? |
|---|---|---|---|---|---|
| AI Usage (/admin/ai-usage) | KPIs, pills, alert, By provider/By feature, ring + bars, calls-over-time, recent calls all present; but "By feature" and "Calls over time" tables render ALL rows unbounded, making the page extremely long | Both tables bounded + paginated (By feature 8/page, Calls over time paginated) | Partial | Unbounded summary tables | Yes |
| Curriculum (/admin/curriculum) | KPIs, CEFR donut+bars, filters, objectives table all present; table dumps all 22 rows with no pagination | Objectives table bounded + paginated | Partial | Unbounded objectives table | Yes |
| Exercise Types (/admin/exercise-types) | KPIs, skill color tiles, filter bar, compact table with pagination present | Skill tiles + filter + compact paginated table | Yes | None material | N/A (already correct) |
| Diagnostics (/admin/diagnostics) | KPIs, recent events + severity side by side, system status grid, event log with pagination present | Same + Background Jobs section (KPI tiles + recent batches) | Partial | Background Jobs section absent | No (needs backend) |
| Dashboard (/admin) | Hero, KPIs, graph cards present | Card-by-card match | Yes | Minor | No (P2, no material gap) |
| Students (/admin/students) | Filter bar + sortable table + pagination present | Same | Yes | None material | N/A |
| AI Config, Prompts, others | Substantial components present from prior phases | Match | Yes | None material | N/A |

## Pages fixed (with specific changes)

### AI Usage — `admin-ai-usage.component.{ts,html}`
- Added client-side pagination for "By feature" summary table (`byFeaturePaged`,
  `byFeatureTotalPages`, `onByFeaturePageChange`, page size 8). Resets to page 1 on
  summary reload.
- Added client-side pagination for "Calls over time" trend table
  (`trendBucketsPaged`, `trendTotalPages`, `onTrendPageChange`, page size 8). Resets
  to page 1 on trend reload.
- Both tables now use `<sp-admin-pagination>` when more than one page exists.
- "Recent calls" table was already server-side paginated; unchanged.

### Curriculum — `admin-curriculum.component.ts` (inline template)
- Imported `SpAdminPaginationComponent`.
- Added client-side pagination for the objectives table (`objectivesPaged`,
  `objectivesTotalPages`, `onObjectivesPageChange`, page size 12). Resets to page 1
  when objectives reload.
- Table loop now iterates `objectivesPaged()` and renders `<sp-admin-pagination>`
  when more than one page exists.

## Shared components changed

None. Reused the existing `SpAdminPaginationComponent` from the admin design system.

## Build result

`npm run build -- --configuration production` succeeded (pre-existing SCSS
"Empty sub-selector" warnings only, no errors).

## Test count

`npm test -- --watch=false --browsers=ChromeHeadless` — 1361 SUCCESS, 0 failures.

## Post-fix screenshots

Not captured. Production Playwright capture targets the deployed environment; these
fixes are not yet deployed, so new screenshots would still show the old build.
Re-run `npx playwright test e2e/prod-admin-screenshots.spec.ts
--config e2e/prod.playwright.config.ts` after this change ships to confirm the
bounded tables visually.

## Remaining mismatches (phase 2B)

- **Diagnostics Background Jobs section:** design shows 4 KPI tiles + a recent
  batches table. No background-jobs API exists in the Angular admin API service.
  Adding it requires a backend endpoint, which is out of scope for a visual-only
  phase. Defer to a backend-aggregation phase.
- **Dashboard live events feed** (already tracked as TODO-VISUAL-12): needs SignalR
  or polling endpoint.
- Fine-grained spacing/typography deltas across pages were not pixel-audited; only
  structural density mismatches were addressed.

## Confirmation

- Not docs-only: production Angular templates and component classes were changed.
- No fake data: pagination only slices real loaded data; placeholders remain
  "No data available".
- No secrets rendered.
- No student UI changes (only `features/admin/*`).
- No migrations, no backend APIs added.
- No heavy chart libraries added.

## Final verdict

Priority visual gaps (unbounded admin tables on AI Usage and Curriculum) fixed.
Exercise Types and Diagnostics already largely match the design. Diagnostics
Background Jobs deferred to backend phase.

## Next recommended action

Phase 10UI-PARITY-REBUILD-2B: deploy and re-capture screenshots to confirm, then
scope the Diagnostics Background Jobs backend endpoint + UI.
