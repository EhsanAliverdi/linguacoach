# Phase 10UI-REDESIGN-0: Admin Reference Redesign Rollout Plan

**Date:** 2026-06-23
**HEAD before work:** a079cfc
**Related sprint:** Phase 10 UI — Admin Reference Alignment
**Reference source:** `docs/design/speakpath/admin/`

---

## Purpose

This document is the route-by-route redesign rollout plan for the entire admin UI.
Earlier phases updated shell, tokens, and reusable components.
Most pages still do not match the SpeakPath reference design at the page layout level.
This plan governs the controlled rollout of page-level structural changes, phase by phase.

---

## Routes inspected

- `/admin` — AdminDashboardComponent (inline template)
- `/admin/students` — AdminStudentsComponent (inline template)
- `/admin/students/:id` — AdminStudentDetailComponent (inline template)
- `/admin/create-student` — CreateStudentComponent (separate HTML)
- `/admin/ai-config` — AdminAiConfigComponent (inline template)
- `/admin/prompts` — AdminPromptsComponent (inline template)
- `/admin/usage` — AdminAiUsageComponent (separate HTML)
- `/admin/usage-policies` — AdminUsagePoliciesComponent (separate HTML)
- `/admin/exercise-types` — AdminExerciseTypesComponent (inline template)
- `/admin/curriculum` — AdminCurriculumComponent (inline template)
- `/admin/notifications` — AdminNotificationsComponent (separate HTML)
- `/admin/integrations` — AdminIntegrationsComponent (separate HTML)
- `/admin/diagnostics` — AdminDiagnosticsComponent (separate HTML)
- `/admin/security` — AdminSecurityComponent (separate HTML)
- `/admin/careers` — redirects to `/admin/curriculum` (resolved)

## Reference files inspected

- `docs/design/speakpath/admin/pages/dashboard.jsx`
- `docs/design/speakpath/admin/pages/students.jsx`
- `docs/design/speakpath/admin/pages/create-student.jsx`
- `docs/design/speakpath/admin/pages/ai-config.jsx`
- `docs/design/speakpath/admin/pages/ai-usage.jsx`
- `docs/design/speakpath/admin/pages/prompts.jsx`
- `docs/design/speakpath/admin/pages/curriculum.jsx`
- `docs/design/speakpath/admin/pages/exercise-types.jsx`
- `docs/design/speakpath/admin/pages/notifications.jsx`
- `docs/design/speakpath/admin/pages/integrations.jsx`
- `docs/design/speakpath/admin/pages/diagnostics.jsx`
- `docs/design/admin-reference-alignment.md`

---

## 1. Page redesign matrix

| Route | Current status | Reference file | Required page-level layout changes | Missing reusable components | Backend data available? | Placeholder needed? | Priority | Recommended phase |
|-------|---------------|----------------|------------------------------------|-----------------------------|------------------------|---------------------|----------|-------------------|
| `/admin` | Old layout | dashboard.jsx | Dark hero weekly-snapshot banner; 4-col KPI strip with icon tiles; activity area chart + system health 2-col; onboarding funnel card; at-risk students card; score distribution bar chart; AI cost donut; session duration bar chart; streak leaderboard; admin actions card; cohort engagement metric strip; students mini-table + live events feed | Dark hero banner, area chart, bar chart, donut chart, score dist bars, streak leaderboard, at-risk card, live events feed, cohort engagement strip | Yes (students, stats, AI categories) | Yes — charts, live feed, at-risk, score dist, cost donut (no endpoint) | P1 | 10UI-REDESIGN-1 |
| `/admin/students` | Looks close | students.jsx | Lifecycle filter dropdown; "Show archived" toggle; rows-per-page selector; sortable column headers with sort icons; action menu (⋯) per row | Sort icon widget, action menu dropdown | Yes (full paged list) | No | P2 | 10UI-REDESIGN-2 |
| `/admin/students/:id` | Partial | students.jsx (detail) | Hero avatar section with coloured avatar, name, email, badge row, and action buttons; Settings tab with danger zone (reset password, reset data, archive) | Danger zone card | Yes (profile, CEFR, readiness pool, activity history) | No | P2 | 10UI-REDESIGN-3 |
| `/admin/create-student` | Old layout | create-student.jsx | Two-column layout: left = multi-section form (credentials, profile, learning context, admin settings); right = sticky "What happens next" summary panel + cohort stats panel; gradient submit button; form section cards with title/sub | Form section card, sticky summary panel, cohort stats card | Yes (create API, stats) | No (cohort stats need `/api/admin/stats`) | P2 | 10UI-REDESIGN-2 |
| `/admin/ai-config` | Partial | ai-config.jsx | Provider credentials list card (connected count badge, per-provider rows with status + Configure/Update button); LLM categories table card with column headers (Category, Code, Provider, Model, Status) and Configure button per row; TTS settings 3-col grid; Rate limits / quotas 3-col progress bars; slide-in drawers for provider and category config | Rate limits / quotas progress bar widget; TTS section card | Yes (categories, providers, pricing) | No | P2 | 10UI-REDESIGN-5 |
| `/admin/prompts` | Looks close | prompts.jsx | Tab bar + search on same row; prompt rows with preview text + meta; prompt count footer; modal editor | Tab+search row layout tweak, prompt row with inline Preview/Edit/Delete buttons | Yes (prompts API) | No | P3 | 10UI-REDESIGN-5 |
| `/admin/usage` | Looks close | ai-usage.jsx | 3-col KPI strip with icon tiles; cost area chart with range pill selector; activities bar chart; student engagement heatmap (7×N grid); date range pills in page header | Area chart, bar chart, heatmap widget, range pills | Yes (usage summary, per-feature, per-model, per-student) | Yes — charts (no chart lib yet) | P2 | 10UI-REDESIGN-6 |
| `/admin/usage-policies` | Looks close | (no reference) | No reference counterpart; current page is governance extension — keep as-is | None | Yes | No | P3 | 10UI-REDESIGN-6 |
| `/admin/exercise-types` | Partial | exercise-types.jsx | Exercise type cards with coloured icon tile, name, Coming Soon badge, toggle, Configure button, and expanded stat grid (total, avg time, avg score) when enabled | Expandable exercise type card with stat grid | Yes (exercise types API) | Yes — avg time, avg score (not in backend) | P2 | 10UI-REDESIGN-4 |
| `/admin/curriculum` | Partial | curriculum.jsx | Track cards with coloured icon tile, track name, Coming Soon badge, exercise count badge, Manage button; list of full domain tracks not objectives table | Track card layout; Coming Soon badge per track | Partial (objectives exist; tracks as domain concept not yet in backend) | Yes — track cards are reference UI; backend has objectives table | P2 | 10UI-REDESIGN-4 |
| `/admin/notifications` | Looks close | notifications.jsx | Reference has email + webhook channels (2-col cards) with toggle; notification triggers table; recently sent table. Angular has all these plus tabs (config/send/history) and SMS warning label (added in FIX-8). No webhook channel card; recently sent is in history tab | Webhook channel card | Partial (email config exists; webhook not implemented) | Yes — webhook card with "Not implemented" | P3 | 10UI-REDESIGN-7 |
| `/admin/integrations` | Old layout | integrations.jsx | Reference has integration card grid (SMTP, Webhook, Slack, Analytics) each with icon, name, status badge, description, meta URL, Manage/Connect buttons; Admin API card with key display and copy/rotate. Angular has MinIO storage, generation settings, background jobs, readiness pool placeholder — completely different domain from reference | Integration card grid widget; Admin API card | Partial (MinIO, generation, batches exist; Slack/Analytics/webhook not implemented) | Yes — Slack, Analytics, Admin API sections all need "Not implemented" state | P1 | 10UI-REDESIGN-7 |
| `/admin/diagnostics` | Looks close | diagnostics.jsx | Reference uses outlined border card with indigo border. Angular uses `sp-admin-card`. Minor visual gap only; filter layout is close. Auto-refresh, level/category/correlation/search filters present. Structurally aligned. | None new needed | Yes | No | P3 | 10UI-REDESIGN-8 |
| `/admin/security` | Looks close | (no reference) | No reference counterpart; governance extension. Deferred capabilities card added in FIX-8. Structurally adequate. | None | Yes | No | P3 | 10UI-REDESIGN-8 |

### Status key

- **Looks close** — Structure and major sections match reference. Minor visual polish only.
- **Partial** — Some sections match; key sections missing or structurally different.
- **Old layout** — Core page structure does not match reference. Needs page-level redesign.

---

## 2. Component gap matrix

| Needed component/widget | Reference usage | Current Angular equivalent | Gap | Reusable? | Recommended phase |
|-------------------------|----------------|---------------------------|-----|-----------|-------------------|
| Dark hero weekly-snapshot banner | Dashboard `WeeklySnapshot` — dark gradient 4-col strip with white text | None | Missing entirely | Yes — `sp-admin-weekly-snapshot` or inline | 10UI-REDESIGN-1 |
| KPI card with icon tile (left accent) | Dashboard `KpiCard`, AI Usage `UsageKpi` | `sp-admin-stat-card` (no left icon tile) | `sp-admin-stat-card` is a number card; reference has icon column + label/value/delta layout | Yes — extend `sp-admin-stat-card` or add `sp-admin-kpi-tile` variant | 10UI-REDESIGN-1 |
| Area chart (SVG inline) | Dashboard activities chart, AI Usage cost chart | None | No chart rendering in Angular | Yes — `sp-admin-area-chart` or native SVG inline | 10UI-REDESIGN-1 |
| Bar chart (SVG inline) | Dashboard session duration, AI Usage activities per day | None | No chart rendering | Yes — `sp-admin-bar-chart` | 10UI-REDESIGN-1 |
| Heatmap (SVG inline) | AI Usage student engagement | None | No heatmap rendering | Yes — `sp-admin-heatmap` | 10UI-REDESIGN-6 |
| Score distribution bars | Dashboard `ScoreDistribution` | None | Missing | Yes — `sp-admin-score-distribution` | 10UI-REDESIGN-1 |
| Donut / cost breakdown placeholder | Dashboard `CostByType`, `DonutChart` | None | Missing; no cost-by-type endpoint either | Yes (with placeholder state) — `sp-admin-donut-chart` | 10UI-REDESIGN-1 |
| At-risk students card | Dashboard `AtRiskAlerts` | None (dashboard only shows recent students table) | Missing entirely; backend has at-risk concept via stats | Yes — `sp-admin-at-risk-card` or inline section | 10UI-REDESIGN-1 |
| Onboarding funnel card | Dashboard `OnboardingFunnel` | None | Missing; backend has onboarding stats | Inline section using `sp-admin-card` | 10UI-REDESIGN-1 |
| Streak leaderboard card | Dashboard `StreakLeaderboard` | None | Missing; no streak endpoint currently | Inline with "Not implemented" placeholder | 10UI-REDESIGN-1 |
| Live events feed | Dashboard `LiveFeed` | None | Missing; no live event endpoint | Inline with "Not implemented" placeholder | 10UI-REDESIGN-1 |
| Admin actions / pending actions card | Dashboard `PendingActions` | `sp-admin-action-card` grid (quick actions) | Reference is a dynamic list with urgent/clear state; current is static 4-icon grid | Yes — extend or replace `sp-admin-action-card` | 10UI-REDESIGN-1 |
| Cohort engagement strip (3-col) | Dashboard `MetricStrip` | None | Missing entirely; needs engagement %, AI spend sparkline, CEFR distribution | Inline `sp-admin-card` trio | 10UI-REDESIGN-1 |
| Integration card grid (icon, name, badge, meta, CTA) | Integrations `IntegrationCard` | None (integrations page is MinIO/jobs domain) | Missing; reference integration cards are a different concept from Angular's MinIO focus | Yes — `sp-admin-integration-card` | 10UI-REDESIGN-7 |
| Admin API card (key display, copy, rotate) | Integrations `AdminIntegrations` | None | Missing; no admin API key endpoint | Inline with "Not implemented" | 10UI-REDESIGN-7 |
| Form section card (title + subtitle header) | Create Student `FormSection` | `sp-admin-card` (no subtitle) | `sp-admin-card` has `title` but no sub-description line | Extend `sp-admin-card` with `subtitle` input, or use heading inside card | 10UI-REDESIGN-2 |
| Sticky summary / "what happens next" panel | Create Student right column | None | Missing; create-student page is single-column flat form | Inline sticky right panel | 10UI-REDESIGN-2 |
| Sortable column headers with sort icons | Students list `SortIcon` | `sp-admin-table` (no built-in sort header) | Sort icons not rendered; table is sortable via API filters but no visual sort state | Extend `sp-admin-table` or add sort icon helper | 10UI-REDESIGN-2 |
| Action menu (⋯ dropdown) per row | Students list `ActionMenu` | None | Row-level action menu not implemented | Yes — `sp-admin-row-action-menu` or inline | 10UI-REDESIGN-2 |
| Danger zone card | Student detail settings tab | None | Missing; reset password / archive exist as buttons but no danger zone section | Yes — `sp-admin-danger-zone-card` | 10UI-REDESIGN-3 |
| Hero avatar + badge row + action buttons | Student detail `adm-detail-hero` | Partial — `sp-admin-page-header` used, no coloured avatar | Avatar colour, initials, badge row layout missing from current header | Extend `sp-admin-page-header` or use inline hero section | 10UI-REDESIGN-3 |
| Rate limits / quotas progress bar widget | AI Config `Rate limits & quotas` | None | Missing; no rate limit usage endpoint exists | Inline with "Not implemented" | 10UI-REDESIGN-5 |
| TTS settings card | AI Config `Text-to-Speech` section | TTS exists in AI Config as part of category config | TTS is a category row, not a dedicated settings section | Inline `sp-admin-card` section | 10UI-REDESIGN-5 |
| Exercise type card with stat grid | Exercise Types `adm-card` with expanded stats | `sp-admin-table` rows | Reference uses card-per-type with expandable stat grid; Angular uses a table | Yes — `sp-admin-exercise-type-card` | 10UI-REDESIGN-4 |
| Curriculum track card | Curriculum `adm-card` per track | `sp-admin-table` rows for objectives | Reference shows content tracks as icon cards; Angular manages curriculum objectives table (different domain layer) | Yes — `sp-admin-track-card` | 10UI-REDESIGN-4 |
| Date range pills | AI Usage page header pills | `sp-admin-filter-bar` with `sp-admin-select` | Reference has pill toggle buttons (7d / 30d / 90d) in header; Angular uses a select dropdown | Yes — `sp-admin-date-range-pills` or `sp-admin-pill-group` | 10UI-REDESIGN-6 |

---

## 3. Route grouping plan

### 10UI-REDESIGN-1 — Dashboard reference redesign

**Routes:** `/admin`

**Goal:** Match the reference dashboard layout:
- Dark hero weekly-snapshot banner (real data: activities this week, engagement %, avg score, action-needed count)
- 4-col KPI icon-tile strip (total students, active this week, activities done, AI cost 7d)
- Activities area chart + system health 2-col (area chart = placeholder; system health = real diagnostics data)
- Onboarding funnel card (real onboarding stats from `/api/admin/stats`)
- At-risk students card (real — students with 0 activity this week)
- Score distribution bars (placeholder — no per-score endpoint)
- AI cost donut (placeholder — no cost-by-type endpoint)
- Session duration bar chart (placeholder)
- Streak leaderboard (placeholder)
- Admin actions card (real — dynamic urgent count from stats)
- Cohort engagement metric strip (real engagement %, placeholder sparkline, real CEFR distribution)
- Students mini-table + live events feed (students = real; live feed = placeholder)

**New components needed:** dark hero strip, KPI icon tile variant, inline SVG area/bar chart stubs, onboarding funnel section, at-risk section, cohort metric strip, score dist bars (placeholder), donut (placeholder), streak (placeholder), live feed (placeholder)

**Estimated size:** Large. Split into 1a (KPI + hero + onboarding funnel + at-risk) and 1b (charts + lower panels) if needed.

---

### 10UI-REDESIGN-2 — Students list + Create Student reference redesign

**Routes:** `/admin/students`, `/admin/create-student`

**Goal:**
- Students list: add lifecycle filter, "Show archived" toggle, rows-per-page selector, sortable column sort icons, per-row action menu (⋯) with View/Edit/Reset password/Reset data/Archive
- Create student: two-column layout with sticky "What happens next" summary panel on the right; multi-section form cards (Account credentials, Student profile, Learning context, Admin settings); cohort stats panel in sidebar showing real `/api/admin/stats`

**New components needed:** sort icon helper, row action menu, form section card (card with title+sub), sticky right panel

---

### 10UI-REDESIGN-3 — Student detail completion and activity / readiness polish

**Routes:** `/admin/students/:id`

**Goal:**
- Hero section: coloured avatar (initial-based), name, email, badge row (lifecycle, CEFR, onboarding), action buttons (Edit, Reset password, Archive)
- Settings tab: add danger zone card (Reset password / Reset data / Archive with confirmation)
- Readiness pool and activity history already added in FIX-7 — verify alignment

**New components needed:** `sp-admin-danger-zone-card`, hero avatar section

---

### 10UI-REDESIGN-4 — Curriculum + Exercise Types reference redesign

**Routes:** `/admin/curriculum`, `/admin/exercise-types`

**Goal:**
- Curriculum: add track-level card view alongside the existing objectives table. Reference shows named content tracks (Professional Communication, Career Vocabulary, etc.) as icon cards. Backend currently manages curriculum objectives, not tracks. Show a static tracks overview card with "Backend not available yet" state for track management; keep the objectives table.
- Exercise types: change from table rows to card-per-type layout with coloured icon tile, exercise count badge, toggle, Configure button, and expanded stat grid (total exercises, avg completion time, avg score). Avg time and avg score are not in backend — show "Not available" cells.

**New components needed:** `sp-admin-track-card` (or inline), `sp-admin-exercise-type-card` (or inline expandable card)

---

### 10UI-REDESIGN-5 — AI Config + Prompts reference redesign

**Routes:** `/admin/ai-config`, `/admin/prompts`

**Goal:**
- AI Config: add Rate limits & quotas 3-col progress bar section (placeholder — no endpoint); add TTS settings card as a dedicated section (currently TTS is just one of the category rows); tighten provider credentials list card to match reference layout (icon column, name, masked key, Connected/Not set badge, Update/Configure button per row)
- Prompts: tab bar + search on same row instead of stacked; prompt row layout with inline Preview/Edit/Delete action buttons and preview text + updated timestamp; total count footer

**New components needed:** rate quota progress bar widget, TTS dedicated section card

---

### 10UI-REDESIGN-6 — AI Usage + Usage Policies reference redesign

**Routes:** `/admin/usage`, `/admin/usage-policies`

**Goal:**
- AI Usage: replace `sp-admin-select` period filter with date range pills (7d / 30d / 90d) in page header; add SVG area chart for cost over time (placeholder with real data points); add bar chart for activities per day (placeholder); add engagement heatmap (placeholder — no day-of-week data endpoint); 3-col KPI strip already exists but upgrade to icon-tile layout
- Usage Policies: no reference counterpart — keep as-is, confirm wrapper completeness

**New components needed:** `sp-admin-date-range-pills`, SVG area chart component, SVG bar chart component, SVG heatmap component

---

### 10UI-REDESIGN-7 — Notifications + Integrations reference redesign

**Routes:** `/admin/notifications`, `/admin/integrations`

**Goal:**
- Notifications: add webhook channel card (2-col channels section: email + webhook). Webhook config is not yet implemented — show "Not implemented" card with connect placeholder. Notification triggers and history tabs already close to reference.
- Integrations: the current Angular integrations page (MinIO, generation jobs, readiness pool) is a different domain from the reference integrations page (SMTP, Webhook, Slack, Analytics, Admin API). The solution is to add a new top section — "Platform integrations" — with integration cards for: SMTP Email (real), Webhook (placeholder/"Not implemented"), Slack (placeholder), Analytics (placeholder). Keep the existing MinIO/jobs sections as "System infrastructure." Add an Admin API card (placeholder — no key endpoint yet).

**New components needed:** `sp-admin-integration-card`, Admin API card section

---

### 10UI-REDESIGN-8 — Diagnostics + Security reference redesign

**Routes:** `/admin/diagnostics`, `/admin/security`

**Goal:**
- Diagnostics: structurally close. Minor: reference uses a visible indigo-border outlined card style for both sections. Angular uses `sp-admin-card`. Consider adding a `variant="outlined"` or `variant="section"` option to `sp-admin-card` (already partially present with `variant="section"`). Auto-refresh, all filters, log terminal display all match.
- Security: no reference counterpart. Deferred capabilities card added in FIX-8. Auth events tab functional. Minor cleanup only.

**New components needed:** possibly `sp-admin-card variant="outlined"` — check if already supported

---

### 10UI-REDESIGN-FINAL — Admin UI reference alignment closure audit

**Routes:** All admin routes

**Goal:** Final pass audit. Verify every route against the reference. Check token/colour consistency. Check ARIA labels. Remove any remaining Tailwind one-off classes. Update `docs/design/admin-reference-alignment.md` status table to reflect all changes.

---

## 4. Priority summary

### P0 — Misleading / stale / hiding critical capability

None remaining. The `/admin/careers` orphan was resolved (redirects to curriculum) in a prior phase.

### P1 — Major page does not match reference or hides important backend capability

| Route | Gap |
|-------|-----|
| `/admin` | Dashboard shows 4 static stat cards and a quick actions grid. Reference has 15+ distinct sections using real engagement, at-risk, onboarding funnel, cost, score, and activity data that the backend already provides. The current dashboard significantly under-represents what the system knows. |
| `/admin/integrations` | Current page is MinIO / generation jobs. Reference integrations concept (SMTP, Webhook, Slack, Analytics, Admin API) is not surfaced at all. An admin visiting this page cannot discover that email (SMTP) is configured or that a webhook channel exists in notifications. |

### P2 — Useful alignment / polish

Students list (sort, filters, action menu), Create Student (2-col + summary panel), Student detail (hero, danger zone), AI Config (rate limits, TTS section), AI Usage (charts, date pills), Exercise Types (cards), Curriculum (track cards).

### P3 — Future polish

Prompts (minor layout tweaks), Notifications (webhook channel card), Diagnostics (border style), Security (no reference counterpart — keep current).

---

## 5. First implementation recommendation

### Next phase: 10UI-REDESIGN-1 — Dashboard reference redesign

**Reason:** The dashboard is the most impactful page gap. It is the first thing an admin sees. It currently shows 4 stat cards and a quick-actions grid — the reference design has 15 distinct data sections. The backend already provides students list, onboarding stats, AI category config, and system status data. Multiple chart/widget sections can be rendered with real data or honest "Not implemented" placeholders. This is the highest-value single page change.

**Recommended scope for 10UI-REDESIGN-1:**

Phase 1a (structural, no charts):
1. Replace quick-actions grid with dynamic admin actions card (shows urgent count from stats)
2. Add dark hero weekly-snapshot banner (real: activities this week from stats, engagement %, action-needed count; placeholder: avg score until endpoint available)
3. Upgrade 4-col KPI strip to icon-tile layout (real: total students, onboarded, activities tracked; add active-this-week if available from stats)
4. Add onboarding funnel card (real data: onboarded, CEFR placed, active learners from stats endpoint)
5. Add at-risk students card (real data: students list filtered for 0 activity)

Phase 1b (chart stubs and lower panels):
6. Add activities area chart placeholder (show "Chart not available — no time series endpoint" clearly)
7. Add system health card (real data from diagnostics endpoint)
8. Add cohort engagement metric strip (real: engagement %, CEFR distribution; placeholder: AI spend sparkline)
9. Add score distribution card (placeholder)
10. Add live events feed (placeholder)

---

## 6. Tiny fixes applied in this phase

None. This phase is planning only. No Angular source files were modified.

---

## 7. Documents created / updated

| Doc | Action |
|-----|--------|
| `docs/reviews/2026-06-23-phase-10ui-redesign-0-admin-reference-redesign-rollout-plan.md` | Created (this file) |
| `docs/design/admin-reference-alignment.md` | To be updated (see below) |
| `TODOS.md` | To be updated |
| `docs/sprints/current-sprint.md` | To be updated |

---

## 8. Frontend gate status

No Angular source changes in this phase. No build or test run required.

`git diff --check` — clean.

---

## 9. Backend gate status

No backend changes. Not required.

---

## 10. Confirmation

- No backend / API / migration changes implemented.
- No student UI changes.
- No full all-pages redesign implemented.
- No fake production data added.
- No dashboard-only scope; full route-by-route plan covers all 14 admin routes.
- Tiny cleanup: none needed.

---

## Risks / unresolved questions

1. Chart rendering: the reference uses inline SVG with manual bezier curves. Angular can do the same with pure SVG — no chart library needed. Charts with no data endpoint should show a clear placeholder rather than being omitted entirely.
2. Dashboard backend data gaps: "activities this week" and "avg score" are not currently in `/api/admin/stats`. These will need either a stats endpoint extension or a "Not available" placeholder.
3. Integrations domain mismatch: the reference integrations page describes third-party connections (Slack, analytics, webhook). The Angular integrations page describes system infrastructure (MinIO, generation jobs). These serve different admin needs and should coexist on the same page, not replace each other.
4. Component count: 10UI-REDESIGN-1 (dashboard) is the largest single phase. If it exceeds a comfortable implementation size, split into 1a and 1b as noted above.
