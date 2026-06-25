# Review: Phase 10UI — Students List Design System Cleanup

**Date:** 2026-06-26
**Sprint:** 10UI Admin Design System Cleanup
**Component:** `/admin/students` — `AdminStudentsComponent`

---

## Files Changed

| File | Change |
|---|---|
| `src/app/features/admin/admin-students/admin-students.component.ts` | Removed inline template and styles; updated imports; added `Router`, `ActivatedRoute`; added `rowActions()`, `onRowAction()`, `resetDataCanSubmit()`, `onArchivedChange()`, `onPageSizeSelectChange()`; converted select options to `SpAdminNativeSelectOption[]` |
| `src/app/features/admin/admin-students/admin-students.component.html` | New file — extracted template |
| `src/app/features/admin/admin-students/admin-students.component.css` | New file — minimal page-scoped styles only |
| `src/app/design-system/admin/components/native-select/sp-admin-native-select.component.ts` | New shared component |
| `src/app/design-system/admin/index.ts` | Exported `SpAdminNativeSelectComponent` |

---

## Part A — Template Extraction

- Inline `template: \`...\`` block removed from `.ts`.
- Template moved to `admin-students.component.html`.
- Component now uses `templateUrl`.

## Part B+C — Inline Styles Removed

- Inline `styles: [...]` block removed entirely.
- A minimal `admin-students.component.css` file was created for layout tokens that are not available in shared components.
- All page-specific CSS classes replaced or renamed with `sp-adm-*` prefix.

### Remaining page-scoped CSS (justified)

| Selector | Reason |
|---|---|
| `.sp-adm-student-cell` | Avatar+name layout specific to Student column |
| `.sp-adm-avatar` | Round coloured initial avatar — no shared avatar exists |
| `.sp-adm-student-info` | Column flex layout |
| `.sp-adm-student-name` | Bold name style inside cell |
| `.sp-adm-sortable-th` | Clickable sort header — no shared sortable-th component |
| `.sp-adm-th-actions` | Right-aligns Actions header |
| `.sp-adm-nowrap` | Prevents date column wrap |
| `.sp-adm-table-footer` | Flex row for count + pagination |
| `.sp-adm-filter-spacer` | Pushes rows-per-page right in filter bar |
| `.sp-adm-rows-label` + `.sp-adm-rows-select` | Rows-per-page label+select pair |
| `.sp-adm-checkbox-group` | Stacked checkbox list in reset-data drawer |
| `.sp-adm-stack` | Vertical spacing inside slide-overs |
| `.sp-adm-col-span-2` | Full-width field inside form grid |
| `.sp-adm-mt-3` | Alert top margin after form grid |

No `style=""` inline attributes remain. No inline SVGs remain.

## Part D — Shared Input/Select/Checkbox

- New `SpAdminNativeSelectComponent` added to design system (`sp-admin-native-select`).
- Rows-per-page raw `<select>` replaced with `sp-admin-native-select`.
- Edit-form raw `<select class="sp-stu-select">` replaced with `sp-admin-native-select`.
- Reset-data preset select replaced with `sp-admin-native-select`.
- Show-archived raw `<input type="checkbox">` replaced with `sp-admin-checkbox`.
- Reset-password must-change raw checkbox replaced with `sp-admin-checkbox`.
- All reset-data checkboxes replaced with `sp-admin-checkbox`.

## Part E — Modals to Slide-Overs

- `sp-admin-modal` (Edit student) replaced with `sp-admin-slide-over` size="lg".
- `sp-admin-modal` (Reset password) replaced with `sp-admin-slide-over` size="sm".
- `sp-admin-modal` (Reset student data) replaced with `sp-admin-slide-over` size="md".
- No `sp-admin-modal` remains in this component.
- Footers use `[slot=footer]` at the top level of each slide-over (not inside `@if/@else` which causes NG8011).
- `sp-admin-form-grid` used for the edit form 2-column layout.
- `sp-admin-alert` used for all error/success messages.

## Part F — Inline SVG Removal

- All inline SVG action items removed from the table rows.
- `sp-admin-table-actions` now uses `[richActions]` API with icon keys: `view`, `edit`, `reset`, `archive`.
- `(actionSelected)` dispatches to `onRowAction()` which calls the appropriate handler.
- View profile navigates via `router.navigate([studentProfileId], { relativeTo: route })`.

## Part C — Table Layout

- Table uses `layout="first-column-fluid"` on `sp-admin-table`.
- Student column is fluid/wide; metadata columns are compact.
- Actions column header is right-aligned via `.sp-adm-th-actions`.

## Build Result

- Production build: **passed**.
- No new errors introduced by this phase.
- Pre-existing warnings remain in `AdminAiUsageComponent`, `AdminDiagnosticsComponent`, `PatternEvaluationResultComponent` — not introduced here.

## Remaining Visual Gaps

- No browser verification was performed (no running dev server in this session). Visual comparison against the target screenshot was not completed.
- Avatar style matches the target (round, coloured initials) but uses page-scoped CSS rather than a shared avatar component. A shared `sp-admin-avatar` component could be extracted in a future phase.
- Sortable column headers are page-scoped. A shared `sp-admin-sortable-th` directive or component could be extracted later.

## Risks

- `navigateToProfile` uses Angular Router with `relativeTo: this.route`. If the route configuration changes this may break. Original behaviour used `[routerLink]="[studentProfileId]"` which was identical relative navigation.
- `SpAdminNativeSelectComponent` coerces `[value]` strings back to numbers when the option values are numeric. This is correct for page size and edit-form selectors but should be noted for future use with mixed-type options.

---

## Documentation Impact

- Docs reviewed: `AGENTS.md`, `CLAUDE.md`, `docs/architecture/speakpath-design-system.md` (not read — no architectural change)
- Docs updated: this review doc
- Docs intentionally not updated: sprint doc, architecture docs, product state — this is a pure UI/template cleanup with no behaviour, API, or data model change
- Reason: No product behaviour, user flow, API contract, or architecture changed
