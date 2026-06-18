---
status: current
lastUpdated: 2026-06-19 10:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-H — Admin Form Wrapper CVA + Remaining Form/Modal Migration Foundation Review

## Date

2026-06-19

## Related sprint

Phase 10X-H (Admin UI design system, TailAdmin adapter track). Follows Phase 10X-G-F.

## Goal

Make the TailAdmin-backed `sp-admin-*` admin form wrappers safe for real Angular forms by
implementing `ControlValueAccessor` (CVA), then migrate remaining deferred form surfaces where safe.
This prepares the admin UI foundation for future enterprise screens (usage governance, quota
editors, student management modals, AI provider/model forms, integrations forms, prompt editor forms).

## Files reviewed

- `src/LinguaCoach.Web/src/app/admin/components/input/sp-admin-input.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/select/sp-admin-select.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/textarea/sp-admin-textarea.component.ts` (new)
- `src/LinguaCoach.Web/src/app/admin/components/form-field/sp-admin-form-field.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/admin-components.spec.ts`
- `src/LinguaCoach.Web/src/app/admin/index.ts`
- `docs/architecture/admin-ui-design-system.md`
- `docs/architecture/admin-tailadmin-adapter-inventory.md`

## CVA wrappers implemented

### sp-admin-input

- Implements `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`.
- Supports template-driven `[(ngModel)]` and reactive `[formControl]` / `formControlName`.
- `writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState` all implemented.
- Inner native `<input>` uses `[value]` + `(input)` + `(blur)` (no inner `ngModel`, avoids cycle).
- Value change propagates up via `onChange`; touched marked on blur via `onTouched`.
- Disabled state propagates from a disabled `FormControl` and disables the native input.
- Pass-through inputs: `type`, `placeholder`, `autocomplete`, `readonly`, `required`, `invalid`.
- TailAdmin focus/error/disabled styling preserved (`.sp-adm-input`, `.sp-adm-input-error`).

### sp-admin-select

- Implements `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`.
- Supports `[(ngModel)]` and reactive forms.
- Options via `[options]` (`{ value, label }[]`) or projected `<option>` content.
- `placeholder` renders a disabled default option when value is empty.
- Disabled propagation and touched-on-blur implemented.
- Inputs: `options`, `placeholder`, `required`, `invalid`.
- TailAdmin select styling preserved.

### sp-admin-textarea (new component)

- New wrapper created and exported from `admin/index.ts`.
- Implements `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`.
- Supports `[(ngModel)]` and reactive forms.
- Disabled propagation and touched-on-blur implemented.
- Inputs: `rows`, `placeholder`, `readonly`, `required`, `invalid`.
- TailAdmin textarea styling preserved; `resize: vertical`.

### sp-admin-form-field

- Renders label, hint, error slot, and projected control.
- Red `*` required marker added via `[required]`.
- Error replaces hint when set. Works with CVA controls or native controls projected inside.

## Migrated form pages/modals

None this phase. The CVA foundation was the deliverable. Per-page form migration was intentionally
deferred to avoid behavior risk in dense, ngModel-driven save flows. See deferred list below.

## Wrappers/forms intentionally deferred with reasons

- AI Config dense provider-credentials grid (TODO-10X-G-AICONFIG-FORMS): high field count and
  ngModel-driven conditional option logic. Migrating every field in a single pass risks silent save
  regressions. CVA now unblocks a dedicated per-field migration pass.
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS): same risk profile, deferred to a
  dedicated pass.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL): still page-local modal markup.
- `sp-admin-modal` enhancements: not required this phase; existing behavior unchanged.

## Native controls intentionally remaining with reasons

- AI Config `.sp-ai-select` provider/model/voice inputs and credentials grid — see above.
- Integrations operational form inputs — see above.
- Any multi-select, `<datalist>`, file inputs, or `multiple` controls: the single-value string CVA
  wrappers cannot safely represent these yet. Documented in admin-ui-design-system.md.

## Dark mode / admin-only status

No dark-mode changes. `AdminThemeService` remains isolated via the `adminTheme` localStorage key.
The admin-only dark-mode class boundary stays open as TODO-10X-G-DARKMODE. No risky global rewrite
attempted. Student UI theme untouched.

## Visual validation summary

Playwright e2e suite (188 tests) passed, exercising admin pages and student smoke flows. Admin
screenshot baselines (admin-02-students, admin-diagnostics) updated. No visual regressions surfaced.

## Test counts

- .NET: 1885 passed (3 architecture + 1233 unit + 649 integration), 0 failed.
- Angular: 394 passed (up from 379), 0 failed. +15 new CVA wrapper specs.
- Playwright: 188 passed, 0 failed.

## CI gate results

| Gate | Result |
|---|---|
| git diff --check | pass |
| dotnet restore | pass |
| dotnet build --configuration Release | pass (0 warnings, 0 errors) |
| dotnet test --configuration Release | pass (1885) |
| npm ci | pass |
| npm run build --configuration production | pass |
| npm test --watch=false --browsers=ChromeHeadless | pass (394) |
| npx playwright test --workers=1 | pass (188) |

Note: the local Windows environment dropped three optional native binaries during `npm ci`
(`lightningcss-win32-x64-msvc`, `@tailwindcss/oxide-win32-x64-msvc`, `@rollup/rollup-win32-x64-msvc`),
a known npm optionalDependencies bug (npm/cli#4828). Restored by fetching each platform tarball via
`npm pack` and copying the `.node` binary into place. No source or lockfile change required.

## New CVA wrapper specs added

1. input ngModel write/display
2. input reactive FormControl set
3. input propagates value change to form
4. input propagates disabled from FormControl
5. input marks touched on blur
6. select ngModel
7. select reactive FormControl
8. select disabled propagation
9. select marks touched on blur
10. textarea writes initial ngModel value
11. textarea propagates typed value to ngModel
12. textarea reactive FormControl + touched on blur
13. form-field renders label, hint, required marker, projected control
14. form-field shows error instead of hint when error set
15. supporting host-component fixtures for the above

## Findings by priority

- P1: none. CVA implementations follow the prescribed pattern (no inner ngModel, no value cycle).
- P2: per-field page migrations remain pending. Tracked in TODOs; foundation now unblocks them.
- P3: dark-mode boundary still open (TODO-10X-G-DARKMODE). Out of scope this phase.

## Decisions made

- CVA controls bind a single `string` value; multi-value/multi-select controls stay native.
- Page form migration deferred to dedicated per-field passes to protect save flows.
- No modal changes since no page modal migration was performed.

## Implementation tasks produced

- TODO-10X-G-AICONFIG-FORMS: now unblocked, awaiting per-field migration pass.
- TODO-10X-G-INTEGRATIONS-FORMS: now unblocked, awaiting per-field migration pass.
- TODO-10X-D-MODAL: student modal migration still open.
- TODO-10X-G-DARKMODE: admin-only dark-mode boundary still open.

## Risks / unresolved questions

- Local optional-binary install flakiness is environment-specific, not a repo defect. CI on Linux
  is unaffected.
- Future per-field migrations must re-verify each two-way binding to avoid silent save regressions.

## Final verdict

Approved. CVA foundation delivered, tested, and documented. All eight gates pass. Scope held.

## Next recommended action

Begin the dedicated per-field migration pass for AI Config and Integrations forms using the new CVA
wrappers, verifying each save flow against existing behavior.

## Explicit non-scope confirmation

Not implemented in this phase: full 10R-F usage governance UX, 10U AI Usage redesign, 10V prompt
playground, notification platform, enterprise auth/security, observability stack, billing/
subscriptions, StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

## Documentation impact

- Docs reviewed: admin-ui-design-system.md, admin-tailadmin-adapter-inventory.md,
  current-sprint.md, current-product-state.md, TODOS.md.
- Docs updated: admin-tailadmin-adapter-inventory.md (CVA status + textarea row + 10X-H phase),
  admin-ui-design-system.md (timestamp; CVA rule already present), current-sprint.md (10X-H active
  sprint), current-product-state.md (10X-H section), TODOS.md (closed TODO-10X-FORMS-CVA, annotated
  AICONFIG/INTEGRATIONS as unblocked), this review doc (new).
- Docs intentionally not updated: backend/AI/architecture layer docs.
- Reason: change is admin UI wrapper only. No product behavior, API contract, DB model, AI prompt,
  or backend logic changed.
