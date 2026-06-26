# Phase 10UI — Diagnostics Design-System Cleanup and Visual Parity

**Date:** 2026-06-26
**Sprint:** Phase 10UI (UI Design-System Cleanup)
**Passes:** 4

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html`
- `src/LinguaCoach.Web/src/app/design-system/admin/tokens/admin-tokens.css`

---

## Pass 1 — Initial cleanup

- Removed all `sp-diag-*` classes and inline `styles: []` block
- Added `SpAdminTableFooterComponent` and `SpAdminNotImplementedStateComponent` to component imports
- Added `sp-admin-table-row--warning` / `sp-admin-table-row--danger` to `admin-tokens.css`
- Replaced `sp-diag-overview` with `sp-admin-card-grid-2`
- Count and pagination moved into `sp-admin-table-footer`

---

## Pass 2 — Slot and card-wrap fix

- Fixed page-header: removed `slot="actions"` (bare child required — `sp-admin-page-header` uses `<ng-content />`)
- Wrapped `sp-admin-event-feed` and `sp-admin-breakdown-bars` in `sp-admin-card`
- Added `@if (eventsTotalPages() > 1)` pagination guard
- Added target-shaped Background Jobs section with 4 status tiles

---

## Pass 3 — Card polish and `sp-adm-*` cleanup

- Removed `[headerDivider]="true"` from overview small cards
- Removed `sp-adm-card-body-md` (internal card class leaked into template)
- Added `sp-admin-card-body-sm` / `sp-admin-card-body-md` as shared padding wrapper tokens to `admin-tokens.css`
- Background Jobs: status-grid in `sp-admin-card-body-md` wrapper + `sp-admin-empty-state`

---

## Pass 4 — Table-section composition (this pass)

### Root cause of remaining visual problems

`sp-admin-table` with `variant="data"` already renders its own card shell (`border-radius:14px; border; overflow:hidden`). Wrapping it inside `sp-admin-card padding="none"` created **double borders** — one from the card, one from the table. The filter bar had no horizontal padding because `sp-admin-card padding="none"` gave it nothing. The footer had a card border-top plus its own padding, creating a heavy doubled line.

### Fix: `sp-admin-table-section` shared token pattern

Added to `admin-tokens.css`:

```css
.sp-admin-table-section         — card shell (border, radius, shadow, overflow:hidden)
.sp-admin-table-section-header  — title + actions row with padding and bottom border
.sp-admin-table-section-title   — 14px/800 heading inside header
.sp-admin-table-section-filters — filter bar wrapper with horizontal padding + subtle bottom border
.sp-admin-table-section-body    — padded sub-section (status-grid, etc.)
.sp-admin-table-section-footer  — count left + pagination right, top border
```

`sp-admin-table` is now used with `[flush]="true"` inside `sp-admin-table-section` — no outer card shell, clean table renders directly into the section.

### Recent Events refactored

- No `sp-admin-card` wrapper. Uses `sp-admin-table-section` directly.
- Header: `sp-admin-table-section-header` + `sp-admin-table-section-title` + `sp-admin-badge-row` actions.
- Filters: `sp-admin-table-section-filters` wrapper gives proper horizontal padding.
- Table: `sp-admin-table [flush]="true"` — no double border.
- Loading/error/empty states: wrapped in `sp-admin-card-body-md` for padding.
- Footer: `sp-admin-table-section-footer` — single clean border, count + conditional pagination.
- `sp-admin-table-footer` component removed from this page (replaced by token).

### Background Jobs refactored

- No `sp-admin-card` wrapper. Uses `sp-admin-table-section`.
- Header: `sp-admin-table-section-header` + title only.
- Status tiles: `sp-admin-table-section-body` with `sp-admin-status-grid [columns]="4"`.
- Footer area: `sp-admin-table-section-footer` with `sp-admin-not-implemented-state` — clean, compact, no table needed.
- `sp-admin-empty-state` removed (not-implemented-state is more precise).

### Header dividers

`[headerDivider]="true"` removed from all cards in this pass. Section structure is now owned by `sp-admin-table-section-*` tokens, not card dividers.

---

## Confirmation

| Check | Result |
|---|---|
| No fake data | confirmed |
| No page CSS (`admin-diagnostics.component.css`) | confirmed |
| No `styles: []` | confirmed |
| No inline `style=` | confirmed |
| No inline SVG | confirmed |
| No `sp-diag-*` classes | confirmed |
| No `sp-adm-*` classes | confirmed |
| All sections inside `sp-admin-page-body` | confirmed |
| Build | passes |

---

## Shared tokens added (admin-tokens.css, cumulative)

```css
/* Table row tone */
.sp-admin-table-row--warning td
.sp-admin-table-row--danger  td

/* Card body padding wrappers */
.sp-admin-card-body-sm
.sp-admin-card-body-md

/* Table section composition */
.sp-admin-table-section
.sp-admin-table-section-header
.sp-admin-table-section-title
.sp-admin-table-section-filters
.sp-admin-table-section-body
.sp-admin-table-section-footer
```

---

## Remaining visual gaps

- Background Jobs tiles show `—` — intentional until a jobs/queue API endpoint exists.
- Overview row (event-feed + severity breakdown) only renders after events load — correct.

---

## Next recommended action

1. Open `/admin/diagnostics` in dev server and confirm visual match against target.
2. When background jobs API is ready: wire real counts; replace not-implemented footer with a table of recent batches using `sp-admin-table-section` pattern.

---

## Documentation impact

- Docs reviewed: none
- Docs updated: this review doc (updated in place, pass 4)
- Docs intentionally not updated: architecture docs
- Reason: UI-only cleanup, no API or model changes
