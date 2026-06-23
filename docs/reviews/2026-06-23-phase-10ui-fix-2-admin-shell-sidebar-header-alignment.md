# Phase 10UI-FIX-2 — Admin Shell, Sidebar, and Header Visual Alignment

**Date:** 2026-06-23
**Sprint:** 10UI-FIX-2
**HEAD before:** c6cf6ce
**Type:** Visual alignment — no backend/API/migration/feature changes

---

## Reference Files Inspected

- `docs/design/speakpath/admin/Admin.html` — full CSS token and layout definitions
- `docs/design/speakpath/admin/shell.jsx` — NAV config, icon system, sidebar/header JSX
- `docs/design/admin-reference-alignment.md` — nav/page/component mapping

Key reference values extracted:

| Token | Reference value |
|-------|----------------|
| Canvas bg | `#F6F4FB` (warm lavender) |
| Surface | `#FFFFFF` |
| Border | `#ECE9F5` (lavender-tinted) |
| Primary | `#5B4BE8` |
| Active nav bg | `#EDEBFF` |
| Active nav text | `#3A2EA8` |
| Active indicator | 3px left bar, `#5B4BE8`, 56% height |
| Sidebar width | 240px / 64px collapsed |
| Header height | 60px |
| Nav item padding | 9px 14px, margin 1px 8px, radius 8px |
| Nav label size | 13px, weight 600 |
| Nav section label | 9.5px, weight 800, `#BDB8CC`, letter-spacing .1em |

---

## Changes Made

### 1. `src/styles.css` — brand palette and nav item CSS

- **Brand palette updated** from TailAdmin blue (`#465fff` family) to SpeakPath indigo (`#5B4BE8` family):
  - `brand-50`: `#edebff` (active nav bg)
  - `brand-500`: `#5b4be8` (primary)
  - `brand-700`: `#3a2ea8` (active nav text)
  - Full 25–950 scale aligned

- **`body.admin-layout` background** changed from `#f9fafb` (cool gray) to `var(--sp-admin-bg)` = `#F6F4FB` (warm lavender)

- **`.sp-nav-item` CSS added** — new semantic nav item classes replacing TailAdmin `menu-item` utilities:
  - Default state: `color:#4B4462`, hover: `background:#F6F4FB`
  - Active state: `background:#EDEBFF`, `color:#3A2EA8`
  - **Active indicator bar**: `::before` pseudo, 3px left, 56% height, `#5B4BE8`, rounded right — reference design signature
  - `.sp-nav-icon`: 18×18, SVG 17×17
  - `.sp-nav-label`: overflow hidden, flex 1

### 2. `admin-tokens.css` — token alignment

- `--sp-admin-sidebar-w-collapsed`: `72px` → `64px`
- `--sp-admin-bg`: `#F8FAFC` → `#F6F4FB`
- `--sp-admin-surface-subtle`: `#F8FAFC` → `#FBFAFE`
- `--sp-admin-border`: `#E2E8F0` → `#ECE9F5`
- `--sp-admin-border-subtle`: `#F1F5F9` → `#F4F2FC`
- `--sp-admin-primary`: `#4338CA` → `#5B4BE8`
- `--sp-admin-primary-bg`: `#EEF2FF` → `#EDEBFF`
- `--sp-admin-primary-hover`: `#3730A3` → `#3A2EA8`
- `--sp-admin-primary-focus`: `#C7D2FE` → `#C0BAF9`
- Shadow action and focus ring rgba updated to match `#5B4BE8`

### 3. `sp-admin-layout.component.ts` — shell widths

- Background: `bg-gray-50` → `style="background:var(--sp-admin-bg)"`
- Collapsed margin: `xl:ml-[90px]` → `xl:ml-[64px]`
- Expanded margin: `xl:ml-[290px]` → `xl:ml-[240px]`

### 4. `sp-admin-sidebar.component.ts` — sidebar width and border

- Expanded width: `w-[290px]` → `w-[240px]`
- Collapsed width: `w-[90px]` → `w-[64px]`
- Border: `border-r border-gray-200` → `style="border-right:1px solid var(--sp-admin-border)"`

### 5. `sp-admin-header.component.ts` — height and border

- Height: `h-16` (64px) → `style="height:var(--sp-admin-header-h)"` (60px)
- Border: `border-b border-gray-200` → `style="border-bottom:1px solid var(--sp-admin-border)"`

### 6. `sp-admin-sidebar-section.component.ts` — section label style

- Font size: `text-xs` (12px) → `text-[9.5px]`
- Color: `text-gray-400` (#98a2b3) → `style="color:var(--sp-admin-text-faint)"` (#CBD5E1)
- Added: `font-extrabold`, `tracking-[.1em]`

### 7. `sp-admin-sidebar-nav-item.component.ts` — nav item class

- Replaced `menu-item`/`menu-item-active`/`menu-item-inactive`/`menu-item-icon-size`/`menu-item-icon-inactive` TailAdmin utilities with `sp-nav-item`/`sp-nav-active`/`sp-nav-icon`/`sp-nav-label` SpeakPath classes defined in styles.css
- Cleaner semantic class names, no dependency on TailAdmin utility coupling

### 8. `admin-app-layout.component.html` — mobile drawer

- Mobile drawer width: `w-[290px]` → `w-[240px]`
- Mobile drawer border: `border-gray-200` → `style="border-right:1px solid var(--sp-admin-border)"`
- Drawer brand area: `border-gray-200 py-4` → `style="height:var(--sp-admin-header-h);border-bottom:1px solid var(--sp-admin-border)"` — aligns with header height
- Desktop brand area: same token treatment

---

## Nav Links Confirmed Present

All required routes verified by test and visual inspection:

| Route | Label | Section |
|-------|-------|---------|
| /admin | Dashboard | Menu |
| /admin/students | Students | Menu |
| /admin/ai-config | AI Config | Menu |
| /admin/prompts | Prompts | Menu |
| /admin/usage | AI Usage | Menu |
| /admin/usage-policies | Usage Policies | Menu |
| /admin/curriculum | Curriculum | Menu |
| /admin/exercise-types | Exercise Types | Menu |
| /admin/notifications | Notifications | Menu |
| /admin/integrations | Integrations | System |
| /admin/diagnostics | Diagnostics | System |
| /admin/security | Security | System |

---

## Behaviours Preserved

- Sticky header (unchanged)
- Mobile drawer open/close/swipe-to-close (unchanged)
- Desktop sidebar collapse/expand with localStorage persistence (unchanged)
- Escape key closes mobile drawer (unchanged)
- Active route highlighting (now via `sp-nav-active` + RouterLinkActive)
- Logout only in user/profile dropdown — NOT in sidebar (verified by test)
- Notification dropdown still renders
- User menu still renders
- Theme toggle still renders
- Dark mode: bg-white/dark:bg-gray-900 sidebar and header preserved
- Router outlet projects page content (unchanged)

---

## Behaviours NOT Changed

- Page-level card/table/button/form components
- Student UI
- Backend/API/migrations
- Admin routing structure

---

## Tests

**New tests added** (10 new specs in second describe block):

1. Desktop sidebar contains all 12 required nav routes
2. Usage Policies renders in desktop sidebar
3. Curriculum renders in desktop sidebar
4. Security renders in desktop sidebar
5. Nav items use `sp-nav-item` class
6. Logout NOT in sidebar nav
7. Mobile drawer contains Usage Policies and Curriculum
8. Mobile drawer contains Security
9. Header renders profile menu
10. Shell projects via router-outlet

**Test count:** 1025 → **1035 / 1035 PASS**

---

## Playwright Status

Not run. No route or navigation behaviour changed. All existing nav tests are component-level. No new page routes added.

---

## Gates

- `git diff --check`: clean
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: 1035/1035 PASS
- Backend: not required (no backend source changed)

---

## Deferred Items

- Page-level visual alignment (dashboard cards, table headers, filter bars) — future phase
- Dark mode border/bg full audit — current dark mode preserved but not deeply aligned with reference
- `sp-nav-soon` badge pattern (reference has "SOON" badge on analytics items) — can be added to nav item component in future phase
- Sidebar footer user area (reference design has name/role in sidebar footer, current design uses header dropdown only) — intentional divergence, not aligned in this phase

---

## Decisions Made

- Replaced TailAdmin `menu-item*` utilities with `sp-nav-item*` SpeakPath classes for the active indicator bar. The TailAdmin utilities did not support the `::before` pseudo-element pattern needed.
- Kept `body.admin-layout` override in `styles.css` rather than moving it to tokens, so it continues to control body element background from a single place.
- Did not change `brand-*` gray palette — only the `brand-*` (indigo) palette changed. All gray values remain TailAdmin.
- Sidebar 240px/64px matches reference exactly. Previous 290px/90px was TailAdmin default.

---

## Next Recommended Phase

**10UI-FIX-3** — Admin dashboard KPI card alignment (live data audit + stat card layout matching reference `dashboard.jsx`).
