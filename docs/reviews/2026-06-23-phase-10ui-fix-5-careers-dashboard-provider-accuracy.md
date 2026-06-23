# Phase 10UI-FIX-5 — Careers Route Tombstone and Dashboard AI Provider Accuracy

**Date:** 2026-06-23
**Sprint:** Current (10UI series)
**HEAD before work:** ede8a24
**Related phases:** 10UI-FIX-4 (identified both issues)

---

## Scope

Two stale/misleading UI issues found by 10UI-FIX-4:

1. `admin-usage/` folder — dead code component never routed, contradicts the real AI usage page with emoji placeholder.
2. `/admin/careers` route — orphan page, title says "Curriculum", UI pre-wrapper-era, duplicate of `/admin/curriculum`.
3. Dashboard "AI provider: Configured" stat card — always static regardless of actual configuration state.

---

## 1. Stale Route / Component Decisions

### AdminUsageComponent — deleted

**Decision: DELETE.**

`src/app/features/admin/admin-usage/admin-usage.component.ts` had no route pointing to it in `app.routes.ts`. The real AI usage page (`/admin/usage`) routes to `AdminAiUsageComponent` in `admin-ai-usage/`. The stale component showed emoji placeholder cards saying "Analytics not yet tracked" — contradicting a fully-implemented page. Fully unreachable → deleted the file and folder.

### `/admin/careers` route — redirected

**Decision: REDIRECT to `curriculum`.**

Changed route from `loadComponent: AdminCareersComponent` to `redirectTo: 'curriculum'`. The `AdminCareersComponent` file is retained (not deleted) because:
- It is imported by name in `admin-careers/` and has real backend wiring.
- Removing the file would require verifying no other references exist.
- A redirect is the safe, bookmarkable-friendly fix. Any saved `/admin/careers` URL now lands on `/admin/curriculum`.

`AdminCareersComponent` itself is still a candidate for deletion in a later cleanup phase (TODO-UI-11 still open).

---

## 2. Dashboard AI Provider Status

### Data source

`AdminApiService.listAiCategories()` — existing method, calls `GET /api/admin/ai/categories`. Returns `AiConfigCategoryItem[]` with `providerName: string | null` per category.

### Stat card behaviour

| State | Label shown | Tone |
|-------|------------|------|
| All categories have `providerName` | "Configured" | violet |
| Some categories have `providerName` | "N/M configured" | amber |
| No category has `providerName` | "Not configured" | amber |
| Empty category list | "Not configured" | amber |
| API call errors | "Unknown" | neutral |
| Loading | "—" (loading spinner) | neutral |

### AI System card behaviour

Replaced four hardcoded "Active" badge rows with a live `@for` loop over `aiCategories()`. Each row shows:
- Category `displayName` (from API)
- `providerName` badge (`tone="success"`) when configured
- "Not configured" badge (`tone="warning"`) when `providerName` is null

Error state: shows "Configuration status — Unavailable" row.
Empty list: shows "No categories configured — Action needed" row.
Loading: shows `sp-admin-loading-state`.

No secrets, API keys, or model credentials are displayed. Only `providerName` (e.g. "OpenAI") and `displayName` are shown.

---

## 3. Files Changed

| File | Change |
|------|--------|
| `src/app/app.routes.ts` | `/admin/careers` changed from `loadComponent` to `redirectTo: 'curriculum'` |
| `src/app/features/admin/admin-usage/` | **Deleted** — entire folder removed |
| `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts` | Import `AiConfigCategoryItem`, `SpAdminStatCardTone`, `computed`; add `aiCategories`, `loadingAiCategories`, `aiConfigError` signals; add `aiProviderLabel` + `aiProviderTone` computed; call `listAiCategories()` in `ngOnInit`; replace static stat card binding; replace static AI System card with live `@for` loop |
| `src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts` | Rewritten — adds `listAiCategories` spy to `makeAdminApi`; adds 10 new tests covering configured/partial/empty/none/error states for stat card and AI System card |
| `src/app/features/admin/admin-wrapper-migration.spec.ts` | Added `listAiCategories` spy (returns `of([])`) to two dashboard mock setups |

---

## 4. Tests Added / Updated

### New tests in `admin-dashboard.component.spec.ts`

- `calls listStudents, getStats, and listAiCategories on init`
- `shows "Configured" label when all categories have a provider`
- `shows partial label when only some categories configured`
- `shows "Not configured" label when no categories have a provider`
- `shows "Not configured" label when categories list is empty`
- `shows "Unknown" label when AI config API errors`
- `renders AI System section with live category names`
- `shows "Not configured" badge for unconfigured category`
- `shows "Action needed" when AI System has no configured categories`
- `shows "Unavailable" in AI System when API errors`
- `renders link to /admin/ai-config`
- `does not display any API key or secret value`

### Updated tests in `admin-wrapper-migration.spec.ts`

- "dashboard renders with admin wrapper components" — added `listAiCategories` spy
- "dashboard renders KPI grid cards" — added `listAiCategories` spy

---

## 5. User-Visible Behaviour

- `/admin/careers` now redirects to `/admin/curriculum`. No dead page shown.
- Dashboard AI provider stat card shows real configured state (not always "Configured").
- Dashboard AI System card shows real category names and providers (not hardcoded "Active" rows).
- Admin can link through to `/admin/ai-config` from the dashboard AI System card footer.

---

## 6. Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1045/1045 |
| Backend unchanged | n/a — no backend changes |
| Playwright | Not run — no new route/E2E behaviour; redirect is covered by Angular router unit tests |

---

## 7. Deferred

- `AdminCareersComponent` file itself (in `admin-careers/`) is retained but orphaned. TODO-UI-11 tracks final removal decision.
- Dashboard reference design gap (area chart, engagement metrics, at-risk students, cost tracking) — deferred. Not in scope for 10UI-FIX-5.
- Static "Online" badge on AI System card header — removed; the card now shows real per-category status. The slot="actions" badge was removed in favour of the live loop.

---

## 8. Confirmation

- No backend/API/migration changes.
- No student UI changes.
- No full dashboard redesign.
- No new admin pages.
- No new major components.
- Only `listAiCategories()` (pre-existing `AdminApiService` method) used for live data.
