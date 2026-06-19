# Phase 10X-K-0 — Admin Wrapper Inventory and Centralization Check

**Date:** 2026-06-19
**Sprint:** Admin UI Cleanup — Phase 10X-K-0
**Scope:** Frontend only. No backend, API, or product behavior changes.

---

## Files Reviewed

- `src/LinguaCoach.Web/src/app/admin/components/` (all 31 component files)
- `src/LinguaCoach.Web/src/app/layouts/admin-app-layout/admin-app-layout.component.html`
- `src/LinguaCoach.Web/src/app/layouts/admin-app-layout/admin-app-layout.component.ts`

---

## Component Inventory by Concept

| Concept | Existing Name | File | Status | Used by Pages | Duplicate Inline? | Next Action |
|---|---|---|---|---|---|---|
| AdminPage | `sp-admin-layout` | `admin/components/layout/` | Production-ready | admin-app-layout | None | None |
| AdminPageHeader | `sp-admin-page-header` | `admin/components/page-header/` | Production-ready | dashboard, students | None | None |
| AdminPageBody | — | — | **Missing** | N/A | N/A | Create thin wrapper or skip if `<main>` suffices |
| AdminSection | `sp-admin-section-card` | `admin/components/section-card/` | Production-ready | dashboard | None | Rename concept mapping only |
| AdminCard | `sp-admin-card` | `admin/components/card/` | Production-ready | dashboard, students | None | None |
| AdminStatCard | `sp-admin-stat-card` | `admin/components/stat-card/` | Production-ready | dashboard | None | None |
| AdminGrid | — | — | **Missing** | N/A | Yes — inline Tailwind grid classes on pages | Consider utility wrapper if grids diverge |
| AdminToolbar | — | — | **Missing** | N/A | Partially — `sp-admin-filter-bar` covers search+filter but not generic toolbar | Alias `sp-admin-filter-bar` for now |
| AdminFilterBar | `sp-admin-filter-bar` | `admin/components/filter-bar/` | Production-ready | students | None | None |
| AdminDataTable | `sp-admin-table` | `admin/components/table/` | Production-ready (full-featured) | dashboard, students | `sp-admin-data-table` is a simpler duplicate | Deprecate `sp-admin-data-table`; migrate to `sp-admin-table` |
| AdminTablePagination | `sp-admin-pagination` | `admin/components/pagination/` | Production-ready | students | None | None |
| AdminActionMenu | `sp-admin-table-actions` | `admin/components/table-actions/` | Production-ready | students | None | None |
| AdminBadge | `sp-admin-badge` | `admin/components/badge/` | Production-ready | students, dashboard | `sp-admin-stat-badge` is a duplicate pill variant | Consolidate `sp-admin-stat-badge` into `sp-admin-badge` via tone variant |
| AdminButton | `sp-admin-button` | `admin/components/button/` | Production-ready | students, modals | None | None |
| AdminIconButton | — | — | **Missing** | N/A | Yes — raw `<button>` with icon inline throughout layout HTML | Add `iconOnly` input to `sp-admin-button` (slot already exists) |
| AdminInput | `sp-admin-input` | `admin/components/input/` | Production-ready | students, forms | None | None |
| AdminSelect | `sp-admin-select` | `admin/components/select/` | Production-ready | students, forms | None | None |
| AdminCheckbox | — | — | **Missing** | N/A | N/A — no checkboxes currently used in admin UI | Defer until needed |
| AdminNumberInput | — | — | **Missing** | N/A | N/A — no number inputs currently used | Defer until needed |
| AdminFormField | `sp-admin-form-field` | `admin/components/form-field/` | Production-ready | student edit modal | None | None |
| AdminFormGrid | — | — | **Missing** | N/A | Yes — inline `grid grid-cols-*` in modals/forms | Create if form layouts diverge |
| AdminFormSection | — | — | **Missing** | N/A | Partially covered by `sp-admin-section-card` | Map to `sp-admin-section-card` for now |
| AdminEmptyState | `sp-admin-empty-state` | `admin/components/empty-state/` | Production-ready | students, dashboard | None | None |
| AdminLoadingState | `sp-admin-loading-state` | `admin/components/loading-state/` | Production-ready | students, dashboard | None | None |
| AdminErrorState | `sp-admin-error-state` | `admin/components/error-state/` | Production-ready | students | None | None |
| AdminCopyableText | — | — | **Missing** | N/A | N/A | Defer until needed |
| AdminTruncatedText | — | — | **Missing** | N/A | N/A | Defer until needed |
| AdminCodePill | — | — | **Missing** | N/A | N/A | Defer until needed |
| AdminDrawer | `sp-admin-drawer` | `admin/components/drawer/` | Production-ready | not yet used by pages | None | Wire up to pages when needed |
| AdminModal | `sp-admin-modal` | `admin/components/modal/` | Production-ready | students | None | None |
| AdminUserMenu | — | — | **Missing (inline)** | N/A | Yes — inline in admin-app-layout.component.html using `sp-admin-dropdown` + CSS classes `sp-admin-avatar`, `sp-admin-profile-menu` | Extract to `sp-admin-user-menu` component |
| AdminSidebarNavItem | — | — | **Missing (inline)** | N/A | Yes — `<a class="menu-item menu-item-inactive">` repeated 8× in desktop + 8× in mobile drawer | Extract to `sp-admin-sidebar-nav-item` component |
| AdminSidebarSection | — | — | **Missing (inline)** | N/A | Yes — `<p class="text-xs font-semibold uppercase ...">` section labels inline | Extract to `sp-admin-sidebar-section` component |

---

## Existing Wrappers (Production-Ready)

31 components total under `src/app/admin/components/`:

- Layout: `sp-admin-layout`, `sp-admin-header`, `sp-admin-sidebar`, `sp-admin-page-header`
- Cards: `sp-admin-card`, `sp-admin-section-card`, `sp-admin-stat-card`, `sp-admin-kpi-card`, `sp-admin-action-card`
- Badges: `sp-admin-badge`, `sp-admin-stat-badge`
- Data: `sp-admin-table`, `sp-admin-data-table`, `sp-admin-table-actions`, `sp-admin-pagination`, `sp-admin-filter-bar`
- Forms: `sp-admin-form-field`, `sp-admin-input`, `sp-admin-select`, `sp-admin-textarea`
- Buttons: `sp-admin-button`, `sp-admin-dropdown`
- States: `sp-admin-loading-state`, `sp-admin-empty-state`, `sp-admin-error-state`, `sp-admin-spinner`
- Overlays: `sp-admin-modal`, `sp-admin-drawer`
- Utility: `sp-admin-theme-toggle`, `sp-admin-toast-outlet`
- Alert: `sp-admin-alert`

---

## Missing Wrappers

These concepts have no dedicated component yet:

| Concept | Priority | Notes |
|---|---|---|
| AdminSidebarNavItem | High | Inline duplicate ×16 in layout HTML |
| AdminUserMenu | High | Inline in layout HTML, uses ad-hoc CSS classes |
| AdminSidebarSection | Medium | Inline section label pattern ×4 |
| AdminPageBody | Low | `<main>` + router-outlet covers this adequately |
| AdminGrid | Low | Inline Tailwind grid; defer unless layouts diverge |
| AdminFormGrid | Low | Inline grid in modals only |
| AdminIconButton | Low | `sp-admin-button` `iconOnly` input already exists conceptually |
| AdminCheckbox | Defer | No current usage in admin UI |
| AdminNumberInput | Defer | No current usage in admin UI |
| AdminCopyableText | Defer | No current usage |
| AdminTruncatedText | Defer | No current usage |
| AdminCodePill | Defer | No current usage |
| AdminFormSection | Defer | `sp-admin-section-card` covers this adequately |
| AdminToolbar | Defer | `sp-admin-filter-bar` covers the current toolbar pattern |

---

## Partial Wrappers

| Component | Issue |
|---|---|
| `sp-admin-sidebar` | Shell only — `ng-content` with no structure. Nav items, section labels, brand, and footer are all inline in `admin-app-layout.component.html` |
| `sp-admin-header` | Shell only — user menu and toggle buttons are inline in layout HTML |
| `sp-admin-data-table` | Duplicate of `sp-admin-table` (simpler version); should be deprecated |
| `sp-admin-stat-badge` | Duplicate pill variant of `sp-admin-badge`; should be folded in |
| `sp-admin-kpi-card` | Overlaps heavily with `sp-admin-stat-card`; review for consolidation |

---

## Duplicate Page-Specific Patterns

| Pattern | Location | Duplication |
|---|---|---|
| Sidebar nav item `<a class="menu-item ...">` | `admin-app-layout.component.html` L44–L98 (mobile), L175–L228 (desktop) | Identical pattern ×8 in each, 16 total |
| Sidebar section label `<p class="text-xs font-semibold uppercase ...">` | `admin-app-layout.component.html` L42, L101 (mobile), L170, L231 (desktop) | 4× inline |
| User menu / avatar / profile dropdown | `admin-app-layout.component.html` L285–L313 | Inline using ad-hoc `sp-admin-avatar`, `sp-admin-profile-*` CSS classes; no component |
| Icon-only toggle buttons (hamburger, collapse) | `admin-app-layout.component.html` L275–L282 | Raw `<button>` with inline SVG; no wrapper |

---

## Recommended Next Phase: 10X-K-1

Extract the three highest-value inline duplicates from `admin-app-layout.component.html` into proper components:

1. **`sp-admin-sidebar-nav-item`** — encapsulate `<a class="menu-item ...">` with inputs: `routerLink`, `label`, `icon` (SVG), `exact`, collapsed state via `@Input`.
2. **`sp-admin-sidebar-section`** — encapsulate the section label `<p class="text-xs ...">` with input: `label`, `collapsed`.
3. **`sp-admin-user-menu`** — extract avatar + profile dropdown from layout into a standalone component with inputs: `email`, `initial`, and output: `logout`.

These three removals reduce `admin-app-layout.component.html` from ~320 lines to ~120 lines and eliminate all major inline duplication.

Defer AdminGrid, AdminFormGrid, AdminCheckbox, AdminNumberInput, AdminCopyableText, AdminTruncatedText, AdminCodePill, AdminPageBody until a page actually needs them.

Deprecate `sp-admin-data-table` in a follow-on phase after migrating its one remaining caller.

---

## Decisions Made

- `sp-admin-filter-bar` covers `AdminToolbar` for current usage. No separate toolbar component needed now.
- `sp-admin-section-card` covers `AdminFormSection` for current usage.
- `sp-admin-button` with `iconOnly` flag covers `AdminIconButton` for current usage; no separate component needed.
- `sp-admin-table` is the canonical data table. `sp-admin-data-table` is redundant.
- `sp-admin-stat-badge` should be folded into `sp-admin-badge` as a tone variant.

---

## Risks and Unresolved Questions

- `sp-admin-kpi-card` vs `sp-admin-stat-card` overlap is significant. Needs a usage audit before deciding which to keep.
- The `sp-admin-avatar` / `sp-admin-profile-*` CSS classes used in the layout are defined somewhere in CSS but have no component backing. Fragile if the CSS is refactored.

---

## Final Verdict

The admin wrapper system is mature. 22 of 33 requested concepts already have production-ready components. The main gap is the layout shell: sidebar nav items, sidebar section labels, and the user menu are all inline duplicates in the layout HTML, not components. These are the priority extractions for Phase 10X-K-1.

---

## Next Recommended Action

Begin Phase 10X-K-1: extract `sp-admin-sidebar-nav-item`, `sp-admin-sidebar-section`, and `sp-admin-user-menu` from `admin-app-layout.component.html`.

---

## Confirmation

- No backend changed.
- No API changed.
- No product behavior changed.
- No commit made.
- No push made.
