---
status: current
lastUpdated: 2026-06-18
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
| `shared/layout/app-layout/` | Layout One shell | `AdminAppLayoutComponent` / `sp-admin-layout` | 10X-E | ✅ Done | `min-h-screen xl:flex` shell, `xl:ml-[290px]/xl:ml-[90px]` transition. Exact TailAdmin Layout One. |
| `shared/layout/app-sidebar/` | Fixed sidebar, icon-only collapse | `sp-admin-sidebar` | 10X-E | ✅ Done | `fixed left-0 top-0 h-screen w-[290px]/w-[90px] bg-white border-r border-gray-200`. TailAdmin sidebar classes. |
| `shared/layout/app-header/` | Sticky header, user dropdown, theme toggle | `sp-admin-header` | 10X-E | ✅ Done | `sticky top-0 flex w-full bg-white border-b border-gray-200 z-[99999]`. Exact TailAdmin header. |
| `shared/components/ui/button/` | Button variants, sizes, loading | `sp-admin-button` | 10X-E | ✅ Done | `inline-flex items-center justify-center gap-2 rounded-lg transition`. Brand-500 primary, outline secondary. |
| `shared/components/ui/badge/` | Tones, sizes | `sp-admin-badge` | 10X-E | ✅ Done | `inline-flex items-center px-2.5 py-0.5 rounded-full font-medium text-xs`. TailAdmin light variant color map. |
| `shared/components/ui/table/` | Table, row, cell, header | `sp-admin-table` | 10X-E | ✅ Done | `rounded-2xl border border-gray-200 bg-white`. th `text-xs text-gray-500 bg-gray-50`. Column sorting in 10X-F. |
| `shared/components/common/component-card/` | Card with header/action slots | `sp-admin-card` / `sp-admin-stat-card` | 10X-E | ✅ Done | `rounded-2xl border border-gray-200 bg-white`. Header `px-6 py-5`. Body `p-4 sm:p-6 border-t border-gray-100`. |
| `shared/components/form/input/` | Text inputs, states | `sp-admin-input` / `sp-admin-form-field` | 10X-E | ✅ Done | `h-11 rounded-lg border border-gray-200 bg-transparent py-2.5 px-4 text-sm`. TailAdmin input pattern. |
| `shared/components/form/select/` | Select, multi-select | `sp-admin-select` | 10X-E | ✅ Done | `h-11 rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm`. TailAdmin select pattern. |
| `shared/components/ui/modal/` | Modal, confirm dialog | `sp-admin-modal` + `AdminModalService` | 10X-E | ✅ Done | `rounded-3xl bg-white`. Backdrop `bg-gray-400/50 backdrop-blur-sm`. Close `rounded-full bg-gray-100`. |
| `shared/components/ui/dropdown/` | Dropdown menu | `sp-admin-dropdown` | — | ⬜ Future | Not yet implemented. 10X-F candidate. |
| `shared/layout/header/` (notification area) | Notification dropdown | `sp-admin-toast-outlet` + notification wrapper | — | ⬜ Future | Toast outlet exists. Full notification dropdown in 10X-G+. |
| `shared/services/sidebar.service.ts` | Sidebar open/collapse state | `AdminSidebarService` (existing) | 10X-C-F | ✅ Done | Collapse/drawer state managed in shell. |
| `shared/services/theme.service.ts` | Dark/light theme toggle | admin-tokens.css + future `AdminThemeService` | — | ⬜ Future | Token layer exists. Theme toggle wiring in 10X-F. |
| `shared/components/ui/pagination/` | Pagination controls | `sp-admin-pagination` | 10X-E | ✅ Done | `flex justify-between border-t border-gray-100 px-5 py-3`. TailAdmin pagination structure. |
| `shared/components/filter/` | Filter bar / search | `sp-admin-filter-bar` | 10X-E | ✅ Done | `flex items-end justify-between gap-3 flex-wrap mb-4`. TailAdmin filter bar pattern. |
| `shared/components/ui/drawer/` | Slide-in drawer | `sp-admin-drawer` + `AdminDrawerService` | 10X-E | ✅ Done | `fixed right-0 h-screen bg-white border-l border-gray-200`. Close `rounded-full bg-gray-100`. |
| `shared/components/form/label/` | Form field label, hint, error | `sp-admin-form-field` | 10X-E | ✅ Done | `block text-sm font-medium text-gray-700`. TailAdmin label pattern. |
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

| Phase | Scope | Status |
|---|---|---|
| 10X-D | Vendor import, folder structure, documentation, adapter inventory | ✅ Done |
| 10X-E | Wrapper alignment: all 15 sp-admin-* wrappers adapted to real TailAdmin patterns | ✅ Done |
| 10X-F (next) | Wrapper completion: table sorting, dropdown, theme toggle, filter bar refinement | ⬜ Pending |
| 10X-G+ | Notification dropdown, breadcrumb, charts, advanced form elements | ⬜ Future |

---

## What Must Never Happen

- Feature pages importing from `templates/`
- Raw TailAdmin class lists copied into feature page templates
- TailAdmin demo pages becoming SpeakPath feature routes
- SpeakPath business logic placed inside `templates/tailadmin/`
- TailAdmin `package.json` dependencies merged into SpeakPath `package.json`
