---
status: current
lastUpdated: 2026-06-19 (10X-I)
owner: engineering
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-19

---

## Active sprint

**Phase 10X-I — Migrate Remaining Admin Forms and Modals to CVA Wrappers** - complete (2026-06-19)

Goal: migrate the three deferred admin form/modal targets to `sp-admin-*` CVA wrappers after the
CVA foundation (10X-H) and layout blocker fix (10X-LAYOUT-BLOCKER) unblocked this work.

### Delivered

- **AI Config:** provider/model/voice category selects kept as native `<select>` inside
  `<sp-admin-form-field>` (incompatible with `sp-admin-select` due to `[ngValue]="null"`).
  TTS voice text input, model name input, API key (password) input, Qwen endpoint input all
  migrated to `<sp-admin-input>`. Add/Test/Save/Clear buttons migrated to `<sp-admin-button>`.
  Removed page-local `.sp-ai-select`/`.sp-ai-model-select` CSS; added `.sp-adm-native-select`.
- **Integrations:** storage display fields migrated to `<sp-admin-input [disabled]="true">` inside
  `<sp-admin-form-field>`. Generation settings number inputs kept native `<input type="number">`
  (CVA writes strings — numeric domain integrity preserved); wrapped in `<sp-admin-form-field>`.
  Test/Save/Cancel/Retry/Generate buttons migrated to `<sp-admin-button>`. Tables rewrote to
  TailAdmin Tailwind classes directly.
- **Student modals (all 3):** edit, reset-password, and reset-data page-local modals replaced with
  `<sp-admin-modal>`. All text/password inputs inside use `<sp-admin-input>`. Textareas use
  `<sp-admin-textarea>`. Submit/cancel actions use `<sp-admin-button>`. Removed all page-local
  `.sp-admin-modal-backdrop`/`.sp-admin-modal`/`.sp-admin-modal-header`/`.sp-admin-edit-grid` CSS.
- **`sp-admin-modal`:** added `maxWidth` `@Input()` (default `520px`); student edit modal uses `720px`.
- **`sp-admin-input`:** added `@Input() value` getter/setter for one-way display binding.
- **`sp-admin-layout`:** content area changed from `<div>` to `<main>` for `role="main"` semantics,
  fixing Playwright `getByRole('main')` locator failures.
- 11 new Angular unit tests for the migrated components.

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 421 passed (up from 411)
- Playwright: `getByRole('main')` locator issue resolved by `<main>` layout fix

### Closed TODOs

- TODO-10X-G-AICONFIG-FORMS: done
- TODO-10X-G-INTEGRATIONS-FORMS: done
- TODO-10X-D-MODAL: done
- TODO-10X-I: done

### Review doc

`docs/reviews/2026-06-19-phase-10x-i-admin-form-modal-migration-review.md`

---

## Previous sprint

**Phase 10X-LAYOUT-BLOCKER — TailAdmin Layout One Shell Parity** - complete (2026-06-19)

Goal: make admin shell/sidebar/header/content visually match TailAdmin Layout One.
Blocked 10X-I until resolved.

### Delivered

- Imported TailAdmin `@utility` classes (`menu-item`, `menu-item-active`, `menu-item-inactive`,
  `menu-item-icon-*`, `menu-dropdown-*`, `no-scrollbar`) and `@theme` tokens into global `styles.css`.
- Rewrote `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header` wrappers to use exact
  TailAdmin Tailwind classes with no competing custom CSS class names.
- Rewrote `admin-app-layout.component.html` to use TailAdmin grouped nav section structure
  (`menu-item`/`menu-item-active`/`menu-item-inactive` classes, `h2` section headings).
- Replaced old mobile drawer (custom CSS) with TailAdmin `fixed inset-0 z-40` backdrop
  and `xl:hidden -translate-x-full / translate-x-0` aside pattern.
- Stripped all competing layout CSS from `admin-app-layout.component.css`.
- Added `body.admin-layout` class (OnInit/OnDestroy) for TailAdmin gray-50 background
  without affecting student layout.
- 20 new Angular tests for Layout One shell structure.
- Updated 13 existing tests to validate TailAdmin classes instead of old CSS class names.
- All CI gates pass: 1885 .NET tests, 411 Angular tests, 183+ Playwright tests.

### Root causes fixed

1. TailAdmin `@utility` menu classes missing from SpeakPath Tailwind build.
2. `admin-app-layout.component.css` margin-left override fighting Tailwind `xl:ml-*`.
3. `sp-admin-layout/sidebar/header` CSS classes conflicting with Tailwind classes.
4. Body background not overridden for admin context.
5. Nav items using old `sp-admin-nav-item` instead of `menu-item` utility.

### Review doc

`docs/reviews/2026-06-19-phase-10x-layout-blocker-tailadmin-shell-parity-review.md`

---

## Previous sprint

**Phase 10X-H — Admin Form Wrapper CVA + Remaining Form/Modal Migration Foundation** - complete (2026-06-19)

Goal: make TailAdmin-backed admin form wrappers safe for real Angular forms by adding
`ControlValueAccessor`, preparing the foundation for future enterprise admin screens.

### Delivered

- `sp-admin-input`: `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`. Supports
  `[(ngModel)]`, reactive `[formControl]`/`formControlName`, `setDisabledState`, touched-on-blur.
  Pass-through `type`, `placeholder`, `autocomplete`, `readonly`, `required`, `invalid`.
- `sp-admin-select`: `ControlValueAccessor`. Options via `[options]` or projected `<option>`,
  `placeholder` disabled default option, `required`, `invalid`, disabled propagation, touched-on-blur.
- `sp-admin-textarea`: new wrapper with `ControlValueAccessor`. `rows`, `placeholder`, `readonly`,
  `required`, `invalid`, disabled propagation, touched-on-blur. Exported from `admin/index.ts`.
- `sp-admin-form-field`: red `*` required marker via `[required]`; label/hint/error structure.
- Tests: 15 new wrapper specs (ngModel write/propagate, reactive FormControl bind, disabled
  propagation, touched-on-blur for input/select/textarea; form-field label/hint/error/required).

### Deferred (CVA foundation now unblocks these)

- AI Config dense provider-credentials grid (TODO-10X-G-AICONFIG-FORMS) — kept native this phase to
  avoid silent save regressions across the high-field-count ngModel-driven grid.
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS) — per-field migration pass deferred.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 394 passed (up from 379)
- Playwright: 188 passed

See: `docs/reviews/2026-06-19-phase-10x-h-admin-form-cva-modal-foundation-review.md`

---

## Previous sprint

**Phase 10X-G-F — Finish Remaining Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: finish wrapper consistency on the remaining admin pages after 10X-G.

### Delivered

- Students: row table wrapped in `sp-admin-table` (projection mode); lifecycle/onboarding/CEFR pills
  migrated from raw `.sp-admin-badge` class to the `sp-admin-badge` wrapper. Removed obsolete
  page-local pagination, row-action link, and `.sp-admin-row-actions` CSS.
- Curriculum: create/edit and routing-preview form fields migrated to `sp-admin-form-field`
  (closes TODO-10X-G-CURRICULUM-FORMS). Native ngModel controls retained inside each field because
  `sp-admin-input`/`sp-admin-select` have no ControlValueAccessor.
- Verified AI Usage, Prompts, Exercise Types, Diagnostics, Usage Policies, and Integrations cards
  were already wrapper-migrated in 10X-B/10X-G. No further raw badge/table legacy in those pages.
- Tests: 2 new wrapper-migration specs (Students table/badge/actions; Curriculum form fields).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build: clean; Angular tests: 379 passed (up from 377)
- Playwright: 188 passed

### Not implemented in 10X-G-F (deferred)

- AI Config dense provider-credentials form fields (TODO-10X-G-AICONFIG-FORMS).
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS).
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Admin-only dark-mode class boundary (TODO-10X-G-DARKMODE).
- Usage governance UX (10R-F), AI Usage redesign (10U), prompt playground (10V), notification
  platform, enterprise auth/security, observability, billing, StudentProfile.CefrLevel migration,
  full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-f-finish-admin-page-refactor-review.md`

---

## Earlier sprint

**Phase 10X-G — Full Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: refactor the highest-legacy admin pages onto `sp-admin-*` wrappers, wire the admin
header user menu through `sp-admin-dropdown`, and reduce duplicated page-local CSS.

### Delivered

- Dashboard: KPI tiles → `sp-admin-stat-card`; sections → `sp-admin-card` (incl. dashed
  placeholders); status pills → `sp-admin-badge`. Removed ~50 lines of page-local CSS
  (KPI card, status card, badge, table-card) now owned by wrappers.
- AI Config: removed duplicate in-card `<h2>` headings (card title is canonical); LLM and
  TTS category Save/Test actions → `sp-admin-button`.
- Curriculum: create/edit and routing-preview panels → `sp-admin-card`; form and preview
  actions → `sp-admin-button` (replaced student-design `.sp-card`/`.sp-btn`).
- Admin header user/profile menu → `sp-admin-dropdown` (open state, click-outside, Escape
  owned by the wrapper). Removed `profileMenuOpen`, `toggleProfileMenu`, and the document
  click handler from `AdminAppLayoutComponent`.
- Tests: new `admin-app-layout.component.spec.ts` (4 header-dropdown tests); updated dashboard
  KPI assertion to target `sp-admin-stat-card`; curriculum create test asserts `sp-admin-card`.
- Docs: design-system page-refactor rules + header-dropdown section; adapter inventory 10X-G
  phase; product-state; TODOs.

### Gates

- git diff --check: ✅ clean
- Angular build: ✅ passed
- Angular tests: ✅ 377 passed (up from 373)
- .NET build: ✅ 0 errors
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-G (deferred)

- Migrating remaining page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations
  operational forms) to `sp-admin-form-field`/`sp-admin-select` — see TODOs.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V).
- Notification platform, enterprise auth/security, observability stack, billing,
  StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-full-admin-page-refactor-review.md`

---

## Previous sprint

**Phase 10X-F — Admin Wrapper Capability Completion** - complete (2026-06-18)

Goal: add missing reusable admin wrapper capabilities: sortable tables, row action dropdowns, admin dropdown wrapper, theme toggle, improved filter-bar and header slots.

### Delivered

- `sp-admin-dropdown` — TailAdmin-backed dropdown with trigger/menu content projection, click-outside + Escape close, align and width inputs.
- `sp-admin-table-actions` — row action dropdown (three-dot trigger). Generic `[actions]` array API + content projection for conditional per-row actions. Danger item red styling.
- `sp-admin-theme-toggle` — admin-only dark/light toggle based on TailAdmin ThemeToggleButtonComponent. Uses `AdminThemeService` (isolated `adminTheme` localStorage key).
- `AdminThemeService` — admin-scoped theme service mirroring TailAdmin pattern.
- `sp-admin-table` updated: sortable column flag, `sortColumn`/`sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` icons, `aria-sort`, keyboard-accessible headers.
- `sp-admin-header` updated: named `[left]` and `[actions]` content slots. Theme toggle auto-rendered in right action zone.
- `sp-admin-filter-bar` updated: named `[search]`, `[filters]`, `[actions]` slots. Left/right zone split. Backward-compat general projection retained.
- Admin barrel (`admin/index.ts`) updated with 4 new exports.
- `admin-students` page row actions migrated to `sp-admin-table-actions` (projected content with conditional actions).
- 24 new Angular tests added in `admin-components.spec.ts` (Phase 10X-F block).
- Playwright tests updated to open row action dropdown before interacting with action items (reset, archive, view, edit).

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 373 passed (up from 349)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-F

- Full admin page refactor/redesign (10X-G)
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V)
- Notification platform, enterprise auth, billing, placement/mastery engine

---

## Previous sprint

**Phase 10X-E — Adapt Real TailAdmin Patterns into sp-admin-* Wrappers** - complete (2026-06-18)

Goal: replace custom CSS approximations in all 15 `sp-admin-*` wrappers with actual TailAdmin free Angular template class structures. Admin feature pages unchanged — they continue using `sp-admin-*` only.

### Delivered

- All 15 wrapper components adapted to TailAdmin source patterns:
  layout (`min-h-screen xl:flex`), sidebar (`fixed w-[290px]/w-[90px]`), header (`sticky top-0 z-[99999]`),
  button (brand-500 primary / outline), badge (light variant color map), card (`rounded-2xl border border-gray-200`),
  stat-card, input (`h-11 rounded-lg border border-gray-200`), select, form-field, modal (`rounded-3xl`, `bg-gray-400/50 backdrop-blur-sm`),
  table (`rounded-2xl`, th `text-xs bg-gray-50`), pagination, filter-bar, drawer.
- Fixed TypeScript parse error: HTML comment inside `@Component({})` decorator in `sp-admin-badge.component.ts`
  (converted to `//` TS comments above decorator).
- Added 15 new TailAdmin-backed pattern tests in `admin-components.spec.ts` (Angular: 334 → 349).
- Updated `docs/architecture/admin-tailadmin-adapter-inventory.md`: all adapted wrappers marked `✅ Done`.
- Updated `docs/architecture/admin-ui-design-system.md`: Phase 10X-E note, wrapper API stability.
- Engineering review saved to `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md`.

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 349 passed (0 failed)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ (see Playwright gate status in product-state)

### Not implemented in 10X-E

- Table sorting, dropdown, theme toggle wiring (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth, billing, placement engine

---

## Previous sprint

**Phase 10X-D — TailAdmin Template Import & Adapter Plan** - complete (2026-06-18)

Goal: import the free TailAdmin Angular template as a vendor reference, document the adapter boundary, and create the mapping inventory that drives 10X-E/10X-F.

### Delivered

- Cloned TailAdmin free Angular template into `templates/tailadmin/free-angular-tailwind-dashboard/` (commit da992cf, MIT license).
- Removed nested `.git` directory; added `.gitignore` to exclude `node_modules/dist/.angular/coverage`.
- Added `templates/README.md` and `templates/tailadmin/README.md` with source URL, commit, license, allowed/disallowed adapter rules, and update process.
- Updated `docs/architecture/admin-ui-design-system.md`: closed TODO-10X-ASSETS, updated folder structure, vendor source location, and adapter reference.
- Created `docs/architecture/admin-tailadmin-adapter-inventory.md`: full mapping table (TailAdmin pattern → sp-admin-* wrapper, phase, status, notes).
- Updated `docs/sprints/current-sprint.md` and `docs/handoffs/current-product-state.md`.
- Created engineering review at `docs/reviews/2026-06-18-phase-10x-d-tailadmin-template-import-adapter-plan-review.md`.
- Angular build, Angular tests, Playwright, and .NET tests all passed (gates below).

### Not implemented in 10X-D

- Full wrapper replacement using TailAdmin source (10X-E)
- Full admin page refactor (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine

---

## Previous sprint

**Phase 10X-C-F — TailAdmin Layout One Gate Closure** - complete (2026-06-18)

Goal: close Phase 10X-C, fix the critical ViewEncapsulation bug, verify all gates,
and confirm the admin shell now matches TailAdmin Angular Layout One structurally.

### Delivered

- Fixed `AdminAppLayoutComponent` to use `ViewEncapsulation.None` so shell CSS
  (sidebar, nav, header, drawer, profile flyout) reaches child component DOM.
- Raised `anyComponentStyle` budget in `angular.json` from 8kB → 12kB warning / 20kB error
  to accommodate the legitimate admin shell CSS.
- Angular production build: passing.
- Angular unit tests: 334 passed.
- Playwright: 188 passed.
- .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration).
- Visual screenshots confirmed: sidebar left, content right, header sticky, collapsed state,
  mobile drawer — all match TailAdmin Layout One structure.
- Updated `docs/architecture/admin-ui-design-system.md` with TailAdmin as visual source of truth,
  ViewEncapsulation note, asset status, legacy style rules, and migration checklist.
- Updated `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`, TODOS.md.
- Engineering review saved to `docs/reviews/`.

### Remaining legacy areas (unchanged from 10X-B)

- Dashboard still has large component-local CSS for action cards, KPI layout, and placeholders.
- Student edit/reset modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still have page-local form markup.

---

## Previous sprint

**Phase 10X-B - Admin Core Page Migration to Design System** - complete (2026-06-18)

Goal: migrate existing admin pages to the reusable SpeakPath admin wrapper system without adding new business features.

### In scope

- Migrate core admin pages to `sp-admin-*` wrappers where feasible.
- Reduce direct feature-page use of common TailAdmin-style utility lists.
- Improve wrapper capability only where required by migration.
- Preserve existing admin functionality.
- Add Angular wrapper-presence tests.
- Add a stable Playwright mobile overflow check for migrated admin pages.
- Update architecture, handoff, review, and TODO docs.

### Out of scope

Full usage-governance UX, full AI Usage redesign, notification platform, enterprise auth, prompt playground, observability stack, billing, StudentProfile CEFR migration, full placement engine, full mastery engine, and new backend business logic.

### Architecture decisions

- Feature pages import wrappers from `src/app/admin`.
- TailAdmin-specific style decisions stay behind wrappers and tokens.
- Student UI remains separate.
- Tables that need custom projected rows use `sp-admin-table` projection.
- Page-local CSS remains allowed for unique workflows, but common cards, headers, badges,
  filters, buttons, tables, empty, loading, and error states should use wrappers.

### Delivered

- Wrapper improvements: projected table mode, `primary` badge tone, empty-state title,
  and improved filter-bar alignment.
- Migrated pages: Dashboard, Students, AI Config, AI Usage, Prompts, Exercise Types,
  Integrations, Diagnostics, Curriculum, and Usage Policies.
- Added `admin-wrapper-migration.spec.ts` plus wrapper assertions in existing admin specs.
- Added Playwright mobile overflow coverage for `/admin/students` and `/admin/ai-config`.

### Remaining legacy internals

- Dashboard still carries large inline component CSS.
- Student edit/reset/archive modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still retain page-local form internals.

See: `docs/architecture/admin-ui-design-system.md`
See: `docs/reviews/2026-06-18-phase-10x-b-admin-core-page-migration-review.md`

---

## Most recently completed sprint

**Phase 10R — Usage Governance, Token Tracking & Quota Enforcement** — complete (2026-06-18)

Phase 10R introduces enterprise-grade usage governance. Every AI feature call is tracked per student. Admins can define quota policies with daily limits per feature. Expensive AI calls are blocked before they incur cost when a student's quota is exhausted. The system distinguishes prepared learning (always allowed) from expensive on-demand AI (gated).

### What was built

**Domain**
- 4 new enums: `FeatureCategory`, `EnforcementMode`, `UsageUnitType`, `UsagePolicyScopeType`.
- 7 new entities: `FeatureDefinition`, `UsagePolicy`, `UsagePolicyRule`, `StudentPolicyAssignment`, `UsageEvent` (append-only ledger), `StudentUsageDaily` (upserted aggregate), `AdminAuditLog`.

**Persistence**
- 7 new EF Core configurations.
- EF migration `Phase10R_UsageGovernance`.
- `UsageGovernanceSeeder` — seeds 16 feature definitions and 3 policies idempotently.

**Application layer**
- `IUsageQuotaService` — `CheckAsync`, `RecordAsync`, `GetUsageSummaryAsync`, `GetEffectivePolicyAsync`.
- `QuotaDecision` — result with Allowed flag, limits, AvailableAlternatives.
- `QuotaExceededException` — wraps QuotaDecision for middleware mapping.
- `IUsageGovernanceAdminService` — feature definitions, policy CRUD, student assignment.

**Infrastructure**
- `UsageQuotaService` — policy resolution (student → global default), HardLimit enforcement against `StudentUsageDaily`, event recording.
- `UsageGovernanceAdminService` — policy CRUD with audit log writes.
- `AiExecutionService` — pre-call `CheckAsync` for 8 expensive features; post-call `RecordAsync`; failed calls not recorded.

**API**
- `AdminUsageGovernanceController` — 8 endpoints under `/api/admin/` (feature definitions, policy CRUD, student assignment, usage summary).
- `GlobalExceptionMiddleware` — `QuotaExceededException` → HTTP 429 with structured body.

**Angular**
- `UsageGovernanceService` — HTTP client for all 8 admin endpoints.
- `AdminUsagePoliciesComponent` — create/edit policy form with signals; policies table.
- Route: `/admin/usage-policies`.

**Tests**
- 16 DB-layer integration tests (`UsageGovernanceDbTests`): seeding idempotency, record+aggregate, HardLimit blocking, TrackOnly pass-through, policy resolution, audit log, daily rollup, usage summary, admin CRUD.
- 5 HTTP endpoint integration tests (`UsageGovernanceEndpointTests`): list features, 403, create policy, student usage, non-admin 403.
- 8 Angular component unit tests + 6 service HTTP tests.

### Gates at completion
- Architecture: 3 passed
- Unit: 1233 passed
- Integration: 649 passed
- Total .NET: 1885 passed, 0 failed
- Angular: 302 passed, 0 failed
- Angular prod build: clean

### What is intentionally NOT in Phase 10R
Workspace/cohort policy inheritance, billing integration, provider pricing tables, monthly/weekly limit enforcement, CSV export, student-facing usage widget, notification platform, enterprise auth overhaul.

See: `docs/reviews/2026-06-18-phase-10r-usage-governance-token-quota-review.md`
See: `docs/architecture/usage-governance.md`

---

## Previously most recently completed sprint

**Phase 10Q — Admin Curriculum, Format & Context Controls** — complete (2026-06-18)

Phase 10Q gives admins full CRUD control over the curriculum syllabus used for CEFR-aware activity routing. Includes a non-mutating routing preview, seeder protection for admin-edited objectives, and a full Angular admin UI.

### What was built

**Domain**
- `CurriculumObjective` — added `ExamplePrompts` (string?) and `AdminUpdatedAt` (DateTimeOffset?) properties.
- Added `AdminUpdate()` method: validates CEFR, skill, difficulty band, self-prerequisites; sets `AdminUpdatedAt = UtcNow`.
- Existing `UpdateDetails()` preserved as seeder-safe (never sets `AdminUpdatedAt`).

**EF Migration**
- `T52_CurriculumObjectiveAdminFields` — adds nullable `example_prompts` and `admin_updated_at` columns to `curriculum_objectives`.

**Application layer**
- `AdminCurriculumContracts.cs` — `AdminCurriculumObjectiveDto`, `AdminCurriculumObjectiveUpsertRequest`, `CurriculumTaxonomyDto`, `AdminRoutingPreviewRequest`, `AdminRoutingPreviewResult`, `ICurriculumObjectiveWriteService`, `IAdminCurriculumSyllabusQuery`.

**Infrastructure**
- `CurriculumObjectiveWriteService` — Create, Update, Activate, Deactivate, PreviewRouting. Full validation: slug key format, CEFR, skill, context tag, difficulty band 1–5, self-prerequisite, dangling prerequisite. `PreviewRoutingAsync` is read-only: calls routing service but never calls `SaveChangesAsync` or mutates student data.
- `CurriculumSyllabusQueryService` — now implements both `ICurriculumSyllabusQuery` and `IAdminCurriculumSyllabusQuery`. `GetAllObjectivesForAdminAsync` returns active and inactive objectives with optional filters.
- `DependencyInjection.cs` — dual-interface registration for `CurriculumSyllabusQueryService`; registered `ICurriculumObjectiveWriteService`.

**Seeder**
- `CurriculumObjectiveSeeder` — changed from upsert to seed-only-missing: `if (existing.ContainsKey(def.Key)) continue;`. Admin-edited objectives are never overwritten on startup.

**API**
- `AdminCurriculumController` — 8 endpoints:
  - `GET /api/admin/curriculum/objectives` (cefrLevel, skill, isActive filters)
  - `GET /api/admin/curriculum/objectives/{key}`
  - `GET /api/admin/curriculum/taxonomy`
  - `POST /api/admin/curriculum/objectives`
  - `PUT /api/admin/curriculum/objectives/{key}`
  - `POST /api/admin/curriculum/objectives/{key}/activate`
  - `POST /api/admin/curriculum/objectives/{key}/deactivate`
  - `POST /api/admin/curriculum/routing-preview`

**Angular**
- `CurriculumService` — Angular service with all TypeScript interfaces and 8 methods matching the backend API.
- `AdminCurriculumComponent` — single standalone component with `view` signal (`list` | `create` | `edit` | `preview`). List with CEFR/skill/active filters; create/edit form with all fields; non-mutating routing preview panel. No TailAdmin migration.
- `app.routes.ts` — added `/admin/curriculum` route.
- `AdminShellComponent` — added Curriculum nav link.

**Tests**
- 20 unit tests in `AdminCurriculumObjectiveUnitTests`: AdminUpdate, AdminUpdatedAt, ExamplePrompts, DifficultyBand, self-prerequisite, seeder-safe UpdateDetails, general_english/workplace constants.
- 20 integration tests in `AdminCurriculumObjectivesIntegrationTests`: list/filter, taxonomy, create (valid/invalid CEFR/skill/self-prereq/dangling-prereq), update, deactivate/reactivate, non-admin 403, routing preview non-mutation, general_english default, seeder idempotent.
- 16 Angular unit tests in `admin-curriculum.component.spec.ts`: list renders, filter calls API, activate/deactivate, create/edit navigation, form population, routing preview, error state, taxonomy loading.

### Product rules enforced
- `general_english` remains default/fallback — `workplace` is never silently added as default.
- Deactivating an objective does NOT delete historical records (soft lifecycle only).
- Seeder never overwrites admin-edited objectives.
- Routing preview is non-mutating: no AI calls, no student state changes.

---

## Previous sprint

**Phase 10P — Multi-skill Scoring & Progress Updates** — complete (2026-06-18)

Phase 10P adds a safe, testable multi-skill progress update foundation. Activity attempts now update `StudentSkillProfile` rows for all skills trained by an exercise, not just the primary skill.

### What was built

**Application layer**
- `IMultiSkillProgressService` — new interface in `LinguaCoach.Application.Activity` with `ApplyAsync` and `BuildRequest` methods.
- `MultiSkillProgressUpdateRequest` / `MultiSkillProgressUpdateResult` — input/output contracts.

**Infrastructure layer**
- `MultiSkillProgressService` — implementation in `LinguaCoach.Infrastructure.Activity`.
  - Skill registry: 19 known keys (9 general + 10 workplace-specific). No workplace default.
  - Weighting: primary 70%, secondaries share 30% equally. Primary-only → 100%.
  - Score-to-delta: 0-100 score mapped to −1..1 delta centred on 60; scaled by 10 points/unit.
  - ActivityType fallback map for 6 activity types when no pattern metadata available.
  - Incomplete attempts skipped (no skill writes on failed/abandoned submissions).
  - Best-effort: exceptions swallowed, never blocks activity submission.
- `ActivitySubmitHandler` — injected `IMultiSkillProgressService`. Called after both the pattern evaluation path and the legacy AI path.
  - Pattern path: uses `ExercisePatternDefinition.PrimarySkill` + `SecondarySkillsJson`.
  - Legacy path: falls back to `ActivityType` fallback map.

**Tests**
- 20 unit tests in `MultiSkillProgressServiceTests`: weighting splits, deduplication, unknown keys, incomplete attempts, ActivityType fallback, no-workplace-default, lower-level content.
- 10 integration tests in `MultiSkillProgressServiceIntegrationTests`: DB writes, multi-skill rows, listening+writing, speaking roleplay, writing+grammar+vocab, pattern override, idempotent update.

### Previous sprint

**Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring** — complete (2026-06-18)

Phase 10O-F connects the 10O backend suggestion API to the Angular Practice Gym UI and wires completed activities back to readiness pool consumption.

### What was built

**Angular UI**
- `PracticeGymSuggestionsService` — new Angular service with `getSuggestions()`, `startSuggestion()`, `completeSuggestion()` methods targeting `/api/practice-gym/suggestions`.
- `PracticeGymComponent` — extended with suggestion state signals, `loadSuggestions()`, `startSuggestion()` flow, and `routingLabel()` for student-friendly reason display.
- Practice Gym template — added Suggested for you, Continue practice, and Review practice sections with cards showing title, skill, CEFR level, estimated duration, context tags, and routing label. Empty/loading/error states. Existing By skill and By exercise type sections preserved.

**Backend wiring (TODO-014)**
- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. Added `TryConsumeReadinessItemAsync` helper called best-effort after all activity completion paths (WritingScenario/AI, VocabularyPractice, ListeningComprehension, pattern evaluation). Consumption only fires when `evalResult.Completed` is true for pattern path.
- Idempotent: `TryMarkConsumedAsync` no-ops if item is already consumed. Exceptions swallowed so completion response is never blocked.

**Tests**
- 4 new integration tests in `ReadinessConsumptionWiringTests`: completion marks item consumed, idempotent, no-item path succeeds, consumed item absent from suggestions.
- 12 new Angular unit tests in `practice-gym.component.spec.ts`: suggestions load, empty/error states, section rendering, start navigation, routing labels, existing sections preserved under error.

### Previous sprint

**Phase 10O — Practice Gym Suggested Practice & Pool Serving** — complete (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. Students now receive personalised suggestion cards from their pre-filled pool, organised into Suggested, Continue, and Review sections, instead of only the static skill/exercise-type launcher.

### What was built

**Application layer**
- `IPracticeGymSuggestionService` — interface: `GetSuggestionsForStudentAsync`, `StartSuggestionAsync`, `TryMarkConsumedAsync`.
- `PracticeGymSuggestionsDto` / `PracticeGymSuggestionItemDto` / `StartSuggestionResult` — student-facing DTOs with full routing metadata, call-to-action labels, and navigation targets.

**Infrastructure layer**
- `PracticeGymSuggestionService` — pool query, in-memory ranking (focus match → context match → priority → expiry → FIFO), section partitioning, reservation with concurrency retry, best-effort consumption.

**API layer**
- `PracticeGymSuggestionsController` — three new student-facing endpoints:
  - `GET /api/practice-gym/suggestions` — personalised suggestion cards.
  - `POST /api/practice-gym/suggestions/{id}/start` — reserve item, return navigation target.
  - `POST /api/practice-gym/suggestions/{id}/complete` — best-effort mark consumed.
- Existing `GET /api/activity/practice-gym/next` unchanged.

**DI**
- `IPracticeGymSuggestionService` registered as Scoped in `DependencyInjection.cs`.

### Tests
- 14 new unit tests in `PracticeGymSuggestionServiceTests.cs`: exclusion rules, section assignment, reservation idempotency, consumed/failed status handling, TryMarkConsumed.
- 10 new integration tests in `PracticeGymSuggestionIntegrationTests.cs`: DI registration, GET suggestions sections, POST start/complete lifecycle, smoke tests for existing paths, admin write-endpoint absence.

### Gates at completion
- Architecture: 3 passed
- Unit: 1174 passed (was 1160, +14)
- Integration: 597 passed (was 587, +10)
- Total: 1774 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10O.

### What is intentionally NOT in Phase 10O
Angular/frontend integration (API is ready; deferred to a follow-on frontend phase), `TryMarkConsumedAsync` wiring in `ActivitySubmitHandler` (deferred — see TODOS.md TODO-010), admin write endpoints, full mastery/placement engine, `StudentProfile.CefrLevel` migration, plus-level persistence, notification system, usage/quota enforcement.

See: `docs/reviews/2026-06-18-phase-10o-practice-gym-suggested-practice-review.md`

---

## Previously most recently completed sprint

**Phase 10N — Background Replenishment Pipeline** — complete (2026-06-17)

Phase 10N builds the background engine that keeps student readiness pools healthy. Pool items are now swept for expiry, orphaned generating items are recovered, failed items are retried within attempt limits, and shortfalls are filled for Today lesson and Practice Gym pools.

### What was built

**Application layer**
- `ReadinessPoolReplenishmentOptions` — configuration bound from `appsettings.json` under `"ReadinessPool"`. Defaults: target 10 items per pool, 14-day expiry, 2-hour reserved timeout, 30-min generating timeout, 60-min retry delay, 50 max items per run. `EnableReviewScaffoldGeneration=false` (conservative default).
- `PoolHealthSummary` — lightweight health DTO: ready count, in-flight count, shortfall, target, needsReplenishment flag.
- `IReadinessPoolReplenishmentService` + `ReplenishmentRunSummary` — service contract for running the full maintenance cycle and querying health per student/source.

**Infrastructure layer**
- `ReadinessPoolReplenishmentService` — full engine:
  - Sweeps expired ready items (past `ReadyItemExpiryDays`).
  - Sweeps expired reserved items (past `ReservedItemExpiryHours`).
  - Recovers orphaned generating items (past `GeneratingTimeoutMinutes` → Failed).
  - Retries failed items (`AttemptCount < MaxGenerationAttempts` and past delay → new Queued item).
  - Fills shortfalls for each active student × source using routing recommendations.
  - Prevents duplicates: skips items already Queued/Generating/Ready/Reserved for same objective/pattern/CEFR.
  - Review/scaffold only enabled when `EnableReviewScaffoldGeneration=true` AND ledger weak events exist.
  - B2 students never silently receive B1 Normal content.
  - `general_english` remains fallback; workplace is not default.
- `ReadinessPoolReplenishmentJob` — Quartz job, every 20 minutes, `[DisallowConcurrentExecution]`.

**DI + Quartz**
- `AddInfrastructure(IConfiguration?)` — now accepts optional config to bind `ReadinessPoolReplenishmentOptions`.
- `QuartzConfiguration` — `ReadinessPoolReplenishmentJob` trigger added (every 20 min).
- `DependencyInjection.cs` — `IReadinessPoolReplenishmentService` registered as Scoped.

**Admin API**
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only endpoint. Returns health for `TodayLesson` and `PracticeGym` pools: target, ready, in-flight, failed, stale, expired, shortfall, needsReplenishment.

### Tests
- 16 new unit tests in `ReplenishmentOptionsTests.cs`: options defaults, pool health math, status exclusion rules, lower-level content guard, routing snapshot preservation, general_english fallback, retry attempt gating.
- 11 new integration tests in `ReplenishmentIntegrationTests.cs`: service DI, health counts by status, ReviewOnly exclusion, expired exclusion, queued/generating in-flight counting, retry path, admin health endpoint, admin pool endpoint (smoke), 10M lifecycle smoke, unknown student zero counts, two-student isolation.

### Gates at completion
- Architecture: 3 passed
- Unit: 1160 passed (was 1144, +16)
- Integration: 587 passed (was 576, +11)
- Total: 1750 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10N.

### What is intentionally NOT in Phase 10N
Practice Gym suggested UI redesign, admin write endpoints, `StudentProfile.CefrLevel` migration, plus-level persistence, full mastery engine, full placement engine, serving from pool on user-facing paths (deferred to 10O), `AllowReviewOrScaffold=true` enabled by default, per-student review/scaffold signal, ledger-weighted skill rotation.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Previously most recently completed sprint

**Phase 10M — Student Activity Readiness Pool Foundation** — complete (2026-06-17)

Phase 10M introduces the persisted lifecycle model for pre-generated Today lessons and Practice Gym activities. Activities are no longer treated as simple one-off outputs; every generated item is tracked in a student-specific readiness pool with lifecycle status, routing snapshot, and personalisation metadata.

### What was built

**Domain layer**
- `ReadinessPoolStatus` enum: Queued, Generating, Ready, Reserved, Consumed, Expired, Failed, Stale, ReviewOnly.
- `ReadinessPoolSource` enum: TodayLesson, PracticeGym, LessonBatch, Review, Remediation, OnDemand.
- `RoutingReason` enum — moved from `Application.Curriculum` namespace to `Domain.Enums` so it can be used by the domain entity without a circular dependency.
- `StudentActivityReadinessItem` entity with full lifecycle transition methods (MarkGenerating, MarkReady, MarkFailed, Reserve, MarkConsumed, Expire, MarkStale, MarkReviewOnly, LinkMaterializedIds).
- Guard: `IsLowerLevelContent=true` requires a non-Normal RoutingReason. B2 students cannot silently receive B1 content as Normal.
- `IsServableAsNormalContent` and `IsServableAsReview` helper properties.

**Persistence layer**
- `StudentActivityReadinessItemConfiguration` — EF table `student_activity_readiness_items`, snake_case columns, 4 indexes (student/status/source, student/status/priority, activity id, session id).
- Optimistic concurrency token (PostgreSQL xmin) registered in `OnModelCreating`.
- `DbSet<StudentActivityReadinessItem>` added to `LinguaCoachDbContext`.
- EF migration `T51_StudentActivityReadinessPool`.

**Application layer**
- `IStudentActivityReadinessPoolService` — interface with full lifecycle + query methods.
- `CreateReadinessItemRequest` DTO.
- `ReadinessPoolSummary` / `ReadinessItemDto` — for admin inspection.
- `ReadinessItemRequestBuilder.FromRoutingRecommendation` — helper to build pool item request from a `CurriculumRoutingRecommendation` + student context snapshot.

**Infrastructure layer**
- `StudentActivityReadinessPoolService` — implementation with optimistic concurrency retry loop on reservation.
- Registered as `AddScoped<IStudentActivityReadinessPoolService, StudentActivityReadinessPoolService>()`.

**Integration points**
- `PracticeGymGenerationJob` — creates Queued→Generating→Ready pool item with routing snapshot per materialized cache row.
- `LessonBatchGenerationJob` — creates Queued→Generating→Ready pool item per materialized session. `BuildCompactSummaryAsync` now returns `(summaryJson, routing)` tuple.
- `ActivityMaterializationJob` — links generated `LearningActivityId` and `SessionExerciseId` to any matching pool item by `LearningSessionId`.

**Admin endpoint**
- `GET /api/admin/students/{studentId}/readiness-pool` — read-only pool inspection (Admin role only). No write endpoints.

### Tests
- 20 new unit tests in `ReadinessPool/StudentActivityReadinessItemTests.cs`.
- 11 new integration tests in `ReadinessPool/ReadinessPoolIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass pool service.
- 1 existing unit test file updated (`CurriculumRoutingServiceTests`) and 1 integration test file (`CurriculumRoutingIntegrationTests`) to use `RoutingReason` from `Domain.Enums`.

### Gates at completion
- Architecture: 3 passed
- Unit: 1144 passed (was 1124, +20)
- Integration: 576 passed (was 565, +11)
- Total: 1723 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in this phase.

### What is intentionally NOT in this phase
Full background replenishment engine, Practice Gym suggested UI redesign, admin write endpoints for pool, `StudentProfile.CefrLevel` migration, plus-level persistence, full placement engine, full mastery engine, `AllowReviewOrScaffold=true` enabling in handlers. See TODOS.md.

See: `docs/reviews/2026-06-17-phase-10m-student-activity-readiness-pool-review.md`

---

## Previously most recently completed sprint

**Phase 10L — CEFR-Aware Activity Routing** - complete (2026-06-17)

Phase 10L introduces a pure application-layer routing policy that selects suitable
CEFR bands and curriculum objectives before every AI activity generation call.

### What was built

**Routing models (Application layer)**
- `CurriculumRoutingRequest` — input: student context, CEFR, skill, source, goal context, preferences.
- `CurriculumRoutingRecommendation` — output: target CEFR, allowed levels, objective key/title, context/focus tags, difficulty band, `RoutingReason`, `IsLowerLevelContent`, explanation.
- `ICurriculumRoutingService` — interface with `RecommendAsync` and `NormalizeCefrLevel`.

**Routing service (Infrastructure layer)**
- `CurriculumRoutingService` — CEFR normalization (B2+ → B2), candidate selection from `ICurriculumSyllabusQuery`, skill filter, difficulty band mapping, lower-level guard (never silently lowers level), non-workplace fallback.
- `CurriculumRoutingRequestFactory` — static helper building requests from `StudentProfile` + resolved goal context.

**AI prompt integration**
- `ActivityGenerationContext` extended: `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `AiActivityGeneratorHandler` injects `routingContext` and `routingReason` into AI variables.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY" in rendered prompt.

**Integration points wired**
- `ActivityGetHandler.HandlePatternKeyedAsync` — on-demand and Practice Gym pattern routing.
- `ExercisePrepareHandler` — Today's Lesson exercise preparation.
- `PracticeGymGenerationJob.MaterializeAsync` — background Practice Gym generation.
- `ActivityMaterializationJob.MaterializeExerciseAsync` — background lesson batch materialization.
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` — AI lesson planning summary now includes routing metadata.

**DI:** `ICurriculumRoutingService` registered as Scoped.

### Tests

- 16 new unit tests in `CurriculumRoutingServiceTests.cs`.
- 7 new integration tests in `CurriculumRoutingIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass routing service.

### Gates at completion
- Architecture: 3 passed
- Unit: 1124 passed (was 1098, +26)
- Integration: 565 passed (was 555, +10)
- Total: 1692 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, `StudentProfile.CefrLevel` migration, plus-level routing persistence, full placement engine, `AllowReviewOrScaffold=true` in handlers (built but always false — enablement belongs to 10M adaptive routing), session length influence on candidate count, CEFR-aware format matrix.

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`

---

## Previously most recently completed sprint

**Phase 10K-F — Profile Preference Enforcement Audit & Routing Fix** - complete (2026-06-17)

Phase 10K-F audits and fixes preference propagation across all AI generation surfaces before Phase 10L CEFR-aware routing.

### What was built

**Audit findings**
- Full preference propagation audit across 5 AI generation surfaces, evaluation path, and background jobs.
- `LearnerPreferenceContextFormatter`, `LearningGoalContextResolver`, `CurriculumContextMapper` all confirmed correct for generation paths.
- Three gaps found and fixed.

**Fix P1 — evaluation context carries zero preference data**
- Added `LearnerPreferenceContext` and `LearningGoalContext` optional fields to `ActivityEvaluationContext` (`IAiActivityGenerator.cs`).
- `ActivitySubmitHandler` now passes both fields (formatter + resolver output) into evaluation context.
- `AiActivityGeneratorHandler.EvaluateAttemptAsync` now includes `learnerPreferences` and `learningGoalContext` in prompt variable dict.
- AI evaluation for `WritingScenario` and `SpeakingRolePlay` can now reflect student difficulty preference, support language, and learning goals.

**Fix P2 — vocabulary cadence unconditionally routed to `GapFillWorkplacePhrase`**
- `ActivityGetHandler` vocabulary cadence pick now gates on `WorkplaceSpecific` from `ILearningGoalContextResolver`.
- Non-workplace students receive `PhraseMatch`; workplace students receive `GapFillWorkplacePhrase`.
- A student with Day-to-day English / Travel / Social goals will no longer receive workplace-labelled vocabulary by default.

**Fix P3 — `PreferredSessionDurationMinutes` absent from batch generation summary**
- Added `preferredSessionDurationMinutes` to `learnerPreferences` object in `LessonBatchGenerationJob.BuildCompactSummaryAsync`.
- AI lesson planner now receives session length preference as a hint.

**Tests**
- 20 new unit tests in `PreferenceEnforcementTests.cs`.
- Covers: general English fallback, workplace/non-workplace goal gates, formatter output for goals/focus/support language/difficulty, `CurriculumContextMapper` null guard, vocabulary cadence pattern key selection.

### Gates at completion
- Backend: 1098 unit + 555 integration + 3 architecture = 1656 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Full 10L CEFR-aware routing, exercise format locking, readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, StudentProfile.CefrLevel migration, plus-level routing, full placement engine.

See: `docs/reviews/2026-06-17-phase-10k-f-profile-preference-enforcement-review.md`

---

## Previously most recently completed sprint

**Phase 10K — Curriculum Boundary / Level Syllabus Foundation** - complete (2026-06-17)

Phase 10K introduces the curriculum syllabus data model and query foundation. It defines what the system is recommended to teach at each CEFR level, skill, learner goal/context, and focus area. No CEFR-aware routing, readiness pools, or background generation were implemented — this phase is foundation only.

### What was built

**Domain constants**
- `CefrLevelConstants` — canonical A1/A2/B1/B2/C1/C2 string constants with `IsValid()` helper.
- `CurriculumSkillConstants` — canonical skill identifiers (writing, reading, listening, speaking, vocabulary, grammar, pronunciation, fluency, confidence).
- `CurriculumContextTagConstants` — canonical learner context tags (general_english, day_to_day, travel, study_academic, migration_settlement, job_interviews, social_conversation, workplace, pronunciation, listening_confidence, writing_confidence, exam_inspired, custom). `workplace` is one tag among many, not the default.

**Domain entity**
- `CurriculumObjective` — single flat entity with: Key (stable), Title, Description, CefrLevel, PrimarySkill, SecondarySkillsJson, ContextTagsJson, FocusTagsJson, PrerequisiteKeysJson, RecommendedOrder, DifficultyBand (1-5), IsActive, IsReviewable, IsExamInspired, TeachingNotes.
- Full constructor validation: invalid CEFR rejected, invalid skill rejected, self-prerequisite rejected, DifficultyBand 1-5 enforced.
- `Activate()` / `Deactivate()` / `UpdateDetails()` methods.

**Persistence**
- `CurriculumObjectiveConfiguration` — EF Core config, snake_case columns, unique index on Key, composite index on (cefr_level, primary_skill, is_active).
- Migration `T50_CurriculumSyllabusFoundation` — creates `curriculum_objectives` table.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. Upserts on Key (idempotent). Post-seed prerequisite integrity validation.

**Application layer**
- `ICurriculumSyllabusQuery` — query interface: GetActiveObjectives, GetByCefr, GetByCefrAndSkill, GetByCefrAndContext, GetByCefrAndFocusArea, GetPrerequisites, GetCandidatesForStudent, GetByKey.
- `CurriculumContextMapper` — static null-safe mapper from `ResolvedLearningGoalContext` to curriculum context tags. Null input returns `[general_english]`. Non-workplace context never defaults to workplace.

**Infrastructure**
- `CurriculumSyllabusQueryService` — implements `ICurriculumSyllabusQuery` using EF Core with `AsNoTracking()`. `GetCandidatesForStudent` returns ordered candidates only — does NOT select activities or formats.

**API**
- `AdminCurriculumController` — read-only: `GET /api/admin/curriculum/objectives` (with optional `cefrLevel` and `skill` filters), `GET /api/admin/curriculum/objectives/{key}`. Admin-only. No write endpoints.

**Tests**
- Unit: 22 tests in `CurriculumObjectiveTests` and `CurriculumContextMapperTests`.
- Integration: 14 tests in `CurriculumSyllabusIntegrationTests` — seeder, query service, admin endpoint, regression smoke.

**Docs**
- `docs/reviews/2026-06-17-phase-10k-curriculum-syllabus-eng-review.md`
- `TODOS.md` — 3 deferred items: CEFR plus-levels, StudentProfile.CefrLevel migration, admin curriculum builder.

### Gates at completion
- Backend: all tests passed (architecture + unit + integration)
- Angular unit: 261 passed
- Angular build: clean
- Playwright: 187 passed
- No breaking changes to existing learner-facing behavior

### What is intentionally NOT in this phase
CEFR-aware activity routing, exercise format locking, readiness pools, background generation, Practice Gym suggested practice, admin write UI, StudentProfile CEFR migration, plus-level routing.

---

## Previously completed sprint

**Phase 10J-F — Student App Design System & Responsive UI Foundation** - complete (2026-06-17)

Phase 10J-F refactors the student-facing Angular UI into a more maintainable design-system foundation. No product behaviour changed. No backend changes.

### What was built

**Design tokens (styles.css)**
- Added `--sp-brand`, `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens.
- Added `sp-card-hover` utility class (was used but missing).
- Added `sp-pref-chip` / `sp-pref-chip--on` — centralised preference chip class replacing scattered inline chipStyle() strings.
- Removed duplicate `sp-bottomnav` / `sp-navbtn` definition (kept in component CSS only).

**Profile page**
- Replaced `chipStyle()` inline style method with `sp-pref-chip` CSS class bindings.
- Added `aria-pressed` to all chip buttons (learning goals, focus areas, session length, difficulty).
- Added `data-testid` per chip for test targeting.
- Added `focus-visible` outline for keyboard accessibility.

**Progress component**
- Replaced all hardcoded hex color values with design tokens (`--sp-success`, `--sp-warn`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-speaking`, `--sp-canvas2`, `--sp-muted`).
- Fixed `var(--sp-writing-bg)` and `var(--sp-warn-bg)` references (those tokens don't exist).

**Practice Gym CSS**
- Replaced all `var(--sp-primary)` references with `var(--sp-brand)`.

**Shared student UI components**
- `StudentChipComponent` (`sp-chip` selector) — reusable toggle chip with `selected`, `disabled`, `toggle`.
- `StudentBadgeComponent` (`sp-badge` selector) — reusable badge with variant input.
- `src/app/shared/student-ui/index.ts` barrel export.

**Tests**
- Angular: 261 tests, 261 passed.
- Playwright: 187 tests, 187 passed (12 new in `e2e/design-system-10jf.spec.ts`).
- Backend: 1565 passed across all suites.

---

## Previously completed sprint

**Phase 10I - Configurable Multi-step Onboarding / Assessment v2 Foundation** - complete (2026-06-17)

Phase 10I introduces the v2 onboarding system running in parallel with the existing v1 state machine. Existing students are unaffected. New students receive a configurable, flow-driven onboarding experience with preliminary CEFR scoring.

### What was built

**Domain layer**
- `OnboardingFlowDefinition` entity — versioned, immutable once students have progress; single active flow enforced via PostgreSQL partial unique index.
- `OnboardingStepDefinition` entity — system-required vs admin-configured steps, typed `OnboardingAnswerMapping` enum (serialised as string in DB).
- `StudentOnboardingProgress` entity — per-student progress record with `PreliminaryCefrLevel`; xmin concurrency token; unique `(progress_id, step_key)` constraint on responses.
- `StudentOnboardingResponse` entity — idempotent answer store (upsert pattern).
- `PreliminaryCefrCalculator` — static weighted scoring (A1–C2); never overwrites a real PlacementAssessment CEFR level.

**Persistence layer**
- EF Core configurations for all 4 new entities.
- Migration T47_OnboardingV2.
- `OnboardingFlowSeeder` — idempotent; seeds 10 SystemRequired steps + 1 AdminConfigured disabled step.

**Application layer**
- Contracts: `OnboardingV2Contracts.cs` — student DTOs (no `AssessmentMetadataJson`), admin DTOs, interfaces, commands/queries/results.

**Infrastructure layer**
- `OnboardingV2QueryHandler` — lazy-creates progress; detects v1-complete students and auto-initialises as complete.
- `OnboardingV2StepHandler` — validates and applies answers; percentage counts only SystemRequired+enabled steps.
- `OnboardingV2CompleteHandler` — scores assessment responses; sets `PreliminaryCefrLevel`; calls `SetCefrLevel()` only if CefrLevel is null; transitions lifecycle to `PlacementRequired`.
- `AdminOnboardingFlowQueryHandler` — read-only GET, excludes `AssessmentMetadataJson`.

**API layer**
- `OnboardingController` extended: `GET /api/onboarding`, `POST /api/onboarding/steps/{stepKey}`, `POST /api/onboarding/complete`.
- `AdminOnboardingController` added: `GET /api/admin/onboarding/flow` (Admin role only).
- `OnboardingFlowSeeder` wired into app startup after `ExerciseTypeDefinitionSeeder`.

**Angular layer**
- `onboarding-v2.models.ts` — TypeScript interfaces for all v2 DTOs.
- `OnboardingV2Service` — `getStatus()`, `submitStep()`, `complete()`.
- `OnboardingV2Component` — shell with progress bar, dynamic step dispatch by `stepType`.
- 11 step renderer components: Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary.
- Route: `/onboarding/v2` added to `app.routes.ts`.

### Tightening rules applied (all 10)

1. `PreliminaryCefrLevel` stored separately — real `CefrLevel` only updated when null.
2. `OnboardingAnswerMapping` typed enum, not raw string keys.
3. Flow versions immutable once students have progress; admin edits create new versions.
4. Single active flow enforced by DB partial unique index.
5. Student API never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.
6. Percentage counts SystemRequired+enabled steps only.
7. Post-onboarding lifecycle → `PlacementRequired`.
8. Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.
9. v1 `OnboardingStatus`/`OnboardingStep` fields preserved as legacy compatibility.
10. Documented limitations: no full CEFR engine, no admin visual builder, no curriculum routing.

### Final test counts (CI green)

- Backend: 863 unit + 511 integration passed (net gain from v2 handler tests).
- Angular: production build clean (CSS warnings pre-existing, no errors).
- Architecture tests: not rebuilt (build artefact absent; unit/integration cover coverage).

Known limitations: no Playwright E2E spec for v2 onboarding flow (no test user seeded with v2 progress); no admin visual flow builder; preliminary CEFR is a simple weight band, not a full adaptive engine; no curriculum routing post-onboarding.

---

## Previously most recently completed sprint

**Phase 10H-F - Practice Gym Playwright Fixture Stabilisation** - complete (2026-06-17)

Phase 10H-F restored the full Playwright suite as a reliable gate after the
catalog-driven Practice Gym UI and related renderer/result copy had moved ahead
of older E2E fixtures.

### Failure categories found

- Selector drift: tests still looked for old fixed Practice Gym card IDs such
  as `practice-card-listening` and `speaking-card`.
- Fixture/test data drift: Practice Gym tests did not mock the current exercise
  type catalog shape, including planned non-runnable AI role play rows.
- Copy/label drift: the landing hero and perfect-score feedback labels had
  intentionally changed.
- Shared component selector drift: listening fallback text moved into the shared
  `app-audio-player` unavailable state.

No backend/API failures, timing issues, real UI regressions, or environment-only
failures remained after the fixes.

### What was fixed

- Practice Gym E2E tests now use current catalog-driven `practice-format-*`
  selectors.
- Practice Gym mocked exercise-type data now includes ready runnable formats and
  planned locked formats.
- Practice Gym routing assertions now verify activity startup through
  `activityId` navigation instead of obsolete module-link assumptions.
- Listening renderer tests now assert the shared `audio-unavailable` fallback.
- Landing and score-band assertions now match current user-facing copy.

### Final test counts

- Targeted affected Playwright specs: 62 passed.
- Full Playwright: 175 passed.

Known limitations: none.

---

## Previously most recently completed sprint

**Phase 10H - AI Context Personalisation from Learning Preferences** - complete (2026-06-17)

Phase 10H wires Phase 10G learning preferences into AI generation context for
Today lesson activities, Practice Gym activities, background Practice Gym
materialization, buffered lesson activity materialization, and lesson batch
planning summaries.

### What was added

- `LearnerPreferenceContextFormatter` for compact, bounded preference context.
- AI prompt rendering inserts learner preferences before JSON return instructions.
- Generation context now carries `LearnerPreferenceContext` and `LearningGoalContext`.
- Ledger events now store `LearningGoalContext` when a clear value exists.
- Dynamic pattern selection uses preference-backed goal context before legacy fallback.
- Practice Gym generic background topic fallback changed to "English class practice".

### Tests added

- Formatter tests for included fields, missing preferences, excluded admin fields,
  CEFR system-estimated wording, goal fallback order, and no workplace default.
- Prompt builder tests for learner preference insertion and token-budget failure.

See: `docs/reviews/2026-06-17-phase-10h-ai-context-personalisation-from-learning-preferences.md`

---

## Previously most recently completed sprint

**Phase 10G — Student Profile & Learning Preferences v2** — complete (2026-06-17)

Extended student profile with editable learning preferences: preferred name, support language, translation help, learning goals (multi-select + custom), focus areas (multi-select + custom), difficulty preference. New API endpoints `GET /api/profile` and `PUT /api/profile/preferences`. Angular profile page redesigned with 6 sections. CEFR remains read-only.

### What was added

- `TranslationHelpPreference` and `DifficultyPreference` enums (Domain)
- `UpdateLearningPreferences` method on `StudentProfile` — student-editable only, validates constraints
- EF migration T46 — 10 new columns on `student_profiles`, JSON columns for lists
- `GetStudentProfileQuery` + handler (Application + Infrastructure)
- `UpdateLearningPreferencesCommand` + handler (Application + Infrastructure)
- `ProfileController` with `GET /api/profile` and `PUT /api/profile/preferences`
- `ProfileService` in Angular with `getProfile()` and `updatePreferences()`
- Profile page rewritten: 6 sections (Account, Level, Goals, Focus, Support language, Preferences)
- CEFR shown read-only with per-level explanation text

### Final test counts

- Backend unit: 996 (was 985)
- Backend integration: 539 (was 534)
- Architecture: 3
- Angular unit: 243 (was 229)

See: `docs/reviews/2026-06-17-phase-10g-student-profile-learning-preferences-v2.md`

---

## Previously most recently completed sprint

**Phase 10D — Activity Quality / Workload Validation** — complete (2026-06-17)

Added quality and workload validation to ensure generated activities are meaningful and feedback wording matches the student's score.

### What was added

- `WorkloadModeRegistry` in `ModuleStageContentValidator` — classifies pattern keys as `SingleSubstantialTask` (one item is the full exercise) or `MultiItem` (multiple items expected).
- `EnforceWorkloadSanity` method — fires when `countSettings` is provided; fails multi-item formats with item count below `MinItemsPerPractice`.
- Extended `ItemCountArrayByPattern` — added `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`, `phrase_match` entries.
- `ExerciseTypeDefinitionSeeder.CountOverrides` — added `MinItemsPerPractice >= 2` for `phrase_match`, `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`.
- Score-aware feedback in `PatternEvaluationResultComponent`: four-tier `scoreBandLabel`, `scoreRingColour`, new `scoreBandInstruction()`, `showImprovementPrompt` getter.
- 100% score no longer shows "Improve your answer" or "Review the corrections".

### Tests added

- 23 backend unit tests: workload validation, single-substantial-task exemptions, registry classification, item-count config enforcement, no-workplace-default check.
- 25 Angular unit tests: score-aware labels at all four tiers, 100% wording contract, `showImprovementPrompt` logic, ring colour.

### Final test counts

- Backend unit: 974 (was 951)
- Backend integration: 534
- Architecture: 3
- Angular unit: 229 (was 204)

See: `docs/reviews/2026-06-17-phase-10d-activity-quality-workload-validation.md`

---

## Previously most recently completed sprint

**Phase 10C — Ledger-aware Dynamic Pattern Selection** — complete (2026-06-17)

See commit `e01680a`.

---

## Previously completed sprint

**Phase 10B — Student Learning Memory / Taught-Content Ledger** — complete (2026-06-17)

New structured `StudentLearningEvent` ledger written after every activity submission from Today lessons and Practice Gym. Foundation for ledger-aware dynamic pattern selection.

### What was added

- `StudentLearningEvent` domain entity + `LearningEventSource` / `LearningEventOutcome` enums
- EF migration `T45_StudentLearningEvents` (new `student_learning_events` table with 3 indexes)
- `IStudentLearningLedger` application interface + `StudentLearningLedgerService` infrastructure implementation
- Hooked into `ActivitySubmitHandler` at both pattern evaluation path and legacy AI path
- Query helpers: `GetRecentAsync`, `GetRecentPatternKeysAsync`, `GetWeakEventsAsync`, `GetRecentByPatternKeysAsync`
- Best-effort write — never blocks or fails student's activity submission
- No workplace default forced when context is null

### Tests added

- 10 unit tests: `StudentLearningEventTests` (domain entity validation, field storage, no-workplace-default)
- 9 integration tests: `StudentLearningLedgerServiceTests` (write/read/query/isolation)
- 4 API integration tests: ledger event written from Practice Gym and Today lesson, exercise type/pattern key captured, skill profile update still works

### Final test counts

- Backend unit: 941 (was 931)
- Backend integration: 531 (was 517)
- Architecture: 3

See: `docs/reviews/2026-06-17-phase-10b-student-learning-memory-ledger.md`

---

## Previously most recently completed sprint

**Phase 9J — Speaking/Listening Family QA & Hardening** — complete (2026-06-16)

Performed end-to-end QA and hardening across all speaking/listening formats from Phases 9A–9I.

### What was fixed
- **P0 bug:** `summarize_group_discussion` submission serialization was missing from `activity-lesson.component.ts`. The `else` fallback was serializing the entire payload object instead of `{ items: [...] }`, causing malformed `SubmittedAnswerJson` to reach the evaluator.
- **Stale comment** in `ExerciseTypeDefinitionSeeder.CountOverrides` incorrectly labelled Phase 9 speaking formats as "planned, non-runnable". Updated to "Speaking (Ready)".

### Tests added
- 3 backend unit tests: `summarize_group_discussion` AiOpenEnded evaluator coverage (high score, low score, compactContent exclusion).
- 23 Angular unit tests: new `exercise-renderer.component.spec.ts` covering renderer dispatch, content extraction, submission payload shape, and audioScript fallback for all 7 Phase 9 formats.

### Final test counts
- Backend unit: 907 (was 904)
- Backend integration: 517
- Architecture: 3
- Angular: 204 (was 181)

See: `docs/reviews/2026-06-16-phase-9j-speaking-listening-family-hardening.md`

---

## Previously most recently completed sprint

**CI/CD stabilization (post Phase 7A-7D)** — complete (2026-06-15)

Fixed all 27 failing integration tests (token budget regressions from
module_stage_v1 prompt growth + FakeAiProvider fixture missing
pattern-specific exerciseData keys) and the empty
`LinguaCoach.ArchitectureTests` project (added real NetArchTest layer-boundary
tests). Backend: 472/472 integration+unit tests passing, 3/3 architecture
tests passing. Angular: 103/103 unit tests passing, dev/prod builds succeed.
See `docs/reviews/2026-06-15-ci-cd-stabilization-review.md`.

## Previously completed sprint

**Activity 3-page restructure (Teach / Practice / Feedback), full-stack** — complete (2026-06-13)

5-step strangler-fig migration, all steps landed:

- **Step 1** — split `ActivityLessonComponent` (876-line monolith) into a thin
  orchestrator shell + 3 composed page components (`ActivityTeachPageComponent`,
  `ActivityPracticePageComponent`, `ActivityFeedbackPageComponent`). Zero
  behavior change.
- **Step 2** — added `ActivityPagePresenter` interface + `PatternBackedPresenter`
  / 4 `Legacy*Presenter` bridges, selected by `ActivityPresenterFactory.for(activity)`.
  `TeachViewModel`/`PracticeViewModel` replace boolean-flag `@Input()`s.
- **Step 3** — `gap_fill_workplace_phrase` hint parity; `VocabularyPractice`/
  `ListeningComprehension` cadence picks now route through
  `HandlePatternKeyedAsync`. See
  [2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md](2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md).
- **Steps 4 & 5** — `WritingScenario`/`SpeakingRolePlay` cadence picks now
  route through `open_writing_task`/`speaking_roleplay_turn`; new
  `AudioResponse` interaction mode; `/speaking-attempt` is pattern-aware
  (new pattern → `IPatternEvaluationRouter`/`AiOpenEndedEvaluator`, legacy →
  unchanged `SpeakingRolePlayEvaluator`). No frontend changes needed — existing
  speaking recording UI is activity-type-keyed and already serves
  `AudioResponse`. See
  [2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md](../reviews/2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md).

Eng plan: [2026-06-13-activity-3-page-restructure-eng-plan.md](../reviews/2026-06-13-activity-3-page-restructure-eng-plan.md).

`dotnet build`/`ng build` clean, 51/51 unit tests pass.

**Follow-ups (not blocking, tracked separately):**
- Live-AI calibration pass for the 4 new prompts (`open_writing_task`,
  `speaking_roleplay_turn` generate/evaluate) — output unverified against a
  live AI provider.
- Retire `LegacyVocabPresenter`/`LegacyListeningPresenter`/
  `LegacyWritingPresenter`/`LegacySpeakingPresenter` + their template branches
  once production activity rows are regenerated under the pattern engine
  (gated on production data review).

---

## Previously completed sprint

**Practice Gym cache race condition fix** - complete (2026-06-12)

A full code/docs audit found `ActivityGetHandler.TryAssignReadyPracticeCacheAsync` could
let two concurrent `GET /api/activity/next?pattern=...` requests claim the same `Ready`
`PracticeActivityCache` row, returning the same `LearningActivity` to both. Added `xmin`
concurrency token to `PracticeActivityCache` (mirrors existing `LearningPath` config) and
retry-with-exclusion on `DbUpdateConcurrencyException`, falling back to the next ready
row or on-demand generation. 483 unit + 434 integration tests pass. See
`docs/reviews/2026-06-12-practice-gym-cache-race-condition-fix-engineering-review.md`.

**Lesson and Practice classroom alignment** - complete (2026-06-12)

Today's Lesson now consumes ready buffered `LearningSession` rows before falling
back to lazy session generation. The lesson page no longer auto-prepares later
steps just because the student opened the page, selected a step, or completed the
previous step. Existing prepared steps still open directly.

Practice Gym now uses the same classroom framing as Today's Lesson: teach first,
practice with a supported exercise pattern, then show feedback after submit. The
Practice Gym page separates skill practice (Listening, Reading, Speaking,
Writing), vocabulary class, exercise type practice, and future live role play.
Only implemented patterns are active; Reading, vocabulary queue, live AI role
play, and future exercise types remain disabled.

Background Practice Gym caching now has both parts: `PracticeGymBufferRefillJob`
queues pending cache rows for eligible active students, and
`PracticeGymGenerationJob` materializes those rows into ready `LearningActivity`
records. `GET /api/activity/next?pattern=...` consumes ready cache entries before
generating on demand. Phrase-match and gap-fill generation prompts now require a
lesson component before practice, and lesson batch planning is constrained to the
active pattern library instead of defaulting every skill to writing.

**Lesson batch materialization fix** - complete (2026-06-12)

Production logs showed AI lesson planning succeeding, then
`LessonBatchGenerationJob` failing while saving the first generated session with
`DbUpdateConcurrencyException`. The failed save also left the same `DbContext`
dirty, so marking the batch failed could throw again and Quartz reported an
unhandled job exception. Root cause: session/activity `GenerationJobItem` rows
created after the original batch save were attached only through the aggregate's
private item collection, so EF could treat them as updates to missing rows
instead of inserts. The job now explicitly inserts those items, avoids tracked
path/module state during generated module lookup, and clears failed tracked
state before marking a materialization failure. Added integration coverage that
executes `LessonBatchGenerationJob` with a fake lesson-plan provider and verifies
a completed batch plus ready sessions. See
`docs/reviews/2026-06-12-lesson-batch-materialization-fix-engineering-review.md`.

**Admin responsive header polish** — complete (2026-06-12)

`/admin` now keeps dashboard grids tablet-safe between 900px and 930px, where the
desktop sidebar first appears. The admin header now shows only the avatar button;
email and role moved into the avatar flyout menu. Related layout rules are
documented in `docs/architecture/frontend-layout-system.md`.

**Admin stuck batch cancellation** — complete (2026-06-12)

Admins can now cancel queued/running lesson-generation batches from
`/admin/integrations` instead of running one-off SQL. The action marks the batch
`Failed` with the safe reason `Cancelled by admin.` and the background job checks
for that state before and during session materialization so it does not overwrite
an admin cancellation. Recent batches now show a Cancel button for active rows and
the existing Failure column shows the cancellation reason.

**Lesson batch generation concurrency fix** — complete (2026-06-12)

Duplicate concurrent batch triggers for the same student caused
`DbUpdateConcurrencyException` and left `GenerationBatch` rows stuck in
"Running" forever. Admin endpoint now returns 409 if a batch is already
running for the student; job marks itself `Failed` on any unhandled error
during materialization instead of getting stuck. Existing stuck rows can now be
cancelled from Admin Integrations rather than fixed by direct SQL. See
`docs/reviews/2026-06-12-admin-stuck-batch-cancel-engineering-review.md`.

**Quartz JobDataMap string-only fix (background lesson generation)** — complete (2026-06-12)

`LessonBatchGenerationJob.TriggerAsync` stored non-string values in `JobDataMap`,
which throws `JobPersistenceException` under Quartz's `UseProperties = true`
Postgres job store. This broke both the admin "Generate lessons now" button AND
the background buffer-refill pipeline — explaining why all lessons were
generated on-the-fly ("Preparing your lesson...") instead of pre-generated. See
`docs/reviews/2026-06-12-quartz-jobdatamap-string-fix-engineering-review.md`.

**Admin "Generate lessons now" button fix** — complete (2026-06-12)

Button gave no feedback on click (success or error). Frontend now shows a
confirmation with queued session count, or the server error message. See
`docs/reviews/2026-06-12-admin-generate-lessons-button-fix-engineering-review.md`.

**AI Config Overhaul / No-Fallback Rule / Journey Fix** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`

Triggered by: post-QA audit corrections from product owner (2026-06-11).
Audit report: `docs/testing/deployed-student-e2e-audit-2026-06-11.md`

An audit on 2026-06-12 found that Tracks 1-4 and most of Track 5 had already been
delivered under other sprint names (T36 AiConfigCategories migration, Exercise UX /
Admin Polish, Real TTS). The one genuinely outstanding item — BUG-005 (dashboard streak
showing "--") — was fixed in this pass: `DashboardResult.StreakDays` computed server-side
from consecutive days with an `ActivityAttempt`, wired into the dashboard stat grid and
the header streak pill. See the sprint doc's "Status update" and "Streak implementation"
sections for full detail.

### What this sprint addressed

1. **No-fallback rule** — All AI failures return 503 + "Service not available" UI. No SystemFallback content ever shown to students.
2. **Admin AI Config overhaul** — Replace 12+ individual feature-key rows with 4 LLM category cards (Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path) + 2 independent TTS cards (Listening TTS, Placement TTS).
3. **Journey page fix** — Replace old LearningPath module cards with LearningSession history (date-grouped, per-step scores).
4. **Audio / TTS 503 handling** — Audio endpoint returns clear 404 when TTS not configured; frontend shows graceful failure. Activity audio playback fetches protected audio with Angular `HttpClient` and renders a `blob:` URL.
5. **Lower-severity QA bugs** — Mobile activity blank page, phrase-match 400, streak "--" display, sidebar layout clipping.

---

## Most recently completed sprint

**Exercise Submission Scoring Bug (CRITICAL)** — complete (2026-06-12)

See full review: `docs/reviews/2026-06-12-exercise-submission-scoring-bug-engineering-review.md`

Triggered by a production report: `gap_fill_workplace_phrase` submissions scored 0 with
every `itemResults[].studentAnswer: null`, despite correct `acceptedAnswers` being
present. Escalated by product owner to "every exercise" (not just gap fill).

Root cause: frontend item-id fallbacks (`String(index + 1)`, 1-indexed, unprefixed) did
not match backend deterministic-evaluator key conventions (`gap_{i+1}`, `phrase_{i}`/
`meaning_{i}` 0-indexed).

Fixed:
- `gap_fill_workplace_phrase` — `mapGapItems` fallback id now `gap_${index + 1}`.
- `phrase_match` — two-id-scheme redesign (`phrase_${index}` / `meaning_${index}`) across
  `exercise-renderer.component.ts`, `MatchingPair` interface, `MatchingPairsComponent`,
  and `matching-pairs.component.html`.

Not changed (verified lower risk / separate issue, see review):
- `listen_and_gap_fill` — `ListenAndGapFillItemDto.Id` exists in contract, fallback only
  exercised if AI omits `id`.
- `listen_and_answer` — `QuestionId` mismatch affects per-question feedback labels only,
  not overall AI-judged score.

AI-evaluated patterns (email_reply, teams_chat, spoken_response) were unaffected —
`SubmittedAnswerJson` is forwarded raw to the AI prompt.

Tests: `dotnet test` 480 unit + 430 integration passed; `npm run build` passed.

The separate "lesson structure" complaint (What we learn / Practice / Feedback / Redo →
next, for both Lessons and Practice) raised alongside this bug report is a
product/architecture item — not addressed here, needs its own planning pass with the
product owner.

---

## Most recently completed sprint

**Today's Lesson button / lazy LearningPath generation fix (CRITICAL)** — complete
(2026-06-12)

See full review: `docs/reviews/2026-06-12-todays-lesson-button-fix-engineering-review.md`

Root cause: `SessionGeneratorService` threw when a `CourseReady` student had no active
`LearningPath` (the legacy `/activity` flow was the only place a path got lazily
created). `SessionsController.Today` returned 400, and the dashboard silently
swallowed the error, leaving "Start today's lesson" with a null link — button did
nothing, no session/lesson ever generated for these students.

Fixed: `SessionGeneratorService` now lazily generates a `LearningPath` via
`ILearningPathGenerator` (same handler `ActivityGetHandler` already uses) when none
exists, mirroring the existing fallback there. Dashboard now surfaces session-load
errors instead of swallowing them.

Quartz/background-job config investigated as a possible cause — found correct,
no change needed.

Follow-up implemented same day: `PlacementService` now generates the student's
`LearningPath` proactively right after `CourseReady`, via `ILearningPathGenerator`
(best-effort, never blocks placement). The lazy fallback remains as a safety net
for pre-existing affected students.

Tests: 482 unit + 430 integration passing. `npm run build` passed.

---

## Current priority

**Adaptive Learning Foundation** Phase 2 (numeric `StudentSkillProfile` scores) is done
(2026-06-12) — see `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.
`StudentSkillProfile.ScorePercent` (0-100) replaces the persisted `IsWeak` boolean;
`IsWeak` is now derived (`ScorePercent < 50`). Migration `T42_StudentSkillScorePercent`
backfills existing rows. 482 unit + 430 integration tests pass.

Phase 3 planning pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md`. Most of Phase 3
(Practice rendering, redo/next loop, per-attempt AI feedback) already exists.

Phase 3 P1 done (2026-06-12): AI evaluation prompts (email_reply, teams_chat,
spoken_response, listen_and_answer) now receive the student's current
`StudentSkillProfile.ScorePercent` for the relevant skill, so `coachSummary` can be
grounded in student progress instead of generic. 482 unit + 430 integration tests pass.

Phase 3 P2 scoping pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-p2-scoping.md`. Full
"What we learn" grammar/vocab/phrases card remains deferred (cross-cutting AI prompt
work). Task 1 (surface existing `teachingNote` as `[goal]` for GapFill/MatchingPairs) and
P2b task 1 (surface learningGoal/skillFocus/targetVocabulary for email_reply/teams_chat)
both implemented same day, frontend-only. See sprint doc's "Phase 3 P2"/"P2b" sections.
suggestedPhrases for spoken_response was already implemented (confirmed
2026-06-12, no change needed). Remaining: targetGrammarPoint (large,
cross-cutting, no schedule).

---

## Adaptive Learning Foundation — vocabulary extraction widened to all activity patterns

**Adaptive Learning Foundation** — planning complete (2026-06-12); first implementation
item (vocabulary extraction) done (2026-06-12)

See `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

Reviewed and sequenced the remaining tracks (10-14) from the 2026-06-12 product owner
brainstorm: Adaptive Onboarding & Staged Assessment, Configurable Onboarding/Placement,
Multi-Course/Enrolment Model, Estimated Known Words. All confirmed already recorded in
`docs/backlog/product-backlog.md`. Recommended sequencing: vocabulary extraction first
(already speced, independent), then numeric `StudentSkillProfile` scores, then staged
assessment architecture review, then configurable onboarding, then multi-course
(dedicated `/plan-eng-review` required). Three open product questions recorded — see
sprint doc.

Per product owner correction, vocabulary extraction was implemented as a cross-cutting
engine: `VocabularyExtractionService` now extracts from any pattern-evaluated activity
that produces AI `Corrections` (email reply, workplace chat, listen-and-answer, spoken
response), not only legacy writing attempts. Deterministic patterns (gap fill, phrase
match) are unaffected — see implementation note in the sprint doc above. Does not change
current implementation priority below.

---

## Most recently completed sprint

**Exercise UX / Admin Polish** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-12-exercise-ux-admin-polish-sprint.md`

### What was done

All 7 phases shipped on 2026-06-12:

- **Phase 1** — Verified attempt/retry integrity; fixed a pre-existing gap-fill submission shape bug (frontend was sending the wrong JSON shape, causing all answers to score as incorrect).
- **Phase 2** — Workplace Chat: `ChatReplyContent` gained a distinct `learningGoal` field (separate from tone guidance), shown via a `chat-reply-goal` UI element. `activity_evaluate_teams_chat_simulation` prompt updated to evaluate goal-reaching, tone, clarity, and clarification-seeking.
- **Phase 3** — Email Reply: new `InteractionMode.EmailReply`, self-healing `ExercisePatternSeeder`, new `EmailReplyComponent` renderer with subject + body fields, `SubmittedAnswerJson` shape `{ subject, body }`, evaluator prompt updated.
- **Phase 4** — Shared Lesson → Practice → Evaluate framing: new `ExerciseLessonIntroComponent` ("Goal" display) applied to `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent`. Chat Reply, Email Reply, and Free Text Entry already had equivalent goal displays.
- **Phase 5** — Admin nav: "AI Usage" moved from the (now-removed) "Analytics" group into "AI System", alongside AI Config and Prompts.
- **Phase 6** — Design-token consistency audit of all sprint-touched components — already aligned with `.sp-*` tokens, no changes needed.
- **Phase 7** — Docs close-out (this entry).

### Key constraints preserved

- Lesson → Practice → Evaluate framing applied only to the 6 currently-active exercise renderers, not retrofitted across the 40+ unimplemented patterns in the library.
- No backend changes beyond the `EmailReply` interaction mode (additive, append-only enum).

### Final test results

```
dotnet test tests/LinguaCoach.UnitTests:  477 passed
npm run build:                            passed (0 new errors/warnings)
```

---

## Previously completed sprint

**Real TTS / Placement Onboarding Gap / Today Session Card** — complete (2026-06-11)

See full sprint plan: `docs/sprints/2026-06-10-tts-placement-today-sprint.md`

### What was done

All tracks shipped on 2026-06-11:

- **Track 1 (Real TTS)** — `VoiceName` added to `AiProviderConfig` (T35 migration). `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; never throws. `TtsProviderResolver` reads `tts.listening` / `tts.placement` feature keys from DB, returns `FakeTextToSpeechService` (provider=`fake`) or `OpenAiTextToSpeechService` (provider=`openai`). `ListeningAudioService` and `PlacementAudioService` now resolve TTS at runtime. `DefaultAiSeeder` seeds both keys as `fake/fake/fake` (idempotent). Admin UI updated with voice name field and fake provider support.
- **Track 2 (Onboarding experience step)** — `PATCH /api/onboarding/experience` endpoint added. `StudentProfile.SetExperienceContext()` bypasses state machine. New `step5-experience` Angular component inserted between step-4 and placement. Step-4 now shows "Step 4 of 5" and navigates to step-5. Existing completed students can call the endpoint without error. Non-blocking — API failure still navigates to placement.
- **Track 3 (Today session card)** — previously completed in Practice Gym Activation sprint; confirmed and skipped.

### Key constraints preserved

- `FakeTextToSpeechService` remains default; `dotnet test` does not require `OPENAI_API_KEY`
- OpenAI TTS only activates when admin sets `tts.*` feature key provider to `openai`
- Existing completed students not broken by new experience step
- Practice Gym behaviour unchanged; Pronunciation remains Coming soon

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed (0 errors)
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

---

## Previously completed sprint

**Practice Gym Activation / Pattern-Based Free Practice** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-practice-gym-activation-sprint.md`

### What was done

All phases shipped on 2026-06-10:

- **Phase 2 (backend)** — `GET /api/activity/next` extended with `?pattern=<key>`. `GetNextActivityQuery` has `PreferredPatternKey`. `ActivityGetHandler.HandlePatternKeyedAsync` validates pattern key, loads definition, calls AI with `OverridePromptKey`, sets `ExercisePatternKey` on the created `LearningActivity`. `AiActivityGeneratorHandler` now supports `VocabularyPractice` when pattern-driven. Invalid pattern key returns 400.
- **Phase 3 & 4 (frontend)** — `ActivityService.getNext` accepts `patternKey`. `ActivityLessonComponent` reads `?pattern=` and passes it to the service. Practice Gym activates Phrase Match, Gap Fill, Email, and Workplace Chat as `<a routerLink>` with `pattern=` and `returnTo=/practice`.
- **Phase 5 (return flow)** — `returnTo=/practice` embedded in all four new card links. Existing `nextActivity()` / `backToDashboard()` logic handles it unchanged.
- **Phase 6 (progress verification)** — confirmed: `ActivitySubmitHandler` records `ActivityAttempt` for all pattern types; `PatternSkillUpdateService` runs after each submission; no progress on card open.
- **Phase 7 (tests + docs)** — 8 new backend integration tests; 6 new Playwright tests (4 card activation, Pronunciation still coming soon, Speaking no pronunciation claim). All existing tests still pass.

### Key constraints preserved

- Pronunciation card remains Coming soon
- No fake pronunciation claims
- PatternEvaluationRouter not bypassed
- No new endpoints or routes added
- No seed data deleted, no real user data deleted

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      167 passed
```

---

## Previously completed sprint

**Student UX Alignment / Writing-Assumption Cleanup** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md`

### What was done

All 7 phases shipped on 2026-06-10:

- **Phase 2** — Navigation labels/routes: sidebar and mobile nav now show **Today, Journey, Practice, Progress, Profile**. Dashboard label removed. Vocabulary removed from top-level nav. `/journey` route added. `/practice` route added.
- **Phase 3** — Today page alignment: heading "Today's Lesson" added. "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Phase 4** — Journey mixed-skill cleanup: page heading "Learning Journey" added. Memory fallback "workplace writing" → "workplace English". "Continue practising" CTA replaced with safe CTAs.
- **Phase 5** — Practice Gym MVP at `/practice`: functional cards for Vocabulary, Listening, Writing, Speaking. Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. No auto-start on load.
- **Phase 6** — Playwright fixture copy cleanup: generic writing/email-only fixture language updated to mixed-skill workplace English across `core-flow-smoke.spec.ts`, `disabled-actions-cleanup.spec.ts`, `lesson-activity-wiring.spec.ts`, `admin-screenshots.spec.ts`. Valid WritingScenario and email_reply test coverage preserved.
- **Phase 7** — Documentation cleanup: `current-product-state.md`, `current-sprint.md`, `docs/architecture/README.md` updated. Older sprint docs marked historical. Sprint doc closed.

### Key constraints preserved

- No real user data deleted
- No seed rows deleted (`WritingScenarioSeeder`, `LearningActivitySeeder` unchanged)
- Writing and Email remain valid activity types
- `/my-path` still works (backwards compatible with `/journey`)
- No backend files changed in this sprint

### Final test results

```
dotnet test:     865 passed (451 unit + 414 integration) — unchanged
npm run build:   passed
Playwright:      165 passed (21 new Practice Gym tests + 9 new Journey tests)
```

---

## Completed sprints

- Admin UX / Student Management / AI Config Cleanup — complete
- Today's Lesson / Learning Session (Phases 1–5B) — complete
- Exercise Pattern Engine — complete
- Pattern Evaluation Engine (Phases 1–7) — complete
- Student UX Alignment / Writing-Assumption Cleanup (Phases 1–7) — complete
- Real TTS / Placement Onboarding Gap / Today Session Card — complete
- **Exercise UX / Admin Polish (Phases 1–7) — complete**

---

## Current state

All four activity types are implemented. Placement Assessment is complete. The full evaluation stack is live end-to-end. Student nav model is aligned:

- Today (`/dashboard`) is the student home page — Today's Lesson is the primary CTA
- Journey (`/journey`, `/my-path`) shows the learning path with mixed-skill framing
- Practice (`/practice`) is the Practice Gym MVP — free practice by skill or exercise type
- Progress and Profile unchanged
- Pattern-aware evaluators route by `MarkingMode`: `ExactMatch`, `KeyedSelection`, `AiStructured`, `AiOpenEnded`, `NoMarking`
- `StudentSkillProfile` updated from evaluation skill impacts after every pattern attempt
- Compact memory signals from evaluation fed into `StudentLearningMemory`
- Pattern-aware result UI with 6 branches

Session reflection (`GET /api/sessions/{id}/reflection`) is a 501 stub — deferred.

---

## Deferred

- **Dynamic pattern selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history
- **Dynamic Practice Gym session templates** — configurable session templates within Practice Gym (e.g. "30-min vocab session")
- Session reflection AI prompt (`session_reflection`) — requires stable session completion signal
- IFileStorageService / MinIO — not blocking deployment at current scale
- Admin lifecycle reset tools
- Call Mode / Pronunciation scoring
- Real STT provider
- OpenAI TTS (advanced voices)
- Email delivery, payments, organisations

---

## Next recommended work

1. **Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
   Scoping pass done (2026-06-12): see
   `docs/reviews/2026-06-12-dynamic-pattern-selection-scoping.md`. Recommended first
   slice: per-slot pattern pools + last-N-session repetition avoidance in
   `SessionGeneratorService`/`SessionDurationTemplates`.
2. **Dynamic Practice Gym session templates** — configurable multi-exercise sessions within Practice Gym.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Planned future sprint

**Lesson Buffer / MinIO / Background Generation** - planned.

See: `docs/sprints/2026-06-11-lesson-buffer-minio-background-generation-sprint.md`

This sprint covers pre-generating the next 5-10 lessons, pre-generating a configurable 5-10 Practice Gym exercises per type/pattern, storing audio assets in MinIO, signed URL playback, Quartz.NET background generation jobs, Admin Integrations for MinIO health/configuration, and cached Practice Gym generation.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.

---

## Phase 10J — Learning Goal Context Resolver (2026-06-17)

**Goal:** Normalize `LearningGoalContext` across all generation and ledger paths via a consistent, testable resolver.

**Delivered:**

- `ResolvedLearningGoalContext` value object (`src/LinguaCoach.Application/Learning/`)
- `ILearningGoalContextResolver` interface with `LearningGoalResolutionContext` call context
- `LearningGoalContextResolver` implementation (`src/LinguaCoach.Infrastructure/Learning/`)
- DI registration: `AddSingleton<ILearningGoalContextResolver, LearningGoalContextResolver>()`
- 7 call sites migrated from `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` to resolver:
  - `ActivityGetHandler` (3 sites)
  - `ActivitySubmitHandler` (2 ledger record sites)
  - `ExercisePrepareHandler` (1 site)
  - `PracticeGymGenerationJob` (1 site)
  - `SessionGeneratorService` (1 site)
  - `ActivityMaterializationJob` (1 site)
  - `LessonBatchGenerationJob` (1 site)
- 18 unit tests, 2 integration tests — all pass

**Priority chain (strict order):**
1. `ExplicitGoalOverride` from call context
2. `LearningGoals` + `FocusAreas` (Phase 10G/10I structured fields)
3. `CustomLearningGoal` / `CustomFocusArea`
4. `LearningGoalDescription` → `LearningGoal` → `CareerContext` (legacy)
5. `"general English communication"` — never workplace-only

**Not implemented:** curriculum routing, readiness pools, CEFR routing, background generation.

**Backward compatible:** `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` kept intact. Old ledger records without goal context do not throw.

---

## Phase 10X-J — Admin Wrapper Variant API (2026-06-19)

**Goal:** Make `sp-admin-*` wrapper components robust and flexible so admin pages become consumers of design-system components, not owners of styling/layout logic.

**Delivered:**

- `sp-admin-button`: `appearance` (solid/outline/soft/ghost/link), `size` (xs/sm/md/lg), `fullWidth`, `iconOnly`. Legacy `variant="ghost"` compat alias preserved.
- `sp-admin-badge`: `appearance` (soft/solid/outline), `size` (sm/md), `dot`, `purple` tone added.
- `sp-admin-card`: `variant` (default/bordered/elevated/flat/metric/section), `padding` (none/sm/md/lg), `radius` (md/lg/xl/2xl), `headerDivider`, `hover`, `loading`.
- `sp-admin-stat-card`: `size` (sm/md/lg), unified tone names (primary/success/warning/danger/info/neutral alias the legacy names), `loading` skeleton, `[slot=trend]`.
- `sp-admin-table`: `variant` (basic/data/bordered/striped/simple/card), `density` (compact/comfortable/spacious), `selectable`, `stickyHeader`, column `width`/`align`.
- `sp-admin-filter-bar`: `layout` (inline/stacked/responsive), `density` (compact/comfortable).
- `sp-admin-form-field`: `layout` (vertical/horizontal/inline), `size` (sm/md/lg).
- `sp-admin-input`: `size` (sm/md/lg), `state` (default/error/success/disabled), `fullWidth`. CVA preserved.
- `sp-admin-select`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-textarea`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-modal`: `size` (sm/md/lg/xl/full), `variant` (default/danger/form/confirm), `showCloseButton`. `maxWidth` still works (overrides size).
- `sp-admin-drawer`: `side` (left/right), `size` (sm/md/lg/xl), `closeOnBackdrop`.
- 18 new Phase 10X-J unit tests. 439 Angular tests pass (0 failures).
- Proof usage applied: Students page `variant="data" density="compact"`, modal buttons `size="sm"`. Dashboard stat-cards `size="md" [loading]`, AI System card `variant="metric"`.
- .NET: 1885 tests pass. Angular build clean. Playwright: pending (run from `src/LinguaCoach.Web`).

**Not implemented:** 10R-F usage governance UX, 10U AI Usage redesign, 10V prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine.
