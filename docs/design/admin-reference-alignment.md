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

## Page Status (updated 10UI-REDESIGN-0 — 2026-06-23)

Full route-by-route redesign plan in: `docs/reviews/2026-06-23-phase-10ui-redesign-0-admin-reference-redesign-rollout-plan.md`

| Route | Component | Wrapper usage | Page-level redesign status | Reference gap | Redesign phase |
|-------|-----------|--------------|---------------------------|---------------|----------------|
| `/admin` | AdminDashboardComponent | Good | **Complete** — dark hero banner, KPI tile row, onboarding funnel, at-risk, CEFR distribution, AI system, cohort engagement, recent students, all unavailable sections show explicit placeholders | Resolved in 10UI-REDESIGN-1 | Done |
| `/admin/students` | AdminStudentsComponent | Complete | **Complete** — 4-tile KPI strip (real stats), rows-per-page selector, filter bar aligned | Resolved in 10UI-REDESIGN-2 | Done |
| `/admin/students/:id` | AdminStudentDetailComponent | Complete | **Complete** — coloured initials avatar hero, KPI strip upgraded, danger zone card | Resolved in 10UI-REDESIGN-3 | Done |
| `/admin/create-student` | CreateStudentComponent | Complete | **Complete** — two-column layout, sticky aside panel, multi-section form cards, security note, back link | Resolved in 10UI-REDESIGN-2 | Done |
| `/admin/ai-config` | AdminAiConfigComponent | Complete | **Complete** — 4-tile KPI strip (LLM configured, TTS, providers with key, pricing), X/N configured badge on LLM card, Rate limits card with "Backend not available yet" | Resolved in 10UI-REDESIGN-5 | Done |
| `/admin/prompts` | AdminPromptsComponent | Complete | **Complete** — KPI strip upgraded to sp-admin-kpi-card, category badge column derived from key, category filter added, subtitle shows live count, title aligned to reference | Resolved in 10UI-REDESIGN-5 | Done |
| `/admin/usage` | AdminAiUsageComponent | Complete | **Complete** — 4-tile KPI strip (total requests, cost, success rate, failed), period pill buttons (7d/30d/All), mini bar chart from real trendBuckets, explicit "Backend not available yet" cards for activities/engagement | Resolved in 10UI-REDESIGN-6 | Done |
| `/admin/usage-policies` | AdminUsagePoliciesComponent | Complete | **Looks close** | No reference counterpart — keep as-is | 10UI-REDESIGN-6 (P3) |
| `/admin/exercise-types` | AdminExerciseTypesComponent | Complete | **Complete** — KPI summary strip, skill-coloured icon tile per row, "Not runnable yet" label for non-ready types | Resolved in 10UI-REDESIGN-4 | Done |
| `/admin/curriculum` | AdminCurriculumComponent | Complete | **Complete** — 4-tile KPI coverage strip (total, active, CEFR bands, skills covered) derived from full objective list | Resolved in 10UI-REDESIGN-4 | Done |
| `/admin/notifications` | AdminNotificationsComponent | Complete | **Complete** — KPI channel summary strip (InApp/Email/SMS Foundation only/Dispatch), CSS token tab bar, sp-admin-kpi-card config status strip, sp-admin-card named titles, sp-admin-button (clicked) actions | Resolved in 10UI-REDESIGN-7 | Done |
| `/admin/integrations` | AdminIntegrationsComponent | Complete | **Complete** — Integration card grid (storage real, SMTP link, Webhook/Slack/Analytics/Admin API all not-implemented), sp-admin-kpi-card for background job metrics | Resolved in 10UI-REDESIGN-7 | Done |
| `/admin/diagnostics` | AdminDiagnosticsComponent | Complete | **Looks close** | Minor: outlined border card style vs sp-admin-card | 10UI-REDESIGN-8 (P3) |
| `/admin/security` | AdminSecurityComponent | Complete | **Looks close** | No reference counterpart — deferred capabilities card added in FIX-8 | 10UI-REDESIGN-8 (P3) |
| `/admin/careers` | — | — | **Resolved** — redirects to `/admin/curriculum` | — | Done |

---

## Redesign phase sequence

| Phase | Routes | Priority |
|-------|--------|----------|
| 10UI-REDESIGN-1 | `/admin` (dashboard) | P1 |
| 10UI-REDESIGN-2 | `/admin/students`, `/admin/create-student` | P2 |
| 10UI-REDESIGN-3 | `/admin/students/:id` | P2 |
| 10UI-REDESIGN-4 | `/admin/curriculum`, `/admin/exercise-types` | P2 |
| 10UI-REDESIGN-5 | `/admin/ai-config`, `/admin/prompts` | P2/P3 |
| 10UI-REDESIGN-6 | `/admin/usage`, `/admin/usage-policies` | P2/P3 |
| 10UI-REDESIGN-7 | `/admin/notifications`, `/admin/integrations` | P1/P3 |
| 10UI-REDESIGN-8 | `/admin/diagnostics`, `/admin/security` | P3 |
| 10UI-REDESIGN-FINAL | All routes — closure audit | — |

---

## Changes Made in 10UI-FIX-1

- Added **Usage Policies** nav link (`/admin/usage-policies`) to desktop sidebar and mobile drawer.
- Added **Curriculum** nav link (`/admin/curriculum`) to desktop sidebar and mobile drawer.
- Both were already routed and fully implemented — only sidebar discoverability was missing.

## Changes Made in 10UI-FIX-4

- Tiny CSS token fixes across 6 admin page components (blue/indigo literals → `var(--sp-admin-primary,#5B4BE8)`).
- Tab bar `border-blue-600`/`text-blue-600` → indigo on notifications page.
- Page matrix, wrapper misuse matrix, stale UI matrix documented in `docs/reviews/2026-06-23-phase-10ui-fix-4-admin-page-level-spot-check.md`.

## Changes Made in 10UI-FIX-5 through 10UI-FIX-8

- FIX-5: Careers redirect resolved. Dashboard AI provider stat card wired to live backend.
- FIX-6: Students header, create-student, curriculum filter bar wrapper migration complete.
- FIX-7: Student detail full wrapper migration + readiness pool section + activity history.
- FIX-8: Notifications SMS foundation-only label, Security deferred capabilities card, Integrations readiness pool placeholder.

## Changes Made in 10UI-REDESIGN-7

- Notifications: KPI channel summary strip (InApp/Email/SMS/Dispatch) from real config, loaded on `ngOnInit`. Tab bar and config cards use CSS token classes. Config channel status section upgraded to `sp-admin-kpi-card` strip. `sp-admin-card title=` used for all config cards. `sp-admin-button (clicked)` for all outbox/template actions. SMS always shows "Foundation only" — never implies production-ready.
- Integrations: Subtitle updated. Integration card grid added (6 cards: storage real with live badge, SMTP links to Notifications, Webhook/Slack/Analytics/Admin API all "Backend not available yet"). `sp-admin-stat-card` → `sp-admin-kpi-card` for background job metrics. All `(click)` → `(clicked)` on `sp-admin-button`. RouterLink added for SMTP navigation card.
- Spec fixes: `getNotificationConfig` mocked in beforeEach, 3 stale test assertions updated for eager init, `provideRouter([])` added to all integrations test setups.
- 1221/1221 PASS.

## Changes Made in 10UI-REDESIGN-6

- AI Usage: `sp-admin-stat-card` (8 tiles) → `sp-admin-kpi-card` (4 tiles: total requests, total cost, success rate, failed calls). Period pill buttons (7d/30d/All) added above filter bar. "Calls over time" card with mini proportional bar chart from real `trendBuckets` data. "Activities per day" and "Student engagement" placeholder cards with "Backend not available yet" labels. All existing filter/export/pagination/zero-cost behaviour preserved.
- 15 new tests. 1221/1221 pass.

## Changes Made in 10UI-REDESIGN-5

- AI Config: 4-tile KPI summary strip (LLM configured, TTS configured, providers with key, pricing models). X/N configured badge on LLM Categories card. Rate limits card with "Backend not available yet" label — no fake usage numbers. API keys never displayed.
- Prompts: `sp-admin-stat-card` → `sp-admin-kpi-card` strip. Category column derived from key first segment with tone-coded badge. Category filter added to filter bar. Page title → "Prompts". Subtitle shows live template count.
- 20 new tests. 1206/1206 pass.

## Changes Made in 10UI-REDESIGN-4

- Curriculum: 4-tile KPI coverage strip added above objectives table (total, active, CEFR bands, skills). Derived from full `listObjectives()` response via separate `allObjectives` signal and `loadAll()` method. No fake data.
- Exercise Types: 4-tile KPI summary strip (total, enabled, ready, skills). Skill-coloured icon tile added to name cell. "Not runnable yet — foundation only" amber label for non-ready types. Reference avg-completion/score stats not shown (no backend endpoint).
- 18 new tests. 1186/1186 pass.

## Changes Made in 10UI-REDESIGN-3

- Student detail: hero section (coloured initials avatar, name, email, lifecycle/onboarding/CEFR/support-language badges, action group). KPI strip upgraded to sp-admin-kpi-card with icon tiles. Danger zone card added (Reset data, Archive, Reactivate). Back-to-students link in header.
- `initials()` and `avatarColor()` helpers added to component class.
- 30 new tests. 1168/1168 pass.

## Changes Made in 10UI-REDESIGN-2

- Students list: 4-tile KPI summary strip using `getStats()` real data. Rows-per-page selector (10/25/50/100). Filter bar spacer aligned to reference.
- Create Student: full two-column template rewrite. Left: credential section, optional profile section (collapsible), actions. Right: sticky "What happens next" aside (5 steps + email note). Security note in credentials section. Back-to-students link in page header.
- 21 new tests + 8 migration spec spy fixes. 1138/1138 pass.

## Changes Made in 10UI-REDESIGN-1

- Dashboard fully redesigned to reference layout: dark hero banner, 5-tile KPI row, onboarding funnel, at-risk, CEFR distribution, AI system, cohort engagement, recent students, placeholder cards for all unavailable backend sections.
- `SpAdminKpiCardComponent` used for KPI row (was `sp-admin-stat-card`).
- `admin-wrapper-migration.spec.ts` updated to match new KPI selector.
- 31 new tests added. 1117/1117 total.

## Changes Made in 10UI-REDESIGN-0

- Full route-by-route redesign rollout plan created.
- Page redesign matrix and component gap matrix produced.
- Phase sequence confirmed: next phase is **10UI-REDESIGN-1** (dashboard reference redesign).
- No Angular source changes in this phase.
