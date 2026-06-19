# Phase 10X-K-17A — Critical Admin Form Primitives Review

**Date:** 2026-06-19
**Phase:** 10X-K-17A
**Type:** Implementation review
**Based on audit:** docs/reviews/2026-06-19-phase-10x-k-16a-admin-visual-consistency-audit.md
**Commit base:** 926dce8

---

## Goal

Replace all raw `<input type="number">`, raw `<input type="checkbox">`, and several raw `<input>`/`<select>` controls in admin pages with proper shared admin wrappers. Add `sp-admin-page-body` where missing. Standardize obvious alert/feedback cases.

---

## Components Created

### `sp-admin-number-input`

**Location:** `src/app/admin/components/number-input/sp-admin-number-input.component.ts`

- Implements `ControlValueAccessor` — emits `number | null` (null when field is cleared)
- Supports: `[(ngModel)]`, `formControl`, `[disabled]`, `[min]`, `[max]`, `[step]`, `[placeholder]`, `[ariaLabel]`, `[size]`
- Visually matches `sp-admin-input` (shared border, radius, focus ring, disabled styles)
- Does not coerce `null` to `0` — an empty field stays `null` in the model

### `sp-admin-checkbox`

**Location:** `src/app/admin/components/checkbox/sp-admin-checkbox.component.ts`

- Implements `ControlValueAccessor` — emits `boolean`
- Supports: `[(ngModel)]`, `formControl`, `[disabled]`, `[label]`, `[helper]`, `(checkedChange)`
- Custom styled checkbox box using CSS (no raw browser default styling)
- Check mark rendered via inline SVG for crisp cross-browser rendering

Both components exported from `src/app/admin/index.ts`.

---

## Pages Changed

### admin-integrations

**Files:** `admin-integrations.component.ts`, `admin-integrations.component.html`

- Replaced 11 raw `<input type="number" class="sp-adm-num-input">` with `<sp-admin-number-input>`
- Replaced 2 raw `<input type="checkbox" class="accent-blue-600 w-4 h-4">` with `<sp-admin-checkbox>`
- Removed `.sp-adm-num-input` local style rule (now dead)
- Replaced storage `<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">` Tailwind grid with scoped `.sp-int-form-grid`
- Replaced settings form Tailwind grid with same `.sp-int-form-grid`
- Replaced `<div class="flex flex-col gap-2 mt-3">` checkbox wrapper with `.sp-int-cb-stack`
- Replaced `<div class="flex items-center gap-4 mt-4">` test connection row with `.sp-int-test-row`
- Replaced `<div class="flex items-end gap-3 mb-4 flex-wrap">` generate row with `.sp-int-generate-row`
- Added `SpAdminNumberInputComponent`, `SpAdminCheckboxComponent` to imports array

### admin-usage-policies

**Files:** `admin-usage-policies.component.ts`, `admin-usage-policies.component.html`

- Added `<sp-admin-page-body>` wrapper (was missing, all other admin pages have it)
- Replaced `<input class="sp-input">` name/description inputs with `<sp-admin-input>`
- Replaced `<select class="sp-input">` scope type select with `<sp-admin-select>` using `scopeTypeOptions` array
- Replaced raw `<input type="checkbox">` default/active checkboxes with `<sp-admin-checkbox>`
- Replaced badge-in-card success notification with `<sp-admin-alert variant="success">`
- Replaced ad-hoc `.sp-admin-form-stack`, `.sp-admin-actions`, `.sp-admin-check` classes with scoped `.sp-up-form-stack`, `.sp-up-cb-stack`, `.sp-up-actions`
- Added `scopeTypeOptions` constant to component class
- Added `SpAdminAlertComponent`, `SpAdminInputComponent`, `SpAdminPageBodyComponent`, `SpAdminSelectComponent`, `SpAdminCheckboxComponent` to imports

### admin-prompts

**Files:** `admin-prompts.component.ts`

- Replaced 2 raw `<input class="sp-input" type="number">` (max input/output tokens) with `<sp-admin-number-input>`
- Replaced raw `<select class="sp-input sp-admin-status-select">` status filter with `<sp-admin-select>` using `statusFilterOptions` array
- Replaced `<p class="sp-admin-text-error">` form error with `<sp-admin-alert variant="error">`
- Removed `.sp-admin-status-select` local style rule (now dead)
- Added `statusFilterOptions` constant to component class
- Added `SpAdminAlertComponent`, `SpAdminNumberInputComponent`, `SpAdminSelectComponent` to imports

---

## Raw Controls Not Replaced in This Phase

The following raw controls were intentionally left unchanged:

| Location | Control | Reason |
|---|---|---|
| `admin-ai-config` — LLM provider/model selects | `<select [ngValue]="null">` | `SpAdminSelectComponent` only supports string values. The `[ngValue]="null"` option for "inherit" requires `ReactiveFormsModule`-style object binding. Replacing safely requires `sp-admin-select-object` (out of scope for 17A). |
| `admin-ai-config` — TTS provider/model selects | Same as above | Same reason. |
| `admin-usage-policies` — scope type select (edit mode hidden) | Replaced with `sp-admin-select` | Done — see above. |

The ai-config native selects are tracked as a deferred item for a future phase when `sp-admin-select` gains `[ngValue]` / object binding support.

---

## Alert Cleanup Done

| Location | Before | After |
|---|---|---|
| admin-usage-policies save success | `<sp-admin-card><sp-admin-badge>` | `<sp-admin-alert variant="success">` |
| admin-prompts form error | `<p class="sp-admin-text-error">` | `<sp-admin-alert variant="error">` |
| admin-ai-config inline save spans | Left as-is — per-card inline spans are a different pattern (not block alerts). Deferred. | — |

---

## Tests

### New spec files

- `src/app/admin/components/number-input/sp-admin-number-input.component.spec.ts` — 8 tests
  - Initial ngModel value renders correctly
  - User input emits numeric value
  - Empty field emits null
  - Null initial value renders as empty string
  - Disabled state propagates
  - min/max attributes set correctly

- `src/app/admin/components/checkbox/sp-admin-checkbox.component.spec.ts` — 8 tests
  - Initial checked/unchecked state renders
  - User check emits true
  - User uncheck emits false
  - Disabled state propagates
  - Label text renders
  - Helper text renders

### Updated specs

- `admin-integrations.component.spec.ts` — 3 new tests added
  - Settings signal holds numeric values
  - Settings signal holds boolean values for checkbox fields
  - Save payload contains correct numeric and boolean types

- `admin-usage-policies.component.spec.ts` — 3 new tests added
  - `sp-admin-page-body` wrapper renders
  - `scopeTypeOptions` contains Global and Student
  - Create payload includes correct scopeType

---

## Build Result

Production build: **PASS** — `dist/lingua-coach.web` generated, no errors.

## Angular Test Result

**628 / 628 PASS** — 0 failures.

(Total increased from 599 pre-phase to 628: +29 new tests across new component specs and updated page specs.)

## Playwright

Not run — no Playwright tests directly cover the changed admin form controls.

---

## Remaining Raw Form Controls After This Phase

| Location | Control | Status |
|---|---|---|
| `admin-ai-config` LLM/TTS provider+model selects | `<select [ngValue]="null">` | Deferred — needs `sp-admin-select` object binding support |
| `admin-ai-config` inline save spans (`text-xs text-emerald-600`) | Raw `<span>` | Deferred (Phase 17B structural cleanup) |

All other critical raw inputs (11 number inputs, 4 checkboxes, text inputs in usage-policies and prompts) have been replaced.

---

## Decisions Made

- `SpAdminNumberInputComponent` emits `number | null`, not `string`. This is intentional — the `GenerationSettings` model fields are `number`, not `string`. Emitting strings would require conversion at every save call.
- `SpAdminSelectComponent` was confirmed to only support string values. Native selects with `[ngValue]="null"` were left in ai-config for safety.
- The ai-config inline per-card save spans (`Saved` / error text) were left unchanged — they are a different UI pattern (inline row-level feedback, not block-level form feedback) and deserve their own treatment.
- `ChangeDetectionStrategy.OnPush` was evaluated and rejected for both new components — default change detection is simpler and consistent with all other admin shared components.

---

## Confirmation

- No backend, API, or product behavior changed.
- No new settings, fields, or API calls added.
- No commit or push performed.
- No Playwright tests run.
- Build: PASS. Angular tests: 628/628 PASS.
