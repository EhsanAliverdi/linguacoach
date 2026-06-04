# Frontend Layout System Architecture

**Date**: 2026-06-04  
**Status**: New

## Why This Exists

The previous design implementation scattered layout logic across individual page components. Every page duplicated:
- Sidebar HTML and navigation links
- Header structure
- Mobile bottom nav
- Content area wrappers
- Card styling patterns

This created maintenance burden and visual inconsistency. The layout system centralises these concerns so pages focus on their specific content and functionality.

## Problem Statement

### Previous Issues

1. **Duplicated sidebar code**: Dashboard, activity, my-path, progress, and profile all had ~80 lines of identical sidebar HTML
2. **Inconsistent content areas**: Each page defined its own padding, max-width, and spacing
3. **Activity page broken layout**: Content started too low, leaving a giant blank area at the top
4. **Login page cramped**: No proper public layout wrapper; inputs and cards poorly spaced
5. **Admin pages visually inconsistent**: Some used top nav, some had no clear structure
6. **No style centralisation**: Same padding, border-radius, and card styles repeated everywhere

## Layout Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     App Component                        │
│                    <router-outlet />                     │
└─────────────────────────────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          │               │               │
          ▼               ▼               ▼
   ┌──────────┐   ┌──────────────┐   ┌───────────┐
   │  Public  │   │ StudentApp   │   │ AdminApp  │
   │  Layout  │   │    Layout    │   │   Layout  │
   └──────────┘   └──────────────┘   └───────────┘
          │               │               │
          │               │               │
          ▼               ▼               ▼
   ┌──────────┐   ┌──────────────┐   ┌───────────┐
   │  Login   │   │  Dashboard   │   │  Admin    │
   │ Landing  │   │  My Path     │   │ Dashboard │
   │  Change  │   │  Activity    │   │ Students  │
   │ Password │   │  Progress    │   │ AI Config │
   │          │   │  Profile     │   │ Prompts   │
   └──────────┘   └──────────────┘   └───────────┘
```

## Layout Variants

### 1. PublicLayout

**Purpose**: Unauthenticated pages that need a clean, focused presentation.

**Structure**:
```html
<div class="sp-public-layout">
  <div class="sp-public-card">
    <!-- Page content here -->
  </div>
</div>
```

**Style characteristics**:
- Centered content vertically and horizontally
- Max width: 480px
- Warm gradient background from `sp-app`
- Clean white card with subtle shadow
- No sidebar, no navigation

**Used by**:
- `/login`
- `/` (landing)
- `/change-password` (when unauthenticated flow needed)

---

### 2. StudentAppLayout

**Purpose**: Authenticated student experience – warm, encouraging, learning-focused.

**Structure**:
```text
┌─────────────────────────────────────────────────────────┐
│                   sp-student-app                         │
│  ┌────────────┐  ┌───────────────────────────────────┐  │
│  │            │  │           sp-header               │  │
│  │  sp-sidebar│  │  - Greeting                       │  │
│  │            │  │  - User avatar/actions            │  │
│  │  - Logo    │  ├───────────────────────────────────┤  │
│  │  - Nav     │  │                                   │  │
│  │  - Streak  │  │         sp-content                │  │
│  │  - Sign out│  │                                   │  │
│  │            │  │    Page-specific content here     │  │
│  │            │  │                                   │  │
│  └────────────┘  └───────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────┐│
│  │              sp-bottom-nav (mobile only)            ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
```

**Style characteristics**:
- Left sidebar: Fixed/sticky, 264px wide on desktop, hidden on mobile
- Sidebar background: Semi-transparent with backdrop blur
- Main header: Sticky top, greeting + user actions
- Content area: Max-width 1080px, consistent padding
- Mobile bottom nav: Fixed position, appears <900px

**Visual tone**:
- Warm gradient backgrounds
- Colourful skill badges
- Encouraging microcopy
- Rounded corners (18–28px)
- Soft shadows and glows

**Used by**:
- `/dashboard`
- `/my-path`
- `/activity`
- `/progress`
- `/profile`

---

### 3. AdminAppLayout

**Purpose**: Admin management interface – professional, structured, efficient.

**Structure**:
```text
┌─────────────────────────────────────────────────────────┐
│                   sp-admin-app                           │
│  ┌───────────────────────────────────────────────────┐  │
│  │              sp-admin-topnav                      │  │
│  │  - Logo      - Students - AI Config - Settings   │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │                                                   │  │
│  │              sp-admin-content                     │  │
│  │                                                   │  │
│  │    Management cards, tables, forms                │  │
│  │                                                   │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

**Style characteristics**:
- Top navigation bar (not sidebar)
- Clean white/neutral backgrounds
- Professional card designs
- Less playful than student UI
- Clear data tables and forms
- Management-focused layouts

**Visual tone**:
- Slate/neutral colour palette
- Indigo accent colour
- Structured grids
- Professional typography
- Minimal decorative elements

**Used by**:
- `/admin` (redirects to students)
- `/admin/students`
- `/admin/create-student`
- `/admin/ai-config`
- `/admin/prompts`
- `/admin/careers`

---

## Central CSS Classes

### Layout Primitives

```css
.sp-public-layout       /* Full-page centered layout wrapper */
.sp-public-card         /* Centered card for public pages */

.sp-student-app         /* Student app root */
.sp-sidebar             /* Left sidebar container */
.sp-sidebar-collapsed   /* Collapsed sidebar state (future) */
.sp-main                /* Main content column */
.sp-header              /* Page header bar */
.sp-content             /* Content area with max-width + padding */

.sp-admin-app           /* Admin app root */
.sp-admin-topnav        /* Admin top navigation bar */
.sp-admin-content       /* Admin content area */

.sp-bottom-nav          /* Mobile bottom navigation */
```

### Card Patterns

```css
.sp-card                /* Base card – surface, border, radius, shadow */
.sp-card-pad            /* Card with padding */
.sp-card-soft           /* Card with soft gradient background */
.sp-admin-card          /* Admin-specific card style */
.sp-student-card        /* Student-specific card style */
.sp-stat-card           /* Stat tile grid item */
.sp-module-card         /* Learning module card */
```

### Navigation

```css
.sp-sidelink            /* Sidebar navigation link */
.sp-sidelink-active     /* Active sidebar link */
.sp-navbtn              /* Mobile bottom nav button */
.sp-navbtn-active       /* Active bottom nav button */
.sp-admin-navlink       /* Admin top nav link */
.sp-admin-navlink-active /* Active admin nav link */
```

### Typography & Sections

```css
.sp-h1                  /* H1 heading – responsive size */
.sp-section-h           /* Section header with optional action link */
.sp-page-header         /* Page-level header block */
.sp-greet-sm            /* Small greeting text */
.sp-greet-lg            /* Large greeting text */
```

### Form Elements

```css
.sp-input               /* Input field with focus states */
.sp-label               /* Form label */
.sp-button-primary      /* Primary button with gradient */
.sp-button-secondary    /* Secondary button */
.sp-button-ghost        /* Ghost/outline button */
```

## Right Slide-In Panel (Future)

The StudentAppLayout includes structural support for a future right slide-in panel:

```text
┌─────────────────────────────────────────────────────────┐
│  Sidebar  │  Main Content  │  [Right Panel – Future]   │
│           │                │                           │
│           │                │  - Context hints          │
│           │                │  - Vocabulary preview     │
│           │                │  - Related activities     │
│           │                │  - Coach tips             │
│           │                │                           │
└─────────────────────────────────────────────────────────┘
```

**Implementation notes**:
- Panel should be fixed/sticky on desktop
- Slide-in animation from right
- Overlay on mobile when open
- Not implemented in this sprint – only structural consideration

## How It Works

### Routing Pattern

Angular routes use outlet patterns to assign layouts:

```typescript
{
  path: '',
  component: PublicLayoutComponent,
  children: [
    { path: '', component: LandingComponent },
    { path: 'login', component: LoginComponent },
  ]
},
{
  path: '',
  component: StudentAppLayoutComponent,
  canActivate: [authGuard],
  children: [
    { path: 'dashboard', component: DashboardComponent },
    { path: 'my-path', component: LearningPathComponent },
    // ...
  ]
},
{
  path: 'admin',
  component: AdminAppLayoutComponent,
  canActivate: [adminGuard],
  children: [
    // ...
  ]
}
```

### Component Usage

Pages no longer include sidebar/header HTML. They only define their content:

```html
<!-- Before: dashboard.component.html -->
<div class="sp-app">
  <aside class="sp-side">
    <!-- 80 lines of sidebar -->
  </aside>
  <div style="flex:1...">
    <header class="sp-topbar">...</header>
    <main class="sp-content">...</main>
  </div>
</div>

<!-- After: dashboard.component.html -->
<h1>Your Dashboard</h1>
<p>Welcome back, {{ firstName() }}</p>
<!-- Just the page-specific content -->
```

## Failure Modes

### What Happens If Layout Fails to Load?

- Router falls back to bare `<router-outlet />`
- Page content still renders, just without layout wrapper
- No white screen of death

### What Happens If CSS Classes Conflict?

- Specific naming (`sp-` prefix) avoids collisions
- Tailwind utilities can override where needed
- Page-specific styles should be minimal and additive

## Extension Points

### Adding a New Student Page

1. Create component in `features/your-feature/`
2. Add route under StudentAppLayout children
3. Write page content only – no layout HTML needed
4. Use central CSS classes for consistency

### Adding a New Admin Page

1. Create component in `features/admin/your-feature/`
2. Add route under AdminAppLayout children
3. Use admin card patterns and data tables

### Modifying the Sidebar

1. Edit `student-app-layout` component only
2. Changes propagate to all student pages
3. No need to touch individual page components

## Key Invariants

1. **Student pages never duplicate sidebar HTML** – Single source of truth in layout component
2. **Admin pages never use student layout** – Visual distinction maintained
3. **Public pages never require auth** – Login flow remains accessible
4. **Mobile bottom nav only on student layout** – Admin uses responsive top nav
5. **Content area max-width consistent** – 1080px on desktop, full width on mobile
