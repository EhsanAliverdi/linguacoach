# Phase 10X-K-PW-FIX: Admin Diagnostics Nav Playwright Fix

**Date:** 2026-06-19
**Sprint:** Post-10R maintenance fix
**Related:** Pre-existing failure first noted in 10R-K validation

---

## Root Cause

The failing test used the selector `[routerlink="/admin/diagnostics"]` to find the Diagnostics nav item.

Angular's `[routerLink]` binding renders a standard `href` attribute in the compiled app — not a `routerlink` attribute. The `routerlink` attribute form only appears transiently during Angular template compilation in dev/debug mode via `ng-reflect-*` attributes, and is never reliably present in a production or Playwright-served build.

The `SpAdminSidebarNavItemComponent` renders:

```html
<a [routerLink]="route" ...>
```

Which produces in the DOM:

```html
<a href="/admin/diagnostics" ...>
```

The selector `[routerlink="/admin/diagnostics"]` matched nothing, causing a 5-second timeout.

The Diagnostics nav item itself was present and correct. No UI change was needed.

---

## Fix

**File changed:** `src/LinguaCoach.Web/e2e/admin-screenshots.spec.ts`

Updated selector from:
```typescript
await page.waitForSelector('[routerlink="/admin/diagnostics"]', { state: 'attached', timeout: 5000 });
```

To:
```typescript
await page.waitForSelector('a[href="/admin/diagnostics"]', { state: 'attached', timeout: 5000 });
```

One line changed. No UI, routing, component, or product behavior changed.

---

## Gates

| Gate | Result |
|---|---|
| `git diff --check` | PASS |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (681/681) |
| Playwright: `admin-screenshots.spec.ts` (failing test only) | PASS (1/1) |
| Playwright: `admin-screenshots`, `admin-student-detail`, `admin-students-reset` | PASS (31/31) |

---

## Confirmation

- No backend/API/product behavior changed.
- No unrelated admin UI refactor.
- No commit. No push.
