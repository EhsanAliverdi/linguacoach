---
status: current
lastUpdated: 2026-06-17 18:00
owner: engineering
---

# Phase 10J-F — Student App Design System & Responsive UI Foundation

**Date:** 2026-06-17
**Related sprint:** Phase 10J-F
**Type:** Engineering review / implementation record

---

## Files reviewed

### Styles
- `src/LinguaCoach.Web/src/styles.css` — global design tokens, utility classes, layout

### Layout
- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.ts`
- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.html`
- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.css`

### Feature pages
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Web/src/app/features/learning-path/learning-path.component.html`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.css`
- `src/LinguaCoach.Web/src/app/features/profile/profile.component.ts`
- `src/LinguaCoach.Web/src/app/features/progress/progress.component.ts`

### New shared components
- `src/LinguaCoach.Web/src/app/shared/student-ui/student-chip.component.ts`
- `src/LinguaCoach.Web/src/app/shared/student-ui/student-badge.component.ts`
- `src/LinguaCoach.Web/src/app/shared/student-ui/index.ts`

### Tests
- `src/LinguaCoach.Web/src/app/shared/student-ui/student-chip.component.spec.ts`
- `src/LinguaCoach.Web/src/app/shared/student-ui/student-badge.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/profile/profile.component.spec.ts` (extended)
- `src/LinguaCoach.Web/e2e/design-system-10jf.spec.ts`

---

## Findings grouped by priority

### P0 — Fixed

1. **`sp-card-hover` missing** — class was used in Practice Gym HTML but not defined globally. Added to `styles.css` with transition/hover/active states.

2. **`sp-bottomnav` / `sp-navbtn` duplicated** — defined in both `styles.css` and `student-app-layout.component.css`. Removed the global copy; component CSS is authoritative.

3. **Profile chips not accessible** — `chipStyle()` returned raw inline style strings; no `aria-pressed`, no CSS class binding, no focus ring. Replaced with `sp-pref-chip` / `sp-pref-chip--on` CSS classes, `aria-pressed` binding, `data-testid` attributes per chip, and `focus-visible` outline.

4. **Hardcoded hex colors in progress component** — `#2e7d32`, `#e65100`, `#1565c0`, `#c62828`, `#e8f5e9`, `#e3f2fd`, `#f5f5f5`, `#fff3e0` etc. used directly. Replaced all with design tokens (`--sp-success`, `--sp-warn`, `--sp-writing-ink`, `--sp-success-soft`, etc.).

5. **`var(--sp-primary)` in practice-gym CSS** — token does not exist in the design system. Replaced with `var(--sp-brand)`.

6. **Missing design tokens** — `--sp-brand`, `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, layout z-index layers, content max-width constants added to `:root`.

### P1 — Added

7. **Shared `StudentChipComponent`** — reusable `sp-chip` Angular standalone component with `selected`, `disabled` inputs and `toggle` output. Tested.

8. **Shared `StudentBadgeComponent`** — reusable `sp-badge` Angular standalone component with `variant` input (success/warn/info/muted/writing/speaking/listening/vocabulary). Tested.

9. **`sp-pref-chip` CSS class** — centralised preference chip style added to global styles. Selected state uses `--sp-brand` background. `focus-visible` ring included.

---

## Decisions made

- **Design token centralisation approach:** CSS custom properties in `styles.css`. No Tailwind config changes required; existing token structure extended.
- **Chip refactor scope:** Profile page only for this phase. Practice Gym format chips (`.sp-chip` in component CSS) left as-is — they serve a different visual purpose (secondary skill labels, not toggle-able).
- **Shared component scope:** `StudentChipComponent` and `StudentBadgeComponent` created. Larger shell components (StudentPageShell, StudentCard etc.) deferred — existing CSS classes already centralised enough.
- **No backend changes:** All changes are purely frontend.
- **`sp-bottomnav` deduplication:** Component CSS wins. Removed global duplicate.

---

## Design tokens added

| Token | Value |
|---|---|
| `--sp-brand` | `#5B4BE8` |
| `--sp-r-md` | `16px` |
| `--sp-nav-h` | `64px` |
| `--sp-sidebar-w` | `264px` |
| `--sp-sidebar-w-collapsed` | `76px` |
| `--sp-content-max` | `520px` |
| `--sp-content-max-desktop` | `1080px` |
| `--sp-zi-header` | `30` |
| `--sp-zi-nav` | `40` |
| `--sp-zi-flyout` | `200` |

---

## Angular test results

**261 tests, 261 passed, 0 failed**

New tests added:
- `StudentChipComponent` — 7 tests (renders, selected class, aria-pressed, click, disabled)
- `StudentBadgeComponent` — 5 tests (renders, variants, default)
- `ProfileComponent` — 6 new chip state tests (selected class, aria-pressed, difficulty, session length)

---

## Playwright results

**187 tests, 187 passed, 0 failed**

New spec: `e2e/design-system-10jf.spec.ts` (12 tests):
- Mobile bottom nav appears exactly once
- Mobile bottom nav hidden on desktop, visible on mobile
- Profile chip selected/unselected class and aria-pressed
- Clicking chip selects it
- Difficulty and session-length pre-selected states
- Today page no horizontal overflow on mobile
- Practice Gym heading visible on mobile
- Desktop sidebar nav links visible

---

## Backend test results

- Architecture tests: 3 passed
- Unit tests: 1021 passed
- Integration tests: 541 passed

---

## Known limitations

- **StudentPageShell / StudentCard / StudentStatCard components** not created. Existing CSS classes (`.sp-card`, `.sp-stat-grid`) handle the pattern adequately for now. These would be useful if page-count grows further.
- **Dashboard and Journey inline styles** — many remain as inline `style=` attributes. Removing them safely requires visual regression tooling or manual QA pass. Not changed this phase.
- **Practice Gym format card `sp-chip` class** — component-scoped chip in `practice-gym.component.css`. Separate from `sp-pref-chip` global. Not merged to avoid visual regression on the format cards.
- **Screenshot baselines** — not updated (project uses Playwright element checks, not pixel snapshots).

---

## Risks / unresolved questions

None blocking.

---

## Final verdict

Phase 10J-F complete. All CI checks pass. Student design token foundation extended, profile chips are accessible and class-driven, progress colors use design tokens, practice-gym CSS references valid tokens, shared chip/badge components added and tested.

---

## Next recommended action

Continue to Phase 10K or backlog priority. Student design system foundation is stable for future feature work.
