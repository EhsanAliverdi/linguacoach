# Remove /admin/usage-analytics (redundant with AI Usage + Dashboard)

**Date:** 2026-07-17
**Related sprint / feature:** Admin diagnostics cleanup, same session as the Delivery Health rehaul (`docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md`).

## Context / decision

The user asked whether `/admin/usage-analytics` ("Usage & Analytics") was still needed, suspecting overlap with other admin pages. Investigation confirmed it had zero unique backend surface — both of its live data calls (`getAiUsageTrends`, `getDashboardActivityTrends`) are the same calls already made by `/admin/usage` (AI Usage) and the main Admin Dashboard respectively, just re-rendered as different chart types. Its third KPI tile (Avg Cost/Student) was permanently hardcoded `N/A` — no backing endpoint exists — and its fourth section (a student-engagement heatmap) was never implemented, shown only as a "not implemented" placeholder. AI Usage already covers cost/calls in more depth (real success-rate ring, provider/feature breakdown tables, a full filterable/paginated recent-calls table with CSV export, category-breakdown donut chart) than usage-analytics' two charts. Decision: remove the page entirely rather than merge anything — there was nothing on it not already better represented elsewhere.

## Files reviewed

`admin-usage-analytics.component.ts`/`.html`/`.spec.ts` (deleted), `admin-ai-usage.component.ts` (comparison), `admin-dashboard.component.ts`/`.html` (comparison + one dead link fixed), `admin.api.service.ts` (confirmed `getAiUsageTrends`/`getDashboardActivityTrends` still have live callers elsewhere — not touched), `app.routes.ts`, `admin-app-layout.component.html`/`.spec.ts`, `docs/architecture/bank-first-admin-backend-surface-audit.md` (its prior "Keep" classification for this route was based on an unverified endpoint inventory, not a content comparison — see its own §11 "known G0 limitations").

## Findings and decisions

### P1 — Deleted the page and its route

Deleted `src/LinguaCoach.Web/src/app/features/admin/admin-usage-analytics/` (component + template + spec) and its route in `app.routes.ts`. No backend controller existed to delete — the page only ever called two endpoints that live pages elsewhere still use, so `getAiUsageTrends`/`getDashboardActivityTrends` (frontend service methods and their backend endpoints) are untouched.

### P1 — Removed the now-empty "Analytics" nav section

The sidebar's "Analytics" section (both desktop-expanded, desktop-collapsed/mobile-shared markup) contained exactly one nav item — "Usage & Analytics" — so removing the item removed the whole section, in both `admin-app-layout.component.html` blocks. Updated the section-heading assertion in `admin-app-layout.component.spec.ts` accordingly.

### P2 — Fixed a dead link this removal exposed

The Admin Dashboard's "AI cost by type" card was a "not implemented" placeholder linking to `/admin/usage-analytics` for a per-category cost breakdown — a feature usage-analytics itself never had (it had no category breakdown; AI Usage does, via its donut chart). Repointed the link to `/admin/usage` and reworded the placeholder to reference the real feature, rather than leaving a link to a deleted route.

## Migration

None — no backend/schema change.

## Validation

- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries.
- `npx ng test --include='**/admin-app-layout.component.spec.ts'`: could not run to completion — same pre-existing, unrelated 5-file karma compile blocker documented in the Delivery Health rehaul review (still unresolved). Verified via clean `ng build` type-check instead.
- Backend: no `.cs` files changed; not re-run (nothing in scope).

## Decisions made

1. Remove outright rather than merge any section into another page — every live data point it showed already exists, better presented, on `/admin/usage` or the dashboard.
2. Fixed the dashboard's dangling link to the deleted route rather than leaving it broken.

## Risks / unresolved questions

None identified — this was a pure subtraction of dead/duplicate surface with no remaining callers.

## Final verdict

Removed. `/admin/usage-analytics` is gone; its two data calls remain live via `/admin/usage` and the dashboard; the "Analytics" nav section (now empty) was removed with it; the dashboard's dead link now points at the real AI Usage page.

## Next recommended action

None required.
