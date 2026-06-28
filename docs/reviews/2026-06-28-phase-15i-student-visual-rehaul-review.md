# Phase 15I — Student UI Visual Rehaul and Design-System Finalization

**Date:** 2026-06-28  
**Sprint:** Phase 15I  
**Type:** Engineering + Design Review  
**Scope:** Visual-only pass across all main student routes. No backend changes, no new features, no routing or logic changes.

---

## Files Reviewed

- `src/LinguaCoach.Web/src/styles.css`
- `src/LinguaCoach.Web/src/app/design-system/student/layouts/student-app-layout/student-app-layout.component.html`
- `src/LinguaCoach.Web/src/app/design-system/student/layouts/student-app-layout/student-app-layout.component.css`
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Web/src/app/features/student/journey/journey.component.html`
- `src/LinguaCoach.Web/src/app/features/student/practice/practice-gym.component.html`
- `src/LinguaCoach.Web/src/app/features/student/practice/practice-gym.component.css`
- `src/LinguaCoach.Web/src/app/features/student/progress/progress.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.ts`

---

## Audit Method

Visual audit via Playwright with mocked API responses and fake JWT auth. Screenshots captured at 1280×900 (desktop) and 390×844 (mobile) for: dashboard, practice, journey, progress, profile.

---

## Findings Grouped by Priority

### P1 — Visible defects

| ID | Finding | File | Fix |
|----|---------|------|-----|
| FINDING-001 | Mobile header greeting wraps to 2 lines on narrow viewports, pushing streak pill and icons off-screen | `student-app-layout.component.css` | Added `white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:100px` to `.sp-greet-lg` at ≤899px |
| FINDING-003 | `sp-stat-grid` hardcoded to `repeat(3,1fr)` — 4-item grids on the progress page wrap a 4th card to a second row | `styles.css` | Changed to `repeat(auto-fit,minmax(110px,1fr))` |
| FINDING-005 | Mobile bottom nav Practice FAB has no label — inconsistent with Today, Journey, Progress, Profile which all show text | `student-app-layout.component.html` | Added `<span class="sp-practice-label">Practice</span>` with `margin-top:22px` |

### P2 — Inconsistency / polish

| ID | Finding | File | Fix |
|----|---------|------|-----|
| FINDING-002 | No shimmer skeleton class in the global sheet — progress page used `sp-card sp-skeleton` but the class had no moving gradient | `styles.css` | Added `.sp-skeleton` with `position:relative; overflow:hidden; background:var(--sp-canvas2)` and a `::after` shimmer animation via `translateX` |
| FINDING-004 | Profile loading state was bare text `"Loading profile..."` with no structure — jarring against skeletal loading elsewhere | `profile.component.ts` | Replaced with 5 `sp-skeleton` cards approximating section heights |
| FINDING-006 | Dashboard loading state used `sp-loading-pulse h-48/h-32/h-40` — Tailwind height classes mixed with sp- classes, and a different animation pattern (pulse) than the rest of the student pages (shimmer) | `dashboard.component.html` | Replaced with `sp-skeleton` with inline heights |
| FINDING-007 | Journey loading state used `sp-loading-pulse` (pulse) — inconsistent with skeleton shimmer pattern now used on progress, profile, and dashboard | `journey.component.html` | Replaced with `sp-skeleton` |

### P3 — CSS hygiene

| ID | Finding | File | Fix |
|----|---------|------|-----|
| Part M | Practice gym defined a local `.sp-chip` (10px, non-interactive tag) that conflicted with global `.sp-chip` (13.5px interactive chip). Angular's emulated encapsulation means the local definition wins on conflicting properties, but the global cursor/transition leak through. Additionally, modifier classes `sp-chip--xs` and `sp-chip--muted` referenced in HTML had no CSS definition anywhere. | `practice-gym.component.css/html` | Renamed local class to `.sp-skill-tag`; removed orphan modifier classes |

### Not an issue

- Practice gym loading animation (animated dots via `sp-practice-loading-dot`) — deliberate, branded, intentionally distinct from skeleton. Left unchanged.
- Lesson and activity page loading states — still use `sp-loading-pulse`. These pages are inside a lesson flow, not main nav destinations, and are out of scope for Phase 15I.

---

## Decisions Made

1. **Shimmer over pulse for page-level loading.** The `sp-skeleton` shimmer is the canonical loading pattern for main page routes. The `sp-loading-pulse` class remains in `styles.css` (used by lesson/activity sub-pages and inline loaders) but is not used on main student nav routes as of this phase.

2. **`sp-stat-grid` uses `auto-fit` not `auto-fill`.** With `auto-fit`, empty tracks collapse, so a 4-item grid fills 4 columns when space allows and wraps gracefully on narrow viewports. `auto-fill` would create extra blank columns.

3. **Practice FAB label always brand-coloured.** Unlike other nav items that turn brand-coloured only when active, the Practice FAB label is always `var(--sp-brand)` since the FAB itself is always the gradient brand circle — there is no "inactive" visual state for it.

4. **`.sp-skill-tag` vs expanding global `.sp-chip`.** The practice gym needs very small (10px) non-interactive tags for secondary skill labels. These are semantically different from interactive filter chips. Renaming rather than extending the global chip avoids misuse of the interactive component in a read-only context.

---

## Implementation Tasks Produced

All 8 findings above were fixed in atomic commits:

| Commit | Message |
|--------|---------|
| `014e161` | style(design): FINDING-001 — fix mobile greeting nowrap in student layout header |
| `6bc10d8` | style(design): FINDING-002 — add sp-skeleton shimmer loading class to global styles |
| `512f2d4` | style(design): FINDING-003 — fix sp-stat-grid to handle 4-item grids with auto-fit |
| `20261a0` | style(design): FINDING-004 — replace bare profile loading text with sp-skeleton cards |
| `05374fd` | style(design): FINDING-005 — add Practice label to mobile FAB nav item |
| `bc6ddc2` | style(design): FINDING-006/007 — standardise loading states to sp-skeleton shimmer on dashboard and journey |
| `8952efe` | refactor(design): Part-M — rename local sp-chip to sp-skill-tag in practice gym to avoid global style collision |

---

## Risks and Unresolved Questions

- **Lesson/activity loading states** remain on `sp-loading-pulse`. This inconsistency is acceptable for now — those pages are transitional and less visible in the core student journey. A separate phase can address them.
- **`sp-practice-label` margin-top: 22px** — this value was chosen to clear the 72px FAB that sits 36px above the nav bar. If the FAB size or position changes, this needs to be updated together with the FAB positioning.
- **Tailwind and sp- mixing** — the dashboard still mixes some `mb-2`, `mb-5` Tailwind utility classes with sp- design tokens (in `sp-alert-info mb-5`, `sp-eyebrow mb-2`). These are pre-existing and low-risk but represent style-system drift that could be cleaned in a future CSS consolidation pass.

---

## Final Verdict

All 8 findings fixed. Build green. 1,464 Angular unit tests pass. 2,732 .NET tests pass. 247 Playwright E2E tests pass (3 skipped — pre-existing, unrelated).

Phase 15I is complete. The student experience now has consistent shimmer loading states on all 5 main nav routes, a properly labelled mobile FAB, responsive stat grids, and a clean design-system CSS boundary between global and component-local classes.

---

## Next Recommended Action

Phase 16 — feature work or Phase 15J if a further polish pass on lesson/activity pages is desired.
