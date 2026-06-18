---
status: current
lastUpdated: 2026-06-18 24:00
owner: architecture
supersedes:
supersededBy:
---

# Admin UI Design System

## Visual Source of Truth

**TailAdmin Angular Layout One** is the template source for the SpeakPath admin app.

- Free template repo: https://github.com/TailAdmin/free-angular-tailwind-dashboard
- Demo reference: https://angular-demo.tailadmin.com/layout-one
- Vendored source: `src/app/templates/tailadmin/free-angular-tailwind-dashboard/` (gitignored — clone separately)
- Commit imported: da992cf (2026-06-18)
- License: MIT

The actual TailAdmin Angular source is stored as a vendor reference under `src/app/templates/`.
SpeakPath exposes TailAdmin **only** through `sp-admin-*` wrapper components.
Feature pages must not import from `templates/` directly.
The `templates/` folder is excluded from the main repo via `.gitignore` and should be cloned separately when onboarding.

## Dependency Direction

```
TailAdmin Angular Layout One (visual reference / licensed source when available)
          ↓
SpeakPath admin tokens (admin-tokens.css) + shell CSS (admin-app-layout)
          ↓
SpeakPath admin wrapper components (sp-admin-*)
          ↓
SpeakPath admin feature pages
```

Feature pages must import from `src/app/admin`.
They must not reference TailAdmin internals directly.
They must not copy TailAdmin page markup or utility class lists.
If TailAdmin source is later licensed, all TailAdmin-specific markup belongs inside
wrapper components or the admin shell — not in feature pages.

## Angular ViewEncapsulation Note

`AdminAppLayoutComponent` uses `ViewEncapsulation.None` so that the shell CSS
(sidebar, nav items, header, drawer, profile flyout) reaches the inner DOM of
`sp-admin-layout`, `sp-admin-sidebar`, and `sp-admin-header` child components.
This is intentional. Shell-level CSS is global for the admin module only.
Wrapper component CSS remains emulated/scoped inside each wrapper.

## TailAdmin Asset Status

**Phase 10X-D (2026-06-18):** TailAdmin free Angular template source has been imported.

- Source: https://github.com/TailAdmin/free-angular-tailwind-dashboard
- Commit: da992cf
- Location: `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
- License: MIT (see `templates/tailadmin/free-angular-tailwind-dashboard/LICENSE`)

The source is stored as a vendor reference under `templates/tailadmin/`.
SpeakPath app code does not import from this path.
Wrapper components and shell CSS adapt TailAdmin patterns.
See `docs/architecture/admin-tailadmin-adapter-inventory.md` for the full mapping.

TODO-10X-ASSETS: closed. Source imported in 10X-D.

**Phase 10X-E (2026-06-18):** All `sp-admin-*` wrappers now adapted to real TailAdmin patterns.

All 15 wrapper components (layout, sidebar, header, button, badge, card, stat-card, input, select,
form-field, modal, table, pagination, filter-bar, drawer) now use TailAdmin-sourced class structures
internally. Custom CSS approximations replaced with actual TailAdmin class names and structures.

Wrapper public APIs (inputs/outputs) remain stable — admin feature pages are unchanged.
See `docs/architecture/admin-tailadmin-adapter-inventory.md` for the full mapping and status.
See `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md` for full findings.

Remaining adapter work (10X-F): table sorting, dropdown, theme toggle, full filter bar alignment.

**Phase 10X-F (2026-06-18):** Admin wrapper capability completion.

New wrappers added:
- `sp-admin-dropdown` — TailAdmin `absolute z-40 rounded-xl border border-gray-200 bg-white shadow-lg` dropdown. Trigger/menu content projection. Click-outside + Escape close. `align` (left/right) and `width` (sm/md/lg) inputs.
- `sp-admin-table-actions` — Row action dropdown based on TailAdmin table-dropdown pattern. Three-dot trigger button. Supports generic `[actions]` array API with `(actionClick)` output, or full content projection for per-row conditional actions. Danger item red styling.
- `sp-admin-theme-toggle` — Admin-only dark/light toggle based on TailAdmin `ThemeToggleButtonComponent`. Uses `AdminThemeService` (stores preference in `adminTheme` localStorage key, isolated from student UI). Sun/moon SVG icons.
- `AdminThemeService` — admin-scoped theme service. Mirrors TailAdmin `ThemeService` pattern. Does not use student-UI theme.

Updated wrappers:
- `sp-admin-table` — now supports `sortable` column flag, `sortColumn`, `sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` sort icons, `aria-sort` attribute, keyboard-accessible sortable headers.
- `sp-admin-header` — now has named `[left]` and `[actions]` content slots. Theme toggle auto-rendered in right action zone.
- `sp-admin-filter-bar` — now has named `[search]`, `[filters]`, `[actions]` slots. Left/right zone split. Backward-compat general projection retained.

## Table Sorting Convention

- Consumer page owns sort state (`sortColumn`, `sortDirection` signals/properties).
- `sp-admin-table` emits `(sortChange)` with `{ column, direction }`.
- Page re-sorts its data array and passes sorted `rows` back to the table.
- Table does not sort internally. This enables server-side sort later.
- `hasActions` input adds an "Actions" column header when true.

## Row Action Dropdown Convention

- Use `sp-admin-table-actions` for all row action menus.
- For static action lists: pass `[actions]` array + handle `(actionClick)`.
- For conditional per-row actions (View/Edit/Archive depend on row state): use content projection inside `sp-admin-table-actions`. Projected buttons use `sp-adm-action-item` class for consistent styling.
- Do not put row action buttons directly in table cells without this wrapper.

## Dropdown Convention

- Use `sp-admin-dropdown` for all admin dropdown menus.
- Admin feature pages must not use TailAdmin `app-dropdown` directly.
- `align="right"` (default) — dropdown opens right-aligned with trigger.
- `width="md"` (default, `w-48`) — adjust for content width.
- Close on Escape and click-outside are built-in.

## Theme Toggle / Admin-Only Dark Mode

- `sp-admin-theme-toggle` is the only entry point for admin dark mode.
- It is automatically included in `sp-admin-header`.
- `AdminThemeService` stores preference in `adminTheme` localStorage key.
- This does not affect student-facing pages, which use their own light theme.
- Full dark-mode scoping (admin-only class boundary) is a TODO for 10X-G+.

Feature pages must import from `src/app/admin`.
They must not reference `templates/` directly.
They must not copy TailAdmin page markup or repeat long utility class lists.

## Folder Structure

```
src/LinguaCoach.Web/src/app/
  templates/
    README.md
    tailadmin/
      README.md                              <- version/license/source info
      free-angular-tailwind-dashboard/       <- vendored TailAdmin source (MIT)
        src/app/shared/layout/              <- Layout One shell reference
        src/app/shared/components/          <- UI primitive reference
        src/app/pages/                      <- demo pages — reference only, never import
        package.json                        <- TailAdmin deps — do NOT merge into SpeakPath
  admin/
    tokens/admin-tokens.css
    components/**                           <- sp-admin-* wrapper components
    services/**
    index.ts
```

`src/app/templates/tailadmin` is the vendor reference boundary.
`src/app/admin` is the SpeakPath adapter/wrapper layer.
No TailAdmin source is imported into SpeakPath app build paths.
Angular's compiler ignores `templates/` — it is not referenced in `tsconfig.app.json` or any import.

## Component Names

Canonical wrapper selectors use `sp-admin-*`.

Implemented foundation:

- `sp-admin-layout`
- `sp-admin-sidebar`
- `sp-admin-header`
- `sp-admin-page-header`
- `sp-admin-card`
- `sp-admin-stat-card`
- `sp-admin-button`
- `sp-admin-badge`
- `sp-admin-table`
- `sp-admin-empty-state`
- `sp-admin-loading-state`
- `sp-admin-error-state`
- `sp-admin-form-field`
- `sp-admin-input`
- `sp-admin-select`
- `sp-admin-pagination`
- `sp-admin-filter-bar`
- `sp-admin-modal`
- `sp-admin-drawer`
- `sp-admin-toast-outlet`

Existing adapter aliases remain for current pages:
`sp-admin-kpi-card`, `sp-admin-section-card`, and `sp-admin-data-table`.

## Design Tokens

Admin tokens live in `admin-tokens.css`.
Use CSS custom properties such as:

- `--sp-admin-bg`
- `--sp-admin-surface`
- `--sp-admin-border`
- `--sp-admin-text`
- `--sp-admin-text-muted`
- `--sp-admin-primary`
- `--sp-admin-green`
- `--sp-admin-amber`
- `--sp-admin-danger`
- `--sp-admin-sidebar-w`
- `--sp-admin-header-h`

Use these tokens inside admin wrappers.
Feature pages should prefer wrappers first.

## Services

Admin service facades live in `src/app/admin/services`.

- `AdminToastService` wraps the shared toast service.
- `AdminModalService` stores confirm-dialog requests.
- `AdminDrawerService` stores slide-in drawer state.

The modal and drawer services are foundations.
Richer orchestration remains deferred.

## Migration Checklist

For each admin page migration:

1. Replace page title blocks with `sp-admin-page-header`.
2. Replace common buttons with `sp-admin-button`.
3. Replace status pills with `sp-admin-badge`.
4. Replace repeated cards with `sp-admin-card` or `sp-admin-stat-card`.
5. Replace simple read-only tables with `sp-admin-table`.
6. Use `sp-admin-loading-state`, `sp-admin-error-state`, and `sp-admin-empty-state`.
7. Keep page-specific layout CSS small.
8. Do not move student UI into admin wrappers.

## Legacy Style Rule

Admin CSS that overrides or fights TailAdmin Layout One must not be added.
Avoid:
- Raw `background`, `color`, `padding` overrides on `.sp-admin-nav-item-active` outside the shell CSS.
- Repeated TailAdmin utility class lists in feature page templates.
- One-off component CSS for common UI that belongs in a wrapper.

When a page needs a visual fix, fix it in the wrapper component, shell, or tokens first.
Only use page-local CSS for truly unique content layout.

## Current Scope

Phase 10X-A migrated the admin shell plus proof pages.
Phase 10X-B migrated the core admin pages to the wrapper layer where feasible.
Phase 10X-C-F fixed the critical ViewEncapsulation bug, verified all gates,
and hardened TailAdmin Layout One alignment.

Migrated pages:

- Dashboard: wrapper page header remains; full inline dashboard CSS reduction is still partial.
- Students: wrapper page header, filter bar, and pagination; existing student management modals preserved.
- AI Config: wrapper page header, section cards, and admin badges around category/provider status.
- AI Usage: wrapper page header, stat cards, cards, table, badges, loading, empty, and error states.
- Prompts: wrapper page header, card, form field, table, badge, empty state, and button components.
- Exercise Types: wrapper page header, table, badges, buttons, and error state.
- Integrations: wrapper page header and section cards; existing operational forms preserved.
- Diagnostics: wrapper page header, loading, and error states from 10X-A remain.
- Curriculum: wrapper page header, filter bar, table, badges, buttons, loading, and error states for the list path.
- Usage Policies: wrapper page header, card, form field, table, badge, loading, empty, and error states.

Remaining legacy areas:

- Dashboard still has large component-local CSS for action cards, KPI layout, and placeholders.
- Student edit/reset modals still use page-local modal markup.
- AI Config still contains page-local form layout and some legacy utility class lists inside section cards.
- Integrations still contains page-local forms and legacy table utility classes inside wrapper cards.
- Curriculum create/edit/preview subviews still have page-local form markup.

Feature pages should not introduce new long TailAdmin class lists for common UI. Add or extend
`sp-admin-*` wrappers first, then use page-local CSS only for truly unique behavior.
