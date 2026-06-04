# Layout Stabilisation Sprint

**Date**: 2026-06-04
**Status**: Complete — v3 final cleanup (collapse in header, admin drawer, overflow, change-password)

---

## Problem

The UI reached feature completion but suffered from inconsistent layout implementation across pages. The design pass added visual polish but scattered layout logic across individual page components, resulting in:

- Duplicated sidebar/header HTML in every student page (learning-path, progress, profile)
- Activity page had an empty HTML file — no content at all
- PublicLayout had a double `sp-public-card` wrapper causing cramped login
- Inconsistent spacing and alignment across pages
- Admin pages visually inconsistent with student pages

---

## What Was Fixed

### Layout components (already existed, routing was correct)

All three layout components already existed and the routing was already correct:
- `PublicLayoutComponent` — login/landing
- `StudentAppLayoutComponent` — dashboard/my-path/activity/progress/profile/onboarding
- `AdminAppLayoutComponent` — admin pages

### Files changed

| File | Change |
|---|---|
| `layouts/public-layout/public-layout.component.ts` | Removed extra `sp-public-card` wrapper (login provides its own) |
| `features/learning-path/learning-path.component.html` | Stripped full duplicated sidebar + bottomnav — kept page content only |
| `features/progress/progress.component.ts` | Stripped full duplicated sidebar + bottomnav — kept stat/skill/results content only |
| `features/profile/profile.component.ts` | Stripped full duplicated sidebar + bottomnav — kept profile card/settings/signout only |
| `features/activity/activity-lesson/activity-lesson.component.html` | Written from scratch — all three states (lesson, writing, feedback) |
| `features/activity/activity-lesson/activity-lesson.component.ts` | Removed unused `RouterLink` import |
| `docs/architecture/frontend-layout-system.md` | Updated to reflect final state |

---

## Architecture: Correct Layout Rules

### Rule: pages render content only

Every component rendered inside a layout outputs **only page content** — no `<aside>`, no `<nav>`, no full-page wrappers.

### PublicLayout
- `sp-public-layout` = background gradient only, no centering, no card
- Landing: full-width via `sp-public-shell` (max-width 1120px)
- Login / change-password: `sp-public-centered` + `sp-public-logo` above + `sp-public-form-card`
- Logo lives **above** the card — never inside it

### StudentAppLayout
- Desktop (≥900px): fixed left sidebar (264px expanded / 76px collapsed) + main column
- Sidebar collapse toggle lives in the **header** (hamburger icon, desktop only)
- State persisted via `localStorage` key `speakpath.sidebarCollapsed`
- Collapsed: icon-only rail (76px), CSS transition `width .22s ease`
- Mobile (<900px): sidebar hidden, bottom nav fixed (5 items, Practice raised center)
- **Mobile bottom nav is unchanged and must not be modified**
- Header: collapse button + greeting + avatar + streak pill
- `sp-student-content`: max 520px mobile / 1080px desktop, `overflow-x:hidden`
- Child pages: content only

### AdminAppLayout
- Desktop (≥900px): fixed left sidebar (240px expanded / 72px collapsed)
- Collapse toggle lives in the **admin header** (hamburger icon, desktop only)
- State persisted via `localStorage` key `speakpath.adminSidebarCollapsed`
- Mobile (<900px): sidebar hidden; hamburger in header opens a **slide-out drawer**
- Drawer: `sp-sidebar-drawer` + `sp-sidebar-backdrop` — closes on backdrop click, close button, or nav item click
- No top navigation — all navigation via sidebar/drawer
- `sp-admin-content`: max 1200px, `overflow-x:hidden`
- Child pages: content only

---

## Activity Page (written from scratch)

Three states driven by `PageState` signal in `ActivityLessonComponent`:

1. **Learning** (`state() === 'learning'`) — shows situation card, Persian instruction, target phrases, common mistake hint, "Start writing" button
2. **Writing** (`state() === 'writing' | 'submitting'`) — compact situation reminder, Persian instruction, textarea labelled "Write your response", word count, submit/back buttons
3. **Feedback** (`state() === 'feedback'`) — score ring with "Overall score" label, "What you did well" list, main mistakes, Persian feedback block, corrected text (collapsible), rewrite challenge, next/try-again/back buttons

Step dots shown above all states except loading/error.

---

## Test Results

```
npm run build           ✓ Application bundle generation complete
playwright smoke test   ✓ 1 passed (14.1s)
```

---

## Manual Verification Checklist

- [ ] `/login` — centered card, no sidebar, no double card
- [ ] `/dashboard` — sidebar on desktop, hero, stats, skill grid
- [ ] `/my-path` — no duplicate sidebar, module journey renders
- [ ] `/activity` — stepper, lesson/writing/feedback states, Persian instruction
- [ ] `/progress` — no duplicate sidebar, stat tiles, skill levels
- [ ] `/profile` — no duplicate sidebar, sign-out visible
- [ ] `/admin/students` — admin topnav, no sidebar, table
- [ ] `/admin/create-student` — clean form
- [ ] `/admin/ai-config` — provider cards

---

## Risks Remaining

- Admin pages use raw Tailwind classes rather than `sp-*` system — cosmetically fine but inconsistent
- Onboarding steps not audited for duplicated shell (low risk — they are simpler forms)
- Right slide-in panel not implemented — deferred

---

## CSS Classes in Use

See [frontend-layout-system.md](../architecture/frontend-layout-system.md) for the full class reference.
