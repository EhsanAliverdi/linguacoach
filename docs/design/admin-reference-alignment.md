# Admin Reference Design Alignment

**Date:** 2026-06-23
**Phase:** 10UI-FIX-1
**Source:** docs/design/speakpath/admin/

---

## Purpose

This document registers the SpeakPath reference design pack as the visual and product source-of-truth for admin UI alignment.
The reference design is React/JSX and HTML — it is **not** imported into the Angular runtime.
Angular uses the existing `sp-admin-*` wrapper component system backed by TailAdmin Angular free.

---

## Reference Design Files

| File | Purpose |
|------|---------|
| `docs/design/speakpath/SpeakPath Brand & System.html` | Brand tokens, colour palette, typography, spacing |
| `docs/design/speakpath/SpeakPath.html` | Public/student UI reference |
| `docs/design/speakpath/admin/Admin.html` | Admin shell and page previews |
| `docs/design/speakpath/admin/shell.jsx` | Sidebar nav config, icon system, layout |
| `docs/design/speakpath/admin/admin-data.jsx` | Mock data (NOT real app data — for UI preview only) |
| `docs/design/speakpath/admin/tweaks-panel.jsx` | Theming panel (reference only) |
| `docs/design/speakpath/admin/pages/*.jsx` | Per-page visual references |

---

## Icon System

The reference design defines SVG icons in `shell.jsx` ICONS map. Angular counterpart uses inline SVG passed as content projection to `sp-admin-sidebar-nav-item`.

| Reference key | Icon SVG description | Used in Angular sidebar |
|--------------|---------------------|------------------------|
| `dashboard` | Four rectangles grid | Dashboard nav item |
| `students` | People silhouettes | Students nav item |
| `aiconfig` | Signal waves + circle | AI Config nav item |
| `prompts` | Chat bubble | Prompts nav item |
| `curriculum` | Book open path | Curriculum nav item (added 10UI-FIX-1) |
| `aiusage` | Bar chart lines | AI Usage nav item |
| `exercises` | Stacked layers polygon | Exercise Types nav item |
| `notifications` | Bell | Notifications nav item |
| `integrations` | Database ellipse | Integrations nav item |
| `diagnostics` | Heartbeat line | Diagnostics nav item |
| `shieldCheck` | Shield with checkmark | Security nav item |

Usage Policies uses a checklist/clipboard icon (not in reference nav — added as governance extension).

---

## Nav Section Mapping

Reference design NAV sections → Angular sidebar sections:

| Reference section | Reference items | Angular section | Angular items |
|-------------------|----------------|-----------------|---------------|
| OVERVIEW | Dashboard | Menu | Dashboard |
| STUDENTS | Students | Menu | Students |
| AI SYSTEM | AI Config, Prompts, Curriculum | Menu | AI Config, Prompts, AI Usage, Usage Policies, Curriculum |
| ANALYTICS | Usage & Analytics | Menu | (merged into above) |
| SYSTEM | Exercise Types, Notifications, Integrations, Diagnostics | System | Exercise Types, Notifications, Integrations, Diagnostics, Security |

**Notes:**
- Reference design does not include Usage Policies (added as governance extension in 10Auth).
- Reference design does not include Security (added in 10Auth-F series).
- Reference design does not include Careers (backend only, no admin UI page planned).
- Angular sidebar uses two sections: "Menu" and "System". Reference uses five. Angular grouping is intentionally broader.

---

## Page Mapping

| Reference page file | Route | Angular component | Status |
|--------------------|-------|-------------------|--------|
| pages/dashboard.jsx | /admin | AdminDashboardComponent | Partial — static stat cards |
| pages/students.jsx | /admin/students | AdminStudentsComponent | Complete |
| pages/create-student.jsx | /admin/create-student | CreateStudentComponent | Complete |
| pages/ai-config.jsx | /admin/ai-config | AdminAiConfigComponent | Complete |
| pages/prompts.jsx | /admin/prompts | AdminPromptsComponent | Complete |
| pages/curriculum.jsx | /admin/curriculum | AdminCurriculumComponent | Complete |
| pages/ai-usage.jsx | /admin/usage | AdminAiUsageComponent | Complete |
| pages/exercise-types.jsx | /admin/exercise-types | AdminExerciseTypesComponent | Complete |
| pages/notifications.jsx | /admin/notifications | AdminNotificationsComponent | Complete |
| pages/integrations.jsx | /admin/integrations | AdminIntegrationsComponent | Complete |
| pages/diagnostics.jsx | /admin/diagnostics | AdminDiagnosticsComponent | Complete |
| (no reference page) | /admin/usage-policies | AdminUsagePoliciesComponent | Complete |
| (no reference page) | /admin/security | AdminSecurityComponent | Complete |
| (no reference page) | /admin/careers | AdminCareersComponent | Backend only, nav link deferred |

---

## Component Mapping

| Reference component | Angular equivalent |
|--------------------|--------------------|
| `Sidebar` (React) | `sp-admin-sidebar` + `AdminAppLayoutComponent` |
| `Header` (React) | `sp-admin-header` + `sp-admin-user-menu` |
| `SlideIn` drawer | Slide-over pattern used in individual page components |
| `KpiCard` | Inline card pattern in admin page components |
| `AIcon` | Inline SVG content projection into `sp-admin-sidebar-nav-item` |
| `adm-nav-item` CSS | `menu-item` / `menu-item-active` TailAdmin utility classes |

---

## Alignment Rules

1. Do not import reference JSX/HTML into Angular runtime.
2. Do not use `admin-data.jsx` mock data as real app data. It is UI preview only.
3. Match icon choices from the reference ICONS map when adding new nav items.
4. New pages not in the reference design (Usage Policies, Security, Careers) follow the same `sp-admin-*` component patterns as existing pages.
5. Page-level layout (stat cards, tables, slide-overs) should visually match reference pages where a counterpart exists.
6. Reference design is the authority on colour, typography, and spacing tokens.

---

## Changes Made in 10UI-FIX-1

- Added **Usage Policies** nav link (`/admin/usage-policies`) to desktop sidebar and mobile drawer.
- Added **Curriculum** nav link (`/admin/curriculum`) to desktop sidebar and mobile drawer.
- Both were already routed and fully implemented — only sidebar discoverability was missing.
