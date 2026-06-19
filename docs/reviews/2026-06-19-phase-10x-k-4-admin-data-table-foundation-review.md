# Phase 10X-K-4 — Admin Data Table Foundation Review

**Date:** 2026-06-19
**Sprint:** 10X-K-4 Admin Data Table Foundation
**Scope:** Frontend only — `sp-admin-table` and admin page table wiring
**Reviewer:** Claude Code

---

## Files Reviewed

- `src/LinguaCoach.Web/src/app/admin/components/table/sp-admin-table.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/pagination/sp-admin-pagination.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/data-table/sp-admin-data-table.component.ts`
- `src/LinguaCoach.Web/src/app/admin/index.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts` + `.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts` + `.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` + `.html`

---

## Findings

### Architecture — sp-admin-table

`sp-admin-table` uses two rendering modes:

1. **Column-mode**: Consumer passes `[columns]` and `[rows]` arrays. The component renders the full table including header, sortable columns, selection checkboxes, hover rows, striping, borders. Loading and empty states are handled internally.

2. **Slot-mode** (`ng-content`): Consumer passes a raw `<table>` as projected content. This activates when `columns.length === 0` (the default). The component provides the outer card shell, scroll wrapper, density classes, and `::ng-deep` utility styles. Pages handle their own loading/error/empty states before rendering the component.

All six target pages use slot-mode. This is the correct pattern for complex tables that need badges, actions, and conditional rendering per cell.

### Existing strengths (no change needed)

- Full-width container with `overflow-x: auto` scroll wrapper — wide tables stay contained.
- Row borders via `border-bottom` on `td`.
- Row hover via `sp-adm-tr-hover` class and `:hover` styles.
- Density system: `compact`, `comfortable`, `spacious` — applied via `::ng-deep` so it cascades to projected content.
- `minWidth` input with CSS custom property `--sp-admin-table-min-width`.
- Sticky header support via `stickyHeader` input.
- `sp-admin-pagination` correctly wired — layout, border-top, responsive — no changes needed.
- Comprehensive utility classes: `sp-admin-table-truncate`, `sp-admin-table-wrap`, `sp-admin-table-num`, `sp-admin-mono`, `sp-admin-cap`, `sp-admin-muted`, `sp-admin-actions`, `sp-admin-table-empty`.

### Issues found and fixed

#### 1. `sp-admin-table` — missing error state support (P1)

The component supported `loading` and `emptyMessage` states for column-mode but had no `error` input for either mode. Pages that use slot-mode handled errors outside the component entirely — which is a valid pattern but inconsistent.

**Fix:** Added `error` and `errorTitle` inputs. Added `SpAdminErrorStateComponent` to imports. Added error branch in template between loading and ng-content branches. Consumers can now optionally bind `[error]="error()"` to let the table shell handle error display.

#### 2. `admin-students` — empty state rendered alongside table (P2)

The students page projected both the `<table>` and a conditional `<sp-admin-empty-state>` as `ng-content`. When `filteredStudents().length === 0`, both the empty table shell and the empty state message rendered simultaneously inside the scroll container.

**Fix:** Moved the empty state outside the `sp-admin-table` wrapper with an `@if`/`@else` guard. The table is only rendered when there are rows. The empty state renders standalone when there are no results (consistent with how other pages handle it).

Also removed the spurious `class="sp-admin-table"` on the inner `<table>` element — it's unnecessary since `::ng-deep table` targets the element directly.

### Pages reviewed — no changes needed

| Page | Table component | Issues found |
|------|----------------|-------------|
| `/admin/usage` (ai-usage) | `sp-admin-table` slot-mode, 3 usages | None — correctly guards loading/error before table |
| `/admin/prompts` | `sp-admin-table` slot-mode | None |
| `/admin/exercise-types` | `sp-admin-table` slot-mode, minWidth 1240px | None |
| `/admin/integrations` | `sp-admin-table` slot-mode, 2 usages | None |
| `/admin/diagnostics` | `sp-admin-table` slot-mode | None |

### Duplicate `sp-admin-data-table` — not touched

`sp-admin-data-table` is a simpler, older component with fewer features (no density, no sorting, no selection, no variants). It is exported from the barrel but not used by any page in the six-page scope.

**Recommendation (future):** Mark `sp-admin-data-table` as deprecated. It has no callers in the target pages and is strictly a subset of `sp-admin-table`. Remove in a dedicated cleanup phase, not in this phase.

### `sp-admin-pagination` — no changes

Component is correct. Layout is flex/space-between with border-top, wraps responsively, clamps page range safely. No issues found.

---

## Decisions Made

1. Keep slot-mode (`ng-content`) as the primary pattern for complex pages. Pages handle their own loading and error states outside `<sp-admin-table>`. The new `error` input is additive and optional.
2. Do not rebuild page UX or add server-side pagination, filters, or charts in this phase.
3. Do not deprecate `sp-admin-data-table` in this phase — defer to cleanup sprint.
4. Do not add Playwright tests — no existing admin/table Playwright tests were found in scope.

---

## Implementation Tasks Produced

None outstanding — all fixes applied in this phase.

---

## Files Changed

| File | Change |
|------|--------|
| `sp-admin-table.component.ts` | Added `SpAdminErrorStateComponent` import, `error`/`errorTitle` inputs, error branch in template |
| `admin-students.component.ts` | Fixed empty state rendering — moved outside `sp-admin-table`, removed inner table class |

---

## Gates

- `git diff --check`: clean (no whitespace errors)
- Production build: **PASS** (output: `dist/lingua-coach.web`)
- Angular tests: **437/437 PASS** (ChromeHeadless)
- Playwright: not run — no existing admin table Playwright tests in scope
- .NET tests: not run (frontend-only phase)

---

## Remaining Table Issues (future phases)

- `sp-admin-data-table` should be deprecated and removed.
- The `admin-usage` placeholder component (`AdminUsageComponent`) uses raw inline styles and is not connected to a real API. It is not the live `/admin/usage` route — that routes to `AdminAiUsageComponent`. The placeholder can be removed if the route is confirmed final.
- No server-side pagination is implemented in any admin page. All pages do client-side paging over full data sets. This is acceptable at pilot scale but will need addressing when student/event counts grow.

---

## Risks / Unresolved Questions

- None blocking for this phase.

---

## Final Verdict

Foundation is solid. `sp-admin-table` is a well-structured, multi-variant component with complete utility class coverage. All target pages use it consistently. Two targeted fixes applied. All gates pass.

---

## Next Recommended Action

Proceed to Phase 10X-K-5 or next planned phase. Consider deprecating `sp-admin-data-table` in a cleanup sprint.

---

## Confirmation

- No backend changes made.
- No API behavior changed.
- No commit or push performed.
