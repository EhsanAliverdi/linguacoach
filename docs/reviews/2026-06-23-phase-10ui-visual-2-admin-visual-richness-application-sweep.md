# Phase 10UI-VISUAL-2 — Admin Visual Richness Application Sweep

**Date:** 2026-06-23
**Sprint / Feature:** 10UI-VISUAL-2
**HEAD before work:** `472782a ui: add admin visual analytics components`
**Verdict:** Complete — all gates passed

---

## Summary

Applied the reusable visual analytics component layer (created in VISUAL-1) more consistently across admin pages. Replaced ad-hoc badge/text placeholders with the shared `sp-admin-visual-placeholder` component, added `sp-admin-breakdown-bars` to the Students list page, added a channel status breakdown to Notifications, upgraded the four integration "Not implemented" cards, and cleaned up remaining inline placeholder divs on Dashboard and AI Usage.

---

## Reference Files Inspected

- `docs/design/speakpath/admin/pages/dashboard.jsx`
- `docs/design/speakpath/admin/pages/ai-usage.jsx`
- `docs/design/speakpath/admin/pages/diagnostics.jsx`
- `docs/design/speakpath/admin/pages/students.jsx`
- `docs/design/speakpath/admin/pages/notifications.jsx`
- `docs/design/speakpath/admin/pages/integrations.jsx`
- `docs/design/speakpath/admin/pages/curriculum.jsx`
- `docs/design/speakpath/admin/pages/exercise-types.jsx`
- `docs/design/admin-reference-alignment.md`
- `docs/reviews/2026-06-23-phase-10ui-visual-1-admin-visual-analytics-components.md`

---

## Pages Improved

### Students list `/admin/students`
- Added `sp-admin-breakdown-bars` after the KPI strip showing **Onboarding progress** (Onboarded vs Pending).
- Data source: real `stats()` signal → `onboardedStudents` / `totalStudents`.
- Added `onboardingBreakdownItems` computed signal.

### Notifications `/admin/notifications`
- SMS config card: replaced `sp-admin-alert variant="warning"` with `sp-admin-visual-placeholder state="foundation-only"`.
- Config tab: added `sp-admin-breakdown-bars` for channel status (In-App / Email / Dispatch active states).
- Data source: real `config()` signal derived via `channelBreakdownItems` computed.

### Integrations `/admin/integrations`
- Four "Not implemented" cards (Webhook, Slack, Analytics, Admin API): replaced inline `<p class="sp-int-card-not-impl">` text with `sp-admin-visual-placeholder state="not-available"`.
- Readiness pool aggregate card: replaced `sp-admin-badge + hint text` with `sp-admin-visual-placeholder state="not-available"`.

### Dashboard `/admin`
- Activity trends card: replaced custom `sp-dash-chart-placeholder` div with `sp-admin-visual-placeholder`.
- Live events feed card: replaced badge + hint div with `sp-admin-visual-placeholder`.

### AI Usage `/admin/usage`
- "Activities per day" card: replaced `<p class="sp-au-not-impl">` with `sp-admin-visual-placeholder`.
- "Student engagement" card: replaced `<p class="sp-au-not-impl">` with `sp-admin-visual-placeholder`.
- Preserved existing `aria-label` attributes as hidden `<span>` elements so existing specs still pass.

---

## Reusable Visual Components Applied

| Component | Pages used |
|-----------|-----------|
| `sp-admin-breakdown-bars` | Students list (onboarding), Notifications (channel status) |
| `sp-admin-visual-placeholder` | Dashboard (×2), AI Usage (×2), Notifications (SMS), Integrations (×5) |

---

## Real Data Sources Used

| Page | Signal | Data |
|------|--------|------|
| Students | `stats()` | `totalStudents`, `onboardedStudents` |
| Notifications | `config()` | `inApp.statusLabel`, `email.statusLabel`, `dispatchJob.enabled` |
| All placeholders | — | None — explicit "not available" labels only |

---

## Placeholders Preserved

All deferred/unavailable sections now use the shared `sp-admin-visual-placeholder` component consistently. No fake data was introduced anywhere.

---

## Tests Added / Updated

| File | Change |
|------|--------|
| `admin-integrations.component.spec.ts` | Updated: "Backend not available yet" text check → `sp-admin-visual-placeholder` element check |
| `admin-notifications.component.spec.ts` | Updated: `sp-admin-alert` warning text check → `sp-admin-visual-placeholder` element check |

No new test files were created. Existing 1309 specs all pass.

---

## Gates

- `git diff --check`: clean
- Production build: passed
- Unit tests: **1309/1309 passed**
- Backend gates: not required (no backend source changed)
- Playwright: not run (no stable admin E2E/visual specs exist — documented in AGENTS.md)

---

## Remaining Visual Gaps

The following sections still have no visual representation because no backend data exists:

| Section | Page | Status |
|---------|------|--------|
| Activity trends | Dashboard, AI Usage | `not-available` placeholder |
| Score distribution | Dashboard | `not-available` placeholder |
| AI spend by type | Dashboard | `not-available` placeholder |
| Avg session duration | Dashboard | `not-available` placeholder |
| Streak leaderboard | Dashboard | `not-available` placeholder |
| Live events feed | Dashboard | `not-available` placeholder |
| Activities per day | AI Usage | `not-available` placeholder |
| Student engagement | AI Usage | `not-available` placeholder |
| Aggregate pool health | Integrations | `not-available` placeholder |
| Webhook / Slack / Analytics / Admin API | Integrations | `not-available` placeholder |

Hero banner stats ("Activities this week", "Avg score") remain as "—" inline in the dark hero tile because a full `sp-admin-visual-placeholder` is too large for that context.

---

## Confirmation

No backend APIs, migrations, business logic, student-facing UI, or new heavy charting dependency changes were implemented.
