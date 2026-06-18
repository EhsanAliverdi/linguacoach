---
status: current
lastUpdated: 2026-06-18 23:00
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
When adapting TailAdmin patterns into wrappers (10X-E/10X-F):
1. Reference `templates/tailadmin/free-angular-tailwind-dashboard/src/app/shared/`.
2. Adapt markup/classes inside `sp-admin-*` wrapper components.
3. Remove approximation CSS replaced by real TailAdmin patterns.
4. Feature pages must not change.

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
