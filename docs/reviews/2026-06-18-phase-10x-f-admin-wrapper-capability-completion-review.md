# Phase 10X-F — Admin Wrapper Capability Completion Review

**Date:** 2026-06-18
**Sprint:** Phase 10X-F
**Review type:** Engineering implementation review
**HEAD before work:** c7f3c16

---

## Files reviewed

- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../shared/components/ui/dropdown/` — DropdownComponent pattern
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../shared/components/common/table-dropdown/` — TableDropdownComponent pattern
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../shared/components/common/theme-toggle/` — ThemeToggleButtonComponent
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../shared/services/theme.service.ts`
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../shared/components/header/user-dropdown/`
- All existing `sp-admin-*` wrapper components
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/admin-components.spec.ts`
- `src/LinguaCoach.Web/e2e/admin-students-reset.spec.ts`
- `src/LinguaCoach.Web/e2e/admin-screenshots.spec.ts`
- `src/LinguaCoach.Web/e2e/admin-student-detail.spec.ts`

---

## TailAdmin source patterns inspected

| TailAdmin source | Pattern inspected | How used |
|---|---|---|
| `shared/components/ui/dropdown/dropdown.component.ts` | `absolute z-40 right-0 mt-2 rounded-xl border border-gray-200 bg-white shadow-theme-lg`, click-outside via `mousedown` listener | `sp-admin-dropdown` panel classes |
| `shared/components/common/table-dropdown/table-dropdown.component.ts` | Three-dot trigger, Popper.js placement, click-outside | `sp-admin-table-actions` — simplified (no Popper, CSS-positioned) |
| `shared/components/common/theme-toggle/theme-toggle-button.component.ts` | `ThemeService` injection, `toggleTheme()` | `sp-admin-theme-toggle` — same pattern, uses `AdminThemeService` instead |
| `shared/services/theme.service.ts` | `BehaviorSubject<Theme>`, `localStorage`, `classList.add('dark')` | `AdminThemeService` mirrors this pattern exactly |
| `shared/components/header/user-dropdown/user-dropdown.component.ts` | Uses `DropdownComponent` + `DropdownItemTwoComponent` | Header user dropdown deferred — `sp-admin-header` now has `[actions]` slot for future wiring |

---

## Wrapper capabilities added

### New components

| Component | File | What it adds |
|---|---|---|
| `sp-admin-dropdown` | `admin/components/dropdown/sp-admin-dropdown.component.ts` | Generic dropdown: trigger/menu projection, click-outside, Escape close, align/width |
| `sp-admin-table-actions` | `admin/components/table-actions/sp-admin-table-actions.component.ts` | Row action three-dot dropdown: generic actions array or content projection, danger styling |
| `sp-admin-theme-toggle` | `admin/components/theme-toggle/sp-admin-theme-toggle.component.ts` | Admin-only dark/light toggle, sun/moon icons, aria-label |
| `AdminThemeService` | `admin/services/admin-theme.service.ts` | Admin-scoped theme: `adminTheme` localStorage key, `dark` class on `<html>` |

### Updated components

| Component | What changed |
|---|---|
| `sp-admin-table` | Added `sortable` column flag, `sortColumn`/`sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` sort icons, `aria-sort`, keyboard-accessible sortable `th` elements. Added `hasActions` input for action column header. |
| `sp-admin-header` | Added `[left]` and `[actions]` named content slots. Auto-renders `sp-admin-theme-toggle` in right action zone. Backward-compat: unnamed `<ng-content>` still works. |
| `sp-admin-filter-bar` | Added `[search]`, `[filters]`, `[actions]` named content slots. Left group / right group split. Backward-compat: unnamed `<ng-content>` goes in left group. |

---

## Pages lightly updated to prove usage

| Page | Change |
|---|---|
| `admin-students` | Row actions (View, Edit, Reset password, Reset data, Archive) migrated from flat inline buttons to `sp-admin-table-actions` projected content. Sorting was already inline on this page (predates `sp-admin-table` sorting support). |

---

## Wrappers intentionally deferred

| Capability | Deferred to | Reason |
|---|---|---|
| Full admin page refactor | TODO-10X-G | Out of scope for this phase — wrapper capabilities must exist before page redesign |
| Header user dropdown (TailAdmin UserDropdownComponent) | 10X-G | `[actions]` slot is ready; full user profile/logout dropdown is a page-level concern |
| Modal/drawer typed payloads | TODO-10X-MODAL, TODO-10X-DRAWER | Foundation is in place and working; typed payloads belong in page-refactor phase |
| Notification dropdown | 10X-G+ | Not implementing notification platform in this phase |
| Full dark mode class scoping (admin-only boundary) | TODO-10X-G | `AdminThemeService` adds `dark` class to `<html>` globally; scoping to admin route teardown is a future refinement |
| Students page migration to `sp-admin-table` with sorting | 10X-G | Students page has its own sorting logic predating the table wrapper; migration appropriate in full page refactor |

---

## Findings

### P0 (blockers fixed)

None.

### P1 (test regressions fixed)

- **Angular test**: "dropdown opens when trigger is clicked" used `querySelector('[trigger] button')` — selector fails because Angular content projection does not preserve attribute selectors in DOM. Fixed to use `.sp-adm-dropdown-trigger` class selector.
- **Playwright regressions** (8 tests): row actions (View, Edit, Archive, Reset data) moved into `sp-admin-table-actions` dropdown. Tests expecting direct button click required updates to: open three-dot trigger first, then locate action by text via `[role="menu"] button/a`. Fixed in `admin-students-reset.spec.ts`, `admin-screenshots.spec.ts`, `admin-student-detail.spec.ts`.

### P2 (design decisions)

- **No Popper.js**: `sp-admin-table-actions` uses CSS `absolute` positioning rather than Popper.js (which TailAdmin's `table-dropdown` uses). This avoids adding a dependency. Acceptable for admin use — overflow clipping will be addressed in 10X-G full layout pass if needed.
- **`adminTheme` isolation**: `AdminThemeService` uses a separate localStorage key from TailAdmin's `theme` key. This prevents student UI dark mode leakage. The `dark` class is still applied to `<html>` (not scoped to admin layout element) — acceptable now since student pages don't use dark styles, but TODO for 10X-G.
- **Projected actions vs. array API**: `sp-admin-table-actions` supports both. Array API (with `(actionClick)` output) is cleaner but cannot handle conditional/stateful actions per row. `admin-students` uses projected content because actions vary by `lifecycleStage`. Both patterns are valid.

### P3 (observations)

- Students page already had its own `sortColumn`/`sortDirection` signals — it does not yet use `sp-admin-table`. That migration is intentionally deferred to 10X-G.
- The `sp-admin-header` backward-compat `<ng-content />` slot still works — admin shell pages that use `<sp-admin-header>` with general content projection are unaffected.

---

## AskUserQuestion decisions

None required.

---

## Test results

| Suite | Count | Delta |
|---|---|---|
| Angular tests | 373 passed | +24 (from 349) |
| .NET architecture | 3 passed | 0 |
| .NET unit | 1233 passed | 0 |
| .NET integration | 649 passed | 0 |
| Playwright | 188 passed | 0 (8 updated) |

---

## CI gate results

| Gate | Result |
|---|---|
| `git diff --check` | ✅ passed |
| `dotnet build --configuration Release` | ✅ passed (7 warnings, 0 errors) |
| `dotnet test --configuration Release` | ✅ 1885 passed |
| Angular production build | ✅ passed |
| Angular tests (ChromeHeadless) | ✅ 373 passed |
| Playwright `--workers=1` | ✅ 188 passed |

---

## Docs updated

- `docs/architecture/admin-tailadmin-adapter-inventory.md` — new rows for dropdown, table-actions, theme-toggle, updated table/header/filter-bar rows
- `docs/architecture/admin-ui-design-system.md` — Phase 10X-F section, sorting/dropdown/theme/filter-bar conventions
- `docs/sprints/current-sprint.md` — Phase 10X-F as active sprint with deliverables and gates
- `docs/handoffs/current-product-state.md` — Phase 10X-F section added
- `TODOS.md` — TODO-10X-F closed, TODO-10X-G added

---

## TODOs added / closed

- ~~TODO-10X-F~~ — **CLOSED** in this phase
- TODO-10X-G added: full admin page refactor using new wrappers (page-by-page redesign, remaining inline CSS removal)

---

## Known limitations

1. `sp-admin-table` sorting is emit-only — consumer must re-sort rows. No built-in sort. Intentional (enables server-side sort).
2. `AdminThemeService` applies `dark` class to `<html>` globally, not admin-route-scoped. Low risk now (student pages don't respond to `dark`).
3. `sp-admin-table-actions` uses CSS absolute positioning (no Popper.js). May clip in deeply nested scrollable containers. Acceptable for MVP admin use.
4. Students page row actions work via projected content — each action button has its own click handler. Not using the generic `[actions]` array API. Both patterns coexist intentionally.
5. Header user dropdown (profile/logout) not yet implemented as a `sp-admin-*` wrapper. The `[actions]` slot is ready for it.

---

## Explicit confirmation of out-of-scope items

The following were NOT implemented in Phase 10X-F, as required by the phase specification:

- Full admin page refactor / redesign
- 10R-F usage governance UX
- 10U AI Usage redesign
- 10V prompt playground
- Notification platform
- Enterprise auth / security
- Observability stack
- Billing
- StudentProfile.CefrLevel migration
- Full placement engine
- Full mastery engine

---

## Final verdict

Phase 10X-F complete. All acceptance criteria met. All gates pass.
The wrapper layer now has sufficient capability for the full admin page-by-page redesign (TODO-10X-G).

## Next recommended action

Begin TODO-10X-G: page-by-page admin UX redesign using the complete `sp-admin-*` wrapper set. Migrate remaining inline CSS from feature pages. Migrate students sorting to `sp-admin-table`. Add header user dropdown. Scope dark mode to admin layout boundary.
