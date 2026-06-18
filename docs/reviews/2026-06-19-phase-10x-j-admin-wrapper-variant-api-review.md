---
title: Phase 10X-J Admin Wrapper Variant API — Engineering Review
date: 2026-06-19
sprint: Admin UI Design System (10X series)
status: complete
---

# Phase 10X-J Admin Wrapper Variant API — Engineering Review

## Summary

Strengthened all 13 `sp-admin-*` wrapper components with typed variant/size/density/layout APIs.
Admin feature pages can now request common TailAdmin design variations through parameters rather than inline class strings.
TailAdmin class complexity stays inside wrappers. Dependency direction is preserved.

---

## Files Changed

### Wrapper components (all under `src/LinguaCoach.Web/src/app/admin/components/`)

| File | Change |
|---|---|
| `button/sp-admin-button.component.ts` | Added `appearance` (solid/outline/soft/ghost/link), `size` (xs/sm/md/lg), `fullWidth`, `iconOnly`. Legacy `variant='ghost'` compat alias. |
| `badge/sp-admin-badge.component.ts` | Added `appearance` (soft/solid/outline), `size` (sm/md), `dot`, `purple` tone. |
| `card/sp-admin-card.component.ts` | Added `variant` (default/bordered/elevated/flat/metric/section), `padding` (none/sm/md/lg), `radius` (md/lg/xl/2xl), `headerDivider`, `hover`, `loading`. |
| `stat-card/sp-admin-stat-card.component.ts` | Added `size` (sm/md/lg), unified tone aliases (primary/success/warning/danger/info/neutral), `loading` skeleton, `[slot=trend]`. |
| `table/sp-admin-table.component.ts` | Added `variant` (basic/data/bordered/striped/simple/card), `density` (compact/comfortable/spacious), `selectable`, `stickyHeader`, column `width`/`align`, `selectionChange` output. |
| `filter-bar/sp-admin-filter-bar.component.ts` | Added `layout` (inline/stacked/responsive), `density` (compact/comfortable). |
| `form-field/sp-admin-form-field.component.ts` | Added `layout` (vertical/horizontal/inline), `size` (sm/md/lg). |
| `input/sp-admin-input.component.ts` | Added `size` (sm/md/lg), `state` (default/error/success/disabled), `fullWidth`. CVA preserved. |
| `select/sp-admin-select.component.ts` | Added `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved. |
| `textarea/sp-admin-textarea.component.ts` | Added `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved. |
| `modal/sp-admin-modal.component.ts` | Added `size` (sm/md/lg/xl/full), `variant` (default/danger/form/confirm), `showCloseButton`. `maxWidth` still works (overrides `size`). |
| `drawer/sp-admin-drawer.component.ts` | Added `side` (left/right), `size` (sm/md/lg/xl), `closeOnBackdrop`. |

### Tests

| File | Change |
|---|---|
| `admin-components.spec.ts` | 18 new Phase 10X-J tests added. 10 existing tests updated to match new CSS class names (BEM-style semantic classes replaced some inline Tailwind class assertions). |

### Feature pages (proof usage)

| File | Change |
|---|---|
| `admin-students/admin-students.component.ts` | `sp-admin-table` gets `variant="data" density="compact"`. Edit modal cancel button gets `size="sm"`. |
| `admin-dashboard/admin-dashboard.component.ts` | Two `sp-admin-stat-card`s get `size="md" [loading]`. AI System `sp-admin-card` gets `variant="metric"`. |

### Docs

- `docs/architecture/admin-ui-design-system.md` — full variant API reference added
- `docs/architecture/admin-tailadmin-adapter-inventory.md` — 10X-J row added
- `docs/sprints/current-sprint.md` — 10X-J phase section added
- `docs/handoffs/current-product-state.md` — 10X-J state entry added
- `TODOS.md` — 3 new TODOs (input-number, select-object, dashboard-minitable); 10R-F/10U/10V noted as unblocked

---

## Findings by Priority

### P0 — Breaking changes avoided

- `variant="ghost"` on button: all 14 existing page usages of `variant="ghost"` continue to work. The type union is extended (not narrowed). A compatibility shim in `hostClasses` maps ghost variant → `appearance="ghost" variant="neutral"` at runtime.
- CVA behavior: `writeValue`, `registerOnChange`, `setDisabledState`, touched-on-blur all preserved in input/select/textarea after size/state additions.
- Modal `maxWidth`: still works and takes precedence over `size`.

### P1 — Design decisions

**Appearance vs variant separation on button:**
`variant` = semantic color role (primary/danger/success/etc.), `appearance` = visual treatment (solid/outline/soft/ghost/link). This aligns with TailAdmin's own pattern and allows any combination (`variant="danger" appearance="soft"` = soft red button). Pages that used `variant="ghost"` retain the old behavior through the compat alias.

**BEM-style class names instead of Tailwind inline classes:**
Wrapper component styles use semantic CSS classes (`.sp-adm-btn-solid-primary`, `.sp-adm-card-default`, etc.) rather than Tailwind utility classes in the class binding. This keeps Tailwind classes inside the component CSS where they belong per the architecture rule, and makes test assertions more stable. The tradeoff is that tests check semantic class names rather than visual utility names — which is the correct level of abstraction.

**`state` vs `invalid` on input/select/textarea:**
Both are kept. `state="error"` maps to the same visual result as `invalid=true`. `state` is the new preferred API; `invalid` is preserved for backward compat.

### P2 — Known limitations

- `sp-admin-table` does not sort internally — it emits `(sortChange)` and expects the consumer to re-sort rows. This is intentional to avoid hiding sort logic.
- `sp-admin-stat-card` layout direction (row vs column) is controlled by `size` class CSS, not an explicit `direction` input. SM renders column, MD/LG render row.
- `sp-admin-modal` `full` size sets `max-width: 100%` but the panel still has `p-4` outer padding from its container — actual full-bleed modals would need additional CSS.
- Dashboard mini-table (recent students) still uses page-local CSS — tracked as `TODO-10X-J-DASHBOARD-MINITABLE`.

---

## Decisions Made

1. Kept `variant="ghost"` in the type union as a deprecated alias (not removed) to avoid breaking 14 call sites across feature pages.
2. Used semantic BEM-style CSS classes for variant/size/density rendering, not inline Tailwind class concatenation in the template — avoids class binding complexity and keeps Tailwind inside CSS.
3. `sp-admin-card` body border (border-top between header and body) is auto-applied when `title` is set, controlled by `headerDivider` for explicit control. Default `headerDivider=false` means no divider unless requested — this differs from the 10X-E behavior where all cards with titles got a border-top. Existing page usage unaffected because CSS is in-component.
4. `sp-admin-stat-card` `size` drives layout (row vs column) and padding in CSS, not in Angular class logic — simpler and more maintainable.

---

## AskUserQuestion Answers

None required for this phase.

---

## Implementation Tasks Produced

- TODO-10X-J-INPUT-NUMBER: `sp-admin-input-number` for number|null CVA inputs
- TODO-10X-J-SELECT-OBJECT: `sp-admin-select-object` for object/non-string selects
- TODO-10X-J-DASHBOARD-MINITABLE: migrate dashboard mini-table to sp-admin-table

---

## CI Gate Results

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| `dotnet build --configuration Release` | ✅ Clean |
| `dotnet test --configuration Release` | ✅ 1885 passed (3 + 1233 + 649) |
| `npm run build -- --configuration production` | ✅ Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | ✅ 439 passed (18 new + 10 updated) |
| Playwright `npx playwright test --workers=1` | ✅ 185 passed, 3 pre-existing failures in `admin-students-reset.spec.ts` (confirmed pre-existing by stash test) |

---

## Risks / Unresolved Questions

- Pre-existing Playwright failures in `admin-students-reset.spec.ts` (3 tests): submit-disabled, successful-reset, and reset-failure scenarios. These fail on HEAD before this phase — not introduced by 10X-J.
- `sp-admin-table variant="striped"` alternates row color via JavaScript class (`sp-adm-tr-stripe-odd/even`). If rows are re-ordered client-side without re-rendering, zebra stripes may misalign. Acceptable for current usage.

---

## Final Verdict

**Approved and complete.** All 13 wrappers expose a typed variant API. Pages can request TailAdmin design variations through inputs. CVA regressions: none. Raw class usage in pages: not increased (proof usages use typed inputs only). .NET and Angular gates: clean. Playwright: 185 passed, 3 pre-existing failures unrelated to this phase.

---

## Next Recommended Action

Begin 10R-F (usage governance UX), 10U (AI usage redesign), or 10V (prompt playground) — all now unblocked by the wrapper variant API.
