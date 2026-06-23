# Phase 10UI-VISUAL-3 — Admin Graph Cards and Reference Visual Density

**Date:** 2026-06-24
**Sprint / Feature:** 10UI-VISUAL-3
**HEAD before work:** `4c2dddc ui: apply admin visual analytics across pages`
**Verdict:** Complete — all gates passed

---

## Summary

Enriched the admin UI visual density to more closely match the SpeakPath reference design. Added four decorative skeleton variants to `sp-admin-visual-placeholder`, created a new `sp-admin-graph-card` wrapper component, inserted real-data visual analytics rows on AI Usage and Diagnostics, and upgraded all remaining blank placeholder cards on Dashboard with skeleton decorations.

---

## New Components

### `sp-admin-visual-placeholder` — skeleton variants added

New `skeleton` input of type `PlaceholderSkeleton`:

| Value | Visual |
|-------|--------|
| `none` (default) | No skeleton, same as before |
| `chart` | 8-bar decorative SVG bar chart outline |
| `ring` | Decorative donut ring (partial arc in primary colour at 25% opacity) |
| `timeline` | Dot-and-bar timeline strip (4 rows) |
| `grid` | 3×6 heatmap-style grid cells in varying opacity bands |

All skeletons are purely decorative — no fake numbers, no fake trend values. Only opacity-band shading on muted fill.

### `sp-admin-graph-card` (new component)

- File: `src/app/design-system/admin/components/graph-card/sp-admin-graph-card.component.ts`
- Exported from `src/app/design-system/admin/index.ts`
- Inputs: `title`, `subtitle`, `status` (`live | partial | unavailable | loading`), `actionLabel`, `actionHref`, `footerNote`
- Status badge: green "Live" (with dot), amber "Partial", muted "Unavailable". Hidden for `loading`.
- Slot: `<ng-content />` for chart body

---

## Pages Improved

### AI Usage `/admin/usage`

- Added `successRingPct` computed signal from `summary().successfulCalls / totalCalls`.
- Added `tokenBreakdownItems` computed signal from `totalInputTokens` / `totalOutputTokens`.
- Added `providerBreakdownItems` computed signal from `byProvider[]`.
- Inserted three-card visual analytics row (Success rate ring, Token split breakdown, Calls by provider breakdown) — shown when summary loads, hidden while loading.
- Upgraded "Activities per day" placeholder to `skeleton="chart"`.
- Upgraded "Student engagement" placeholder to `skeleton="ring"`.
- Added `.sp-au-visual-row` CSS grid (2-col → 3-col at 1100px).

Data sources: all from real `summary()` signal — no fake values.

### Diagnostics `/admin/diagnostics`

- Added `severityBreakdownItems` computed signal from loaded `events()` array.
- Inserts an "Event severity breakdown" card above System status when events are loaded and at least one severity group is present.
- Groups: Error (danger), Warning (amber), Information (indigo), Debug (slate). Zero-count groups are filtered out.

Data source: real `events()` signal — no fake values.

### Dashboard `/admin`

- Activity trends placeholder: added `skeleton="chart"`.
- Score distribution placeholder: added `skeleton="chart"`.
- AI spend by type placeholder: added `skeleton="grid"`.
- Avg session duration placeholder: added `skeleton="ring"`.
- Streak leaderboard placeholder: added `skeleton="timeline"`.
- Live events feed placeholder: added `skeleton="timeline"`.

---

## Components Added / Modified

| File | Change |
|------|--------|
| `components/visual-placeholder/sp-admin-visual-placeholder.component.ts` | Added `skeleton` input and 4 SVG skeleton variants; added `gridCells` pre-computed array |
| `components/visual-placeholder/sp-admin-visual-placeholder.component.spec.ts` | Added 5 skeleton variant tests |
| `components/graph-card/sp-admin-graph-card.component.ts` | New component |
| `components/graph-card/sp-admin-graph-card.component.spec.ts` | 9 tests |
| `design-system/admin/index.ts` | Exported graph-card |
| `admin-ai-usage/admin-ai-usage.component.ts` | Added ring, breakdown-bars, graph-card imports; added 3 computed signals |
| `admin-ai-usage/admin-ai-usage.component.html` | Added visual analytics row; upgraded 2 skeleton placeholders |
| `admin-diagnostics/admin-diagnostics.component.ts` | Added breakdown-bars import; added `severityBreakdownItems` computed |
| `admin-diagnostics/admin-diagnostics.component.html` | Added severity breakdown card |
| `admin-dashboard/admin-dashboard.component.ts` | Upgraded 6 placeholder cards with skeleton variants |

---

## Security / Scope Constraints

All original constraints respected:

- No React/JSX in Angular runtime.
- No mock data imported.
- No new backend APIs.
- No fake production data.
- No heavy charting library added.
- No business logic changed.
- Skeleton shapes are purely decorative — zero numbers or trend values shown in skeleton mode.
- All real chart data uses only already-loaded signals (`summary()`, `events()`).

---

## Tests Added / Updated

| File | Change |
|------|--------|
| `sp-admin-visual-placeholder.component.spec.ts` | +5 skeleton variant tests |
| `sp-admin-graph-card.component.spec.ts` | +9 new tests |

No existing specs were broken.

---

## Gates

- `git diff --check`: clean
- Production build: passed
- Unit tests: **1324/1324 passed** (was 1309 before VISUAL-1/2, 1324 after VISUAL-3)
- Backend gates: not required (no backend source changed)
- Playwright: not run (no stable admin E2E/visual specs exist — documented in AGENTS.md)

---

## Remaining Visual Gaps

The following are deferred with skeleton placeholders and explicit "Backend not available yet" labels:

| Section | Page | Skeleton |
|---------|------|---------|
| Activity trends | Dashboard, AI Usage | chart |
| Score distribution | Dashboard | chart |
| AI spend by type | Dashboard | grid |
| Avg session duration | Dashboard | ring |
| Streak leaderboard | Dashboard | timeline |
| Live events feed | Dashboard | timeline |
| Activities per day | AI Usage | chart |
| Student engagement | AI Usage | ring |

---

## Confirmation

No backend APIs, migrations, business logic, student-facing UI, or new heavy charting dependency changes were implemented.
