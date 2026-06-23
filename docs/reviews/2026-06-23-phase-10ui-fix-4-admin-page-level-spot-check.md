# Phase 10UI-FIX-4 — Admin Page-Level Spot-Check and Alignment Plan

**Date:** 2026-06-23
**Sprint:** Current (10UI series)
**HEAD before work:** a991976
**New commit:** (see final section)
**Related phases:** 10UI-AUDIT-0, 10UI-FIX-1, 10UI-FIX-2, 10UI-FIX-3

---

## Scope

Read-only inspection of all 14 admin routes against backend capability, reference design, wrapper usage, and stale/misleading UI. Apply only tiny safe fixes. Produce a prioritised page-level plan for future phases.

---

## Routes inspected

| Route | Component file |
|-------|---------------|
| `/admin` | `admin-dashboard.component.ts` |
| `/admin/students` | `admin-students.component.ts` |
| `/admin/students/:id` | `admin-student-detail.component.ts` |
| `/admin/create-student` | `create-student.component.ts` / `.html` |
| `/admin/ai-config` | `admin-ai-config.component.ts` |
| `/admin/prompts` | `admin-prompts.component.ts` |
| `/admin/usage` | `admin-ai-usage.component.ts` |
| `/admin/usage-policies` | `admin-usage-policies.component.ts` |
| `/admin/exercise-types` | `admin-exercise-types.component.ts` |
| `/admin/curriculum` | `admin-curriculum.component.ts` |
| `/admin/notifications` | `admin-notifications.component.ts` / `.html` |
| `/admin/integrations` | `admin-integrations.component.ts` |
| `/admin/diagnostics` | `admin-diagnostics.component.ts` |
| `/admin/security` | `admin-security.component.ts` / `.html` |
| `/admin/careers` | `admin-careers.component.ts` (orphan — see below) |

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/dashboard.jsx`
- `docs/design/speakpath/admin/pages/notifications.jsx`
- `docs/design/speakpath/admin/pages/students.jsx` (skimmed)
- `docs/design/admin-reference-alignment.md`
- `docs/design/speakpath/SpeakPath Brand & System.html` (previously reviewed)

---

## A. Page Matrix

| Route | Component | Reference file | UI status | Main gaps | Tiny fix done | Priority | Recommended phase |
|-------|-----------|---------------|-----------|-----------|---------------|----------|-------------------|
| `/admin` | AdminDashboardComponent | dashboard.jsx | Partial | Static "AI provider: Configured" stat card; AI System card shows always-active badges not tied to real health; reference has activity chart, at-risk list, avg score | `sp-admin-link` color token fix | P1 | 10UI-FIX-5 |
| `/admin/students` | AdminStudentsComponent | students.jsx | Good baseline | Uses raw `<a>` + page-local `.sp-admin-btn-primary` class in header slot instead of `sp-admin-button`; `accent-color` literal fixed | `accent-color` token fix | P2 | 10UI-FIX-6 |
| `/admin/students/:id` | AdminStudentDetailComponent | (no reference page) | Partial | Uses raw `<div class="sp-admin-page-header">` instead of `sp-admin-page-header`; uses `.sp-admin-badge` class strings instead of `sp-admin-badge` wrapper; missing readiness pool health section; missing activity history section; back-link color literal fixed | Back-link + link-button color token fix | P1 | 10UI-FIX-7 |
| `/admin/create-student` | CreateStudentComponent | create-student.jsx | Partial | Uses raw `sp-input`/`sp-label`/`.sp-admin-form-card` CSS; no `sp-admin-*` wrappers for inputs or button; `.sp-alert-error`/`.sp-alert-success` are non-standard | None | P2 | 10UI-FIX-6 |
| `/admin/ai-config` | AdminAiConfigComponent | ai-config.jsx | Good baseline | Native `<select>` elements retained (intentional — ngValue null incompatibility); overall wrapper usage is good | None | P3 | 10UI-FIX-9 |
| `/admin/prompts` | AdminPromptsComponent | prompts.jsx | Good baseline | Full sp-admin-* wrapper usage; filter bar, table, pagination, stat cards all present | None | — |
| `/admin/usage` | AdminAiUsageComponent | ai-usage.jsx | Good baseline | Full sp-admin-* usage; rich filters, trend, export; no significant gaps | None | — |
| `/admin/usage-policies` | AdminUsagePoliciesComponent | (no reference) | Good baseline | `#465fff` brand literal in expand-btn fixed; full wrapper usage | Expand-btn + border fallback token fix | — |
| `/admin/exercise-types` | AdminExerciseTypesComponent | exercise-types.jsx | Good baseline | Full sp-admin-* wrapper usage; filter, pagination, table-actions | None | — |
| `/admin/curriculum` | AdminCurriculumComponent | curriculum.jsx | Partial | Filter selects use raw `<select class="sp-input">` not `sp-admin-select`; page-body wrapper missing; missing `sp-admin-page-body` | None | P2 | 10UI-FIX-6 |
| `/admin/notifications` | AdminNotificationsComponent | notifications.jsx | Good baseline | Tab bar used `border-blue-600`/`text-blue-600` instead of indigo — fixed; 4-tab structure (Notifications / Delivery Queue / Config / Templates) is production-complete; SMS channel shows in config without "foundation only" label (TODO-UI-07) | Tab bar blue→indigo fix | P2 (SMS label) | 10UI-FIX-8 |
| `/admin/integrations` | AdminIntegrationsComponent | integrations.jsx | Good baseline | Full sp-admin-* usage; storage, generation settings, batch jobs all present; missing readiness pool replenishment status (TODO-UI-09) | None | P2 | 10UI-FIX-8 |
| `/admin/diagnostics` | AdminDiagnosticsComponent | diagnostics.jsx | Good baseline | Full sp-admin-* usage; status grid, event log, filters, pagination | None | — |
| `/admin/security` | AdminSecurityComponent | (no reference) | Good baseline | 2-tab (Overview / Auth Events); real data from API; missing deferred-feature notes for CSP/HSTS/MFA/distributed rate limiting (TODO-UI-10) | None | P2 | 10UI-FIX-8 |
| `/admin/careers` | AdminCareersComponent | (no reference) | Misleading/stale | **Orphan**: routed at `/admin/careers`, no sidebar link, title says "Curriculum", uses legacy raw CSS classes, no sp-admin-* wrappers, duplicates vocabulary management UI that now lives in `/admin/curriculum`. Backend capabilities are real (career profiles, vocabulary words) but UI is pre-wrapper-era | None (routed — cannot delete) | P0 | 10UI-FIX-5 |

---

## B. Component/Wrapper Misuse Matrix

| Page | Misuse / bypass | Risk | Recommended fix |
|------|----------------|------|-----------------|
| `/admin` (dashboard) | `.sp-admin-link` page-local CSS class with hardcoded color | Minor — now uses token | Already fixed in this phase |
| `/admin/students` | Header action uses raw `<a routerLink>` with `.sp-admin-btn-primary` class instead of `sp-admin-button` | Low — visual only | Replace with `<sp-admin-button>` in header slot |
| `/admin/students/:id` | Entire page uses raw `<div class="sp-admin-page-header">` and `.sp-admin-badge` class strings; not using `sp-admin-page-header`, `sp-admin-page-body`, or `sp-admin-badge` wrapper | Medium — diverges from token system | Full wrapper migration needed (10UI-FIX-7) |
| `/admin/create-student` | Raw `sp-input`, `sp-label`, `.sp-admin-form-card`, `.sp-alert-error` classes throughout | Medium | Wrapper migration with `sp-admin-input`, `sp-admin-button`, `sp-admin-alert` (10UI-FIX-6) |
| `/admin/curriculum` | Filter selects use `<select class="sp-input">` not `sp-admin-select`; missing `sp-admin-page-body` | Low — functional | Replace selects with `sp-admin-select`; add page-body wrapper |
| `/admin/careers` | No sp-admin-* wrappers at all; uses `.sp-admin-table`, `.sp-admin-form-card`, raw `<button>` | High (orphan — needs decision) | Redirect or replace with tombstone page; don't migrate it |

---

## C. Static / Fake / Stale UI Matrix

| Page | Stale/static element | Why it is a problem | Recommended fix phase |
|------|---------------------|--------------------|-----------------------|
| `/admin` | "AI provider: Configured" stat card — always shows "Configured" regardless of actual configuration | Misleads admin about real provider health; could show "Configured" even with no API key set | 10UI-FIX-5 — replace with live config status from `/api/admin/ai-config` |
| `/admin` | AI System card badges (Writing, Feedback, Speaking, Listening) all always show "Active" | No real health check — these are static strings | 10UI-FIX-5 — either remove or clearly label as "Configured" not live health |
| `/admin/careers` | Entire page is orphan UI — title says "Curriculum", route is `/careers`, sidebar has no link | Admin can only reach it by typing URL; functionality duplicates curriculum vocabulary management | 10UI-FIX-5 — add tombstone/redirect or suppress route |
| `/admin/notifications` | SMS channel in config tab shows full configuration UI without noting it is foundation-only / no real provider | Admin may believe SMS is production-ready | 10UI-FIX-8 — add `sp-admin-badge tone="neutral"` "Foundation only" label |
| `/admin/security` | No notes about deferred features (CSP, HSTS, MFA, distributed rate limiting) | Admin reads the page as complete security posture | 10UI-FIX-8 — add a deferred-features info alert below the Overview card |

---

## D. Tiny Fixes Applied in This Phase

| File | Fix | Reason |
|------|-----|--------|
| `admin-notifications.component.html` | Tab bar `border-blue-600`/`text-blue-600` → `border-indigo-600`/`text-indigo-600` | Align active tab colour to brand indigo; security page already used indigo |
| `admin-dashboard.component.ts` | `.sp-admin-link` color `#4338CA` → token, hover `#3730A3` → token | Token alignment |
| `admin-dashboard.component.ts` | `.sp-admin-link:hover` `#3730A3` → `var(--sp-admin-primary-hover,#3A2EA8)` | Token alignment |
| `admin-careers.component.ts` | `.sp-admin-link-button` color `#4338CA` → token | Token alignment |
| `admin-student-detail.component.ts` | `.sp-admin-back-link` color `#4338CA` → token | Token alignment |
| `admin-student-detail.component.ts` | `.sp-admin-link-button` color `#4338CA` → token | Token alignment |
| `admin-students.component.ts` | `accent-color: #4338CA` → token | Token alignment |
| `admin-usage-policies.component.ts` | `.sp-up-expand-btn` `#465fff` → primary token; `.sp-up-rule-limit` border/text fallbacks updated | Token alignment |

---

## E. Recommended Next Phases

### 10UI-FIX-5 — Dashboard accuracy + careers orphan (P0/P1)

- Replace static "AI provider: Configured" stat card with live config status (call existing `/api/admin/ai-config` to get configured provider name; show provider name or "Not configured").
- Replace AI System static "Active" badges with "Configured" or remove the always-green section.
- Add a tombstone/redirect for `/admin/careers` — or suppress the route. Do not attempt to migrate it.
- Scope: `admin-dashboard.component.ts`, `app.routes.ts` (optional careers suppress), docs.

### 10UI-FIX-6 — Student list + create-student + curriculum wrapper migration (P2)

- `/admin/students`: Replace header `<a>` with `sp-admin-button`.
- `/admin/create-student`: Migrate raw `sp-input`/`sp-label` to `sp-admin-input`/`sp-admin-form-field`; replace `.sp-alert-error` with `sp-admin-alert`.
- `/admin/curriculum`: Replace raw `<select class="sp-input">` filters with `sp-admin-select`; wrap page in `sp-admin-page-body`.
- Scope: 3 component files + templates.

### 10UI-FIX-7 — Student detail full wrapper migration (P1)

- Migrate `AdminStudentDetailComponent` from raw `<div class="sp-admin-page-header">` to `sp-admin-page-header` + `sp-admin-page-body`.
- Replace `.sp-admin-badge` class strings with `sp-admin-badge` wrapper (already done for onboarding and lifecycle in the students list; detail page still uses class strings).
- Add readiness pool health section (TODO-UI-02).
- Add activity history section (TODO-UI-03).
- Scope: `admin-student-detail.component.ts` + `.html` (large file — careful scoping needed).

### 10UI-FIX-8 — Notifications SMS label + security deferred notes + integrations readiness pool (P2)

- Notifications config tab: add "Foundation only" badge/note next to SMS channel.
- Security overview: add deferred-features info alert (CSP, HSTS, MFA, distributed rate limiting not yet implemented).
- Integrations: add readiness pool replenishment status section (TODO-UI-09).
- Scope: 3 small HTML/template additions.

### 10UI-FIX-9 — AI Config + minor polish (P3)

- AI Config: evaluate whether native `<select>` with `[ngValue]="null"` can be wrapped now that the CVA components have matured; if yes, migrate.
- Any remaining raw Tailwind-heavy sections in low-traffic pages.

### 10UI-FIX-FINAL — Admin UI catch-up closure audit

- Full re-audit after FIX-5 through FIX-9.
- Confirm all P0/P1 items closed.
- Update docs/design/admin-reference-alignment.md with final status.
- Update docs/handoffs/current-product-state.md.

---

## Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1035/1035 |
| Backend unchanged | n/a — no backend changes |
| Playwright | Not required — no routing/navigation behaviour changed |

---

## Confirmation

- No backend/API/migration changes implemented.
- No student UI changes.
- No full page redesigns.
- No new admin pages.
- No new major components.
- Only tiny safe CSS token fixes and one tab-bar colour alignment applied.
- All changes are in admin feature page component files only.
