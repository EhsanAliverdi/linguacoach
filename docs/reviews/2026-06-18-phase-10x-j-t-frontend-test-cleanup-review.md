---
status: current
lastUpdated: 2026-06-19 14:30
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-J-T Frontend Test Cleanup Review

## Scope

Frontend tests only. No backend code, backend tests, product behavior, API behavior, or UI
functionality changed.

## Summary

Phase 10X-J-T removed or rewrote brittle frontend assertions that checked Tailwind, TailAdmin,
BEM, wrapper implementation, border/radius/spacing, inline style, and exact CSS class details.

Angular tests now prefer:
- rendered text and projected content
- form and CVA values
- emitted events
- role and ARIA attributes
- modal/dropdown open and close behavior
- sorting events
- wrapper/component presence where that protects migration intent

Playwright tests now prefer:
- accessible buttons and links
- roles and landmarks
- `aria-pressed` state
- visible text
- `data-testid` locators
- page behavior and smoke flows

## Tests Removed

- Angular test count before: 497
- Angular test count after: 480
- Playwright test count before: 190
- Playwright test count after: 188
- Total tests removed: 19

Removed tests were style-only checks such as:
- `sp-adm-btn-solid-primary`
- `sp-adm-badge-soft-success`
- `rounded-2xl`
- `rounded-3xl`
- `border-r`
- `text-xs text-gray-500`
- `flex` / `items-center`
- `sp-pref-chip--on`
- `sp-admin-modal` legacy CSS absence

## Tests Rewritten

Behavior-preserving rewrites replaced class checks with semantic assertions:
- row action tests use `getByRole('button', { name: 'Row actions' })`
- chip selection tests use `aria-pressed`
- sortable table tests use `role="button"` and `aria-sort`
- modal and drawer tests use `role="dialog"` and `aria-label`
- theme toggle tests use the button `aria-label`
- student layout Playwright checks use `getByRole('main')`
- alert checks use `role="alert"`

## Gates

Passed:
- `cd src/LinguaCoach.Web && npm ci`
- `cd src/LinguaCoach.Web && npm run build -- --configuration production`
- `cd src/LinguaCoach.Web && npm test -- --watch=false --browsers=ChromeHeadless`
  - 422 successful
- `npx playwright test e2e/admin-students-reset.spec.ts --workers=1 --reporter=dot`
  - 6 passed
- `npx playwright test --workers=1 --reporter=dot`
  - 188 passed
- `git diff --check`
  - clean

Backend gates intentionally not run for this frontend-only phase.

Graph update:
- `graphify update .` was attempted after code changes.
- It refused to overwrite `graphify-out/graph.json` because the rebuilt graph had fewer nodes than
  the existing graph. Graph files were left untouched.

## Documentation

Updated:
- `docs/architecture/admin-ui-design-system.md`
- `docs/sprints/current-sprint.md`
- `docs/handoffs/current-product-state.md`
- `TODOS.md`

Added:
- `TODO-10X-J-T-VISUAL-BASELINE`

## Risk

The test suite now protects behavior rather than visual class implementation.
Future visual regressions should be caught by a dedicated visual regression baseline, not by
restoring class assertions.
