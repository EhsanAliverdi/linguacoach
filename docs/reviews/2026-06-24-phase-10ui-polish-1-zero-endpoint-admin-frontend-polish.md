# Phase 10UI-POLISH-1 — Zero-Endpoint Admin Frontend Polish

**Date:** 2026-06-24
**Sprint / Feature:** Phase 10UI-POLISH-1
**Commit message:** `ui: polish admin frontend interactions`

---

## Files Modified

- `src/LinguaCoach.Web/src/app/design-system/admin/components/toggle/sp-admin-toggle.component.ts` (new)
- `src/LinguaCoach.Web/src/app/design-system/admin/components/toggle/sp-admin-toggle.component.spec.ts` (new)
- `src/LinguaCoach.Web/src/app/design-system/admin/index.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
- `TODOS.md`

---

## Findings and Decisions

### Task 1: Computed pending-actions list (TODO-VISUAL-08) — DONE

Added `pendingActions` computed signal on `admin-dashboard.component.ts`. Derives from:
- `aiCategories()` — categories where `providerName === null` (unconfigured)
- `students()` — students with no `cefrLevel` and not Archived, and students with `onboardingStatus === 'NotStarted'`

Both signals were already loaded on dashboard init. No new API calls added.

Rendered as a "Pending actions" card below the recent students table. Shows a polished empty state if no actions exist. Each action links to the relevant admin route (`/admin/ai-config` or `/admin/students`). Coloured dot indicators: warning (amber) for AI config, info (blue) for student actions.

Scope boundary respected: no new backend endpoints, no fake data.

### Task 2: `sp-admin-toggle` component (TODO-VISUAL-10) — DONE (partial for Exercise Types)

New reusable component at `design-system/admin/components/toggle/`. Features:
- `checked`, `disabled`, `loading`, `label`, `description` inputs
- `(changed): EventEmitter<boolean>` output
- Implements `ControlValueAccessor` — compatible with `[(ngModel)]` and reactive forms
- `role="switch"`, `aria-checked`, `aria-disabled` — fully accessible
- Keyboard: Space and Enter trigger toggle
- Loading state: spinner inside thumb, track opacity reduced, cursor: wait
- 40×22px track, 16px thumb, CSS transition for smooth animation
- Token-based colours: on = `--sp-admin-primary`, off = `--sp-admin-border`
- 17 unit specs covering all states, CVA, keyboard, DOM structure

**Applied to:** Notifications channel `isEnabled` fields (in-app, email, SMS) — replaced `sp-admin-checkbox` with `sp-admin-toggle [(ngModel)]`.

**Exercise Types deferred:** The table uses a context-menu (`sp-admin-table-actions`) for enable/disable. Adding an inline toggle requires a table column change that touches the table layout and action dispatch simultaneously — risk exceeds scope for this phase. Deferred to a future task.

### Task 3: AI Config slide-over polish (TODO-VISUAL-09) — DEFERRED

The AI Config category edit is a 981-line component with complex nested `CategoryState` interface. Converting the centered `sp-admin-modal` to a right-side `sp-admin-slide-over` requires restructuring the entire template's form/save/test flow. Risk is high and out of scope for zero-endpoint polish. Deferred to TODO-VISUAL-09 in the next appropriate phase.

### Task 4: Students list avatar initials (TODO-VISUAL-11) — DONE

Added `avatarInitial(student)` and `avatarColor(student)` helper methods to `admin-students.component.ts`:
- `avatarInitial` falls back: `displayName → firstName → email → '?'`
- `avatarColor` hashes `email || studentProfileId` using `(h * 31 + charCode) & 0xFFFFFF`, maps to 7-colour palette (same algorithm as dashboard)
- Student name cell updated to show coloured initial circle (28×28px) beside name + copyable email
- CSS: `.sp-stu-avatar-row` flex row, `.sp-stu-avatar` circle tile
- 8 specs added: `avatarInitial` fallback chain (4 cases), `avatarColor` hex format, consistency, DOM avatar element, DOM initial text

---

## Gate Results

| Gate | Result |
|------|--------|
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1350/1350 |

---

## Risks and Deferred Items

- **TODO-VISUAL-09** (AI Config slide-over): deferred, needs careful template refactor.
- **Exercise Types toggle** (TODO-VISUAL-10B, new): inline toggle needs table column addition; deferred.

---

## Security / Scope Constraints Respected

- No new backend endpoints added.
- No migrations.
- No business logic changes.
- No fake data or invented values.
- No student-facing UI touched.
- No mock data imported.

---

## Next Recommended Action

Commit with message `ui: polish admin frontend interactions`. Then evaluate:
1. TODO-VISUAL-09 (AI Config slide-over) — medium complexity, isolated template change.
2. TODO-VISUAL-10B (Exercise Types inline toggle) — requires table column addition.
