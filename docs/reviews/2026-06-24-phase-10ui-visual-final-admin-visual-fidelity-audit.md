# Phase 10UI-VISUAL-FINAL — Admin Visual Fidelity Audit

**Date:** 2026-06-24
**Sprint / Feature:** 10UI-VISUAL-FINAL
**HEAD before work:** `5d08ac8 ui: enrich admin graph cards and visual density`
**Verdict:** Audit complete. Tiny polish fixes applied. Next phases recommended.

---

## Purpose

Compare the current Angular admin implementation against the SpeakPath reference design (docs/design/speakpath/admin/) and produce a precise visual gap report. Apply tiny safe polish fixes only. No new backend APIs, no broad redesign.

---

## Reference Files Inspected

- `docs/design/speakpath/admin/shell.jsx`
- `docs/design/speakpath/admin/pages/dashboard.jsx`
- `docs/design/speakpath/admin/pages/students.jsx`
- `docs/design/speakpath/admin/pages/ai-usage.jsx`
- `docs/design/speakpath/admin/pages/diagnostics.jsx`
- `docs/design/speakpath/admin/pages/curriculum.jsx`
- `docs/design/speakpath/admin/pages/exercise-types.jsx`
- `docs/design/speakpath/admin/pages/notifications.jsx`
- `docs/design/speakpath/admin/pages/integrations.jsx`
- `docs/design/admin-reference-alignment.md`
- All prior redesign and visual phase review docs (10UI-REDESIGN-0 through VISUAL-3)

---

## Screenshot Capture

Screenshot capture was not performed. The dev server was not started as part of this audit phase. Visual inspection was performed by reading Angular templates and styles against reference JSX source. A screenshot regression harness (Playwright) is recommended as a follow-up phase (10UI-VISUAL-E2E-1).

---

## Visual Gap Matrix

| Area | Reference expectation | Current implementation | Gap type | Severity | Recommended fix |
|------|-----------------------|------------------------|----------|----------|-----------------|
| Dashboard hero gradient | Warm near-black `#211B36 → #2D2455` | Cool indigo `#1e1b4b → #3730a3` (before fix) | colour/tokens | P2 | **FIXED** — gradient updated to warm-purple |
| Dashboard hero label colour | `rgba(255,255,255,.45)` | `#a5b4fc` (cool indigo) | colour/tokens | P2 | **FIXED** — changed to `rgba(255,255,255,.45)` |
| Dashboard hero key subtext | `rgba(255,255,255,.5)` | `#c7d2fe` (cool indigo) | colour/tokens | P2 | **FIXED** — changed to `rgba(255,255,255,.5)` |
| Dashboard hero placeholder values | Dim warm opacity | `#818cf8` (cool indigo) | colour/tokens | P2 | **FIXED** — changed to `rgba(255,255,255,.35)` |
| Dashboard hero "Not implemented" label | Consistent "Backend not available yet" | Mixed labels ("Not implemented" vs "Backend not available yet") | typography | P3 | **FIXED** — unified to "Backend not available yet" |
| Dashboard onboarding funnel stages | 4: Signed up / Onboarded / CEFR placed / Active | 3: Completed / In progress / Not started | layout composition | P2 | **FIXED** — added CEFR placed stage, renamed labels |
| Dashboard at-risk list avatars | Coloured initial circle per student | Name text only, no avatar | component styling | P2 | **FIXED** — avatar tiles added |
| Dashboard students table avatars | Coloured initial circle | No avatar | component styling | P2 | **FIXED** — avatar tiles added to email cell |
| Dashboard activity chart | Full bezier SVG area chart, 14-day, hover | `skeleton="chart"` placeholder | chart/widget missing | P1 | Needs `/admin/stats/activities-per-day` — TODO-VISUAL-01 |
| Dashboard score distribution | 5-bin coloured bar chart | `skeleton="chart"` placeholder | chart/widget missing | P1 | Needs score distribution endpoint — TODO-VISUAL-02 |
| Dashboard AI spend donut | SVG donut + category legend | `skeleton="grid"` placeholder | chart/widget missing | P1 | Partially buildable from `byFeature[]` — TODO-VISUAL-03 |
| Dashboard streak leaderboard | Named list with streak counts | `skeleton="timeline"` placeholder | chart/widget missing | P1 | Needs streak endpoint — TODO-VISUAL-04 |
| Dashboard system health card | Per-service latency bars | AI category config status list | layout composition | P2 | Needs health endpoint — TODO-VISUAL-07 |
| Dashboard pending actions | 5 contextual urgent items (computed) | 4 hardcoded navigation link cards | layout composition | P2 | Buildable now from loaded signals — TODO-VISUAL-08 |
| Dashboard metric strip per-student bars | Per-student activity colour bar strip | Lifecycle count rows (text only) | visual density | P2 | Buildable from loaded `students()` — TODO-VISUAL-08 |
| Dashboard live events feed | Real-time 8-item event timeline | `skeleton="timeline"` placeholder | chart/widget missing | P2 | Needs realtime events endpoint — TODO-VISUAL-12 |
| Students list Streak column | Streak count + emoji | Missing — replaced by Profile column | layout composition | P2 | Wire when backend exposes `streak` |
| Students list Mins/wk column | Minutes active this week | Missing | backend capability missing | P2 | Wire when activity minutes endpoint available |
| Students list avatar in rows | Circular coloured initials | No avatar (name only) | component styling | P3 | TODO-VISUAL-11 |
| AI Usage area chart | Bezier SVG line+area, gradient fill, hover | Mini proportional bar chart | chart/widget missing | P1 | Needs daily cost buckets — TODO-VISUAL-05 |
| AI Usage activities bar chart | 14-bar SVG chart | `skeleton="chart"` placeholder | chart/widget missing | P1 | Needs activities endpoint — TODO-VISUAL-05 |
| AI Usage engagement heatmap | 7×12 GitHub-style heatmap | `skeleton="ring"` placeholder | chart/widget missing | P1 | Needs heatmap endpoint — TODO-VISUAL-06 |
| AI Config slide-in drawers | Right-side slide-in drawer | Centered `sp-admin-modal` | layout composition | P2 | Use `sp-admin-slide-over` — TODO-VISUAL-09 |
| Toggle switch component | Dedicated CSS toggle pill | Button variants | component styling | P2 | Add toggle-switch component — TODO-VISUAL-10 |
| Diagnostics section borders | `1.5px solid var(--indigo)` | Standard card border | colour/tokens | P3 | Minor — add accent border variant |
| Shell nav sections | 5 labelled groups | 2 groups (Menu / System) | layout composition | P3 | Low value divergence; acceptable |
| Shell "SOON" pill | Nav item "SOON" indicator | Not present | iconography | P3 | Add to preview features |

---

## Route Fidelity Matrix

| Route | Fidelity /10 | Main mismatch | Real-data limitation? | Placeholder limitation? | Next action |
|-------|-------------|---------------|----------------------|------------------------|-------------|
| `/admin` | 6/10 | Activity chart, score dist, donut, streak, live feed missing; pending actions weakened | Yes — 6 sections need new endpoints | Yes — 5 skeleton placeholders remain | 10UI-BACKEND-AGG-1 to wire activity + score endpoints |
| `/admin/students` | 8/10 | Streak + Mins/wk columns missing; no row avatars | Yes — streak/minutes need backend | No | Add avatars now; add columns when endpoint available |
| `/admin/students/create` | 9/10 | Matches reference closely | No | No | No urgent action |
| `/admin/students/:id` | 8/10 | Pool health using ring/breakdown bars (added in VISUAL-1) | Partial | No | No urgent action |
| `/admin/ai-config` | 7/10 | Rate limits placeholder; edit forms use centered modal not right slide-over | Yes — rate limits need endpoint | Yes — rate limits card | TODO-VISUAL-09: convert to slide-over |
| `/admin/prompts` | 8/10 | KPI strip beyond reference; prompt edit modal acceptable | No | No | No urgent action |
| `/admin/curriculum` | 8/10 | KPI strip beyond reference (improvement); track list matches | No | No | No urgent action |
| `/admin/usage` | 5/10 | Area chart replaced by mini bar; activities + heatmap missing | Yes — 3 missing endpoints | Yes — 2 skeleton placeholders | 10UI-BACKEND-AGG-1 |
| `/admin/exercise-types` | 8/10 | Toggle switch visual differs; avg stats suppressed (correct) | Yes — avg stats need endpoint | No | TODO-VISUAL-10: toggle component |
| `/admin/notifications` | 7/10 | Tab restructure diverges (acceptable improvement); SMS added beyond reference | No | No | Document as intentional |
| `/admin/integrations` | 7/10 | More cards than reference; job metrics KPI strip extra | Yes — most cards unimplemented | Yes — 4 placeholder cards | No urgent action |
| `/admin/diagnostics` | 7/10 | Severity breakdown + KPI strip added (beyond reference — improvement); section borders differ | No | No | Minor border accent fix |
| `/admin/security` | 7/10 | No reference counterpart | No | No | No urgent action |
| `/admin/usage-policies` | 7/10 | No reference counterpart | No | No | No urgent action |

---

## Component Fidelity Matrix

| Component | Reference match | Current issue | Severity | Fix recommendation |
|-----------|----------------|---------------|----------|-------------------|
| `sp-admin-kpi-card` | 80% | Icon tile 40px square vs reference 56px-wide left band; value 28px vs 24px reference | P3 | Document as intentional variant |
| `sp-admin-visual-placeholder` | No reference equivalent | Correctly communicates unavailability with skeleton shapes | P3 | No fix needed |
| `sp-admin-graph-card` | No reference equivalent | Useful wrapper; not in reference | P3 | No fix needed |
| `sp-admin-badge` | 90% | Colours close to reference tokens | P3 | No urgent action |
| `sp-admin-button` | 85% | Matches reference patterns | P3 | No urgent action |
| `sp-admin-sidebar` | 85% | Width matches; 2 nav sections vs 5; "SOON" pill absent | P3 | Low priority |
| Hero banner tokens | Mismatch before this phase | Warm near-black now; cool indigo removed | — | **FIXED** |
| Avatar component | Absent from list rows before this phase | Present in detail hero; now added to dashboard lists | P2 | **FIXED** — students list rows still need avatars (TODO-VISUAL-11) |
| Toggle switch | Dedicated CSS pill in reference | Button variants in Angular | P2 | New component — TODO-VISUAL-10 |
| SVG area chart | Custom bezier, gradient, hover | No equivalent anywhere | P1 | Defer until backend endpoint available |
| SVG donut chart | Custom SVG for cost-by-type | No equivalent | P1 | Defer until backend endpoint available |
| SVG heatmap | 7×12 GitHub heatmap | No equivalent | P1 | Defer until backend endpoint available |
| Slide-in drawer (right panel) | `SlideIn` right-to-left for AI Config | Centered modal | P2 | TODO-VISUAL-09 |

---

## Data/Backend Limitation Analysis

| Missing section | Page | Classification |
|-----------------|------|----------------|
| Activity trends area chart | Dashboard, AI Usage | Needs new backend aggregate endpoint |
| Score distribution (5 bins) | Dashboard | Needs new backend aggregate endpoint |
| AI cost by type (donut) | Dashboard | Partially buildable from existing `byFeature[]` cost data in AI Usage summary |
| AI spend sparkline | Dashboard | Daily cost time-series partially available via AI Usage trend buckets |
| Session duration | Dashboard | Needs new backend aggregate endpoint |
| Streak leaderboard | Dashboard | Needs new backend aggregate endpoint |
| Pending actions (contextual list) | Dashboard | Can be built now from existing loaded signals |
| Per-student activity engagement strip | Dashboard metric | Buildable from loaded `students()` lifecycle signal |
| Live events feed | Dashboard | Needs SignalR or polling realtime endpoint |
| Activities per day bar chart | AI Usage | Needs new backend aggregate endpoint |
| Student engagement heatmap | AI Usage | Needs new backend aggregate endpoint (complex) |
| Rate limits usage bars | AI Config | Needs rate-limit telemetry endpoint |
| Avg completion time + avg score | Exercise Types | Should remain placeholder — reference used hardcoded mock data |
| Per-service health latency bars | Dashboard system health | Needs `/admin/health/detailed` endpoint |

---

## Tiny Safe Fixes Applied

| Fix | File | Description |
|-----|------|-------------|
| Hero gradient warm-purple | `admin-dashboard.component.ts` | `#1e1b4b → #3730a3` → `#211B36 → #2D2455` |
| Hero label opacity | `admin-dashboard.component.ts` | `#a5b4fc` → `rgba(255,255,255,.45)` |
| Hero key subtext opacity | `admin-dashboard.component.ts` | `#c7d2fe` → `rgba(255,255,255,.5)` |
| Hero placeholder value opacity | `admin-dashboard.component.ts` | `#818cf8` → `rgba(255,255,255,.35)` |
| Hero placeholder label consistency | `admin-dashboard.component.ts` | "Not implemented" → "Backend not available yet" |
| Onboarding funnel — 4 stages | `admin-dashboard.component.ts` | Added CEFR placed stage; renamed to Onboarded/CEFR placed/In progress/Not onboarded |
| Avatar tiles in at-risk list | `admin-dashboard.component.ts` | Added coloured initial circles to at-risk student rows |
| Avatar tiles in recent students table | `admin-dashboard.component.ts` | Added coloured initial circles to email cell |
| Avatar CSS helpers | `admin-dashboard.component.ts` | `.sp-dash-avatar`, `.sp-dash-avatar-row` styles added |
| `avatarInitial()` + `avatarColor()` methods | `admin-dashboard.component.ts` | 7-colour palette hash; returns uppercase initial |

Tiny fixes intentionally NOT applied (beyond safe scope):
- AI Usage "90d" period pill — requires `PeriodPreset` type extension and `buildRange` change; deferred
- Diagnostics accent border — minor cosmetic; deferred to 10UI-POLISH-1
- Nav "SOON" pill — requires nav item data model change; deferred
- Students list row avatars — fix scoped to a future POLISH phase

---

## Recommended Next Phases

### 10UI-POLISH-1 — Admin Frontend Visual Polish (zero new endpoints)
**Focus:** All gaps buildable now from existing loaded signals or small component additions:
- Computed contextual pending-actions list on Dashboard (from `atRiskStudents()`, unconfigured AI cats, CEFR-less students)
- Per-student activity colour strip on Dashboard metric section (lifecycle from `students()`)
- Toggle-switch component for Exercise Types and Notifications
- AI Config category edit: switch from centered modal to `sp-admin-slide-over`
- Students list: add avatar tiles to table rows
- Diagnostics: add accent border to System status and Recent events cards
- Shell: add "SOON" pill to preview nav items
- Dashboard: convert AI cost KPI tile from italic text to using `—` + `sp-admin-visual-placeholder` below
**Estimated size:** ~1 sprint, zero backend changes.
**Dashboard fidelity gain:** 6/10 → 7/10 (limited by missing endpoints)

### 10UI-BACKEND-AGG-1 — Admin Dashboard Aggregate Endpoints (backend required)
**Focus:** Minimum set of new backend aggregate endpoints to unblock the highest-impact missing charts:
1. `GET /admin/stats/activities-per-day?days=N` — unblocks Dashboard activity chart + AI Usage activities bar chart (2 P1 gaps with 1 endpoint)
2. `GET /admin/stats/score-distribution` — unblocks Dashboard score distribution chart
3. `GET /admin/ai/usage/summary` extension: add `dailyCostBuckets[]` — unblocks Dashboard AI spend sparkline and improves AI Usage area chart
**Estimated size:** ~1–2 sprint cycles (backend + frontend wiring).
**Dashboard fidelity gain:** 6/10 → 8–9/10

### 10UI-VISUAL-E2E-1 — Admin Screenshot Visual Regression Harness
**Focus:** Playwright screenshot capture + visual diffing for admin routes:
- Set up Playwright screenshot test suite against dev server
- Capture all 14 admin routes
- Store reference screenshots for regression
- Document gaps found visually that code audit may miss
**Estimated size:** ~0.5 sprint.
**Benefit:** Prevents visual regressions across future sprints; enables honest fidelity tracking.

---

## Gates

- `git diff --check`: clean
- Production build: **passed**
- Unit tests: **1324/1324 passed**
- Backend gates: not required (no backend source changed)
- Playwright: not run (no stable admin visual specs exist)

---

## Files Changed (this phase)

| File | Change |
|------|--------|
| `admin-dashboard.component.ts` | Gradient fix, label fixes, funnel stages, avatar tiles, avatar helpers, avatar CSS |
| `TODOS.md` | Added TODO-VISUAL-01 through TODO-VISUAL-12 |
| `docs/design/admin-reference-alignment.md` | VISUAL-FINAL section appended |
| `docs/reviews/2026-06-24-phase-10ui-visual-final-admin-visual-fidelity-audit.md` | Created (this file) |

---

## Confirmation

No backend APIs, migrations, business logic, student-facing UI, AI provider code, or database model changes were implemented in this phase.
