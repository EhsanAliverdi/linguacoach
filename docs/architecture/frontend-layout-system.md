# Frontend Layout System

**Date**: 2026-06-04
**Status**: Stabilised (layout-stabilisation-sprint)

---

## Why the UI broke

Several pages were written as standalone self-contained components that embedded the full sidebar, header, and mobile bottom-nav HTML directly in their own templates. These components were later moved under layout wrapper routes (e.g., `StudentAppLayoutComponent`) without removing the duplicated shell. This caused:

- **Double sidebar** on desktop (one from layout, one from the page template)
- **Double mobile bottom nav** (fixed-position overlay stacking)
- **Giant blank space** on pages where an inner header competed with the layout header
- **Cramped or broken spacing** because inner `.sp-content` max-width was inside another `.sp-content`

Affected pages at the time of the layout stabilisation sprint:
- `learning-path.component.html` — full sidebar + bottomnav duplicated
- `progress.component.ts` — full sidebar + bottomnav duplicated
- `profile.component.ts` — full sidebar + bottomnav duplicated
- `activity-lesson.component.html` — empty file (template was never written)

An additional issue: `PublicLayoutComponent` had an extra `sp-public-card` wrapper inside it, causing a double-card on the login page (the login component already renders its own `sp-public-card`).

---

## Final Layout Architecture

```
AppComponent (router-outlet)
├── PublicLayoutComponent       → login, landing, change-password
│   └── <router-outlet>         (child renders sp-public-card directly)
├── StudentAppLayoutComponent   → dashboard, my-path, activity, progress, profile, onboarding
│   ├── sp-student-sidebar      (desktop only, ≥900px)
│   ├── sp-student-header       (greeting + avatar)
│   ├── sp-student-content      (router-outlet — page content only)
│   └── sp-bottomnav            (mobile, fixed bottom)
└── AdminAppLayoutComponent     → /admin/**
    ├── sp-admin-topnav          (sticky top nav)
    └── sp-admin-content         (router-outlet — page content only)
```

### Rule: pages render content only

Every component rendered inside a layout must output **only page content** — no `<aside>`, no `<nav>`, no full-page wrappers. The layout component owns the shell.

---

## Route → Layout Mapping

| Route | Layout |
|---|---|
| `/` | `PublicLayoutComponent` |
| `/login` | `PublicLayoutComponent` |
| `/change-password` | `PublicLayoutComponent` |
| `/dashboard` | `StudentAppLayoutComponent` |
| `/my-path` | `StudentAppLayoutComponent` |
| `/activity` | `StudentAppLayoutComponent` |
| `/progress` | `StudentAppLayoutComponent` |
| `/profile` | `StudentAppLayoutComponent` |
| `/assessment` | `StudentAppLayoutComponent` |
| `/speaking` | `StudentAppLayoutComponent` |
| `/onboarding/**` | `StudentAppLayoutComponent` |
| `/admin/**` | `AdminAppLayoutComponent` |

---

## PublicLayout Rules

- Full-page centered background (`sp-public-layout`)
- No sidebar, no topnav, no bottom nav
- Each child page is responsible for rendering its own `sp-public-card` wrapper
- Used for: login, landing, change-password

## StudentAppLayout Rules

- Desktop (≥900px): sticky left sidebar (264px) + main column
- Mobile (<900px): fixed bottom nav (5 items, Practice is raised center button)
- Header shows greeting + avatar + streak pill
- `sp-student-content` caps width at `520px` mobile / `1080px` desktop with correct padding
- Child pages must not add their own sidebar, header, or bottom nav
- Child pages must not use `sp-app`, `sp-side`, or `sp-bottomnav`

## AdminAppLayout Rules

- Sticky top navigation bar with brand + nav links + sign-out
- `sp-admin-content` max-width 1200px with padding
- No sidebar, no bottom nav
- Visually distinct from student UI (no gamification, clean data-table aesthetic)
- Child pages output content only (headings, tables, forms)

---

## Central CSS Class Strategy

All design tokens and utility classes live in `src/styles.css`. Key classes:

| Class | Purpose |
|---|---|
| `sp-student-app` | Student layout root flex container |
| `sp-student-sidebar` | Desktop sidebar (hidden on mobile) |
| `sp-student-main` | Main column flex container |
| `sp-student-header` | Top header bar inside main column |
| `sp-student-content` | Scrollable content area (max-width + padding) |
| `sp-admin-app` | Admin layout root |
| `sp-admin-topnav` | Sticky admin top nav |
| `sp-admin-content` | Admin content area |
| `sp-public-layout` | Full-page centered wrapper for auth pages |
| `sp-public-card` | White card for login/auth forms |
| `sp-card`, `sp-card-soft` | Content cards |
| `sp-stat-grid` | 3-column stat grid |
| `sp-skill-grid` | 2-col mobile / 5-col desktop skill grid |
| `sp-grid-2col` | 2-column responsive grid |
| `sp-section-h` | Section heading row with optional action link |
| `sp-button-primary`, `sp-button-ghost`, `sp-button-secondary` | Button variants |
| `sp-input`, `sp-label` | Form elements |
| `sp-alert-info`, `sp-alert-error`, etc. | Alert banners |
| `sp-source-lang-block` | RTL Persian text block |
| `sp-empty-state` | Centered empty/placeholder state |
| `sp-loading-pulse` | Animated skeleton loader |

Do not add random `margin-top` or `padding-top` to fix spacing issues. Fix the layout structure instead.

---

## Placeholder Strategy

- Stats not yet tracked: show `—` value + `Coming soon` label below
- Skills not yet implemented: show at 60% opacity with `Coming soon` badge
- Features not yet built: `sp-empty-state` with honest label + CTA to available feature
- Never show fake data (e.g., fake streak count, fake score)

---

## Future: Right Slide-In Panel

The activity flow may eventually use a right-side slide-in panel for coach feedback, keeping the scenario visible on the left. This is not implemented yet. When added, it should be a fixed overlay or CSS Grid column extension inside `StudentAppLayoutComponent`, not added inline per page.

---

## Testing Checklist

After any layout change, verify these pages in browser:

- [ ] `/login` — centered card, no sidebar, works on mobile
- [ ] `/dashboard` — sidebar visible on desktop, hero card, stat grid, skill cards
- [ ] `/my-path` — no duplicate sidebar, module journey renders
- [ ] `/activity` — stepper visible, lesson/writing/feedback states work
- [ ] `/progress` — no duplicate sidebar, stat tiles, skill levels
- [ ] `/profile` — no duplicate sidebar, sign-out button accessible
- [ ] `/admin` → `/admin/students` — admin topnav, no sidebar, clean table
- [ ] `/admin/create-student` — clean form, no sidebar
- [ ] `/admin/ai-config` — provider cards render

Check for:
- No double sidebar on desktop
- No double bottom nav on mobile
- No giant blank area at top of pages
- No horizontal overflow
- Active nav state highlights correct item
- Sign out accessible from student profile and admin topnav
- Mobile layout usable (bottom nav, content readable)
