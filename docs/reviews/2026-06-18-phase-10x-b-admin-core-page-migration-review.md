---
status: current
lastUpdated: 2026-06-18 18:39
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-B Admin Core Page Migration Review

## Scope

Migrated existing admin pages toward the SpeakPath-owned `sp-admin-*` wrapper layer.
No new backend business features were added.

## Pages Migrated

- Dashboard: retained wrapper page header; further CSS reduction remains deferred.
- Students: wrapper page header, filter bar, pagination.
- AI Config: wrapper page header, section cards, and status badges.
- AI Usage: wrapper page header, stat cards, cards, tables, badges, loading, empty, and error states.
- Prompts: wrapper page header, card, form field, table, badge, empty state, and buttons.
- Exercise Types: wrapper page header, table, badges, buttons, and error state.
- Integrations: wrapper page header and section cards.
- Diagnostics: wrapper header/loading/error proof migration preserved.
- Curriculum: wrapper page header, filter bar, table, badges, buttons, loading, and error states for list view.
- Usage Policies: wrapper page header, card, form field, table, badge, loading, empty, and error states.

## Wrapper Improvements

- `sp-admin-table` now supports projected custom table content.
- `sp-admin-badge` gained `primary` tone.
- `sp-admin-empty-state` supports an optional title.
- `sp-admin-filter-bar` now spaces filter/action content more flexibly.

## Tests

- Added `admin-wrapper-migration.spec.ts`.
- Extended Exercise Types, Curriculum, and Usage Policies specs for wrapper assertions.
- Added Playwright mobile overflow assertion for `/admin/students` and `/admin/ai-config`.

## Deferred

- Dashboard inline CSS reduction.
- Student edit/reset/archive modal internals.
- AI Config form internals.
- Integrations form and table internals.
- Curriculum create/edit/preview form internals.
- Full 10R-F usage governance UX.
- Full 10U AI usage redesign.
- 10V prompt playground.
- Notification platform, enterprise auth/security, observability stack, billing,
  StudentProfile.CefrLevel migration, full placement engine, and full mastery engine.

## Risk

The migration is intentionally shallow in the most complex operational pages.
Those pages now sit inside wrapper sections, but some inner controls still use
legacy page-local classes. This keeps behavior stable while documenting the next cleanup pass.
