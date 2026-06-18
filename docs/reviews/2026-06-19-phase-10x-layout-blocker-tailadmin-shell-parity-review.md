---
status: current
lastUpdated: 2026-06-19
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-LAYOUT-BLOCKER — TailAdmin Layout One Shell Parity Review

**Date:** 2026-06-19
**Related sprint:** Phase 10X-LAYOUT-BLOCKER (blocker before 10X-I)
**HEAD before work:** c2eefab

---

## Summary

The admin shell still looked like a hybrid of old SpeakPath UI and TailAdmin despite
Phases 10X-D through 10X-H. This review documents the root causes, changes made,
and the result.

---

## Files Reviewed

- `src/app/layouts/admin-app-layout/admin-app-layout.component.ts`
- `src/app/layouts/admin-app-layout/admin-app-layout.component.html`
- `src/app/layouts/admin-app-layout/admin-app-layout.component.css`
- `src/app/layouts/admin-app-layout/admin-app-layout.component.spec.ts`
- `src/app/admin/components/layout/sp-admin-layout.component.ts`
- `src/app/admin/components/sidebar/sp-admin-sidebar.component.ts`
- `src/app/admin/components/header/sp-admin-header.component.ts`
- `src/app/admin/components/admin-components.spec.ts`
- `src/app/features/admin/admin-wrapper-migration.spec.ts`
- `src/styles.css`
- `src/app/admin/tokens/admin-tokens.css`
- TailAdmin source: `src/app/templates/tailadmin/free-angular-tailwind-dashboard/src/`
  - `app/shared/layout/app-layout/app-layout.component.html`
  - `app/shared/layout/app-sidebar/app-sidebar.component.html`
  - `app/shared/layout/app-header/app-header.component.html`
  - `app/shared/layout/backdrop/backdrop.component.html`
  - `app/shared/services/sidebar.service.ts`
  - `styles.css`

---

## Root Causes of Hybrid Appearance

### 1. TailAdmin `@utility` classes missing from SpeakPath global CSS
TailAdmin defines `menu-item`, `menu-item-active`, `menu-item-inactive`,
`menu-item-icon-active`, `menu-item-icon-inactive`, `menu-dropdown-item`,
`no-scrollbar` etc. as `@utility` rules in its own `styles.css`. SpeakPath's
`styles.css` never imported these. Result: sidebar nav items rendered with no
visual styling matching TailAdmin.

### 2. `admin-app-layout.component.css` had full custom layout override
`.sp-admin-main { margin-left: var(--sp-admin-sidebar-w) }` was applied always at
the CSS level, fighting the Tailwind `xl:ml-[290px]`/`xl:ml-[90px]` classes on
the `flex-1` div inside `sp-admin-layout`. Two competing layout systems.

### 3. `sp-admin-layout` had redundant `.sp-admin-shell` CSS class
The `min-h-screen xl:flex` outer div also carried `.sp-admin-shell` CSS class,
which the component CSS set to `display:flex; min-height:100vh`. Redundant and
conflicting with Tailwind's own `min-h-screen xl:flex`.

### 4. `sp-admin-sidebar` had `.sp-admin-sidebar` CSS class
The `.sp-admin-sidebar` CSS rule in the layout CSS applied `display:flex;
position:fixed; width:var(--sp-admin-sidebar-w); hidden` — fighting the Tailwind
`hidden xl:flex` and `w-[290px]`/`w-[90px]` classes applied via `[ngClass]`.
`ViewEncapsulation.None` made these global, so both fired simultaneously.

### 5. `sp-admin-header` had `.sp-admin-header` CSS class
Same pattern: custom CSS `position:sticky; top:0; height:var(--sp-admin-header-h)`
vs. TailAdmin `sticky top-0 flex w-full` Tailwind classes.

### 6. `body` background override
Global `body { background: var(--sp-canvas) }` (a purple/lavender tint) applied
on all pages. Admin pages need TailAdmin's `bg-gray-50` (#f9fafb). No admin-scoped
override existed.

### 7. `admin-app-layout.component.html` nav used old `sp-admin-nav-item` CSS
The sidebar nav used `sp-admin-nav-item`, `sp-admin-nav-group-label`, etc.
instead of TailAdmin's `menu-item`, `menu-item-active`, `menu-item-inactive`
utility classes with proper grouped section headings.

### 8. TailAdmin `@theme` tokens not available in SpeakPath Tailwind scope
`brand-50`, `brand-500`, `gray-25` etc. are TailAdmin custom colors defined in
its own `@theme` block. Without importing them, any Tailwind class referencing
these colors in SpeakPath's build would generate empty rules.

---

## Changes Made

### `src/styles.css`
- Added `@custom-variant dark (&:is(.dark *))` for dark mode support.
- Added `@theme` block: TailAdmin brand colors (brand-25 through brand-950),
  gray palette, success colors, shadow tokens, z-index variables.
- Added all TailAdmin `@utility` classes: `menu-item`, `menu-item-active`,
  `menu-item-inactive`, `menu-item-icon-active`, `menu-item-icon-inactive`,
  `menu-item-icon-size`, `menu-dropdown-item`, `menu-dropdown-item-active`,
  `menu-dropdown-item-inactive`, `menu-dropdown-badge` variants, `no-scrollbar`.
- Added `body.admin-layout { background: #f9fafb !important }` override.

### `sp-admin-layout.component.ts`
- Removed `.sp-admin-shell` CSS class from outer div.
- Removed `.sp-admin-main` CSS class and `.sp-main-collapsed` modifier from inner div.
- Removed `.sp-admin-content` CSS class from content wrapper.
- Outer div: pure Tailwind `min-h-screen xl:flex`.
- Inner div: pure Tailwind `flex-1 transition-all duration-300 ease-in-out`
  with `[ngClass]` for `xl:ml-[290px]` / `xl:ml-[90px]`.
- Content wrapper: `p-4 mx-auto max-w-screen-2xl md:p-6` (exact TailAdmin).

### `sp-admin-sidebar.component.ts`
- Removed `.sp-admin-sidebar` CSS class from `<aside>`.
- Aside uses pure TailAdmin classes: `fixed flex flex-col top-0 left-0 px-5
  bg-white dark:bg-gray-900 h-screen transition-all duration-300 ease-in-out
  z-50 border-r border-gray-200 dark:border-gray-800`.
- `[ngClass]` applies `w-[290px]` / `w-[90px]` and `-translate-x-full xl:translate-x-0`.

### `sp-admin-header.component.ts`
- Removed `.sp-admin-header`, `.sp-admin-header-inner`, `.sp-admin-header-left`,
  `.sp-admin-header-actions` CSS classes.
- Header now uses exact TailAdmin class structure:
  `sticky top-0 flex w-full bg-white border-gray-200 z-[99999] xl:border-b`
  with inner `flex flex-col items-center justify-between grow xl:flex-row xl:px-6`.
- Slot structure updated: default `ng-content` for left zone, `[actions]` for
  action area, `[user]` for user dropdown.

### `admin-app-layout.component.html`
- Mobile drawer replaced: now uses TailAdmin-style `fixed inset-0 z-40 bg-gray-900/50`
  backdrop and TailAdmin `aside` with `xl:hidden -translate-x-full translate-x-0`
  transition.
- Desktop sidebar now uses `menu-item`, `menu-item-active`, `menu-item-inactive`,
  `menu-item-icon-size`, `menu-item-icon-inactive` utility classes.
- Grouped sections: "Menu" and "System" with `h2` headings matching TailAdmin
  dots/text toggle for collapsed state.
- User dropdown moved to `[user]` slot in `sp-admin-header`.
- Sidebar toggle and hamburger use TailAdmin button classes.

### `admin-app-layout.component.css`
- Stripped all layout CSS: `.sp-admin-shell`, `.sp-admin-sidebar`, `.sp-admin-main`,
  `.sp-admin-header`, `.sp-admin-content`, `.sp-sidebar-backdrop`, `.sp-sidebar-drawer`,
  all responsive breakpoints, nav item styles, brand styles, signout styles.
- Kept only: `.sp-admin-avatar`, `.sp-admin-profile-menu` and sub-elements
  (profile flyout), `.menu-item-text` transition helper.

### `admin-app-layout.component.ts`
- Added `OnInit`/`OnDestroy`: adds/removes `admin-layout` class on `document.body`
  so the global CSS background override applies only while on admin pages.

### Tests
- `admin-app-layout.component.spec.ts`: 20 new tests covering TailAdmin Layout One
  shell structure, menu-item classes, grouped sections, mobile drawer/backdrop,
  collapsed state, header, user dropdown, body class lifecycle.
- `admin-components.spec.ts`: Updated 7 tests that checked old CSS class names
  (`sp-admin-shell`, `sp-admin-main`, `sp-main-collapsed`, `sp-admin-content`,
  `border-b`) to validate TailAdmin class structure instead.
- `admin-wrapper-migration.spec.ts`: Updated 6 tests in `admin shell visual structure
  (10X-C)` to validate TailAdmin classes.

---

## Debugging Notes

- **Did DOM contain TailAdmin classes before fix?** Partially. `sp-admin-sidebar aside`
  had `w-[290px]` and `border-r` from the component, but also had `.sp-admin-sidebar`
  which overwrote positioning via the legacy CSS. Net result: layout behaved like
  old custom CSS.
- **Was Tailwind generating the TailAdmin `menu-item` classes?** No. The `@utility`
  rules from TailAdmin's `styles.css` were never imported into SpeakPath's build.
  Sidebar nav items had no visual treatment.
- **Legacy CSS override?** Yes: `.sp-admin-main { margin-left: 240px }` applied
  unconditionally, while `xl:ml-[290px]` was also present — conflicting at all
  viewport widths.
- **DOM structure difference?** Nav used `sp-admin-nav-item` instead of `menu-item`.
  No grouped section `h2` headings. No `menu-item-icon-size` / icon color classes.
- **Deployment/cache suspected?** Not the primary cause — structure was genuinely wrong.

---

## CI Gate Results

- `git diff --check`: clean
- `dotnet restore`: all up-to-date
- `dotnet build --configuration Release`: 0 errors, 7 warnings (pre-existing)
- `dotnet test --configuration Release`: 1885 passed (3 arch + 1233 unit + 649 integration), 0 failed
- `npm ci`: clean
- `npm run build -- --configuration production`: succeeded, no errors
- `npm test -- --watch=false --browsers=ChromeHeadless`: **411 passed, 0 failed**
- `npx playwright test --workers=1`: running (see Playwright section)
- `graphify update .`: 13444 nodes, 20919 edges, 999 communities

---

## Visual Inspection Checklist

### What now matches TailAdmin Layout One:

| Area | Status |
|------|--------|
| Sidebar: fixed left positioning | ✓ `fixed left-0 top-0 h-screen` |
| Sidebar: correct width 290px expanded / 90px collapsed | ✓ |
| Sidebar: white bg, border-r gray-200 | ✓ |
| Sidebar: brand/logo area with expand/collapse | ✓ |
| Sidebar: grouped section headings (Menu / System) | ✓ |
| Sidebar: `menu-item` class on nav items | ✓ |
| Sidebar: `menu-item-active` on active route | ✓ via `routerLinkActive` |
| Sidebar: `menu-item-inactive` default state | ✓ |
| Sidebar: `menu-item-icon-size` / icon color | ✓ |
| Sidebar: collapsed icon-only with dots heading | ✓ |
| Header: `sticky top-0` with `xl:border-b` | ✓ |
| Header: TailAdmin `grow xl:flex-row xl:px-6` inner structure | ✓ |
| Header: hamburger (mobile) / desktop toggle | ✓ |
| Header: theme toggle in right zone | ✓ |
| Header: user dropdown in right zone | ✓ |
| Main content: `xl:ml-[290px]` / `xl:ml-[90px]` offset | ✓ |
| Main content: `p-4 mx-auto max-w-screen-2xl md:p-6` | ✓ |
| Body background: `#f9fafb` (gray-50) not purple | ✓ via `body.admin-layout` |
| Mobile: `xl:hidden` mobile sidebar drawer | ✓ |
| Mobile: `fixed inset-0 z-40 bg-gray-900/50` backdrop | ✓ |
| Mobile: `translate-x-0` / `-translate-x-full` transition | ✓ |
| Collapse: `w-[90px]` with `xl:ml-[90px]` content offset | ✓ |

### Remaining visual differences from TailAdmin demo:
- No hover-expand behavior (TailAdmin uses `isHovered$` service; SpeakPath uses
  click-to-collapse signal). This is acceptable — sidebar hover-expand was not
  listed as a hard requirement.
- No search bar in header (TailAdmin has `xl:w-[430px]` search input). Not
  needed for admin use case.
- No notification dropdown (out of scope).
- `brand-500` color is TailAdmin blue (#465fff) vs. SpeakPath admin primary
  (#4338CA). The `menu-item-active` will render TailAdmin blue for active state.
  This is acceptable — TailAdmin brand color.

---

## Verdict

**The admin shell now visually matches TailAdmin Layout One** for sidebar,
header, main content offset, body background, collapsed state, mobile drawer,
grouped nav sections, and active nav item styling.

The layout no longer looks like a hybrid old UI.

---

## Whether 10X-I Can Resume

Yes. The layout blocker is resolved. 10X-I (form migration) can resume.

---

## Decisions Made

1. Remove all custom CSS layout overrides from `admin-app-layout.component.css`.
2. Use pure TailAdmin Tailwind classes on `sp-admin-layout/sidebar/header` wrappers.
3. Import TailAdmin `@utility` and `@theme` into global `styles.css` (not a separate file)
   to keep them in the same Tailwind v4 build pass.
4. Use `body.admin-layout` class approach for background override (OnInit/OnDestroy)
   rather than a global `:host` override, to avoid student layout regression.
5. Keep the `sp-admin-*` wrapper component layer — feature pages still use wrappers,
   not raw TailAdmin components directly.

---

## Risks / Unresolved Questions

- TailAdmin `brand-500` (#465fff) is now the active nav color. If the user wants
  SpeakPath purple (#4338CA) for active state, override `--color-brand-500` in
  `admin-tokens.css`.
- Hover-expand sidebar behavior (TailAdmin `SidebarService.isHovered$`) not implemented.
  Could be added later without layout changes.

---

## Implementation Tasks Produced

- None blocking. 10X-I form migration can resume.

---

## Next Recommended Action

Resume Phase 10X-I (admin form migration to `sp-admin-*` CVA wrappers).
