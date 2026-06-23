# Phase 10UI-FIX-6 — Students Header, Create Student Wrapper Migration, Curriculum Filter Bar

**Date:** 2026-06-23
**Sprint:** Current (10UI series)
**HEAD before work:** be798b0
**Related phases:** 10UI-FIX-4 (identified gaps)

---

## Scope

Three admin page-level alignment items from the 10UI-FIX-4 matrix:

1. Students list: header action button uses raw CSS class instead of `sp-admin-button`.
2. Create Student: entire page uses raw `sp-input`/`sp-label`/`.sp-admin-form-card` — no `sp-admin-*` wrappers.
3. Curriculum: filter bar selects use raw `<select class="sp-input">` — not `sp-admin-select`; page body not wrapped in `sp-admin-page-body`.

---

## Reference Files Inspected

| File | Outcome |
|------|---------|
| `docs/design/speakpath/admin/pages/students.jsx` | Confirmed: header has `adm-btn adm-btn-indigo` "Create student" action — Angular equivalent is `sp-admin-button` |
| `docs/design/speakpath/admin/pages/create-student.jsx` | Confirmed: form sections use `adm-card adm-card-p` — Angular equivalent is `sp-admin-card`; fields use `Field` wrapper — equivalent is `sp-admin-form-field` + `sp-admin-input` |
| `docs/design/speakpath/admin/pages/curriculum.jsx` | Note: reference does not have a filter bar — it shows static track cards. Angular implementation is richer (objectives table + filters). Filter bar already exists; selects needed wrapper migration only. |

---

## 1. Students List — Header Action Button

**Change:** `<a routerLink="../create-student" class="sp-admin-btn-primary">` → `<sp-admin-button routerLink="../create-student">`.

**Why:** Using a raw `<a>` with page-local CSS class bypasses the token system wired into `sp-admin-button`. The wrapper handles hover states, focus rings, and disabled state consistently with all other admin pages.

**Behaviours preserved:** Route unchanged (`../create-student`), button text unchanged, list filters/pagination unaffected.

---

## 2. Create Student — Wrapper Migration

### Wrappers added

| Before | After |
|--------|-------|
| `<div class="sp-admin-page-header">` raw div | `sp-admin-page-header` |
| No page body | `sp-admin-page-body` |
| `<div class="sp-admin-form-card">` | `sp-admin-card` |
| `<div class="sp-alert-error">` | `sp-admin-alert variant="error"` |
| `<div class="sp-alert-success">` | `sp-admin-alert variant="success"` |
| `<label class="sp-label">` + `<input class="sp-input">` | `sp-admin-form-field` + `sp-admin-input` |
| `<button class="sp-admin-btn-primary">` submit | `sp-admin-button` |

### Selects kept native

`sessionDuration`, `experienceLevel`, `roleFamiliarity` selects use `[ngValue]` with number/null values. `sp-admin-select` CVA writes strings — incompatible. These remain native `<select>` elements wrapped in `sp-admin-form-field` for label/hint alignment. This matches the established pattern from `admin-ai-config` (see TODOS.md TODO-10X-J-SELECT-OBJECT).

### Behaviours preserved

- `onSubmit()` payload unchanged — same field bindings, same `AdminService.createStudent()` call.
- `validate()` logic unchanged — email/password errors still display.
- Optional profile field section toggle unchanged.
- `mustChangePassword` checkbox preserved.
- `startAnother()` reset logic unchanged.
- Temp password security wording preserved.
- 409 conflict / 500 error handling preserved.

---

## 3. Curriculum — Filter Bar + Page Body

### Changes

- Added `SpAdminPageBodyComponent` to imports and wrapped page content in `<sp-admin-page-body>`.
- Added `SpAdminSelectComponent` + `SpAdminSelectOption` to imports.
- Added `computed` import to component.
- Added `cefrOptions`, `skillOptions`, `activeOptions` derived from `taxonomy()` signal.
- Replaced three raw `<select class="sp-input">` in filter bar with `<sp-admin-select [options]="..." [(ngModel)]="..." (ngModelChange)="load()">`.

### Behaviours preserved

- Filter values (`filterCefr`, `filterSkill`, `filterActive`) remain plain strings — fully compatible with `sp-admin-select` CVA.
- `load()` called on change — same trigger pattern.
- `listObjectives()` call semantics unchanged.
- All create/edit/preview/activate/deactivate flows unaffected.

---

## 4. Files Changed

| File | Change |
|------|--------|
| `admin-students/admin-students.component.ts` | Header `<a class="sp-admin-btn-primary">` → `<sp-admin-button routerLink>` |
| `admin-students/admin-students.component.spec.ts` | Added "Create student button via sp-admin-button wrapper" test |
| `create-student/create-student.component.ts` | Added `SpAdminAlertComponent`, `SpAdminButtonComponent`, `SpAdminCardComponent`, `SpAdminFormFieldComponent`, `SpAdminInputComponent`, `SpAdminPageBodyComponent`, `SpAdminPageHeaderComponent` to imports |
| `create-student/create-student.component.html` | Full wrapper migration (page-header, page-body, card, form-field, input, alert, button); native selects retained for ngValue |
| `create-student/create-student.component.spec.ts` | **New file** — 17 tests covering render, validation, submit payload, error states, optional fields, security wording |
| `admin-curriculum/admin-curriculum.component.ts` | Added `SpAdminPageBodyComponent`, `SpAdminSelectComponent`, `SpAdminSelectOption`; added `computed`; added `cefrOptions`, `skillOptions`, `activeOptions`; replaced raw selects; added `sp-admin-page-body` wrapper |
| `admin-curriculum/admin-curriculum.component.spec.ts` | Added `sp-admin-page-body` assertion to existing render test; added new "renders sp-admin-select wrappers in filter bar" test |

---

## 5. Tests Added / Updated

| Spec | Tests added |
|------|------------|
| `admin-students.component.spec.ts` | 1 (Create student button via sp-admin-button) |
| `create-student.component.spec.ts` | 17 (new file) |
| `admin-curriculum.component.spec.ts` | 2 (page-body assertion + sp-admin-select filter bar) |

Total new tests: **20**. Previous count: 1045. New total: **1065/1065**.

---

## 6. Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1065/1065 |
| Backend | No changes |
| Playwright | Not run — no route/navigation behaviour changed |

---

## 7. Deferred

- Student detail full wrapper migration (`sp-admin-page-header`, `sp-admin-badge` wrappers, readiness pool, activity history) → **10UI-FIX-7**
- Notifications SMS "foundation only" label, security deferred-feature notes, integrations readiness pool → **10UI-FIX-8**
- AI Config native selects evaluation (`[ngValue]="null"`) → **10UI-FIX-9**
- Dashboard full redesign (area chart, engagement metrics, at-risk students) → future
- Onboarding admin flow viewer → TODO-UI-04
- Curriculum table/card redesign to match reference tracks view → future (current objectives table is richer than reference)

---

## 8. Confirmation

- No backend/API/migration changes.
- No student-facing UI changes.
- No full page redesigns.
- No new admin pages.
- No new major components.
- Only wrapper migrations and filter bar alignment applied.
