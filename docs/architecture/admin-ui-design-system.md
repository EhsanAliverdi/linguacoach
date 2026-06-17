---
status: current
lastUpdated: 2026-06-18 18:39
owner: architecture
supersedes:
supersededBy:
---

# Admin UI Design System

SpeakPath admin UI uses a wrapper component layer.

```
TailAdmin-inspired tokens and patterns
SpeakPath admin wrapper components
Admin feature pages
```

Feature pages must import from `src/app/admin`.
They should not copy TailAdmin page markup directly.
They should not repeat long utility class lists.

## Folder Structure

- `src/app/admin/tokens/admin-tokens.css`
- `src/app/admin/components/**`
- `src/app/admin/services/**`
- `src/app/admin/index.ts`
- `src/app/admin-template/tailadmin/**`

`admin-template/tailadmin` is the vendor adapter boundary.
No large TailAdmin assets are copied today.

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

## Current Scope

Phase 10X-A migrated the admin shell plus proof pages.
Phase 10X-B migrated the core admin pages to the wrapper layer where feasible.

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
