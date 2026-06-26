# Phase 11E — Production Admin Live Smoke Test

**Date:** 2026-06-27
**Sprint:** Phase 11E
**Browser:** Headless Chromium (gstack/browse)
**Target:** https://speakpath.app/admin
**Tester:** Claude Code (automated + visual review)

---

## Login

- Login page at `/login` — rendered correctly, redirected from `/admin`
- Credentials accepted, redirected to `/admin/dashboard`
- No console errors on login

---

## Pages Visited

All 16 admin routes tested:

| Route | HTTP | API Calls | Console Errors | Status |
|---|---|---|---|---|
| `/admin` | 200 | students, stats, ai/categories, activity-trends, score-distribution, ai-usage/aggregate-trends | None (post-login) | OK |
| `/admin/students` | 200 | students?page=1&pageSize=10 | None | OK |
| `/admin/students/:id` | 200 | student detail, learning-memory, activity-history, audit-history, usage-policy, readiness-pool/health, readiness-pool | None | OK |
| `/admin/create-student` | 200 | None required | None | OK |
| `/admin/ai-config` | 200 | ai/pricing/overrides | None | OK |
| `/admin/prompts` | 200 | prompts | None | OK |
| `/admin/usage` | 200 | ai-usage/summary, ai-usage/by-category | None | OK |
| `/admin/usage-policies` | 200 | usage-policies | None | OK |
| `/admin/usage-analytics` | 200 | None (inline data from prior period) | None | OK |
| `/admin/lessons` | 200 | generation/batches, generation/settings | None | OK |
| `/admin/curriculum` | 200 | objectives (x2), validation, coverage, taxonomy | None | OK |
| `/admin/exercise-types` | 200 | exercise-types | None | OK |
| `/admin/notifications` | 200 | notifications, outbox, config | None | OK |
| `/admin/integrations` | 200 | integrations/storage, notifications/config | None | OK |
| `/admin/diagnostics` | 200 | diagnostics/status, diagnostics/events?limit=100 | None | OK |
| `/admin/security` | 200 | security/auth-events?page=1&pageSize=20 | None | OK |
| `/admin/onboarding` | 200 | onboarding/flows, onboarding/flow | None | OK |

---

## Console / Network Error Summary

### Pre-login artifact (not a live bug)
- `[2026-06-26T23:05:05.087Z] [error] Failed to load resource: 401` appeared in the console buffer on every page during the session.
- Root cause: the browser session cached the 401 response from a preflight `GET /api/admin/students?pageSize=100` that fired before login completed. After clearing the console buffer and reloading, no errors appeared.
- Verified: fresh reload of `/admin/students` after login = zero console errors, zero 4xx/5xx.

### AI provider status
- Diagnostics page reports: **"AI provider: Not configured"**
- All 4 LLM categories show Gemini configured in AI Config UI — this discrepancy is likely because the diagnostics status endpoint checks for a different env var or provider health ping.
- No impact on current admin workflow; worth monitoring.

---

## Findings Table

| ID | Severity | Page | Issue | Evidence | Console/Network | Reproduction | Fix |
|---|---|---|---|---|---|---|---|
| F-01 | **P1** | All admin pages using `(clicked)` binding | `SpAdminButtonComponent` had no `@Output() clicked` — all `(clicked)="..."` bindings were silently broken; buttons appeared clickable but did nothing | Add step, Refresh, Edit step, Remove step, Save settings, Activate flow, etc. all non-functional | No error — Angular silently ignores unmatched output bindings | Click any button on Onboarding, Notifications, Lessons, Diagnostics, Security, Integrations | **FIXED** — added `@Output() clicked = new EventEmitter<void>()` and `(click)="!disabled && !loading && clicked.emit()"` to `SpAdminButtonComponent` |
| F-02 | P2 | Diagnostics | "AI provider: Not configured" shown in system status despite AI Config showing 4/4 LLM categories configured with Gemini | Diagnostics screenshot, Diagnostics text content | GET /api/admin/diagnostics/status → 200 with `aiProvider: "Not configured"` | Visit /admin/diagnostics | Backend diagnostics status endpoint may be checking an env var or provider health that isn't set in production |
| F-03 | P3 | Create student | "Welcome email: backend not available yet — no automated email send endpoint. Share the temporary password manually." shown in sidebar | Create student screenshot | None | Visit /admin/create-student | Honest pending state — no fix needed now |
| F-04 | P3 | Lessons | "Aggregate pool endpoint not yet implemented" shown in Readiness pool section | Lessons screenshot | None | Visit /admin/lessons | Honest pending state — no fix needed now |
| F-05 | P3 | Usage Analytics | "Student engagement — Pending endpoint. Engagement heatmap data requires a dedicated backend endpoint." | Usage Analytics screenshot | None | Visit /admin/usage-analytics | Honest pending state — no fix needed now |
| F-06 | P3 | Integrations | Webhook, Analytics, Admin API cards show "Not implemented" | Integrations screenshot | None | Visit /admin/integrations | Honest pending state — no fix needed now |
| F-07 | P3 | Diagnostics | Background jobs section shows "Pending endpoint — Background job history requires a dedicated jobs/queue API endpoint not yet available." | Diagnostics text | None | Visit /admin/diagnostics | Honest pending state — no fix needed now |

---

## Interactions Tested

| Action | Result |
|---|---|
| Students search box | Renders, focusable |
| Students lifecycle filter dropdown | 14 options, works |
| Students rows per page selector | 4 options, works |
| Students row actions menu | Opens correctly (View profile, Edit student, Reset password, Reset data, Archive) |
| Student detail Overview tab | Loads hero, profile, readiness pool, mastery eval, learning memory |
| Student detail Activity tab | Navigates without error |
| Student detail Settings tab | Navigates without error |
| AI Config Configure slide-over | Opens/closes correctly |
| Onboarding Add step button | Broken before fix; will work after deploy |
| Sidebar navigation all items | All 16 items navigate correctly |
| Sidebar toggle button | Present and interactive |
| Profile menu button | Present in header |

---

## Screenshots Captured

- `admin-dashboard.png` — Dashboard full page
- `admin-students.png` — Students list
- `admin-student-detail.png` — Student detail overview
- `admin-create-student.png` — Create student form
- `admin-ai-config.png` — AI Config LLM categories
- `ai-config-slideover.png` — AI Config Configure slide-over
- `admin-prompts.png` — Prompts library
- `admin-usage.png` — AI Usage full page
- `admin-usage-policies.png` — Usage Policies list
- `admin-usage-analytics.png` — Usage Analytics
- `admin-lessons.png` — Lessons buffer settings
- `admin-curriculum.png` — Curriculum objectives
- `admin-exercise-types.png` — Exercise Types list
- `admin-notifications.png` — Notifications page
- `admin-integrations.png` — Integrations cards
- `admin-diagnostics.png` — Diagnostics system status
- `admin-security.png` — Security settings and auth events
- `admin-onboarding.png` — Onboarding flow steps
- `row-actions.png` — Student row actions menu open
- `onboarding-after-click.png` — Add step button non-response (before fix)

---

## Fix Applied

**File:** [sp-admin-button.component.ts](src/LinguaCoach.Web/src/app/design-system/admin/components/button/sp-admin-button.component.ts)

**Change:**
1. Added `EventEmitter` to imports: `import { Component, EventEmitter, Input, Output } from '@angular/core'`
2. Added `@Output() clicked = new EventEmitter<void>()` to component class
3. Added `(click)="!disabled && !loading && clicked.emit()"` to the inner `<button>` template

**Scope:** This fixes all 24 `(clicked)` bindings across 6 admin pages:
- `/admin/onboarding` — Add step, Edit step, Remove step, Save step, Cancel, Close slide-over, Activate flow
- `/admin/notifications` — Send notification and related actions
- `/admin/lessons` — Generate now, Save settings
- `/admin/diagnostics` — Refresh
- `/admin/security` — Refresh
- `/admin/integrations` — Configure, Test storage

---

## Validation

```
Production build:     PASS
Frontend unit tests:  1381/1381 PASS
```

Backend tests not run — only frontend changed.

---

## Production Data Changed

**No.** No production records created, modified, archived, or deleted.

## Email / SMS / Test Provider Actions Triggered

**No.** No email, SMS, or AI test provider actions were triggered.

---

## Remaining Production Issues

| Issue | Severity | Action Required |
|---|---|---|
| AI provider "Not configured" in diagnostics | P2 | **FIXED in Phase 11E-FINAL** — `DiagnosticsController` now reads DB category config |
| SpAdminTableComponent cell template ignored (Edit/Remove row actions empty) | P2 | **FIXED in Phase 11E-FINAL** — added `ContentChild('cell')` + `NgTemplateOutlet` |
| Welcome email not sent on create-student | P3 | Backend SMTP endpoint not wired |
| Aggregate lesson pool health endpoint | P3 | Backend endpoint not yet implemented |
| Student engagement heatmap | P3 | Backend endpoint not yet implemented |
| Webhook / Analytics / Admin API integrations | P3 | Backend not implemented |
| Background job history | P3 | Backend endpoint not yet implemented |

---

## Phase 11E-FINAL Summary

All P1/P2 issues resolved. See full final report: [2026-06-27-phase-11e-final-report.md](2026-06-27-phase-11e-final-report.md)
