# Admin: Student Detail Page

Date: 2026-06-14
Related backlog: `docs/backlog/product-backlog.md` — "Admin UX, Student Management & AI Config Cleanup" deferred follow-ups, and "Admin dashboard — real data" (learning memory view, student list "view individual student").

## Context

The admin students list page had Edit / Reset password / Reset data / Archive
actions per row, but no way to see a student's full profile or learning
memory in one place. The backend endpoint
`GET /api/admin/students/{id}/learning-memory` existed (added during the
Student Learning Memory sprint) but had no Angular consumer.

## What was implemented

### Routing
- New route `/admin/students/:id` →
  `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`.
- Added a "View" link per row on the students list
  (`admin-students.component.ts`) linking to the new route.

### Admin Student Detail page
- Fetches the student via `AdminApiService.listStudents(true)` (includes
  archived) and finds the matching `studentProfileId` — there is no
  single-student GET endpoint, so the existing list endpoint is reused.
- Fetches learning memory via the existing
  `AdminApiService.getStudentLearningMemory(id)`.
- **Profile card**: lifecycle stage badge, onboarding status badge, CEFR
  level, career context, learning goal, learning goal description, difficult
  situations text, preferred session duration, experience level, role
  familiarity, joined date.
- **Learning memory card**: journey summary, strong skills, weak skills,
  recurring mistakes, next recommended focus, covered scenario count, skill
  profile tags (weak skills shown in amber, others in green).
- **Actions** (moved/duplicated from the list page, same modals): Edit,
  Reset password, Reset data (with presets, clear-flag checkboxes, reason,
  typed-email confirmation), Archive. Archiving or resetting data reloads the
  student and memory in place.
- "Back to students" link returns to `/admin/students`.

### Frontend only
No backend changes — the learning-memory endpoint and reset/archive/password
endpoints already existed and are reused as-is.

## Verification

- `npx tsc -p tsconfig.app.json --noEmit`: clean.
- `npx ng build --configuration development`: succeeds (pre-existing NG8102
  warnings in `pattern-evaluation-result.component.html`, unrelated).
- New Playwright spec `e2e/admin-student-detail.spec.ts` (3 tests): profile +
  learning memory render, back-link navigation, reset-data modal works from
  the detail page. All passing.
- Existing `e2e/admin-screenshots.spec.ts` (27 tests) and
  `e2e/admin-students-reset.spec.ts` (6 tests): all passing, no regressions.

## Out of scope / follow-ups

- Activity history on the detail page — not implemented, separate backlog
  item added ("Add activity history to admin student detail page").
- No dedicated `GET /api/admin/students/{id}` endpoint was added; the page
  reuses `GET /api/admin/students?includeArchived=true` and filters
  client-side. Acceptable at current student counts; revisit if the list
  grows large enough that fetching all students per detail-page view becomes
  wasteful.

## Status

Implemented and verified (build + Playwright e2e green). No outstanding
blockers.
