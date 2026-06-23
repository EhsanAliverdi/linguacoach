# Engineering Review — Phase 10UI-REDESIGN-2: Students List and Create Student Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-2
**Commit:** a44a44d

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-2 entry

---

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/create-student/create-student.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/create-student/create-student.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/create-student/create-student.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

---

## Findings

### P0 — Blockers

None.

### P1 — Critical

None.

### P2 — Significant

None. All original form fields, validation, submit payload, pagination, sort, filter, and modal actions preserved.

### P3 — Minor / informational

- Welcome email aside note is intentionally static ("backend not available yet"). No send endpoint exists. No action required.
- Rows-per-page selector uses native `<select>` + `[(ngModel)]` binding, consistent with existing native selects in create-student for number/null bindings.

---

## Changes made

### Students list (`/admin/students`)

- 4-tile KPI summary strip added above the table using `SpAdminKpiCardComponent`.
  - Tile 1 — Total students: real data from `getStats()` → `totalStudents`.
  - Tile 2 — Onboarded: real data from `getStats()` → `onboardedStudents`.
  - Tile 3 — Activities tracked: real data from `getStats()` → `totalActivityAttempts`.
  - Tile 4 — Showing this page: `totalCount()` from paged list response.
- `getStats()` called in `ngOnInit()` alongside existing `load()`.
- `stats = signal<AdminStats | null>(null)` and `loadingStats = signal(true)` added.
- Loading state shows `—` in KPI tiles until stats resolve.
- Error state silently leaves loading=false (tiles show last value or `—`).
- Rows-per-page selector added to filter bar: options `[10, 25, 50, 100]`, default 25 (unchanged).
- `onPageSizeChange()` resets page to 1 and calls `load()`.
- `pageSize` changed from `readonly` to mutable to support selector binding.
- CSS: `.sp-stu-summary-row`, `.sp-stu-filter-spacer`, `.sp-stu-rows-label`, `.sp-stu-rows-select`.

### Create Student (`/admin/create-student`)

- Full template rewrite from single-card to two-column reference-style layout.
- Left column: form sections (Account credentials, Student profile/Optional, Actions).
- Right column: sticky aside "What happens next" (5 sequential steps + welcome email not-available-yet note).
- Page header: `sp-admin-page-header` with "← Back to Students" ghost button (`routerLink="/admin/students"`).
- Account credentials section: email, temporary password, must-change-password checkbox.
- Security note: amber-tinted warning box — temporary password shown once, student must change before onboarding.
- Student profile section: "Optional" badge, collapsible via `showOptional` toggle button.
- Optional fields: first name, last name, display name, career context, learning goal, session duration, experience level, role familiarity.
- Actions: Cancel (routes to `/admin/students`), Create student (submit).
- All original form fields, validation (`validate()`), submit payload, and `AdminService.createStudent()` call preserved unchanged.
- `RouterLink` added to imports. Inline `styles: [...]` added.
- Grid: `1fr 300px` at ≥1100px, single column below.

---

## Tests

### Students list spec (10 new tests)

- `summary strip` describe block (5 tests): KPI strip renders, `getStats` called on init, tile values match stats fixture (42/30/500), tile labels present, no fake/hardcoded numbers.
- `rows per page` describe block (5 tests): selector renders, "Rows per page" label present, `onPageSizeChange` resets page to 1, `onPageSizeChange` calls `listStudents`, page size updates via binding.

### Create Student spec (15 new tests)

- `page structure` (6 tests): back link, Account credentials heading, Student profile heading, Optional badge, What happens next panel, aside steps include Onboarding and Placement test.
- `security note` (3 tests): temporary password / "once" wording, Welcome email note, no API keys or secrets in rendered output.
- `profile toggle` (2 tests): optional fields hidden by default (no "First name"), appear after setting `showOptional = true`.
- `cancel action` (1 test): Cancel button renders.
- Updated `renders card wrapper` and `renders optional profile fields toggle` to match new template structure.

### Migration spec (8 spy fixes)

- 8 locations in `admin-wrapper-migration.spec.ts` where `AdminStudentsComponent` was constructed without a `getStats` spy — all updated to include `'getStats'` + return value `of(STATS)`.

---

## Security constraints verified

- No fake production data.
- No invented numbers, fake students, fake activities, or fake system health.
- No "All clear" indicators without backend support.
- No heavy chart library added.
- No backend API changes, migrations, student UI changes, or redesign of other admin pages.
- Every section: Real data / Partial real data / Not implemented / Error/unknown.

---

## Gates passed

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| Production build (`npm run build -- --configuration production`) | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1138/1138 PASS |

---

## Decisions made

1. KPI strip uses `getStats()` (existing endpoint) rather than deriving from paged list, matching the dashboard approach and minimising new API calls.
2. "Showing this page" tile uses `totalCount()` (paged response total count) rather than the page size, matching the reference design intent.
3. Aside panel is static HTML — no dynamic steps based on `showOptional` state, as the reference design shows a fixed 5-step flow.
4. Welcome email note is a static aside note, not a status badge, because no email send endpoint exists.
5. `pageSize` made mutable (removed `readonly`) rather than adding a separate signal, to avoid template binding complexity.

---

## Risks and unresolved questions

- None for this phase. The `welcomeEmail` note will remain static until a `/api/admin/students/{id}/send-welcome` endpoint is implemented (separate backend phase).

---

## Final verdict

**Complete.** 1138/1138. Build clean. All security constraints satisfied. All existing behaviour preserved.

---

## Next recommended action

**10UI-REDESIGN-3** — Student Detail reference alignment (coloured avatar hero, danger zone card).

See: `docs/design/admin-reference-alignment.md` — `/admin/students/:id` row.
