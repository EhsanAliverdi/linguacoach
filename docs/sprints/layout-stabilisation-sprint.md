# Layout Stabilisation Sprint

**Date**: 2026-06-04  
**Status**: In Progress

## Current State

The UI has reached feature completion but suffers from inconsistent layout implementation across pages. The recent design pass added visual polish but scattered layout logic across individual page components, resulting in:

- Duplicated sidebar/header HTML in every student page
- Inconsistent spacing and alignment
- Activity page content starting too low with blank areas
- Login page cramped and poorly spaced
- Admin pages visually inconsistent with student pages
- No centralised layout system or reusable components

## Product Goal

Stabilise the frontend layout architecture to ensure:

1. All authenticated pages share a consistent, maintainable layout structure
2. Student and admin experiences have appropriate visual distinction
3. Public pages (login, landing) use a clean, centered layout
4. Styles are centralised in `styles.css` for reusability
5. The core user flow is demo-ready and visually coherent

## Architecture Decision

Implement three distinct layout variants:

### 1. PublicLayout
- Used for: login, landing, change password (unauthenticated)
- Style: Clean centered card, no sidebar, max-width ~480px

### 2. StudentAppLayout
- Used for: dashboard, my path, activity, progress, profile
- Style: Warm SpeakPath learning-app aesthetic
- Components: Fixed left sidebar, top header, content area, mobile bottom nav

### 3. AdminAppLayout
- Used for: admin dashboard, students, AI config, prompts, careers
- Style: Professional admin dashboard, structured and clean
- Components: Top navigation bar, content area, management-focused cards

## Central CSS Strategy

Move repeated styles into `styles.css` as reusable classes:

```css
/* Layout primitives */
.sp-layout          /* Main page wrapper */
.sp-sidebar         /* Left sidebar container */
.sp-sidebar-collapsed /* Collapsed state */
.sp-main            /* Main content column */
.sp-header          /* Page header bar */
.sp-content         /* Content area with max-width + padding */

/* Card patterns */
.sp-card            /* Base card */
.sp-admin-card      /* Admin-specific card style */
.sp-student-card    /* Student-specific card style */
.sp-stat-card       /* Stat tile */

/* Navigation */
.sp-nav-item        /* Nav link */
.sp-nav-item-active /* Active nav link */
.sp-bottom-nav      /* Mobile bottom nav */

/* Typography & sections */
.sp-section-title   /* Section heading */
.sp-page-header     /* Page-level header */

/* Form elements */
.sp-input           /* Input field */
.sp-button-primary  /* Primary button */
.sp-button-secondary /* Secondary button */
```

## In Scope

- [x] Audit current layout structure
- [ ] Create layout stabilisation documentation
- [ ] Implement centralised CSS classes
- [ ] Create PublicLayout component
- [ ] Create StudentAppLayout component
- [ ] Create AdminAppLayout component
- [ ] Migrate login page to PublicLayout
- [ ] Migrate all student pages to StudentAppLayout
- [ ] Migrate all admin pages to AdminAppLayout
- [ ] Fix activity page layout (remove giant top blank area)
- [ ] Fix dashboard layout consistency
- [ ] Update routing to use layouts correctly
- [ ] Run build and E2E smoke test
- [ ] Manual verification of all pages

## Out of Scope

- New product features
- Backend API changes
- Redesigning the visual language
- Adding new pages or routes
- Right slide-in panel implementation (plan structure only)

## Files to Change

### New files
- `src/app/layouts/public-layout/`
- `src/app/layouts/student-app-layout/`
- `src/app/layouts/admin-app-layout/`

### Modified files
- `src/styles.css` – Add centralised layout classes
- `src/app/app.routes.ts` – Route restructuring for layouts
- `src/app/features/auth/login/login.component.*` – Use PublicLayout
- `src/app/features/dashboard/dashboard.component.*` – Use StudentAppLayout
- `src/app/features/activity/activity-lesson.component.*` – Use StudentAppLayout
- `src/app/features/learning-path/learning-path.component.*` – Use StudentAppLayout
- `src/app/features/progress/progress.component.*` – Use StudentAppLayout
- `src/app/features/profile/profile.component.*` – Use StudentAppLayout
- `src/app/features/admin/admin-shell.component.*` – Replace with AdminAppLayout
- `src/app/features/admin/admin-*.component.*` – Use AdminAppLayout

## Test Plan

### Automated
1. `npm run build` – Angular production build must succeed
2. `dotnet test --no-build` – Backend tests (if untouched, skip)
3. Playwright E2E: `core-flow-smoke.spec.ts` – Must pass

### Manual Verification Checklist
- [ ] Login page renders cleanly, centered, proper spacing
- [ ] Student dashboard shows sidebar, header, content correctly
- [ ] Activity page has no giant top blank area
- [ ] My Path page aligns with dashboard layout
- [ ] Progress page uses same layout
- [ ] Profile page uses same layout
- [ ] Admin dashboard uses professional admin layout
- [ ] Admin create student page works correctly
- [ ] Admin AI config page works correctly
- [ ] Mobile responsive: bottom nav appears on student pages
- [ ] Sign out accessible from all authenticated pages
- [ ] Active nav state works correctly

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing page functionality | Keep page component logic intact; only wrap with layout |
| CSS conflicts between old and new styles | Use specific class naming; test each page |
| Routing complexity | Use Angular route children pattern for layouts |
| Mobile responsiveness regression | Verify bottom nav on all student pages |

## Future Follow-up

- Right slide-in panel structural support
- Sidebar collapsible animation
- Admin usage analytics dashboard
- Student notification preferences UI
