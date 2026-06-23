---
status: current
lastUpdated: 2026-06-23 12:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10UI-AUDIT-0 — UI / Backend Capability Reconciliation

**Date:** 2026-06-23
**Sprint:** 10UI-AUDIT-0
**Type:** Audit / reconciliation — no code changes

---

## HEAD before work

`c158ea4` — docs: close enterprise auth security hardening

---

## Files inspected

### Frontend
- `src/LinguaCoach.Web/src/app/app.routes.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage/admin-usage.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/learning-path/learning-path.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/practice/practice-gym.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/progress/progress.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`

### Backend controllers
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `src/LinguaCoach.Api/Controllers/AdminCurriculumController.cs`
- `src/LinguaCoach.Api/Controllers/AdminSecurityController.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`
- `src/LinguaCoach.Api/Controllers/AdminOnboardingController.cs`
- `src/LinguaCoach.Api/Controllers/AdminGenerationController.cs`
- `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs`
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs`
- `src/LinguaCoach.Api/Controllers/AuthController.cs`
- `src/LinguaCoach.Api/Controllers/NotificationsController.cs`
- `src/LinguaCoach.Api/Controllers/PracticeGymSuggestionsController.cs`

### Docs
- `AGENTS.md`
- `docs/architecture/README.md`
- `docs/handoffs/current-product-state.md`
- `docs/sprints/current-sprint.md`
- `docs/backlog/deferred-work.md`
- `TODOS.md`

---

## Admin sidebar navigation — what is actually shown

The `admin-app-layout.component.html` sidebar renders these nav items:

| Nav label | Route |
|---|---|
| Dashboard | /admin |
| Students | /admin/students |
| AI Config | /admin/ai-config |
| Prompts | /admin/prompts |
| AI Usage | /admin/usage |
| Exercise Types | /admin/exercise-types |
| Notifications | /admin/notifications |
| Integrations | /admin/integrations |
| Diagnostics | /admin/diagnostics |
| Security | /admin/security |

**Missing from nav (routes exist but no nav link):**
- `/admin/curriculum` — route exists, component exists, but NO nav link in sidebar
- `/admin/usage-policies` — route exists, component exists, but NO nav link in sidebar
- `/admin/careers` — route exists, component exists, but NO nav link in sidebar

**Critical finding:** Three admin routes are inaccessible from the sidebar. Admins must know the URL to reach them. `/admin/usage-policies` is particularly important because usage governance is a complete production capability.

---

## Route-by-route matrix — Admin

| Route | Component | Backend capabilities | UI exposes | Missing/incomplete UI | Stale/misleading UI | Priority | Rec phase |
|---|---|---|---|---|---|---|---|
| /admin | AdminDashboardComponent | Stats, student list preview, quick actions | KPI cards (total students, onboarded, activities), quick-action links, recent students mini-table | No link to usage-policies, no link to curriculum, no notification count, no AI health status | "AI provider: Configured" is always static — never reflects real provider status | P1 | 10UI-FIX-4 |
| /admin/students | AdminStudentsComponent | Paged list, search, filter (lifecycle/onboarding/CEFR), sort, archive toggle | Full server-driven list, search, all three filter selects, sort, include-archived, pagination | No readiness pool health visible, no policy column | — | P2 | 10UI-FIX-2 |
| /admin/students/:id | AdminStudentDetailComponent | Detail, onboarding progress, preferences, CEFR set/clear, lifecycle controls (pause/unpause/reactivate), reset, audit history, usage policy assign/reset, send reset link, learning memory | All of the above | Readiness pool health (`GET /api/admin/students/{id}/readiness-pool/health`) exists in backend but is NOT shown in UI. Onboarding flow viewer (`GET /api/admin/onboarding/flow`) not linked from student detail. Per-user session revocation UI (admin-initiated) deferred. | — | P2 | 10UI-FIX-2 |
| /admin/ai-config | AdminAiConfigComponent | Provider list, category config (provider/model/voice), API key set, endpoint set, model add/test, category test, pricing read, pricing overrides CRUD | Category config, model/provider/voice edit, API key, test, pricing overrides list/create/edit/deactivate, pricing read panel | — | — | Complete | — |
| /admin/prompts | AdminPromptsComponent | Prompt list, detail, create version, activate/deactivate | Full CRUD, activation, preview | — | — | Complete | — |
| /admin/usage | AdminAiUsageComponent | Paged AI usage recent calls, summary cards, date filters, column filters (provider/model/featureKey/status/studentId), daily trend, CSV export, zero-cost alert, pricing overrides | All of the above | — | — | Complete | — |
| /admin/usage-policies | AdminUsagePoliciesComponent | Policy list, create, edit, deactivate, rule CRUD, feature definitions, student assignment, student reset | Full CRUD including rule editor, student assignment | **NO nav link** — page is unreachable unless URL is typed directly | — | P0 | 10UI-FIX-4 |
| /admin/exercise-types | AdminExerciseTypesComponent | Exercise type catalog list, enable/disable, practice item count config | Full list, enable/disable, item count editing | — | — | Complete | — |
| /admin/curriculum | AdminCurriculumComponent | Objective list (filter by CEFR/skill/active), get by key, taxonomy, create, update, activate/deactivate, routing preview | Full CRUD, filter, routing preview panel | **NO nav link** — page is unreachable unless URL is typed directly | — | P0 | 10UI-FIX-4 |
| /admin/notifications | AdminNotificationsComponent | Notification list (filter/search/page), outbox list (retry/cancel), config (InApp/Email/SMS, test email), templates (CRUD, preview), send notification slide-over, user preferences read | All of the above | — | SMS shown in config but backend is always-disabled (`DisabledSmsSender`); no "Coming soon" label in UI to clarify | P2 | 10UI-FIX-2 |
| /admin/security | AdminSecurityComponent | Security settings read (password policy, lockout, rate limits, JWT, refresh tokens, Google config), auth events paged/filtered | Settings overview tab, auth events tab with filters | Per-user session revocation not wired (deferred per 10Auth-F-FINAL). Google config is read-only. No "Deferred" labels for SMS/MFA/CAPTCHA. | — | P2 | 10UI-FIX-3 |
| /admin/integrations | AdminIntegrationsComponent | Storage settings/test, generation settings, lesson batch triggers, batch list, retry/cancel, ready buffer stats | All of the above | Readiness pool replenishment status not shown (backend: `GET /api/admin/students/{id}/readiness-pool/health` is per-student only; no aggregate health endpoint) | — | P2 | 10UI-FIX-3 |
| /admin/diagnostics | AdminDiagnosticsComponent | Diagnostics status, event log (paged, filtered, level/search) | Status cards, event log, filters, pagination | — | — | Complete | — |
| /admin/careers | AdminCareersComponent | Career list, words CRUD | Basic list and word management | **NO nav link** — page unreachable from sidebar | Career CRUD UI exists but is not surfaced to admins; docs mention this is seed-only | P1 | 10UI-FIX-4 |
| /admin/create-student | CreateStudentComponent | POST /api/admin/students | Create form | — | — | Complete | — |

---

## Route-by-route matrix — Student

| Route | Component | Backend capabilities | UI exposes | Missing/incomplete UI | Stale/misleading UI | Priority | Rec phase |
|---|---|---|---|---|---|---|---|
| /dashboard (Today) | DashboardComponent | Today's session, dashboard stats, learning path summary, learning memory, placement result | Today's Lesson card (start/resume), learning path progress, memory summary, practice link | Auth notice shown but no persistent notification bell integration on this page | "AI provider: Configured" dashboard card is static (not live) — but that is admin-side | P2 | 10UI-FIX-3 |
| /journey (/my-path) | LearningPathComponent | Learning path, session history (paged), learning memory | Journey summary, session history with pagination, memory recap | — | — | Complete | — |
| /practice | PracticeGymComponent | Practice Gym suggestions (SuggestedItems/ContinueItems/ReviewItems), skill cards (pool-aware), exercise type catalog, practice start | All three suggestion sections, skill cards, exercise type cards, routing labels | — | Coming-soon cards still shown for several formats that remain unimplemented — this is intentional and not misleading | Complete | — |
| /progress | ProgressComponent | Progress summary, module list, score trend points, vocabulary | Stats grid, module progress cards, vocabulary sample | No link to vocabulary detail from progress; no CEFR level display | — | P2 | 10UI-FIX-3 |
| /profile | ProfileComponent | Profile preferences (name, support language, goals, focus, difficulty, session duration, translation help), notification preferences (category x channel) | Learning preferences with chip toggles, notification preferences table with required/coming-soon indicators | Student CEFR is NOT editable — correct. Prompt editing not exposed — correct. | — | Complete | — |
| /onboarding/v2 | OnboardingV2Component | Full v2 onboarding flow (11 step types, all step renderers) | All 11 renderers, step progression, summary | No Playwright E2E for v2 flow | — | P2 | 10UI-FIX-3 |
| /placement | PlacementComponent | Placement assessment 6-section, server TTS audio, AI evaluation | Full placement flow, audio player, transcript toggle | — | — | Complete | — |
| /activity | ActivityLessonComponent | Activity CRUD, all 4 activity types, all pattern renderers, AI feedback | All types, pattern renderers, feedback UI | — | — | Complete | — |
| /lesson/:sessionId | LessonComponent | Session detail, exercise steps, prepare, complete | Session steps, prepare, activity nav, completion summary | — | — | Complete | — |
| /vocabulary | VocabularyComponent | Vocabulary list | Vocabulary list | Not a top-level nav item — correct per product model | — | Complete | — |

---

## Backend capability matrix

| Capability | Backend status | API endpoint | UI route/page | UI status | Gap | Priority | Rec phase |
|---|---|---|---|---|---|---|---|
| Local login | Complete | POST /api/auth/login | /login | Complete | — | — | — |
| Account lockout (5 attempts/15 min) | Complete | embedded in login | — | Not student-visible (correct) | — | — | — |
| Rate limiting (5 policies) | Complete | middleware | — | Not UI-visible (correct) | — | — | — |
| Password policy | Complete | server-side | /change-password | Complete | — | — | — |
| Reset password (token email) | Complete | POST /api/auth/reset-password, POST /api/admin/students/{id}/send-reset-link | /reset-password, /admin/students/:id | Complete | — | — | — |
| Refresh tokens / session management | Complete | POST /api/auth/refresh, /logout, /revoke-sessions | Silent (interceptor) | Complete | No UI for per-session revocation (admin-initiated) — deferred | P2 | 10UI-FIX-3 |
| Auth event audit log | Complete | GET /api/admin/auth-events, GET /api/admin/security/auth-events | /admin/security (Auth Events tab) | Complete | — | — | — |
| Security notifications (5 groups) | Complete | triggered by auth flows | /profile (notification prefs) | Complete | — | — | — |
| Google external login | Complete (disabled by default) | POST /api/auth/external/google | No UI (intentional — no login button wired) | Missing | Google login button not present on /login page. Backend ready. Deferred intentionally. | P2 | 10UI-FIX-3 |
| Admin security settings read | Complete | GET /api/admin/security/settings | /admin/security | Complete | — | — | — |
| In-app notifications (bell) | Complete | GET /api/notifications, /unread-count, /read, /archive | StudentAppLayout (bell dropdown) | Complete | Bell only in student layout; no admin-side notification bell | P3 | — |
| Email notifications (SMTP) | Complete | outbox/dispatch job | — | Admin config UI complete | — | — | — |
| Notification outbox (retry/cancel) | Complete | GET /api/admin/notifications/outbox, retry/cancel | /admin/notifications (Delivery queue tab) | Complete | — | — | — |
| Notification templates CRUD | Complete | GET/POST/PUT /api/admin/notifications/templates | /admin/notifications (Templates tab) | Complete | — | — | — |
| Notification channel config (DB-backed) | Complete | GET/PUT /api/admin/notifications/config/email, /sms, /in-app | /admin/notifications (Configuration tab) | Complete | SMS channel shown without clear "not active" / "coming soon" label | P2 | 10UI-FIX-2 |
| Notification preferences (user) | Complete | GET/PUT /api/notifications/preferences | /profile (Notification preferences section) | Complete | — | — | — |
| SMS foundation | Complete (disabled) | ISmsSender / DisabledSmsSender | /admin/notifications config tab | Misleading — SMS config shown without clear "disabled / foundation only" indication | P2 | 10UI-FIX-2 |
| Send notification (admin manual) | Complete | POST /api/admin/notifications/send | /admin/notifications (Send notification button) | Complete | — | — | — |
| AI provider/model config | Complete | GET/PUT /api/admin/ai-providers, /api/admin/ai/categories | /admin/ai-config | Complete | — | — | — |
| AI pricing overrides | Complete | GET/POST/PUT/DELETE /api/admin/ai/pricing/overrides | /admin/ai-config (pricing section) | Complete | — | — | — |
| AI usage reporting (summary/recent/trends/export) | Complete | GET /api/admin/ai-usage/summary, /recent, /trends, /export.csv | /admin/usage | Complete | — | — | — |
| AI usage filters (date/provider/model/feature/status/student) | Complete | query params on all usage endpoints | /admin/usage | Complete | — | — | — |
| Zero-cost alert | Complete | embedded in summary | /admin/usage | Complete | — | — | — |
| Student list (paged/filtered/sorted) | Complete | GET /api/admin/students | /admin/students | Complete | — | — | — |
| Student detail endpoint | Complete | GET /api/admin/students/{id} | /admin/students/:id | Complete | — | — | — |
| Student lifecycle controls (pause/unpause/reactivate/archive) | Complete | POST /api/admin/students/{id}/pause, /unpause, /reactivate, /archive | /admin/students/:id | Complete | — | — | — |
| Student CEFR management | Complete | PUT /api/admin/students/{id}/cefr | /admin/students/:id | Complete | — | — | — |
| Student audit history | Complete | GET /api/admin/students/{id}/audit-history | /admin/students/:id | Complete | — | — | — |
| Student onboarding progress | Complete | embedded in detail endpoint | /admin/students/:id | Complete | — | — | — |
| Student preferences read | Complete | embedded in detail endpoint | /admin/students/:id | Complete | Admin edit of preferences intentionally not implemented. | — | — |
| Student usage policy assignment | Complete | GET/PUT /api/admin/students/{id}/usage-policy, DELETE | /admin/students/:id | Complete | — | — | — |
| Student learning memory read | Complete | GET /api/admin/students/{id}/learning-memory | /admin/students/:id | Complete | — | — | — |
| Student activity history read | Complete | GET /api/admin/students/{id}/activity-history | /admin/students/:id | Partial | Student detail has no activity-history section in the UI (endpoint exists but is not wired in admin-student-detail template) | P1 | 10UI-FIX-2 |
| Readiness pool health (per student) | Complete | GET /api/admin/students/{id}/readiness-pool/health, /readiness-pool | /admin/students/:id | Missing | No readiness pool section in student detail page — endpoint exists since Phase 10N | P1 | 10UI-FIX-2 |
| Readiness pool aggregate status | Missing | No aggregate endpoint exists | /admin/integrations | Missing | Integrations page has no readiness pool health summary | P2 | 10UI-FIX-3 |
| Onboarding flow admin view | Complete | GET /api/admin/onboarding/flow | No UI wired | Missing | Admin cannot view the active onboarding flow definition from any admin page | P1 | 10UI-FIX-2 |
| Usage governance — policy list/CRUD | Complete | Full CRUD under /api/admin/usage-policies | /admin/usage-policies | Complete (page complete, no nav link) | P0: page unreachable from nav | P0 | 10UI-FIX-4 |
| Usage governance — rule CRUD | Complete | POST/PUT/DELETE /api/admin/usage-policies/{id}/rules | /admin/usage-policies | Complete | — | — | — |
| Curriculum objectives list/CRUD | Complete | GET/POST/PUT /api/admin/curriculum/objectives, activate/deactivate | /admin/curriculum | Complete (page complete, no nav link) | P0: page unreachable from nav | P0 | 10UI-FIX-4 |
| Curriculum taxonomy | Complete | GET /api/admin/curriculum/taxonomy | /admin/curriculum | Complete | — | — | — |
| Curriculum routing preview | Complete | POST /api/admin/curriculum/routing-preview | /admin/curriculum | Complete | — | — | — |
| Exercise type catalog | Complete | GET /api/admin/exercise-types, PATCH enable/disable | /admin/exercise-types | Complete | — | — | — |
| Practice item count config | Complete | PATCH /api/admin/exercise-types/{key} | /admin/exercise-types | Complete | — | — | — |
| Prompt management | Complete | GET/POST/activate/deactivate prompts | /admin/prompts | Complete | — | — | — |
| Background lesson generation | Complete | trigger, batch list, retry, cancel | /admin/integrations | Complete | — | — | — |
| Practice Gym background caching | Complete | pool replenishment job | /admin/integrations | Partial | No specific practice gym pool health shown on integrations page | P2 | 10UI-FIX-3 |
| Diagnostics logs | Complete | GET /api/admin/diagnostics | /admin/diagnostics | Complete | — | — | — |
| Onboarding v2 engine | Complete | GET/POST /api/onboarding, /onboarding/steps/{key}, /onboarding/complete | /onboarding/v2 | Complete | No Playwright E2E for v2 | P3 | — |
| Today lesson (dynamic session) | Complete | GET /api/sessions/today | /dashboard | Complete | — | — | — |
| Practice Gym suggestions | Complete | GET/POST /api/practice-gym/suggestions/{id}/start, /complete | /practice | Complete | — | — | — |
| Student progress | Complete | GET /api/progress | /progress | Complete | CEFR level not shown on progress page despite backend returning it | P2 | 10UI-FIX-3 |
| Student profile preferences | Complete | GET/PUT /api/profile/preferences | /profile | Complete | — | — | — |
| Google login button | Missing | POST /api/auth/external/google | /login | Missing | Backend exists, no login UI button | P2 | 10UI-FIX-3 |
| Admin usage (old /admin/usage-analytics route) | Stale placeholder | N/A | /admin/usage (route exists as redirect from /admin/ai-usage) | **MISLEADING** — `/admin/usage` route in nav goes to the real AI usage page. But there is an orphan `AdminUsageComponent` (admin-usage folder) that renders a placeholder with static emoji cards saying "not yet tracked." This component is only reachable if someone navigates directly to what used to be the usage route before the redirect. | P1 | 10UI-FIX-4 |

---

## Top P0 findings

### P0-1: `/admin/usage-policies` has no nav link
The usage governance page is complete and production-ready. Admins cannot find it from the sidebar. They must type the URL manually. This makes the entire usage governance capability invisible.

### P0-2: `/admin/curriculum` has no nav link
The curriculum management page is complete with full CRUD and routing preview. Admins cannot find it from the sidebar.

---

## Top P1 findings

### P1-1: Student activity history not wired in student detail UI
`GET /api/admin/students/{id}/activity-history` exists in `AdminController`. `AdminApiService.getStudentAuditHistory` is wired. But there is no activity history section in the admin student detail template — it only shows audit history (admin actions), not the student's actual activity attempts. These are different datasets.

### P1-2: Readiness pool health not shown in student detail
`GET /api/admin/students/{id}/readiness-pool` and `/readiness-pool/health` exist since Phase 10N. No UI section in `/admin/students/:id` shows pool state for a student. Admins cannot diagnose why a student has no ready activities.

### P1-3: Onboarding flow admin viewer not wired
`GET /api/admin/onboarding/flow` exists since Phase 10I. No admin page shows the active onboarding flow definition. Admins cannot inspect step definitions or answer mappings without a database query.

### P1-4: Stale `AdminUsageComponent` (admin-usage folder) is a misleading placeholder
The `admin-usage` folder contains a `AdminUsageComponent` that renders static placeholder cards with emoji saying "not yet tracked." The `/admin/usage` route alias redirects to the real AI usage page correctly — but the old placeholder component still exists and would be rendered if the route configuration ever reverted. This is a confusion risk and should be removed.

### P1-5: Dashboard "AI provider: Configured" card is always static
The dashboard KPI card with label "AI provider" always shows "Configured" regardless of whether any AI provider is actually configured. This could mislead admins who have not set up provider credentials.

---

## Top P2 findings

### P2-1: `/admin/careers` has no nav link
The careers page exists but is not in the nav. Career profile management is mentioned in docs as admin-accessible but is hidden.

### P2-2: SMS shown in notification config without clear "coming soon / disabled" label
The admin notification Configuration tab shows an SMS section with `HasApiKey` and `IsEnabled` fields, but `DisabledSmsSender` always skips. There is no clear "SMS is foundation only — not active" label in the UI. Admins could waste time trying to configure SMS.

### P2-3: Student CEFR not shown on `/progress` page
The backend returns CEFR level as part of student profile and progress. The `/progress` page does not display the student's current CEFR level badge.

### P2-4: Google login button missing from `/login` page
Backend `POST /api/auth/external/google` is fully implemented. The login page does not offer a Google login button. This is an intentional deferral but is not documented in the UI.

### P2-5: No readiness pool aggregate status on `/admin/integrations`
Integrations page shows lesson batch generation status but not the readiness pool replenishment job health or pool sizes across students.

### P2-6: Per-user admin session revocation UI deferred but undocumented in UI
Admins can revoke all sessions for themselves (`/api/auth/revoke-sessions`) but cannot revoke a specific student's sessions from the admin student detail. This is a known deferred item with no placeholder or note in the UI.

---

## Stale / misleading UI findings

| Finding | Location | Description | Priority |
|---|---|---|---|
| Static "AI provider: Configured" card | /admin dashboard | Always shows "Configured" — never reflects real provider health | P1 |
| Orphan AdminUsageComponent placeholder | admin-usage folder | Old placeholder with emoji and "not yet tracked" copy — stale, never shown but confusing | P1 |
| SMS config shown without disabled label | /admin/notifications | No "foundation only" or "coming soon" label on SMS section | P2 |
| Dashed analytics placeholder cards on dashboard | /admin dashboard | Several dashed placeholder sections marked "coming soon" with no dates or milestones | P3 |
| No "deferred" labels for MFA, CAPTCHA, HSTS in security page | /admin/security | Users may assume these are omitted rather than intentionally deferred | P3 |

---

## Recommended implementation roadmap

Based on findings, the following phases are proposed. Each is small and independently shippable.

### 10UI-FIX-1 — Admin navigation: add missing nav links
**Scope:** Add three missing nav links to the admin sidebar:
- `/admin/usage-policies` (label: "Usage Policies")
- `/admin/curriculum` (label: "Curriculum")
- `/admin/careers` (label: "Careers")

Group them under appropriate sidebar sections. This is a single file change (`admin-app-layout.component.html`).

**Priority:** P0 blocker — two complete production capabilities are invisible.
**Estimate:** 1-2 hours. No backend changes. No migrations.

### 10UI-FIX-2 — Admin student detail: readiness pool + activity history
**Scope:**
- Add a "Readiness pool" section to `/admin/students/:id` showing pool health (`/readiness-pool/health`) and item list (`/readiness-pool`).
- Add an "Activity history" section to `/admin/students/:id` showing the student's recent activity attempts (`/activity-history`).
- Add `AdminApiService` methods for both endpoints.
- Wire SMS "foundation only" label in `/admin/notifications` configuration tab.

**Priority:** P1 — backend capabilities exist but are completely invisible in UI.
**Estimate:** 1 day. No backend changes. No migrations.

### 10UI-FIX-3 — Secondary gaps: security, integrations, student UX
**Scope:**
- Admin security page: add deferred feature notes (per-user session revoke, MFA, CAPTCHA, HSTS).
- Admin integrations: add readiness pool health summary card.
- Admin onboarding flow viewer: add a basic read-only page or modal at `/admin/onboarding` showing `GET /api/admin/onboarding/flow` output.
- Student `/progress` page: display CEFR level badge.
- Login page: add placeholder Google login button (disabled, "coming soon") or document deferral clearly.

**Priority:** P2 — usability and transparency improvements.
**Estimate:** 1-2 days. No backend changes. No migrations.

### 10UI-FIX-4 — Dashboard accuracy + orphan cleanup
**Scope:**
- Admin dashboard: make "AI provider" stat card reflect real configuration state (call `/api/admin/ai/categories` to check if any provider is configured).
- Admin dashboard: add link/card to usage-policies.
- Remove orphan `AdminUsageComponent` from `admin-usage` folder (or repurpose as learning analytics placeholder with proper copy).
- Add nav links confirmed in 10UI-FIX-1.

**Priority:** P1 for static card, P1 for orphan cleanup.
**Estimate:** 1 day. No backend changes. No migrations.

### 10UI-FIX-FINAL — Closure audit
Verify all P0/P1 gaps are resolved. Run full test gates. Update docs.

---

## Decisions made

1. No code was changed in this phase. Audit only.
2. The orphan `AdminUsageComponent` is confirmed stale and should be removed or repurposed in 10UI-FIX-4.
3. `/admin/usage-policies` and `/admin/curriculum` missing from nav are classified P0 because they represent complete production capabilities that are completely invisible to admins.
4. Google login UI deferral is P2 — backend is ready but no product decision has been made to expose it.
5. `AdminUsageComponent` in the `admin-usage` folder should NOT be confused with `AdminAiUsageComponent` in `admin-ai-usage`. They are different components. The latter is the real, complete AI usage page.

---

## Risks and unresolved questions

- The admin sidebar has no section grouping for "governance" items (usage policies, curriculum). Adding three nav items may make the sidebar crowded. Consider grouping under a collapsible "Content & Governance" section.
- Career profiles are seed-only. Even if the nav link is added, admins cannot add new career profiles without a backend write endpoint (TODO-2 in deferred-work.md). The careers nav link will expose a limited write surface.
- No aggregate readiness pool health endpoint exists. Integrations page improvement (10UI-FIX-3) would need either a new admin endpoint or a student-by-student approach.

---

## Final verdict

The backend has advanced significantly ahead of admin UI visibility in three areas:

1. **Navigation:** Two complete production admin pages (`/admin/usage-policies`, `/admin/curriculum`) are fully built but have no nav links. P0 fix is a single HTML file change.
2. **Student detail gaps:** Readiness pool health and student activity history are complete backend capabilities not visible anywhere in the admin student detail page.
3. **Dashboard accuracy:** The static "AI provider: Configured" card and orphan placeholder component are misleading.

Student-facing UI is broadly aligned with backend capabilities. No student-facing P0 gaps found.

---

## Next recommended action

Implement **10UI-FIX-1** immediately: add three missing nav links to admin sidebar. This is the highest-impact / lowest-effort fix and unblocks two complete production admin capabilities.

---

## Documentation impact

- Docs reviewed: AGENTS.md, docs/architecture/README.md, docs/handoffs/current-product-state.md, docs/sprints/current-sprint.md, docs/backlog/deferred-work.md, TODOS.md
- Docs updated: docs/sprints/current-sprint.md, docs/handoffs/current-product-state.md, TODOS.md (see below)
- Docs intentionally not updated: architecture docs (no architecture changes), deployment docs (no deployment changes)
- Reason: Audit-only phase. No code changed. Doc updates limited to sprint record, product state summary, and TODOS for tracked gaps.

---

## Gate result

- `git diff --check`: not required (no code changed)
- Backend tests: not run (no code changed)
- Frontend tests: not run (no code changed)
- Code changed: NO
- UI/backend implementation done: NO — audit only
