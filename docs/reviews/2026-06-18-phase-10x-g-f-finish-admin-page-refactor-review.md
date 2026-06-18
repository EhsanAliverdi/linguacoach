---
status: current
lastUpdated: 2026-06-18
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-G-F — Finish Remaining Admin Page Refactor to TailAdmin-backed Wrappers

**Date:** 2026-06-18
**Related sprint:** Phase 10X-G-F (admin UI design system)
**HEAD before work:** 4708d24

## Summary

Phase 10X-G-F finishes the wrapper consistency work begun in 10X-B/10X-G. The goal was to ensure all
existing admin pages consistently use TailAdmin-backed `sp-admin-*` wrappers, never raw TailAdmin
internals or duplicated legacy markup.

A key finding during investigation: the priority pages named in the brief (AI Usage, Prompts, Exercise
Types, Diagnostics, Usage Policies, Integrations cards, Curriculum list) were already migrated to
wrappers in 10X-B and 10X-G. The genuine remaining legacy was concentrated in two places: the Students
row table/badges, and the Curriculum form fields (a deferred 10X-G TODO). Those were completed here.

## Files reviewed

- `src/app/features/admin/admin-students/admin-students.component.ts`
- `src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` (verified already migrated)
- `src/app/features/admin/admin-prompts/admin-prompts.component.ts` (verified)
- `src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts` (verified)
- `src/app/features/admin/admin-diagnostics/*` (verified)
- `src/app/features/admin/admin-usage-policies/*` (verified)
- `src/app/features/admin/admin-integrations/*` (verified; operational forms deferred)
- `src/app/admin/components/{table,badge,form-field,input,select}/*` (wrapper APIs)
- `src/app/features/admin/admin-wrapper-migration.spec.ts`

## Files changed

- `admin-students.component.ts` — table wrapped in `sp-admin-table`; lifecycle/onboarding/CEFR badges
  → `sp-admin-badge`; removed obsolete page-local CSS (`.sp-admin-pagination`, `.sp-admin-row-actions`,
  `.sp-admin-link-button`, `.sp-admin-danger-link`).
- `admin-curriculum.component.ts` — create/edit and routing-preview form fields → `sp-admin-form-field`.
- `admin-wrapper-migration.spec.ts` — 2 new tests (Students table/badge/actions; Curriculum form fields).
- Docs: design-system, adapter inventory, sprint, product-state, TODOs.

## Pages refactored

| Page | Wrappers added this phase | Status |
|---|---|---|
| Students | `sp-admin-table` (projection), `sp-admin-badge` x3 | Refactored |
| Curriculum | `sp-admin-form-field` (create/edit + preview) | Refactored |
| AI Usage | (already migrated 10X-B/G) | Verified |
| Prompts | (already migrated) | Verified |
| Exercise Types | (already migrated) | Verified |
| Diagnostics | (already migrated) | Verified |
| Usage Policies | (already migrated) | Verified |
| Integrations | cards already migrated; operational forms deferred | Partial (deferred) |

## Findings by priority

### P1 — Form wrapper value-binding limitation (resolved by design decision)

`sp-admin-input` and `sp-admin-select` bind to a plain `@Input() value` with an internal `[(ngModel)]`
and no `ControlValueAccessor` or value output. Replacing a `[(ngModel)]`-driven native control with
them would silently break form submission. Decision: use `sp-admin-form-field` for label/hint/error
structure and keep native `.sp-input` controls inside. Documented as a hard rule in the design-system
doc and tracked as TODO-10X-FORMS-CVA. This is why the AI Config and Integrations field-level
migrations remain deferred.

### P2 — Most priority pages already migrated

The brief assumed broad legacy remained. In fact 10X-B/10X-G already migrated headers, cards, stat
cards, tables (projection), badges, and state components across AI Usage, Prompts, Exercise Types,
Diagnostics, and Usage Policies. Avoided redundant churn and regression risk by verifying rather than
rewriting.

### P3 — Legacy CSS reduced

Removed dead page-local CSS from Students now owned by wrappers (pagination, row-action links, badge).
No student-UI CSS touched.

## Decisions made

- Do not migrate ngModel forms to `sp-admin-input`/`sp-admin-select` until a ControlValueAccessor
  exists. Use `sp-admin-form-field` + native control instead.
- Do not rewrite already-migrated pages; verify and move on.
- Defer Integrations operational forms, AI Config dense credentials grid, student modals, and the
  dark-mode class boundary (unchanged scope decisions from 10X-G).

## Implementation tasks produced

- TODO-10X-FORMS-CVA (new): add ControlValueAccessor to `sp-admin-input`/`sp-admin-select`.
- TODO-10X-G-AICONFIG-FORMS, TODO-10X-G-INTEGRATIONS-FORMS, TODO-10X-D-MODAL, TODO-10X-G-DARKMODE
  remain open.

## Risks / unresolved questions

- The form-field migration is structural only (labels/hints); the underlying `.sp-input` styling is
  still student-design-system sourced. Full visual parity awaits the CVA wrappers.
- Admin-only dark-mode boundary still relies on localStorage key isolation only (TODO-10X-G-DARKMODE).

## Gate results

- git diff --check: clean
- dotnet build (Release): 0 errors
- dotnet test (Release): 1885 passed (3 architecture + 1233 unit + 649 integration), 0 failed
- Angular build (production): clean (pre-existing CSS selector warnings only)
- Angular tests (ChromeHeadless): 379 passed (up from 377), 0 failed
- Playwright (workers=1): 188 passed

## Final verdict

Approved. All required gates pass. Wrapper consistency completed for the genuinely remaining legacy
(Students, Curriculum forms). Other priority pages verified already migrated. Deferred items are
documented with clear rationale and TODOs.

## Next recommended action

Add `ControlValueAccessor` to `sp-admin-input`/`sp-admin-select` (TODO-10X-FORMS-CVA), then complete
field-level migration of AI Config and Integrations forms and the student management modals.
