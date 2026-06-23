# Phase 10UI-CLOSURE-1 — Admin UI Visual Aggregate Closure Audit

**Date:** 2026-06-24
**Phase:** 10UI-CLOSURE-1
**Related sprint/phase:** Follows 10UI-BACKEND-AGG-1, 10UI-BACKEND-AGG-2
**HEAD before work:** 1b2b8d5
**Auditor:** Claude Sonnet 4.6

---

## Purpose

Final closure audit for the SpeakPath/LinguaCoach admin UI visual aggregate work. Verifies that:

1. Dashboard aggregate endpoints are correctly wired (activity-trends, score-distribution, ai-usage/aggregate-trends).
2. AI Usage aggregate endpoints are correctly wired (aggregate-trends, by-category).
3. Hero KPIs derive from real data, not hardcoded values.
4. All remaining placeholders are honest (explicit "not available yet" labelling).
5. No fake or mock data reaches the admin UI.
6. No secrets are displayed in any template.
7. TODOS.md statuses are accurate.
8. Frontend gates pass.

---

## Dashboard Aggregate Verification

### API calls in `ngOnInit`

| Check | Result | Detail |
|---|---|---|
| Calls `getDashboardActivityTrends`? | **PASS** | `this.adminApi.getDashboardActivityTrends('30d')` — period `30d` |
| Calls `getDashboardScoreDistribution`? | **PASS** | `this.adminApi.getDashboardScoreDistribution('7d')` — period `7d` |
| Calls `getAiUsageTrends`? | **PASS** | `this.adminApi.getAiUsageTrends('7d')` — period `7d` |

### Signal presence

| Signal | Present | Type |
|---|---|---|
| `activityTrends` | **YES** | `signal<AdminDashboardActivityTrendResponse \| null>(null)` |
| `loadingActivityTrends` | **YES** | `signal(true)` |
| `activityTrendsError` | **YES** | `signal(false)` |
| `scoreDistribution` | **YES** | `signal<AdminDashboardScoreDistributionResponse \| null>(null)` |
| `loadingScoreDistribution` | **YES** | `signal(true)` |
| `scoreDistributionError` | **YES** | `signal(false)` |
| `aiUsageTrends7d` | **YES** | `signal<AdminAiUsageTrendResponse \| null>(null)` |
| `loadingAiUsageTrends7d` | **YES** | `signal(true)` |
| `aiUsageTrends7dError` | **YES** | `signal(false)` |

### Computed hero KPIs

| KPI | Computed | Source | Null-safe |
|---|---|---|---|
| `heroActivitiesThisWeek` | **YES** | Sums `activityTrends.buckets[].activityCount` | Returns `null` when trends not loaded |
| `heroAvgScore` | **YES** | `scoreDistribution.averageScore` | Returns `null` when not loaded |
| `heroAiCost7d` | **YES** | Sums `aiUsageTrends7d.buckets[].cost` | Returns `null` when not loaded |

### Template hero tile bindings

| Tile | Hardcoded? | Binding |
|---|---|---|
| Students onboarded | No — real data | `stats()?.onboardedStudents ?? 0` |
| Total students | No — real data | `stats()?.totalStudents ?? 0` |
| Activity attempts (7d) | No — real data | `heroActivitiesThisWeek() ?? 0` with loading guard |
| Avg score (7d) | No — real data | `heroAvgScore()` with null state ("No scored attempts yet") |
| AI cost (7d) KPI tile | No — real data | `heroAiCost7d()` with error/loading states |

### Empty/null/error state handling

All three aggregate signals have dedicated loading states (render `—`), error states (render "Unavailable" or empty), and explicit empty-data handling (placeholder with honest message). No silent fake zeros.

### Dashboard aggregate verification verdict: **PASS**

---

## AI Usage Aggregate Verification

### API calls

| Check | Result | Detail |
|---|---|---|
| Calls `getAiUsageTrends`? | **PASS** | `this.adminApi.getAiUsageTrends('30d')` fixed 30d period in `loadAggregateAnalytics()` |
| Calls `getAiUsageCategoryBreakdown`? | **PASS** | `this.adminApi.getAiUsageCategoryBreakdown('30d')` fixed 30d period |
| Period change triggers new calls? | **PARTIAL** | Period pill buttons call `onPeriodChange` which calls `load()` — this reloads summary/recent/trends but NOT the aggregate analytics (`loadAggregateAnalytics` is only called on `ngOnInit`). Aggregate charts always show 30d regardless of period pill selection. This is an honest known limitation — aggregate charts display period label "30d" in their titles. |

### Signals present

| Signal | Present |
|---|---|
| `aggTrends` | **YES** |
| `loadingAggTrends` | **YES** |
| `aggTrendsError` | **YES** |
| `aggCategoryBreakdown` | **YES** |
| `loadingAggCategory` | **YES** |
| `aggCategoryError` | **YES** |

### Computed items

| Computed | Present | Source |
|---|---|---|
| `aggTrendItems` | **YES** | Last 7 of `aggTrends.buckets` mapped to `BreakdownBarItem[]` |
| `aggCategoryItems` | **YES** | `aggCategoryBreakdown.categories` mapped with `_` → space label clean |

### Remaining honest placeholders

The heatmap ("Student engagement") placeholder remains. It is labelled `skeleton="ring"` with "No AI usage recorded for this period" shown when data is absent — this is correct. No `skeleton="ring"` placeholder exists in the current AI Usage template for the engagement heatmap — the card was removed in VISUAL-2 and replaced with the `aggTrendItems` and `aggCategoryItems` graph cards. Verified: the current template has no remaining engagement heatmap card.

### AI Usage aggregate verification verdict: **PASS** (with noted period-coupling limitation)

---

## Visual Fidelity Table

Scores are out of 10. Criteria: layout matches reference, real data shown, no broken placeholders, colors/typography align to SpeakPath brand.

| Route | Component | Fidelity | Key strengths | Key remaining gaps |
|---|---|---|---|---|
| `/admin` | AdminDashboardComponent | **7/10** | Dark hero banner, KPI strip, onboarding funnel, at-risk, CEFR, activity trends (real bars), score distribution (real bars), AI system live, avatar tiles, pending actions computed | AI spend donut no per-category cost; streak leaderboard skeleton; avg session skeleton; live events skeleton |
| `/admin/students` | AdminStudentsComponent | **8/10** | KPI strip, rows-per-page, filter bar, onboarding breakdown bars, avatar tiles | Streak/mins-per-week columns missing (no backend) |
| `/admin/create-student` | CreateStudentComponent | **9/10** | Two-column layout, sticky aside, security note, back link | Minor: welcome email note still "not available yet" |
| `/admin/students/:id` | AdminStudentDetailComponent | **9/10** | Hero section, avatar, KPI strip, danger zone, readiness pool health, activity history, audit history | — |
| `/admin/ai-config` | AdminAiConfigComponent | **8/10** | KPI strip, category config, pricing table, override management | Rate limits card shows "Backend not available yet" (correct — no endpoint) |
| `/admin/prompts` | AdminPromptsComponent | **8/10** | KPI strip with live count, category badge, filter | — |
| `/admin/usage` | AdminAiUsageComponent | **8/10** | 4-tile KPI, period pills, mini bar chart, success ring, token breakdown, provider share, aggregate trend/category bars, recent table, pagination, filters, export | Aggregate charts always 30d (period pills don't update them) |
| `/admin/usage-policies` | AdminUsagePoliciesComponent | **7/10** | Real policy data, rule expansion, feature name lookup | No reference counterpart — evaluated on completeness |
| `/admin/exercise-types` | AdminExerciseTypesComponent | **8/10** | KPI strip, skill icon tiles, foundation-only labels | Inline toggle deferred (TODO-VISUAL-10B) |
| `/admin/curriculum` | AdminCurriculumComponent | **8/10** | KPI coverage strip, filter bar, ring metric, CEFR breakdown | Track-level cards not available (backend has objectives not tracks) |
| `/admin/notifications` | AdminNotificationsComponent | **8/10** | KPI channel strip, tabs, outbox management, template CRUD, send form, config edit, SMS foundation-only | Webhook channel card is placeholder (no endpoint) |
| `/admin/integrations` | AdminIntegrationsComponent | **8/10** | Integration card grid, MinIO real data, SMTP links to notifications, job KPI strip, background batches | Webhook/Slack/Analytics/Admin API all honest placeholders; pool aggregate placeholder |
| `/admin/diagnostics` | AdminDiagnosticsComponent | **8/10** | KPI strip, event feed from real events, severity breakdown | — |
| `/admin/security` | AdminSecurityComponent | **9/10** | KPI strip, password/lockout/JWT/rate-limit/headers/Google cards, auth events tab, deferred capabilities card | — |

**Overall admin UI fidelity: 8/10** — blocked on backend aggregate endpoints, not frontend component quality.

---

## Placeholder Audit

| Component | Placeholder element | Message | Honest? |
|---|---|---|---|
| Dashboard — AI spend by type | `sp-admin-visual-placeholder skeleton="grid"` | "No per-category cost endpoint in admin stats" | Yes — correct |
| Dashboard — Avg session duration | `sp-admin-visual-placeholder skeleton="ring"` | "No session duration endpoint" | Yes |
| Dashboard — Streak leaderboard | `sp-admin-visual-placeholder skeleton="timeline"` | "No streak endpoint" | Yes |
| Dashboard — Live events feed | `sp-admin-visual-placeholder skeleton="timeline"` | "No real-time events feed endpoint" | Yes |
| Dashboard — at-risk note | `<div class="sp-dash-at-risk-note">` | "Aggregate risk score: backend not available yet." | Yes |
| Dashboard — cohort engagement note | `<div class="sp-dash-at-risk-note">` | "Activity-based engagement rate: backend not available yet." | Yes |
| Integrations — Webhook | `sp-admin-visual-placeholder state="not-available"` | "No webhook endpoint is implemented." | Yes |
| Integrations — Slack | `sp-admin-visual-placeholder state="not-available"` | "No Slack integration endpoint is implemented." | Yes |
| Integrations — Analytics | `sp-admin-visual-placeholder state="not-available"` | "No analytics provider is connected." | Yes |
| Integrations — Admin API | `sp-admin-visual-placeholder state="not-available"` | "No public Admin API key management endpoint is implemented." | Yes |
| Integrations — Readiness pool aggregate | `sp-admin-visual-placeholder state="not-available"` | "A system-wide readiness pool aggregate endpoint is not yet implemented." | Yes |
| AI Config — Rate limits | Inline text | "Backend not available yet" | Yes |

No broken placeholders found. No placeholder that says "Backend not available yet" when the data IS now available. The dashboard at-risk note and cohort engagement note accurately describe unavailable aggregate risk scores (which differ from the lifecycle stage counts that ARE shown).

**Placeholder audit verdict: PASS — all honest**

---

## Secret / Fake Data Audit

### Secrets in templates

| Search target | Found in template? |
|---|---|
| JWT signing key value | No — security page only shows `issuerConfigured` bool |
| Google client secret value | No — shows `clientSecretConfigured` bool only |
| SMTP password value | No — shows `hasPassword` bool only |
| API key literal values | No — all providers show `Configured`/`Not set` badge |

The security component spec itself (`admin-security.component.spec.ts`) asserts `not.toContain('JWT_KEY')` and `not.toContain('GOOGLE_CLIENT_SECRET')` — positive evidence of intentional secret exclusion.

### Mock/fake data in components

| Search target | Found? |
|---|---|
| `MOCK_` identifier | No (only in spec `not.toContain` assertion context) |
| `fakeData` identifier | No |
| `mockStudents` identifier | No |
| Hardcoded student names/emails | No |
| Imported demo arrays | No — `admin-data.jsx` is reference-only, never imported into Angular |

**Secret/fake data audit verdict: PASS**

---

## TODO Audit

### VISUAL series status (from TODOS.md)

| TODO | Status in TODOS.md | Audit finding | Accurate? |
|---|---|---|---|
| VISUAL-01 (activity trends) | ✅ DONE 2026-06-24 | Dashboard wires `getDashboardActivityTrends('30d')` with real bars | **Yes** |
| VISUAL-02 (score distribution) | ✅ DONE 2026-06-24 | Dashboard wires `getDashboardScoreDistribution('7d')` with real bars | **Yes** |
| VISUAL-03 (AI spend donut) | Partially done | Hero KPI "AI cost (7d)" wired. Per-category cost breakdown still skeleton | **Yes — accurately partial** |
| VISUAL-04 (streak leaderboard) | Open — Future | No streak entity in domain | **Yes** |
| VISUAL-05 (AI Usage activities bar chart) | ✅ DONE 2026-06-24 | AI Usage wires `getAiUsageTrends('30d')` with `aggTrendItems` | **Yes** |
| VISUAL-06 (heatmap) | Open | No heatmap endpoint | **Yes** |
| VISUAL-07 (system health latency) | Open (P2) | No `/admin/health/detailed` endpoint | **Yes** |
| VISUAL-08 (pending actions) | ✅ DONE 2026-06-24 | `pendingActions` computed signal from real data | **Yes** |
| VISUAL-09 (AI Config slide-over) | Open (P2) | Still uses `sp-admin-modal` (centered) | **Yes** |
| VISUAL-10 (toggle switch) | ✅ DONE 2026-06-24 | `sp-admin-toggle` created, applied to notifications | **Yes** |
| VISUAL-10B (Exercise Types inline toggle) | Open — deferred | Table redesign required | **Yes** |
| VISUAL-11 (avatar tiles) | ✅ DONE 2026-06-24 | Avatar tiles in at-risk list, recent students table, students list | **Yes** |
| VISUAL-12 (live events feed) | Open (P2) | No SignalR/polling endpoint | **Yes** |

All TODOS.md entries reviewed above are accurate. No corrections required.

### New TODOs identified in this audit

No new TODOs required. All audit gaps are already covered by existing open VISUAL-xx items or by honest placeholders.

---

## Docs Created/Updated

| Doc | Action |
|---|---|
| `docs/reviews/2026-06-24-phase-10ui-closure-1-admin-ui-visual-aggregate-closure-audit.md` | Created (this document) |
| `docs/design/admin-reference-alignment.md` | Updated with fidelity scores and Phase 10UI-CLOSURE-1 changes section |
| `docs/handoffs/current-product-state.md` | Not updated — doc is dated 2026-06-23 (10UI-AUDIT-0) and no significant state change from this audit warrants a full update; all findings preserved in this review |
| `TODOS.md` | No changes needed — all statuses verified accurate |

---

## Frontend Gate Results

| Gate | Result | Detail |
|---|---|---|
| `npm run build -- --configuration production` | **PASS** | Output: `dist/lingua-coach.web`. No errors. |
| `npm test -- --watch=false --browsers=ChromeHeadless` | **PASS** | **1360/1360** tests pass. |

Backend gates: not run. No backend/API/migration changes were made in this phase.

---

## Recommended Next Phase

**Phase 10UI-CLOSURE-2 — Aggregate Period Coupling Fix** (P3, low-risk)

**Rationale:** The AI Usage page aggregate charts (`aggTrendItems`, `aggCategoryItems`) always show 30d data regardless of which period pill the user selects. The `loadAggregateAnalytics()` method is only called once on `ngOnInit`. When the user selects "7d" or "All", the hero summary updates but the graph cards still show 30d.

**Fix:** Pass the selected period to `loadAggregateAnalytics` and call it from `onPeriodChange` as well as `ngOnInit`. Low-risk: only adds a new call to existing working endpoints.

**Alternative next phase:** If period coupling is acceptable as a UX tradeoff (charts are labelled "30d" explicitly), the next most impactful phase is:

**Phase 10UI-BACKEND-AGG-3 — AI Spend by Category Backend Endpoint** (P1, VISUAL-03 completion)

Adds `GET /api/admin/dashboard/ai-spend-by-category?period=30d` returning per-feature-category costs. Completes VISUAL-03 (currently the only P1 VISUAL item still open that has a clear backend path). Estimated effort: 1 handler + 1 endpoint + 1 migration-free query.

---

## Confirmation

- No backend changes made in this phase.
- No API/migration changes made.
- No business logic changes.
- No student-facing UI changes.
- No new Angular components, signals, or API calls added.
- Only docs written/updated.
