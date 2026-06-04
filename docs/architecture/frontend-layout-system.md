# Frontend Layout System

**Date**: 2026-06-04
**Status**: Stable — v2 (full UI stabilisation + admin sidebar sprint)

---

## Why the UI broke (history)

Prior iterations scattered layout HTML into individual page components. Pages rendered their own sidebar, header, and bottom nav. When pages were moved under layout components (StudentAppLayoutComponent, AdminAppLayoutComponent), the duplicated shells caused double sidebars, double bottom navs, and large blank areas.

Additionally, the AdminAppLayoutComponent used a horizontal top navigation bar — not a left sidebar — which made the admin area look like a consumer app rather than a management tool.

Both issues were fixed in the layout stabilisation sprints.

---

## Final Layout Architecture

```
AppComponent (router-outlet)
├── PublicLayoutComponent            → /, /login, /change-password
│   └── router-outlet
│       └── Page renders sp-public-card itself
│
├── StudentAppLayoutComponent        → /dashboard, /my-path, /activity, /progress, /profile, /onboarding/**
│   ├── .sp-student-sidebar          fixed left sidebar (≥900px), full 100vh
│   └── .sp-student-main             flex:1, margin-left:264px on desktop
│       ├── .sp-student-header       greeting + avatar + streak pill
│       ├── .sp-student-content      router-outlet, max-width 1080px
│       └── .sp-bottomnav            fixed bottom mobile nav (hidden ≥900px)
│
└── AdminAppLayoutComponent          → /admin/**
    ├── .sp-admin-sidebar            fixed left sidebar (≥900px), full 100vh
    │   ├── brand block              SpeakPath logo + "Admin Panel" label
    │   ├── nav groups               Overview / Students / AI System / Analytics
    │   └── footer                   sign out button
    └── .sp-admin-main               flex:1, margin-left:240px on desktop
        ├── .sp-admin-header         sticky top bar with user chip + role badge
        └── .sp-admin-content        router-outlet, max-width 1200px
```

### Rule: pages render content only

Every component rendered inside a layout outputs **only page content**. No `<aside>`, no `<nav>`, no full-page wrappers. The layout component owns the shell.

---

## Route → Layout Mapping

| Route | Layout | Component |
|---|---|---|
| `/` | PublicLayout | LandingComponent |
| `/login` | PublicLayout | LoginComponent |
| `/change-password` | PublicLayout | ChangePasswordComponent |
| `/dashboard` | StudentAppLayout | DashboardComponent |
| `/my-path` | StudentAppLayout | LearningPathComponent |
| `/activity` | StudentAppLayout | ActivityLessonComponent |
| `/progress` | StudentAppLayout | ProgressComponent |
| `/profile` | StudentAppLayout | ProfileComponent |
| `/assessment` | StudentAppLayout | CefrAssessmentComponent |
| `/speaking` | StudentAppLayout | SpeakingSessionComponent |
| `/onboarding/**` | StudentAppLayout | Onboarding step components |
| `/admin` | AdminAppLayout | AdminDashboardComponent |
| `/admin/students` | AdminAppLayout | AdminStudentsComponent |
| `/admin/create-student` | AdminAppLayout | CreateStudentComponent |
| `/admin/ai-config` | AdminAppLayout | AdminAiConfigComponent |
| `/admin/prompts` | AdminAppLayout | AdminPromptsComponent |
| `/admin/careers` | AdminAppLayout | AdminCareersComponent |
| `/admin/usage` | AdminAppLayout | AdminUsageComponent (placeholder) |

---

## PublicLayout Rules

- Full-page gradient background (`sp-public-layout`)
- No sidebar, no topnav, no bottom nav
- Each child page renders its own `sp-public-card` wrapper
- Login card: max-width 460px, centered, white, rounded

## StudentAppLayout Rules

- Desktop (≥900px): fixed left sidebar (264px, 100vh) + main with `margin-left:264px`
- Mobile (<900px): sidebar hidden, fixed bottom nav (5 items)
- Sidebar: `position:fixed; top:0; left:0; height:100vh` — always full viewport
- Header: greeting (time-aware) + avatar + streak pill placeholder
- Content: `max-width:520px` mobile / `max-width:1080px` desktop
- Child pages: content only — no sidebar, no header, no shell classes

## AdminAppLayout Rules

- Desktop (≥900px): fixed left sidebar (240px, 100vh) + main with `margin-left:240px`
- Mobile (<900px): sidebar hidden (full-width main) — future: hamburger menu
- No top navigation — sidebar is the only nav
- Professional clean aesthetic: slate/indigo palette, no gradients, no gamification
- Nav grouped: Overview / Students / AI System / Analytics
- "Usage & Analytics" is a "Soon" disabled link
- Header: sticky 56px bar showing user email + "Admin" badge
- Content: max-width 1200px
- Guard: `adminGuard` (handles auth + role check in one guard)

---

## Admin Visual Style

Inspired by TailAdmin. Key characteristics:
- Background: `#F8FAFC` (slate-50)
- Cards: `#fff`, `border: 1px solid #E2E8F0`, `border-radius: 14px`
- Sidebar: `#fff`, `border-right: 1px solid #E2E8F0`
- Accent: indigo-700 (`#4338CA`) for active nav, badges, primary buttons
- No `sp-grad-brand`, no gamified elements, no emoji stats
- Tables with `th` using uppercase labels and `#94A3B8` color
- Status badges using green/amber/indigo/slate color system

---

## Admin Dashboard Sections

`/admin` loads `AdminDashboardComponent` which shows:

1. **KPI cards row** — Total students (real), Onboarded (real), AI provider (static "Configured"), Activities tracked ("Coming soon")
2. **Quick actions grid** — 5 action cards: Add student, Manage students, AI Config, Prompts, Curriculum
3. **Recent students table** — last 5 from `listStudents()` API
4. **AI System status card** — Writing/Feedback (Active), Speaking/Listening (Planned)
5. **Analytics placeholders** — 3 placeholder cards: Usage analytics, Learning progress, Feedback quality

---

## Central CSS Classes

All shared classes live in `src/styles.css`.

### Admin shared classes (used in page components)
| Class | Purpose |
|---|---|
| `.sp-admin-page-header` | Page header wrapper (margin-bottom: 24px) |
| `.sp-admin-page-title` | Page h1 (22px, bold, dark) |
| `.sp-admin-page-sub` | Subtitle below title |
| `.sp-admin-btn-primary` | Indigo action button |
| `.sp-admin-table-card` | Table wrapper card |
| `.sp-admin-table` | Styled data table |
| `.sp-admin-table-loading` | Centered loading state in table area |
| `.sp-admin-table-muted` | Muted secondary table cell text |
| `.sp-admin-table-empty` | Greyed-out empty placeholder |
| `.sp-admin-empty-row` | Centered empty-state row |
| `.sp-admin-badge` | Pill badge base class |
| `.sp-admin-badge-green/amber/indigo/slate` | Badge color variants |
| `.sp-admin-spinner` | Indigo loading spinner |
| `.sp-admin-alert-error` | Red error alert |
| `.sp-admin-link` | Indigo text link |
| `.sp-admin-form-card` | White card for forms |
| `.sp-admin-form-desc` | Muted description below form card header |

### Layout classes (owned by layout component CSS)
| Class | Purpose |
|---|---|
| `.sp-admin-shell` | Root flex container |
| `.sp-admin-sidebar` | Fixed left sidebar |
| `.sp-admin-brand` | Brand/logo section |
| `.sp-admin-nav` | Nav list container |
| `.sp-admin-nav-group-label` | Section label ("Overview" etc.) |
| `.sp-admin-nav-item` | Nav link/button |
| `.sp-admin-nav-item-active` | Active route highlight (indigo) |
| `.sp-admin-nav-item-soon` | Disabled/greyed nav item |
| `.sp-admin-nav-soon-badge` | "Soon" pill on nav item |
| `.sp-admin-sidebar-footer` | Sign out area |
| `.sp-admin-signout-btn` | Sign out button |
| `.sp-admin-main` | Main content area |
| `.sp-admin-header` | Sticky top header |
| `.sp-admin-header-inner` | Header flex row |
| `.sp-admin-header-user` | User chip (avatar + email + badge) |
| `.sp-admin-avatar` | Gradient initial avatar |
| `.sp-admin-header-email` | Email display |
| `.sp-admin-role-badge` | "Admin" badge |
| `.sp-admin-content` | Page content area |

---

## Placeholder Strategy

- Data not yet tracked: show `—` or "Not tracked yet" — never fake numbers
- Features not yet built: `sp-empty-state` or `sp-admin-placeholder-card` with honest copy
- Soon features in admin nav: greyed link with "Soon" badge
- Admin analytics section: intentional dashed-border placeholder cards

---

## Future: Right Slide-In Panel

The activity flow may eventually use a right-side feedback panel keeping the scenario visible. Not implemented. When added, it should be a fixed overlay or CSS Grid column in `StudentAppLayoutComponent`, not per-page.

---

## Testing Checklist

After layout changes, verify:

- [ ] `/login` — centered card, no sidebar, gradient background
- [ ] `/dashboard` — student sidebar full-height on desktop, hero card, stat grid
- [ ] `/my-path` — no duplicate sidebar, module journey renders
- [ ] `/activity` — stepper, lesson/writing/feedback states, Persian instruction
- [ ] `/progress` — no duplicate sidebar, stat tiles, skill levels
- [ ] `/profile` — no duplicate sidebar, sign-out visible
- [ ] `/admin` — admin left sidebar, dashboard with KPI cards, student table
- [ ] `/admin/students` — table with real data, no top nav
- [ ] `/admin/create-student` — clean form, active sidebar item
- [ ] `/admin/ai-config` — provider/model config, no top nav
- [ ] No double sidebar anywhere
- [ ] No horizontal overflow
- [ ] Active nav state correct
- [ ] Sign out accessible everywhere
- [ ] Mobile: student bottom nav visible, admin sidebar hidden
