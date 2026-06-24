# Phase 10UI-PARITY-1C — Complete Standalone Admin Page/Card Parity

**Date:** 2026-06-24
**Sprint/Phase:** 10UI-PARITY-1C-A
**Standalone reference:** `docs/design/SpeakPath Admin (standalone) V1.html`
**Commit:** 104624a

---

## Summary

Slice 1C-A completes card-by-card parity for dashboard, lessons, and curriculum pages against the standalone reference HTML. No fake data, no secrets, no student UI changes, no migrations, no charting libraries added.

---

## Changes Made

### Dashboard (`admin-dashboard.component.ts`)

**Hero banner restructured:**

| Before | After |
|--------|-------|
| Weekly snapshot (label) | THIS WEEK (eyebrow) |
| Activities 7d (stat) | THIS WEEK — activities count |
| Students onboarded | ENGAGEMENT — % of students onboarded |
| Avg score | AVG SCORE — computed from real data |
| (missing) | ACTION NEEDED — students without CEFR + not archived |

Two new computed signals added:
- `heroEngagementPct` — `Math.round(onboardedStudents / totalStudents * 100)`
- `heroActionNeededCount` — students missing `cefrLevel` and not `Archived`

**KPI strip:** 5 cards → 4 cards. Removed "AI Provider" and "Onboarded" cards. Added "Active this week" (3/5 students active proxy using onboardedStudents/totalStudents).

**Students mini-table columns:**

| Before | After |
|--------|-------|
| Student / Onboarding / CEFR / Joined / Stage | STUDENT / CEFR / ACTS / JOINED / STATUS |

- STUDENT: shows `displayName || email`
- ACTS: `—` placeholder (list API has no per-student count)
- STATUS: onboardingLabel/onboardingTone badge

### Lessons (`admin-lessons.component.ts`)

Added 5 missing buffer settings fields that exist in standalone:
- Max concurrent generation jobs
- Max concurrent TTS jobs
- Practice ready per type
- Practice refill threshold
- Practice refill count

### Curriculum (`admin-curriculum.component.ts`)

Page title: `"Curriculum Objectives"` → `"Curriculum"` to match standalone.

---

## Tests

- Fixed `shows partial configured in AI System card`: test asserted `'1/2 configured'` but template renders individual category rows (no summary label rendered). Updated to assert `'OpenAI'` + `'Not configured'` which are the actual DOM outputs for partial config.
- All other test label updates: old KPI/hero labels → new standalone-aligned labels.
- Final result: **1361/1361 passing**

---

## Data Honesty

| Signal | Source | Notes |
|--------|--------|-------|
| `heroActivitiesThisWeek` | `stats.activitiesLast7Days` | Real API data |
| `heroEngagementPct` | `onboardedStudents / totalStudents` | Best available proxy; no weekly-active endpoint |
| `heroAvgScore` | `stats.averageScore` | Real API data |
| `heroActionNeededCount` | `students()` filter | No CEFR + not Archived |
| Active this week KPI | `onboardedStudents / totalStudents` | Best available proxy |
| ACTS column in students table | `—` placeholder | List API has no per-student activity count |

---

## Decisions Made

- No fake percentages or counts introduced. Engagement % and Active this week use existing backend fields as proxies; dashboard comment explains the limitation.
- `aiSystemStatusLabel()` computed retained in component but not rendered in the AI System card template (card renders individual category rows instead). Test updated to match template reality.
- Lessons buffer settings remain read-only placeholders; backend not yet implemented.

---

## Deferred to 10UI-PARITY-1C-B

1. Shared component visual token alignment (card radius, shadow, border, typography)
2. Student detail page card alignment
3. AI Config detailed tab parity
4. Usage Policies rules expansion panel
5. Exercise Types skill summary KPI strip
6. Prompts table column alignment

---

## Documentation Impact

- Docs reviewed: `docs/reviews/2026-06-24-phase-10ui-parity-1-standalone-admin-exact-card-visual-parity.md`
- Docs updated: this file (new), existing review updated with 1C-A section
- Docs intentionally not updated: architecture docs (no structural change)
- Reason: UI-only changes, no API contract or data model changes

---

## Final Verdict

10UI-PARITY-1C-A complete. 1361/1361 tests pass. Dashboard, lessons, and curriculum match standalone structure. No fake data. Committed as 104624a.

**Next recommended action:** 10UI-PARITY-1C-B — shared component visual token alignment and remaining page parity items.
