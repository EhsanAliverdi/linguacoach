# Phase 10Students-F-G — Student List Filter Select UI Review

**Date:** 2026-06-20
**Sprint:** Phase 10Students-F-G
**Related feature:** Admin student list — lifecycle, onboarding status, CEFR filter selects

---

## Files reviewed / changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts`
- `docs/reviews/2026-06-20-phase-10students-f-g-student-list-filter-select-ui-review.md` (this file)
- `docs/sprints/current-sprint.md`
- `docs/handoffs/current-product-state.md`
- `TODOS.md`

---

## Context

Phase 10Students-F-F (2026-06-19) wired up server-side filtering with `lifecycleStage`, `onboardingStatus`, and `cefrLevel` query params. The backend already accepted these params. The admin UI had no controls to set them. This phase adds the UI selects.

---

## Findings

### Priority 1 — Implemented

**Filter selects added to filter bar.**
Three `sp-admin-select` instances inserted into `sp-admin-filter-bar`:
- Lifecycle stage (12 options from `StudentLifecycleStageName`)
- Onboarding status (4 options: NotStarted, Pending, InProgress, Complete)
- CEFR level (A1–C2 with friendly labels)

Each select uses `size="sm"` and `[fullWidth]="false"` to fit compactly in the filter bar.

**Filter signals added.**
`filterLifecycleStage`, `filterOnboardingStatus`, `filterCefrLevel` signals (all `string`, empty string = no filter). Each change handler resets page to 1 and calls `load()`.

**Load method extended.**
`load()` now passes `lifecycleStage`, `onboardingStatus`, `cefrLevel` as `undefined` when empty, which matches the `StudentListQuery` optional fields. The API service passes them to the backend only when set.

**Clear filters button.**
Shows only when `hasActiveFilters()` is true (any of: searchTerm, lifecycleStage, onboardingStatus, cefrLevel is non-empty). Clears all four, resets page to 1, does NOT touch `includeArchived`, calls `load()`.

**SpAdminSelectComponent imported.**
Added to the component's `imports` array and to the import statement from `'../../../admin'`. The component was already exported from `admin/index.ts` (line 33) — no barrel change needed.

### No issues found

- No backend change required.
- No student-facing change.
- No bulk operations.
- No unrelated refactor.

---

## Decisions made

- Used `sp-admin-select` (not native `<select>`) for visual consistency with the design system. The component works for string values, which these all are.
- `hasActiveFilters()` excludes `includeArchived` from the "active filters" check — toggling archived is a view mode, not a filter to be cleared.
- Empty string signals map to `undefined` before being passed to `listStudents()`, so the API receives no param when no filter is selected.
- CEFR levels hardcoded as A1–C2; sourced from the CEFR standard, not from the backend. Backend accepts any string.
- Onboarding status values sourced from `admin-badge.utils.ts` (`Complete`, `InProgress`, `NotStarted`, `Pending`) to match exactly what the backend emits.

---

## Tests added

7 new tests in `admin-students.component.spec.ts`:
- `lifecycleStage filter calls listStudents with param and resets page`
- `onboardingStatus filter calls listStudents with param and resets page`
- `cefrLevel filter calls listStudents with param and resets page`
- `clearFilters clears search and all filter params then calls listStudents`
- `clearFilters does not touch includeArchived`
- `hasActiveFilters returns false when no filters set`
- `hasActiveFilters returns true when any filter is set`

All 32 tests pass (25 pre-existing + 7 new).

---

## Gate results

- `git diff --check`: clean
- `npm run build -- --configuration production`: pass
- `npm test -- --watch=false --browsers=ChromeHeadless`: 32/32 pass

---

## Risks / unresolved questions

None. The backend params were already validated and tested in Phase 10Students-F-F.

---

## Final verdict

Complete. Filter selects are wired, tested, and passing all gates.

---

## Next recommended action

Review TODOS.md for remaining Phase 10Students items.
