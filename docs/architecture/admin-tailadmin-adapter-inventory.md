---
status: current
lastUpdated: 2026-06-18 23:00
owner: architecture
supersedes:
supersededBy:
---

# Admin UI — TailAdmin Adapter Inventory

This document maps TailAdmin free Angular template patterns to SpeakPath `sp-admin-*` wrapper components.

It drives implementation work for phases 10X-E and 10X-F.

**TailAdmin source**: `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
**Commit imported**: da992cf
**Source URL**: https://github.com/TailAdmin/free-angular-tailwind-dashboard

---

## Dependency Direction

```
src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/   (vendor reference — MIT)
          ↓
src/app/admin/tokens/admin-tokens.css                  (design tokens)
src/app/admin/components/sp-admin-*/                   (wrapper components)
          ↓
src/LinguaCoach.Web/src/app/admin/pages/               (feature pages — use sp-admin-* only)
```

---

## Adapter Mapping Table

| TailAdmin source path | TailAdmin pattern | SpeakPath wrapper | Phase | Status | Notes |
|---|---|---|---|---|---|
| `shared/layout/app-layout/` | Layout One shell | `AdminAppLayoutComponent` / `sp-admin-layout` | 10X-C-F | ✅ Partial | Shell matches Layout One structure. ViewEncapsulation.None. Full token alignment in 10X-E. |
| `shared/layout/sidebar/` | Fixed sidebar, icon-only collapse | `sp-admin-sidebar` | 10X-C-F | ✅ Partial | CSS approximation. Adapt real sidebar classes in 10X-E. |
| `shared/layout/header/` | Sticky header, user dropdown, theme toggle | `sp-admin-header` | 10X-C-F | ✅ Partial | Structure correct. Full dropdown wiring in 10X-E. |
| `shared/components/ui/button/` | Button variants, sizes, loading | `sp-admin-button` | 10X-B | ✅ Partial | Foundation exists. Align TailAdmin variants in 10X-E. |
| `shared/components/ui/badge/` | Tones, sizes | `sp-admin-badge` | 10X-B | ✅ Partial | Foundation exists. Align tones in 10X-E. |
| `shared/components/ui/table/` | Table, row, cell, header | `sp-admin-table` | 10X-B | ✅ Partial | Foundation exists. Column sorting, sticky header in 10X-F. |
| `shared/components/common/component-card/` | Card with header/action slots | `sp-admin-card` / `sp-admin-stat-card` | 10X-B | ✅ Partial | Foundation exists. Slot alignment in 10X-E. |
| `shared/components/form-elements/input-field/` | Text inputs, states | `sp-admin-input` / `sp-admin-form-field` | 10X-B | ✅ Partial | Foundation exists. Error/disabled states in 10X-E. |
| `shared/components/form-elements/select/` | Select, multi-select | `sp-admin-select` | 10X-B | ✅ Partial | Foundation exists. |
| `shared/components/ui/modal/` | Modal, confirm dialog | `sp-admin-modal` + `AdminModalService` | 10X-B | ✅ Partial | Service-backed foundation. Full slot wiring in 10X-E. |
| `shared/components/ui/dropdown/` | Dropdown menu | `sp-admin-dropdown` | — | ⬜ Future | Not yet implemented. 10X-F candidate. |
| `shared/layout/header/` (notification area) | Notification dropdown | `sp-admin-toast-outlet` + notification wrapper | — | ⬜ Future | Toast outlet exists. Full notification dropdown in 10X-G+. |
| `shared/services/sidebar.service.ts` | Sidebar open/collapse state | `AdminSidebarService` (existing) | 10X-C-F | ✅ Done | Collapse/drawer state managed in shell. |
| `shared/services/theme.service.ts` | Dark/light theme toggle | admin-tokens.css + future `AdminThemeService` | — | ⬜ Future | Token layer exists. Theme toggle wiring in 10X-F. |
| `shared/components/ui/pagination/` | Pagination controls | `sp-admin-pagination` | 10X-B | ✅ Partial | Foundation exists. |
| `shared/components/filter/` | Filter bar / search | `sp-admin-filter-bar` | 10X-B | ✅ Partial | Foundation exists. |
| `shared/components/ui/drawer/` | Slide-in drawer | `sp-admin-drawer` + `AdminDrawerService` | 10X-B | ✅ Partial | Foundation exists. |
| `shared/components/common/breadcrumb/` | Breadcrumb navigation | — | — | ⬜ Future | Not yet wrapped. |
| `shared/components/charts/` | Chart components | — | — | ⬜ Future | Reference only. Dashboard charts TBD. |
| `pages/dashboard/` | Demo dashboard page | — | — | 🚫 Do not copy | Demo page — reference layout patterns only. |
| `pages/*/` | All other demo pages | — | — | 🚫 Do not copy | Reference UI patterns only. Never import. |

---

## Status Legend

| Symbol | Meaning |
|---|---|
| ✅ Done | Fully adapted, no known gaps |
| ✅ Partial | Foundation exists, alignment gaps remain |
| ⬜ Future | Not yet implemented, planned for future phase |
| 🚫 Do not copy | Must not be imported into SpeakPath feature pages |

---

## Phase Assignment

| Phase | Scope |
|---|---|
| 10X-D (current) | Vendor import, folder structure, documentation, adapter inventory |
| 10X-E | Wrapper alignment: layout, sidebar, header, button, badge, card, input, modal |
| 10X-F | Wrapper completion: table sorting, dropdown, theme toggle, filter bar, pagination |
| 10X-G+ | Notification dropdown, breadcrumb, charts, advanced form elements |

---

## What Must Never Happen

- Feature pages importing from `templates/`
- Raw TailAdmin class lists copied into feature page templates
- TailAdmin demo pages becoming SpeakPath feature routes
- SpeakPath business logic placed inside `templates/tailadmin/`
- TailAdmin `package.json` dependencies merged into SpeakPath `package.json`
