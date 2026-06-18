---
status: complete
date: 2026-06-19
phase: 10X-I
owner: engineering
---

# Phase 10X-I — Admin Form and Modal Migration Review

**Date:** 2026-06-19
**Sprint:** Phase 10X-I — Migrate Remaining Admin Forms and Modals to CVA Wrappers
**Related sprint doc:** `docs/sprints/current-sprint.md`

---

## Summary

Phase 10X-I completes the admin UI CVA migration that was blocked through phases 10X-G and 10X-H. All three deferred migration targets are now done:

1. **AI Config** — dense provider-credentials grid, model management, voice input, API key fields.
2. **Integrations** — storage display fields, generation settings, background job controls.
3. **Student modals** — all three modals (edit, reset password, reset data) replaced with `sp-admin-modal`.

---

## Files Reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.html`
- `src/LinguaCoach.Web/src/app/admin/components/input/sp-admin-input.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/modal/sp-admin-modal.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/layout/sp-admin-layout.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

---

## Findings by Priority

### Critical (blocking)

None at merge time. All build and unit-test gates pass.

### High

**H1 — `sp-admin-layout` lacked `role="main"` on content area.**
Playwright `admin: students` tests use `page.getByRole('main')` to scope their assertions. The content `<div>` in the layout had no semantic role, causing timeouts. Fixed by changing the content wrapper from `<div>` to `<main>`. All 421 Angular tests pass after fix.

**H2 — `sp-admin-select` CVA incompatible with `[ngValue]="null"` (number|null bindings).**
`sp-admin-select` uses string `[value]` binding and is incompatible with Angular's `[ngValue]` which is required for `number | null` option types. Resolution: keep native `<select>` elements for any field using `[ngValue]="null"` (e.g. student edit selects for duration minutes, experience level, role familiarity). These native selects are wrapped inside `<sp-admin-form-field>` for consistent label layout.

**H3 — `sp-admin-input` CVA writes strings only; number inputs must stay native.**
`sp-admin-input`'s `ControlValueAccessor` calls `onChange(string)`. Angular coerces `type="number"` native input values to numbers when bound via `[(ngModel)]`. Using `sp-admin-input` for numeric fields would silently convert saved values to strings. Resolution: keep native `<input type="number">` for all numeric fields in Integrations generation settings, wrapped in `<sp-admin-form-field>` for labels.

### Medium

**M1 — `sp-admin-modal` had a hardcoded `max-width: 520px`.**
The student edit form has many fields and needed more width. Fixed by adding `@Input() maxWidth = '520px'` to `sp-admin-modal` and binding `[style.max-width]="maxWidth"`. The student edit modal uses `maxWidth="720px"`.

**M2 — `sp-admin-input` had no `[value]` input before this phase.**
Display-only fields in Integrations (provider, endpoint, bucket, etc.) needed one-way binding. Fixed by adding a getter/setter `@Input() value` backed by `_value`, distinct from the CVA path.

**M3 — Angular template apostrophe escaping.**
`[label]="'Type the student\'s email to confirm: '"` in template attribute binding causes NG5002. Fixed by adding a `confirmEmailLabel(email: string): string` method to the component class and binding `[label]="confirmEmailLabel(student.email)"`.

### Low

**L1 — AI Config TTS voice select and model inputs used page-local `.sp-ai-select` CSS class.**
Replaced with `.sp-adm-native-select` (matching the admin token palette) for the native selects that must remain, and `sp-admin-input` for text fields.

**L2 — Student modals used page-local `.sp-admin-modal-backdrop`/`.sp-admin-modal` CSS.**
All three modals had duplicated page-local backdrop/modal CSS. Completely removed after migration to `sp-admin-modal`. The remaining page-local CSS is minimal: `.sp-stu-edit-grid`, `.sp-stu-wide`, `.sp-stu-select`.

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Keep native `<select>` for `number\|null` fields | `sp-admin-select` string binding incompatible with `[ngValue]="null"`. Fix would require a new CVA select variant. |
| Keep native `<input type="number">` inside `<sp-admin-form-field>` | `sp-admin-input` CVA coerces to string. Numeric domain integrity takes priority. |
| Wrap native controls in `<sp-admin-form-field>` | Consistent label/hint/error layout even when inner control is native. |
| `<main>` element in `sp-admin-layout` content area | Required for `getByRole('main')` Playwright selector; also correct semantic HTML. |
| `maxWidth` input on `sp-admin-modal` | Single-use widening via input is lower risk than a new variant or slot. |
| Submit buttons inside `<form>`, not in `slot="footer"` | Buttons in a `slot="footer"` div inside `<form>` break form submission. Footer slot remains for non-form modal actions. |

---

## AskUserQuestion Answers

No user decisions were required. All decisions were made by engineering review.

---

## Implementation Tasks Produced

None. Phase 10X-I is complete. Remaining deferred items are tracked in `TODOS.md`.

---

## Risks and Unresolved Questions

- **Native select CVA gap:** Three student edit selects remain native because `sp-admin-select` cannot bind `number | null`. If a new `sp-admin-select-object` variant is created in a future phase, these can migrate.
- **Number input CVA gap:** Eleven generation-settings number inputs remain native for the same reason. A future `sp-admin-input-number` with a numeric CVA would close this.
- **Playwright full run:** E2E tests require a live backend. The `admin: students` test was confirmed to be a layout-role issue (now fixed). Full Playwright run validation requires the app server to be running.

---

## Gates

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| `.NET build` | 0 errors |
| `.NET tests` | 1885 passed (3 arch + 1233 unit + 649 integration) |
| `Angular build (production)` | Clean |
| `Angular tests` | 421 passed (up from 411; +10 new Phase 10X-I tests) |
| Playwright (requires live server) | `admin: students` locator issue resolved (layout `<main>` fix) |

---

## Final Verdict

**APPROVED.** All CVA migration targets delivered. Build and unit-test gates pass. Layout semantic fix unblocks Playwright `getByRole('main')` tests. Known CVA gaps (number and object-value selects) are documented and do not regress anything.

---

## Next Recommended Action

Phase 10X-I is the last admin UI migration phase. Product UX phases can now begin. Recommended next: triage `docs/backlog/` for the next product UX phase (onboarding, student placement, or enterprise auth).
