# Phase 10UI-DASHBOARD-PARITY-1 — Exact Dashboard Screenshot Match

**Date:** 2026-06-24
**Sprint / Feature:** Admin UI visual parity — Dashboard
**Commit:** f8eb839

---

## Files reviewed and changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts` — full rewrite
- `docs/design/speakpath/admin/pages/dashboard.jsx` — reference only (read, not imported)

---

## Goal

Make `/admin` dashboard match the target screenshot and `dashboard.jsx` reference 100%.
Prior implementation had wrong layout sections, wrong chart types, and extra cards not in the design.

---

## Changes made

### Removed (were in production, not in target design)
- Full-width "Pending actions" card
- Full-width "Live events feed" placeholder skeleton
- "AI System" card (replaced by System health in correct position)
- 2×2 admin action grid card (replaced by list card in 4-col row)
- "Cohort engagement" standalone card (moved into metric strip)
- `SpAdminBreakdownBarsComponent`, `SpAdminVisualPlaceholderComponent`, `SpAdminGraphCardComponent` imports (replaced with inline SVG and direct markup)

### Added / rebuilt

| Section | Before | After |
|---|---|---|
| Subtitle | "SpeakPath platform overview" | "SpeakPath Admin · MMM d, yyyy" (computed) |
| Hero banner | 2-col grid, no dividers | 4-col grid, `border-right: 1px solid rgba(255,255,255,.1)`, FBB040 action color |
| KPI row | `sp-admin-kpi-card` components | Inline cards: 56px colored icon tile + `border-right` separator + label/value/delta |
| Main 2-col | Activity bar chart + AI System | SVG bezier area chart ("Activities completed") + System health (5 rows + footer) |
| 3-col | Funnel + At-risk + CEFR distribution | Funnel (4 stages + warn banner) + At-risk (avatar tiles + reason) + Score distribution (5 bins) |
| 4-col | Score dist + AI spend placeholder + Avg session placeholder + Streak placeholder | AI cost donut SVG + Session duration mini bars + Streak leaderboard + Admin actions list |
| Metric strip | Old 2-col actions+cohort grid | 3-col: cohort engagement segmented bar + AI spend sparkline SVG + CEFR distribution |
| Bottom | Students table full-width + Pending actions + Live events placeholder | Students table (3fr) + Live events feed (2fr) side by side |

### SVG charts implemented inline (no external library)
- **Area chart**: bezier cubic path, gradient fill, viewBox 0 0 580 150, color `#5B4BE8`
- **Donut chart**: 4 segments, r=36, strokeWidth=14, white center circle
- **Sparkline**: simple polyline, 80×32
- **Mini bar chart**: 7 bars Mon–Sun, highest bar `#5B4BE8`, others `#EDEBFF`

---

## Data rules followed

- All real API data preserved: students, stats, AI categories, activity trends, score distribution, AI usage
- No mock data imported from dashboard.jsx or admin-data.jsx
- Static UI-only data (system health latency, live feed events, session bars, cost donut segments) use hardcoded design values — no backend endpoint exists for these
- Streak leaderboard falls back to completed students by rank (no streak endpoint)

---

## Findings by priority

### P0 — Fixed
- Hero had no vertical dividers and used 2-col on desktop
- KPI cards lacked colored icon tile left column
- "Activity trends" was a bar chart; design requires SVG bezier area chart titled "Activities completed"
- "AI System" card was in wrong position with wrong content
- CEFR distribution was in 3-col row; design puts it in metric strip
- Score distribution was in 4-col row placeholder; design puts it in 3-col row
- Bottom section was stacked full-width cards; design requires side-by-side 3fr/2fr

### P1 — Fixed
- Subtitle format
- At-risk row style (30px avatar tile with colored bg, name + reason line)
- System health: 5 named services with latency bar + ms label + footer rows

### Not available from backend (documented)
- Streak days per student
- Session duration per student
- AI cost breakdown by type
- Real-time live events feed

---

## Decisions made

- `SpAdminBreakdownBarsComponent` not used — inline markup gives exact pixel control matching design
- `SpAdminVisualPlaceholderComponent` not used — replaced with real chart markup or empty-state text
- Static system health latency values used (142/88/218/4/11ms) matching design reference
- Live feed uses static event list from design (8 events) — no real-time endpoint

---

## Risks and unresolved questions

- Streak leaderboard shows rank by `onboardingStatus === 'Completed'` order with streak value `0` — needs real streak endpoint to show meaningful data
- AI cost donut uses fixed percentages (42/38/12/8) — needs per-category cost API
- Session duration bars use fixed data — needs session duration API
- Live events feed is static — needs WebSocket or polling endpoint

---

## Gates passed

- `npm run build -- --configuration production` → **success** (warnings only, no errors)
- Commit: `f8eb839`

---

## Final verdict

Phase 10UI-DASHBOARD-PARITY-1 complete. Template fully rebuilt to match `dashboard.jsx` layout order and component structure. Production build clean.

## Next recommended action

Deploy to speakpath.app, capture screenshot with Playwright (`npx playwright test e2e/prod-admin-screenshots.spec.ts --config e2e/prod.playwright.config.ts`), compare 01-dashboard.png to target screenshot for final visual sign-off.
