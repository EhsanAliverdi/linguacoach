---
status: current
lastUpdated: 2026-06-18
owner: engineering
supersedes:
supersededBy:
---

# Phase 10X-G — Full Admin Page Refactor to TailAdmin-backed Wrappers — Engineering Review

**Date:** 2026-06-18
**Related sprint:** Phase 10X-G (admin UI design system)
**HEAD before work:** 792462d

---

## Goal

Refactor the highest-legacy admin pages onto the SpeakPath `sp-admin-*` wrapper layer,
wire the admin header user menu through `sp-admin-dropdown`, and reduce duplicated
page-local CSS, while keeping all admin business logic and API behaviour unchanged.

Dependency rule preserved throughout:

```
TailAdmin free Angular source → sp-admin-* wrappers → admin feature pages
```

Feature pages consume wrappers only. No feature page imports TailAdmin internals.

---

## Files reviewed

- `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/app/layouts/admin-app-layout/admin-app-layout.component.ts` / `.html`
- `src/app/admin/components/*` wrapper APIs (stat-card, card, badge, table, button, dropdown)
- `src/app/features/admin/admin-wrapper-migration.spec.ts`
- `src/app/features/admin/admin-curriculum/admin-curriculum.component.spec.ts`
- Existing wrapper usage across all admin feature pages (763 occurrences, 18 files)

---

## Findings by priority

### P0 — none

No correctness or security defects found. Backend untouched; admin role checks, API
contracts, and AI behaviour unchanged.

### P1 — addressed

- **Dashboard was the prime legacy offender.** It hand-rolled 4 KPI cards, a status card,
  a mini table, and custom badges with ~80 lines of component CSS that duplicate wrapper
  behaviour. Refactored to `sp-admin-stat-card`, `sp-admin-card`, and `sp-admin-badge`;
  removed the duplicated KPI/status/badge/table-card CSS. Kept only the unique action-card
  grid and analytics-placeholder content layout as page-local CSS.
- **`sp-admin-button` navigation trap.** The wrapper renders an inner `<button>`, so
  `routerLink` on the host does not navigate. The dashboard empty-state "Create first
  student" call-to-action was kept as a plain anchor styled as a button rather than a
  non-navigating `sp-admin-button`. Documented this rule in the design-system doc.

### P2 — addressed

- **AI Config duplicate headings.** Each titled `sp-admin-card` also rendered a page-local
  `<h2>` repeating the title. Removed the duplicates; the card title is canonical. Converted
  the LLM and TTS category Save/Test actions to `sp-admin-button`.
- **Curriculum used student-design-system classes.** The create/edit and routing-preview
  panels used `.sp-card`/`.sp-btn` (student tokens) inside the admin module. Replaced the
  containers with `sp-admin-card` and the actions with `sp-admin-button`.
- **Header user menu duplicated dropdown logic.** The layout carried its own open/close
  signal, document click handler, and Escape handling for the profile flyout. Rewired through
  `sp-admin-dropdown`, which owns those behaviours, and deleted the now-dead layout code.

### P3 — deferred (tracked in TODOs)

- Page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations operational forms) were
  intentionally not migrated to `sp-admin-form-field`/`sp-admin-select`. These are dense,
  `ngModel`-driven controls with custom option logic; a field-by-field migration is higher
  risk than its visual payoff in this pass. Split into TODO-10X-G-AICONFIG-FORMS,
  TODO-10X-G-CURRICULUM-FORMS, TODO-10X-G-INTEGRATIONS-FORMS.
- Admin-only dark-mode class boundary (TODO-10X-G-DARKMODE).

---

## Decisions made

1. **Scope bounded to highest-value pages.** Most admin pages (Students, Prompts, Exercise
   Types, AI Usage, Diagnostics, Usage Policies, Careers, Integrations shell) already consumed
   wrappers from 10X-B/E/F. 10X-G targeted the remaining real legacy: Dashboard inline UI,
   AI Config/Curriculum form-panel chrome, and the header dropdown. This keeps the change
   reviewable and the gates green rather than rewriting working pages for churn.
2. **Wrappers replace visuals, page-local CSS keeps unique layout.** Grid layout and
   placeholder content remain page-local; card/table/badge/button/stat visuals move to wrappers.
3. **Header dropdown via wrapper, not bespoke state.** Removes duplicate click-outside/Escape
   logic and aligns with the TailAdmin dropdown pattern.

---

## Dark mode / admin-only status

`AdminThemeService` stores the admin theme in the `adminTheme` localStorage key, isolated from
the student UI theme. The admin theme toggle ships inside `sp-admin-header`. A full admin-only
dark-mode **class boundary** (guaranteeing no dark styles leak into student pages) is still
future work — captured as TODO-10X-G-DARKMODE. No regression: student UI was not touched.

---

## Header user dropdown status

Implemented. `AdminAppLayoutComponent` now projects the avatar into `sp-admin-dropdown`'s
`trigger` slot and the profile flyout (email, role, disabled Profile item, Sign out) into the
`menu` slot. Open/close, click-outside, and Escape are owned by the wrapper. Four new unit
tests cover render, avatar initial, open-on-click, and sign-out.

---

## Pages intentionally deferred

No page was left un-touched that needed touching for this scope. Form-field-level migration of
AI Config, Curriculum, and Integrations forms is explicitly deferred (see TODOs). Student
management modals remain page-local (TODO-10X-D-MODAL).

---

## Visual validation summary

- Angular production build is clean (pre-existing student-component CSS-selector and NG8102/
  NG8103 warnings only; present at baseline).
- Playwright admin specs (mobile overflow + admin screenshots + flows) pass at 188, including
  pages whose markup changed (Dashboard, header).
- The header dropdown open/sign-out path is covered by unit tests against the real layout.

---

## Implementation tasks produced

- TODO-10X-G-AICONFIG-FORMS, TODO-10X-G-CURRICULUM-FORMS, TODO-10X-G-INTEGRATIONS-FORMS,
  TODO-10X-G-DARKMODE (added to `TODOS.md`).

---

## Risks / unresolved questions

- Page-local form controls remain visually distinct from wrapper inputs until the deferred
  form-field migrations land. Low risk: behaviour unchanged, contained to forms.
- `sp-admin-button` host `(click)` relies on event bubbling from the inner button; disabled
  state correctly suppresses clicks. Verified by build and existing/added tests.

---

## CI gate results

| Gate | Result |
|---|---|
| git diff --check | clean |
| dotnet build (Release) | 0 errors |
| dotnet test (Release) | 1885 passed (3 arch + 1233 unit + 649 integration) |
| Angular production build | clean |
| Angular unit tests | 377 passed (was 373) |
| Playwright (--workers=1) | 188 passed |

---

## Explicitly NOT implemented

Full 10R-F usage governance UX, 10U AI Usage redesign, 10V prompt playground, notification
platform, enterprise auth/security, observability stack, billing/subscriptions,
StudentProfile.CefrLevel migration, full placement engine, and full mastery engine were
**not** implemented.

---

## Final verdict

**Pass.** The flagship legacy admin UI (Dashboard) and the AI Config/Curriculum form chrome
now use the wrapper layer, the header user menu uses `sp-admin-dropdown`, duplicated page-local
CSS was reduced, and all gates are green with no backend or student-UI changes.

## Next recommended action

Land the deferred form-field migrations (TODO-10X-G-*-FORMS) so AI Config, Curriculum, and
Integrations forms render through `sp-admin-form-field`/`sp-admin-select`, then define the
admin-only dark-mode class boundary (TODO-10X-G-DARKMODE).
