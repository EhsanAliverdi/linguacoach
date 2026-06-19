# Phase 10X-K-5 — Admin Action Menu Foundation Review

**Date:** 2026-06-19
**Sprint:** 10X-K-5 Admin Action Menu Foundation
**Scope:** Frontend only — `sp-admin-table-actions` and page action wiring
**Reviewer:** Claude Code

---

## Files Reviewed

- `src/LinguaCoach.Web/src/app/admin/components/table-actions/sp-admin-table-actions.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/admin-components.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html`

---

## Findings

### sp-admin-table-actions — pre-existing state

The component already had a solid foundation:

- Three-dot SVG trigger button (not raw `...` text) — intentional and styled.
- `aria-expanded`, `aria-haspopup="menu"`, `aria-label="Row actions"` on the trigger.
- `role="menu"` on the dropdown panel.
- Click-outside close via `HostListener('document:mousedown')`.
- Escape-to-close via `HostListener('document:keydown.escape')`.
- Two usage modes: `[actions]` array input (generates items with `role="menuitem"`) and `ng-content` projection for fully custom items.

### Issues found

#### 1. Projected items lacked styling (P1)

When pages used the `ng-content` projection path, items were styled with verbose inline Tailwind class strings repeated on every button and anchor:

```
class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
```

Danger items repeated a different long class string. This was fragile, inconsistent, and impossible to update globally.

**Fix:** Added `::ng-deep .sp-adm-action-item` styles to the component so the single class `sp-adm-action-item` provides full item appearance (display, width, padding, font-size, color, hover, focus-visible). Added `::ng-deep .sp-adm-action-danger` for destructive item appearance. Added `::ng-deep` disabled styles for `[disabled]` and `.sp-adm-action-disabled`.

#### 2. Projected items lacked `role="menuitem"` (P2)

Items in the ng-content path had no ARIA role, making them invisible to assistive technology and inconsistent with the array-mode items which get `role="menuitem"` via the component template.

**Fix:** Added `role="menuitem"` to all projected `<button>` and `<a>` items in `admin-students` and `admin-prompts`.

#### 3. Danger item inconsistency (P2)

Destructive actions (Reset data, Archive) in `admin-students` used `text-red-600` inline Tailwind but had no semantic class to distinguish them. The array-mode supported `danger: true` on action objects.

**Fix:** Replaced inline Tailwind danger styling with `sp-adm-action-danger` class on destructive projected items.

### Pages with no row actions

- `/admin/exercise-types`: uses inline `sp-admin-button` elements per row (Toggle/Save counts), not a dropdown. This is appropriate — these are inline editing controls, not a menu. No change.
- `/admin/diagnostics`: log events — no row actions. No change.
- `/admin/usage` (ai-usage): read-only usage data tables — no row actions. No change.
- `/admin/integrations`: batch actions use `sp-admin-button` inline per row (Cancel/Retry). Appropriate for the use case. No change.

---

## Changes Made

### `sp-admin-table-actions.component.ts`

- Added `::ng-deep .sp-adm-action-item` base styles — replaces per-item Tailwind verbosity.
- Added `::ng-deep .sp-adm-action-item.sp-adm-action-danger` — red color + red hover background.
- Added `::ng-deep .sp-adm-action-item:disabled` / `[disabled]` / `.sp-adm-action-disabled` — opacity 0.4 + no-pointer-events.
- Added `::ng-deep .sp-adm-action-item:focus-visible` — indigo outline ring.
- No template or TypeScript logic changed.

### `admin-students.component.ts`

- Stripped verbose inline Tailwind from all projected action items.
- Added `role="menuitem"` to all projected items.
- Replaced `text-red-600 hover:bg-red-50` inline classes with `sp-adm-action-danger`.

### `admin-prompts.component.ts`

- Added `role="menuitem"` to all projected action items.
- Items already used only `sp-adm-action-item` class — no Tailwind stripping needed.

### `admin-components.spec.ts`

- Added `TableActionsProjectedHostComponent` — host component for ng-content projection tests.
- Added 4 new tests:
  - trigger renders (projected mode)
  - menu opens on trigger click (projected mode)
  - clicking projected item calls handler
  - menu closes on Escape (projected mode)

---

## Decisions Made

1. The three-dot SVG trigger was already correct and intentional. No trigger redesign needed.
2. The `[actions]` array API was already complete. Focused changes on the ng-content path.
3. `::ng-deep` is the correct mechanism for styling projected content in Angular standalone components. Scoped to `:host` so no bleed.
4. No new product actions added. No drawers or modals introduced.
5. `admin-exercise-types` inline buttons are deliberate inline controls, not a dropdown menu. Left unchanged.

---

## Gates

- `git diff --check`: clean
- Production build: **PASS**
- Angular tests: **441/441 PASS** (4 new tests added, ChromeHeadless)
- Playwright: not run — no existing admin action-menu Playwright tests in scope
- .NET tests: not run (frontend-only phase)

---

## Remaining Action Menu Issues (future phases)

- The `[actions]` array mode and ng-content mode are parallel paths in the component. A future phase could unify them with a single `<sp-admin-action-item>` child component, but this is not needed at current scale.
- `admin-exercise-types` could benefit from a dropdown if the action count per row grows, but current Toggle + Save counts is appropriate.

---

## Confirmation

- No backend changes made.
- No API behavior changed.
- No new product actions added.
- No commit or push performed.
