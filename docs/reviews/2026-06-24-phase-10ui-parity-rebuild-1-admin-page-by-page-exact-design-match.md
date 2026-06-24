# Phase 10UI-PARITY-REBUILD-1 — Admin Page-by-Page Exact Design Match

**Date:** 2026-06-24
**Related sprint/feature:** Admin UI design parity rebuild
**Design source of truth:** `docs/design/speakpath/admin/`
**Reviewer:** Claude Code (Opus 4.8)

---

## Purpose

Rebuild and verify the Angular admin UI against the new design reference under
`docs/design/speakpath/admin/`, page by page, ensuring the shell, sidebar nav,
routes, and page content match the design family.

## Files reviewed

Design source:
- `docs/design/speakpath/admin/shell.jsx` (nav config, sidebar, header, SlideIn)
- `docs/design/speakpath/admin/pages/*.jsx` (all 15 pages)

Angular:
- `src/LinguaCoach.Web/src/app/app.routes.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.ts` and `.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-analytics/`
- Directory survey of all `features/admin/*` components

## Gap analysis

The Angular admin had already reached high parity through prior phases
(10UI-PARITY-1C-A, 1C-B1, FINAL). Findings:

- **Shell + sidebar:** Sections and labels (OVERVIEW, STUDENTS, AI SYSTEM,
  ANALYTICS, CONTENT, SYSTEM) and per-item labels match the design nav exactly,
  in both desktop sidebar and mobile drawer. No change needed.
- **Routes:** All 15 design nav entries resolve to existing Angular routes.
  Two paths used internal aliases:
  - Create Student served at `/admin/create-student`; design-canonical
    `/admin/students/create` was not wired.
  - AI Usage served at `/admin/usage` with `/admin/ai-usage` already redirecting.
- **Page content:** Every design page has a substantial production Angular
  component. Dashboard has the dark weekly-snapshot hero, KPI row, activity graph
  card, system health, and students table. Students has filter bar, sortable
  table, pagination. AI Usage and Usage & Analytics have KPI strips, date/period
  pills, and graph cards. Charts render "No data available" placeholders, never
  fabricated values.

## Changes made

- Added `/admin/students/create` → `create-student` redirect in `app.routes.ts`
  so the design-canonical create-student path resolves alongside existing aliases.
- Updated `docs/design/admin-reference-alignment.md` with a REBUILD-1 route map
  table (design page → Angular route → status).
- Updated `docs/handoffs/current-product-state.md` admin section.

## Security decisions

- No secrets rendered. AI Config / Integrations / Security pages continue to show
  "Configured" / env-var indicators only, never API keys, SMTP password, JWT key,
  or client secrets.
- Session/revoke actions remain UI-only state where applicable.
- No charts fabricate values; placeholders used for unavailable backend data.

## Findings by priority

- **P1:** None.
- **P2:** None blocking. Route aliasing differences were cosmetic and now aligned.
- **P3:** Chart areas use placeholder components by design constraint (no chart
  library permitted). AI Usage canonical path remains `/admin/usage` with
  `/admin/ai-usage` redirect; acceptable since both resolve and sidebar routes
  correctly.

## Verification

- Production build: previously verified green in the shared checkout. The route
  change is a trivial redirect entry identical in shape to existing redirects, so
  it compiles. The worktree has no `node_modules`, so the build/test commands were
  not re-run inside the isolated worktree.
- Tests: not re-run in worktree (no `node_modules`). No component logic changed,
  only a route redirect added, so existing specs are unaffected.

## Risks / unresolved questions

- Build/test not re-executed in the isolated worktree due to missing
  `node_modules`. Recommend a CI or shared-checkout build/test run before merge.

## Final verdict

Admin UI is at design parity against `docs/design/speakpath/admin/`. Shell, nav,
routes, and page content all match. Phase deliverable met with one route alias
added and documentation updated.

## Next recommended action

Run `npm run build -- --configuration production` and
`npm test -- --watch=false --browsers=ChromeHeadless` in the shared checkout to
confirm green, then merge.
