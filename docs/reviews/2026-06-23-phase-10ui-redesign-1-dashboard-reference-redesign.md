# Phase 10UI-REDESIGN-1 Review: Admin Dashboard Reference Redesign

**Date:** 2026-06-23
**Sprint / feature:** Phase 10 UI redesign — admin reference alignment
**Baseline tests:** 1086/1086 (after REDESIGN-0)
**Final tests:** 1117/1117

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

---

## Files reviewed (read-only)

- `docs/design/speakpath/admin/pages/dashboard.jsx` — reference layout
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — confirmed AdminStats shape (3 fields only)
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — confirmed no time-series/cost/streak endpoints
- `src/LinguaCoach.Web/src/app/design-system/admin/index.ts` — confirmed SpAdminKpiCardComponent exported
- `src/LinguaCoach.Web/src/app/design-system/admin/components/kpi-card/sp-admin-kpi-card.component.ts` — inputs: label, variant

---

## Dashboard sections implemented

| Section | Data source | Status |
|---------|-------------|--------|
| Page header | Static | Real |
| Weekly snapshot hero banner | `stats.totalStudents`, `stats.onboardedStudents` | Partial real |
| Activities this week (hero slot) | No endpoint | Not implemented |
| Avg score (hero slot) | No endpoint | Not implemented |
| KPI icon tile row — Total students | `stats.totalStudents` | Real |
| KPI icon tile row — Onboarded | `stats.onboardedStudents` | Real |
| KPI icon tile row — Activities tracked | `stats.totalActivityAttempts` | Real |
| KPI icon tile row — AI provider | `listAiCategories()` live | Real |
| KPI icon tile row — AI cost (7 d) | No endpoint | Not implemented |
| Activity trends chart | No time-series endpoint | Not implemented |
| AI System card | `listAiCategories()` live | Real |
| Onboarding funnel | `stats` + `students` derived | Real |
| At-risk students | `students.onboardingStatus` derived | Partial real |
| CEFR distribution | `students.cefrLevel` derived | Real |
| Score distribution | No endpoint | Not implemented |
| AI spend by type | No per-category cost endpoint | Not implemented |
| Avg session duration | No endpoint | Not implemented |
| Streak leaderboard | No endpoint | Not implemented |
| Admin actions | Real router links + AI config status | Real |
| Cohort engagement | `students.lifecycleStage` derived | Partial real |
| Recent students table | `listStudents()` | Real |
| Live events feed | No endpoint | Not implemented |

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| Replace 4 `sp-admin-stat-card` tiles with `sp-admin-kpi-card` row (5 tiles) | Matches reference KpiCard row; `sp-admin-kpi-card` already exists and has icon slot |
| Add dark hero banner with inline CSS gradient | Reference has dark "WeeklySnapshot" banner; no design-system component exists for it; inline styles are acceptable per project convention |
| Derive onboarding funnel from students list, not a dedicated endpoint | No aggregate funnel endpoint exists; `onboardingStatus` field on each student is sufficient |
| Derive at-risk from `onboardingStatus === NotStarted | InProgress` | No aggregate at-risk endpoint; lifecycle/onboarding status is a reasonable proxy |
| Derive CEFR distribution from `cefrLevel` field | No distribution endpoint; grouping students by level produces accurate counts |
| Show "Not implemented" / "Backend not available yet" for all unavailable sections | Security constraint — no fake data |
| Keep AI System section from old dashboard | Real data, already tested; preserves FIX-5 behaviour |
| Update `admin-wrapper-migration.spec.ts` to use `sp-admin-kpi-card` selector | Old assertion queried `sp-admin-stat-card` which no longer appears in the KPI row |

---

## Tests produced

### admin-dashboard.component.spec.ts

22 existing tests preserved unchanged.

31 new tests added:

**Weekly snapshot hero banner (5):**
- Renders the hero banner section
- Shows onboarded count from real stats
- Shows total students count from real stats
- Shows "Not implemented" for activities this week slot
- Shows "Backend not available yet" for avg score slot

**KPI tile row (1):**
- Renders AI cost tile with "Not implemented"

**Activity trends placeholder (1):**
- Renders activity chart card with placeholder text

**Onboarding funnel (4):**
- Renders onboarding funnel section
- Shows not-started count derived from students
- Shows in-progress count derived from students
- Shows total students from stats in funnel

**At-risk students (4):**
- Renders at-risk card section
- Shows empty state when no at-risk students
- Identifies not-started students as at-risk
- Identifies in-progress students as at-risk
- Shows note about aggregate risk score not available

**CEFR distribution (4):**
- Renders CEFR distribution section
- Derives CEFR counts from students list
- Excludes students with no CEFR level
- Shows empty state when no CEFR data

**Placeholder cards (5):**
- Score distribution card with placeholder
- AI spend by type card with placeholder
- Avg session duration card with placeholder
- Streak leaderboard card with placeholder
- Live events feed card with placeholder

**Cohort engagement (4):**
- Renders cohort engagement section
- Derives course-ready count
- Derives placement-pending count
- Derives onboarding-pending count
- Shows note about activity-based rate not available

**No fake data (1):**
- Does not show "$" or "All clear"

### admin-wrapper-migration.spec.ts (1 updated)

- "dashboard renders KPI grid cards" — updated selector from `sp-admin-stat-card` to `sp-admin-kpi-card`

---

## Risks / unresolved questions

- The hero banner uses inline CSS `linear-gradient`. When a dark-mode token is added to the design system, the banner should be updated to use tokens.
- CEFR distribution bar widths are relative to the highest count, not absolute. If only one student has a CEFR level the bar shows 100% width — visually fine but not a true distribution.
- At-risk derivation is a proxy only. When a proper risk-score endpoint ships, this card should be replaced.
- Activity trends, score distribution, AI spend, streak, and live events feed are all placeholder. Backend endpoints for these are not in the current sprint.

---

## Final verdict

**Complete.** All 17 reference dashboard sections are rendered. 9 use real or derived backend data. 8 show explicit "Not implemented" / "Backend not available yet" placeholders. No fake production data. No heavy chart library added. 1117/1117 tests pass (31 new, 1 updated). Build clean.

## Next recommended action

Proceed to **10UI-REDESIGN-2** (students list + create-student pages).

---

## Documentation impact

- Docs reviewed: `docs/design/admin-reference-alignment.md`, `docs/sprints/current-sprint.md`, `TODOS.md`
- Docs updated: this review doc; `docs/design/admin-reference-alignment.md` (page status table); `docs/sprints/current-sprint.md`; `TODOS.md`
- Docs intentionally not updated: architecture docs — no architecture, API contract, or model changes in this phase
- Reason: frontend-only redesign with no backend changes
