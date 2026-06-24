# Phase 10UI-PARITY-1A — Standalone Admin Exact Page/Card Parity (Slice A)

**Date:** 2026-06-24
**Sprint/Phase:** 10UI-PARITY-1A
**Standalone reference:** `docs/design/SpeakPath Admin (standalone) V1.html`

---

## Summary

This is slice A of the standalone admin parity work. It covers:
1. Route/page inventory from standalone vs Angular
2. Two missing pages created (Lessons, Usage & Analytics)
3. Sidebar nav restructured to match standalone section hierarchy
4. Tests fixed and build verified

Full page-by-page card alignment (dashboard, students, ai-config, prompts, usage, usage-policies, exercise-types, curriculum, notifications, integrations, diagnostics, security) deferred to **10UI-PARITY-1B**.

---

## Standalone Reference Inspection

File opened via headless browser: `docs/design/SpeakPath Admin (standalone) V1.html`

### Sidebar nav structure (standalone)

| Section | Items |
|---------|-------|
| OVERVIEW | Dashboard |
| STUDENTS | Students |
| AI SYSTEM | AI Config, Prompts, AI Usage, Usage Policies |
| ANALYTICS | Usage & Analytics |
| CONTENT | Lessons, Curriculum, Exercise Types |
| SYSTEM | Notifications, Integrations, Diagnostics, Security |

### Standalone pages inspected

| Page | Route | Cards/Sections |
|------|-------|----------------|
| Dashboard | `/admin` | Hero banner (weekly snapshot KPIs), 4 stat cards, Activities trend + System health, Onboarding funnel, At-risk students, Score distribution, AI cost by type, Avg session + heatmap, Streak leaderboard, Admin actions, Cohort engagement, CEFR distribution, Students mini-table, Live events |
| Students | `/admin/students` | Filter bar (lifecycle tabs, search, rows-per-page), Student table (lifecycle, onboarding, CEFR, streak, mins/wk, joined) |
| Student Create | `/admin/create-student` (actual: `/admin/create-student`) | Full form: account credentials, profile, learning context, admin settings, cohort preview |
| AI Config | `/admin/ai-config` | Summary KPIs, tabs (LLM Categories, TTS, Provider Credentials, Model Pricing, Rate Limits), LLM category cards with configure/test |
| Prompts | `/admin/prompts` | Summary KPIs, filter bar, prompt version table |
| AI Usage | `/admin/usage` | Period pills, KPI strip, By provider table, By feature table, Visual analytics row, Calls over time, AI calls per day graph, Calls by feature graph, Recent calls table with export |
| Usage Policies | `/admin/usage-policies` | Summary KPIs, Policies table with rules expansion |
| Usage & Analytics | `/admin/usage-analytics` | Period pills (7d/30d/90d), KPI strip (cost/calls/avg-cost/activities), API cost chart, Activities chart, Student engagement heatmap |
| Lessons | `/admin/lessons` | Lesson generation card, Ready buffer per student, Lesson buffer settings, Readiness pool aggregate health |
| Curriculum | `/admin/curriculum` | Summary KPIs, routing preview, filter bar, objectives table |
| Exercise Types | `/admin/exercise-types` | Summary KPIs, skill breakdown, filter bar, exercise types table |
| Notifications | `/admin/notifications` | Channel status KPIs (In-App/Email/SMS/Dispatch Job), tabs (Notifications/Delivery Queue/Configuration/Templates), Delivery queue table |
| Integrations | `/admin/integrations` | Integration card grid (Object Storage, SMTP/Email, Webhook, Analytics, Admin API) |
| Diagnostics | `/admin/diagnostics` | Status KPIs, Recent events (top 8), Event severity breakdown, System status, Recent events table, Background jobs |
| Security | `/admin/security` | Security posture, Access controls toggles, Password, Active sessions, Audit log, Danger zone |

---

## Route/Page Inventory

| Standalone route | Angular route exists (before) | Action taken |
|-----------------|-------------------------------|--------------|
| `/admin` (Dashboard) | Yes | No change (parity deferred to 1B) |
| `/admin/students` | Yes | No change (parity deferred to 1B) |
| `/admin/create-student` | Yes | No change |
| `/admin/students/:id` | Yes | No change |
| `/admin/ai-config` | Yes | No change (parity deferred to 1B) |
| `/admin/prompts` | Yes | No change (parity deferred to 1B) |
| `/admin/usage` | Yes | No change (parity deferred to 1B) |
| `/admin/usage-policies` | Yes | No change (parity deferred to 1B) |
| `/admin/usage-analytics` | **No** | **Created** |
| `/admin/lessons` | **No** | **Created** |
| `/admin/curriculum` | Yes | No change (parity deferred to 1B) |
| `/admin/exercise-types` | Yes | No change (parity deferred to 1B) |
| `/admin/notifications` | Yes | No change (parity deferred to 1B) |
| `/admin/integrations` | Yes | No change (parity deferred to 1B) |
| `/admin/diagnostics` | Yes | No change (parity deferred to 1B) |
| `/admin/security` | Yes | No change (parity deferred to 1B) |

---

## Pages Created

### `/admin/lessons` — `AdminLessonsComponent`

File: `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts`

Cards:
- **Lesson generation** — button to generate lessons. Backend not available yet (placeholder status shown).
- **Ready lesson buffer per student** — placeholder. Per-student data available on student detail; aggregate endpoint not implemented.
- **Lesson buffer settings** — form for buffer size, refill threshold, refill batch, max attempts, generation timeout, TTS timeout, background generation toggle, TTS generation toggle. All settings: "Backend not available yet."
- **Readiness pool — aggregate health** — placeholder. "A system-wide readiness pool aggregate endpoint is not yet implemented."

### `/admin/usage-analytics` — `AdminUsageAnalyticsComponent`

Files:
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-analytics/admin-usage-analytics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-analytics/admin-usage-analytics.component.html`

Cards:
- **Period pills** — 7 days / 30 days / 90 days (real, wired to API)
- **KPI strip** — Total cost, Total API calls, Avg cost / student, Activities (period) — all real data from existing endpoints
- **API cost over time** — mini bar chart from `getAiUsageTrends()` — real data
- **Activities completed per day** — mini bar chart from `getDashboardActivityTrends()` — real data
- **Student engagement** — placeholder. "Engagement heatmap — backend not available yet"

Real endpoints wired:
- `AdminApiService.getAiUsageTrends(period)`
- `AdminApiService.getDashboardActivityTrends(period)`

---

## Sidebar Changes

File: `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.html`

Before:
- Section "Menu" containing all pages mixed
- Section "System" containing Integrations, Diagnostics, Security
- Missing: Usage & Analytics, Lessons nav entries

After (matches standalone exactly):
- Section **Overview**: Dashboard
- Section **Students**: Students
- Section **AI System**: AI Config, Prompts, AI Usage, Usage Policies
- Section **Analytics**: Usage & Analytics (new)
- Section **Content**: Lessons (new), Curriculum, Exercise Types
- Section **System**: Notifications, Integrations, Diagnostics, Security

Both desktop sidebar and mobile drawer updated.

---

## Routes Added

File: `src/LinguaCoach.Web/src/app/app.routes.ts`

```
/admin/lessons          → AdminLessonsComponent
/admin/usage-analytics  → AdminUsageAnalyticsComponent
```

---

## Tests

- Fixed: `AdminAppLayoutComponent (Phase 10UI-FIX-2) desktop sidebar has Menu and System section headings`
  - Updated to assert new section labels: Overview, AI System, Analytics, Content, System
- Total: **1360 / 1360 passing**
- Build: **production build succeeds, 0 errors**

---

## Cards Rendered as Not Implemented / Backend Unavailable

| Page | Card | Status shown |
|------|------|-------------|
| Lessons | Lesson generation | "Backend not available yet" |
| Lessons | Ready lesson buffer per student | "Backend not available yet" |
| Lessons | Lesson Buffer Settings | "Backend not available yet" |
| Lessons | Readiness pool aggregate health | "A system-wide readiness pool aggregate endpoint is not yet implemented" |
| Usage & Analytics | Student engagement | "Engagement heatmap — backend not available yet" |

---

## Real Data Cards Wired

| Page | Card | Endpoint |
|------|------|----------|
| Usage & Analytics | Total cost KPI | `getAiUsageTrends(period)` |
| Usage & Analytics | Total API calls KPI | `getAiUsageTrends(period)` |
| Usage & Analytics | Avg cost / student KPI | `getAiUsageTrends(period)` |
| Usage & Analytics | Activities KPI | `getDashboardActivityTrends(period)` |
| Usage & Analytics | API cost over time chart | `getAiUsageTrends(period)` |
| Usage & Analytics | Activities completed per day chart | `getDashboardActivityTrends(period)` |

---

## Shared Components Changed

- `admin-app-layout.component.html` — sidebar section restructure (both desktop and mobile)

No shared `sp-admin-*` component styling was changed in this slice. Visual token/style alignment is deferred to 10UI-PARITY-1B.

---

## Remaining Gaps (10UI-PARITY-1B)

The following work is deferred to the next slice:

1. **Dashboard** — full card-by-card alignment (hero banner, stat cards, onboarding funnel, at-risk students, score distribution, AI cost by type, avg session, streak leaderboard, admin actions, cohort engagement, CEFR distribution, students mini-table, live events)
2. **Students** — table column alignment, filter bar
3. **Student Create** — form section alignment
4. **Student Detail** — card section alignment
5. **AI Config** — tab structure, category card layout
6. **Prompts** — table column alignment
7. **AI Usage** — match standalone KPI label names (standalone shows "TOKEN CALLS" note, fallback column renamed)
8. **Usage Policies** — table and rules expansion alignment
9. **Exercise Types** — table column alignment, skill summary KPIs
10. **Curriculum** — objectives table alignment
11. **Notifications** — tab structure alignment (Delivery Queue, Configuration, Templates tabs)
12. **Integrations** — match standalone card order and status labels
13. **Diagnostics** — match standalone layout (top KPIs, recent events top-8, severity breakdown, system status, full events table, background jobs)
14. **Security** — match standalone card structure (security posture, access controls, password, sessions, audit log, danger zone)
15. **Shared component styling** — card radius, border, shadow, typography, badge style, table density alignment to standalone tokens

---

## Decisions Made

- Sidebar sections restructured to match standalone exactly rather than keeping old "Menu" grouping.
- `/admin/usage-analytics` wires real AI trend + activity trend data where available.
- Lessons page uses honest placeholders for all backend-unavailable features.
- No fake data, no mock imports, no secrets, no migrations introduced.
- No student-facing UI changes.

---

## Next Recommended Phase

**10UI-PARITY-1B** — page-by-page card alignment for all 14 remaining pages, plus shared component visual token updates.
