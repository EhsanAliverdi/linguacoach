# Phase 10X-K-11 — Students Admin Page Cleanup Review

**Date:** 2026-06-19
**Sprint/Phase:** 10X-K-11
**Related backlog:** Admin UI cleanup batch

---

## Files Changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts` — component refactored
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts` — new spec file created

---

## Files Reviewed

- `admin-students.component.ts` — full template and class
- `admin/components/truncated-text/sp-admin-truncated-text.component.ts` — API check
- `admin/components/copyable-text/sp-admin-copyable-text.component.ts` — API check
- `admin/components/button/sp-admin-button.component.ts` — renders `<button>` only, no routerLink support
- `admin-ai-usage.component.ts` — reference pattern for truncated/copyable usage

---

## Page Improvements Made

### 1. Profile column — truncated text wrapper
- Replaced raw `max-width` + `overflow-wrap:anywhere` CSS with `sp-admin-truncated-text`
- Sets `maxLength=60`, `maxWidth='260px'` — tooltip shows full value on hover
- Shows `sp-admin-table-empty` "Not set" fallback when neither `careerContext` nor `learningGoal` is set
- Removed raw overflow CSS from component styles

### 2. Email column — copyable text wrapper
- Replaced `<div class="sp-admin-table-muted sp-safe-text">{{ s.email }}</div>` with `sp-admin-copyable-text [value]="s.email"`
- Admins can copy student email with one click

### 3. Imports updated
- Added `SpAdminCopyableTextComponent` and `SpAdminTruncatedTextComponent` to both import list and decorator `imports` array

### 4. Create student link
- Assessed whether `sp-admin-button` could replace the `<a routerLink>` header action
- `sp-admin-button` renders a `<button>` only — does not support `routerLink`
- Kept original `<a routerLink="../create-student" class="sp-admin-btn-primary">` — this is correct and consistent with other pages

---

## Wrappers / Helpers Used

| Wrapper | Usage |
|---|---|
| `sp-admin-page-header` | Already present — retained |
| `sp-admin-page-body` | Already present — retained |
| `sp-admin-filter-bar` | Already present — retained |
| `sp-admin-table` | Already present — retained |
| `sp-admin-pagination` | Already present — retained |
| `sp-admin-table-actions` | Already present — retained |
| `sp-admin-badge` | Already present — retained |
| `sp-admin-loading-state` | Already present — retained |
| `sp-admin-error-state` | Already present — retained |
| `sp-admin-empty-state` | Already present — retained |
| `sp-admin-modal` | Already present — retained |
| `sp-admin-truncated-text` | **Added** — profile column |
| `sp-admin-copyable-text` | **Added** — email column |

---

## Pagination / Filter Changes

- No structural changes to pagination or filter bar
- Filter bar retains: archived toggle, search input, count label
- Pagination retains: page signal, totalPages computed, `sp-admin-pagination`

---

## Spec File Created

**`admin-students.component.spec.ts`** — 20 tests covering:

- Page header renders
- Student rows render
- Loading state
- Error state on load failure
- Empty state
- Filter by search term (accounts for non-signal `searchTerm` — term must be set before first computed evaluation)
- Lifecycle badge renders
- Onboarding badge renders
- Profile text shown when careerContext set
- "Not set" shown when no profile
- Archived row CSS class applied
- Pagination shows when > 1 page
- Pagination hidden when 1 page
- Action menu renders
- Archive action calls API
- startEdit populates editForm
- cancelEdit clears editing signal
- displayName fallback to email
- Sort by name
- Sort indicator arrows

---

## Build Result

Production build: **SUCCESS** (10.4 seconds)
Warnings present: pre-existing (PatternEvaluationResultComponent nullish, AdminDiagnosticsComponent optional chain, budget, unused import in ExerciseTypes) — none introduced by this change.

---

## Angular Test Result

- Students-only run: **20/20 SUCCESS**
- Full suite: **522/522 SUCCESS** — no regressions

---

## Playwright Result

Not run. No existing Playwright tests target the Students admin page directly. No user-visible navigation or routing changes were made.

---

## Remaining Students Page Issues

The following are known out-of-scope items for this phase:

- `searchTerm` is a plain class property, not a signal — `filteredStudents()` computed does not re-evaluate reactively when the search input changes. Works visually because Angular's two-way binding calls `(ngModelChange)` which sets `page.set(1)` triggering zone re-render, but the computed caches stale data. A future cleanup should convert `searchTerm` to a `signal<string>('')`.
- No student detail drawer (deferred per phase scope)
- No usage limits or policy assignment (deferred per phase scope)
- No server-side filtering (deferred per phase scope)

---

## Decisions Made

| Decision | Reason |
|---|---|
| Keep `<a>` for Create student header action | `sp-admin-button` renders `<button>` only, no routerLink support |
| Use `@if` guard before `sp-admin-truncated-text` | Component renders empty `<span>` on empty value — guarding avoids invisible DOM noise |
| Set searchTerm before first detect in filter spec | Non-signal property — computed evaluates term at call time but caches after first signal read |

---

## Confirmation

- No backend, API, or product behavior changed
- No commit or push performed
- Data loading and service calls unchanged
- Create/edit/archive/reset/password-reset behavior unchanged
- All modals and forms unchanged
- Pagination behavior unchanged

---

## Next Recommended Action

Continue with remaining admin page cleanups per the 10X-K batch plan. Consider converting `searchTerm` to a signal in a future cleanup pass.
