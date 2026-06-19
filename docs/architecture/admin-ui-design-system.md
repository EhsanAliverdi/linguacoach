---
status: current
lastUpdated: 2026-06-19 14:30
owner: architecture
supersedes:
supersededBy:
---

# Admin UI Design System

## Visual Source of Truth

**TailAdmin Angular Layout One** is the template source for the SpeakPath admin app.

- Free template repo: https://github.com/TailAdmin/free-angular-tailwind-dashboard
- Demo reference: https://angular-demo.tailadmin.com/layout-one
- Vendored source: `src/app/templates/tailadmin/free-angular-tailwind-dashboard/` (gitignored — clone separately)
- Commit imported: da992cf (2026-06-18)
- License: MIT

The actual TailAdmin Angular source is stored as a vendor reference under `src/app/templates/`.
SpeakPath exposes TailAdmin **only** through `sp-admin-*` wrapper components.
Feature pages must not import from `templates/` directly.
The `templates/` folder is excluded from the main repo via `.gitignore` and should be cloned separately when onboarding.

## Dependency Direction

```
TailAdmin Angular Layout One (visual reference / licensed source when available)
          ↓
SpeakPath admin tokens (admin-tokens.css) + shell CSS (admin-app-layout)
          ↓
SpeakPath admin wrapper components (sp-admin-*)
          ↓
SpeakPath admin feature pages
```

Feature pages must import from `src/app/admin`.
They must not reference TailAdmin internals directly.
They must not copy TailAdmin page markup or utility class lists.
If TailAdmin source is later licensed, all TailAdmin-specific markup belongs inside
wrapper components or the admin shell — not in feature pages.

## Angular ViewEncapsulation Note

`AdminAppLayoutComponent` uses `ViewEncapsulation.None` so that the shell CSS
(sidebar, nav items, header, drawer, profile flyout) reaches the inner DOM of
`sp-admin-layout`, `sp-admin-sidebar`, and `sp-admin-header` child components.
This is intentional. Shell-level CSS is global for the admin module only.
Wrapper component CSS remains emulated/scoped inside each wrapper.

## TailAdmin Asset Status

**Phase 10X-D (2026-06-18):** TailAdmin free Angular template source has been imported.

- Source: https://github.com/TailAdmin/free-angular-tailwind-dashboard
- Commit: da992cf
- Location: `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
- License: MIT (see `templates/tailadmin/free-angular-tailwind-dashboard/LICENSE`)

The source is stored as a vendor reference under `templates/tailadmin/`.
SpeakPath app code does not import from this path.
Wrapper components and shell CSS adapt TailAdmin patterns.
See `docs/architecture/admin-tailadmin-adapter-inventory.md` for the full mapping.

TODO-10X-ASSETS: closed. Source imported in 10X-D.

**Phase 10X-E (2026-06-18):** All `sp-admin-*` wrappers now adapted to real TailAdmin patterns.

All 15 wrapper components (layout, sidebar, header, button, badge, card, stat-card, input, select,
form-field, modal, table, pagination, filter-bar, drawer) now use TailAdmin-sourced class structures
internally. Custom CSS approximations replaced with actual TailAdmin class names and structures.

Wrapper public APIs (inputs/outputs) remain stable — admin feature pages are unchanged.
See `docs/architecture/admin-tailadmin-adapter-inventory.md` for the full mapping and status.
See `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md` for full findings.

Remaining adapter work (10X-F): table sorting, dropdown, theme toggle, full filter bar alignment.

**Phase 10X-F (2026-06-18):** Admin wrapper capability completion.

New wrappers added:
- `sp-admin-dropdown` — TailAdmin `absolute z-40 rounded-xl border border-gray-200 bg-white shadow-lg` dropdown. Trigger/menu content projection. Click-outside + Escape close. `align` (left/right) and `width` (sm/md/lg) inputs.
- `sp-admin-table-actions` — Row action dropdown based on TailAdmin table-dropdown pattern. Three-dot trigger button. Supports generic `[actions]` array API with `(actionClick)` output, or full content projection for per-row conditional actions. Danger item red styling.
- `sp-admin-theme-toggle` — Admin-only dark/light toggle based on TailAdmin `ThemeToggleButtonComponent`. Uses `AdminThemeService` (stores preference in `adminTheme` localStorage key, isolated from student UI). Sun/moon SVG icons.
- `AdminThemeService` — admin-scoped theme service. Mirrors TailAdmin `ThemeService` pattern. Does not use student-UI theme.

Updated wrappers:
- `sp-admin-table` — now supports `sortable` column flag, `sortColumn`, `sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` sort icons, `aria-sort` attribute, keyboard-accessible sortable headers.
- `sp-admin-header` — now has named `[left]` and `[actions]` content slots. Theme toggle auto-rendered in right action zone.
- `sp-admin-filter-bar` — now has named `[search]`, `[filters]`, `[actions]` slots. Left/right zone split. Backward-compat general projection retained.

## Table Sorting Convention

- Consumer page owns sort state (`sortColumn`, `sortDirection` signals/properties).
- `sp-admin-table` emits `(sortChange)` with `{ column, direction }`.
- Page re-sorts its data array and passes sorted `rows` back to the table.
- Table does not sort internally. This enables server-side sort later.
- `hasActions` input adds an "Actions" column header when true.

## Row Action Dropdown Convention

- Use `sp-admin-table-actions` for all row action menus.
- For static action lists: pass `[actions]` array + handle `(actionClick)`.
- For conditional per-row actions (View/Edit/Archive depend on row state): use content projection inside `sp-admin-table-actions`. Projected buttons use `sp-adm-action-item` class for consistent styling.
- Do not put row action buttons directly in table cells without this wrapper.

## Dropdown Convention

- Use `sp-admin-dropdown` for all admin dropdown menus.
- Admin feature pages must not use TailAdmin `app-dropdown` directly.
- `align="right"` (default) — dropdown opens right-aligned with trigger.
- `width="md"` (default, `w-48`) — adjust for content width.
- Close on Escape and click-outside are built-in.

## Theme Toggle / Admin-Only Dark Mode

- `sp-admin-theme-toggle` is the only entry point for admin dark mode.
- It is automatically included in `sp-admin-header`.
- `AdminThemeService` stores preference in `adminTheme` localStorage key.
- This does not affect student-facing pages, which use their own light theme.
- Full dark-mode scoping (admin-only class boundary) is a TODO for 10X-G+.

Feature pages must import from `src/app/admin`.
They must not reference `templates/` directly.
They must not copy TailAdmin page markup or repeat long utility class lists.

## Header User Dropdown (Phase 10X-G)

The admin header user/profile menu uses `sp-admin-dropdown` (not a page-local open/close signal).

- `AdminAppLayoutComponent` projects the avatar button into the dropdown `trigger` slot
  and the profile flyout (email, role, disabled Profile item, Sign out) into the `menu` slot.
- Open state, click-outside, and Escape close are owned by `sp-admin-dropdown`.
- The layout no longer carries `profileMenuOpen`, `toggleProfileMenu`, or a document click handler.
- `.sp-admin-header-user`, `.sp-admin-avatar`, and `.sp-admin-profile-*` shell CSS classes are retained.

## Page Refactor Rules (Phase 10X-G)

When refactoring an admin feature page, prefer wrappers in this order:

1. Page title block → `sp-admin-page-header` (with projected `sp-admin-button` actions).
2. KPI/metric tiles → `sp-admin-stat-card` (icon via `slot="icon"`, `tone`, `label`, `value`).
3. Section containers → `sp-admin-card` (set `title`; use `[dashed]="true"` for placeholders;
   project header-right content via `slot="actions"`). Do not re-render the title with a
   page-local `<h2>` inside a titled card.
4. Status pills → `sp-admin-badge` (tones: success/warning/info/primary/danger/neutral).
5. Action buttons → `sp-admin-button` (`variant`, `size`, `[loading]`, `[disabled]`).
   Note: `sp-admin-button` renders an inner `<button>`; do not put `routerLink` on it.
   For navigation, use a plain anchor styled as a link/button.
6. Tables → `sp-admin-table` (data-driven `columns`/`rows`, or projected `<table>` for
   custom row layout); sortable columns via `sortable` + `(sortChange)`; row actions via
   `sp-admin-table-actions`.
7. Filter/search rows → `sp-admin-filter-bar`.
8. States → `sp-admin-loading-state`, `sp-admin-empty-state`, `sp-admin-error-state`.

Keep page-local CSS only for unique grid layout and content that no wrapper covers.
Remove component-local CSS once a wrapper owns the visual (card/table/badge/button/stat).

## Form Field Conventions (Phase 10X-H / 10X-I)

- Always wrap inputs in `<sp-admin-form-field label="...">` for consistent label/hint/error layout.
- Use `<sp-admin-input>` for text, password, email, search fields.
  - Supports `[(ngModel)]`, `[formControlName]`, `[value]` (one-way display), `[disabled]`.
  - `type="password"` for credential fields.
- Use native `<input type="number" [(ngModel)]="...">` inside `<sp-admin-form-field>` for numeric fields.
  - `sp-admin-input` CVA writes strings; numeric domain integrity requires the native element.
- Use native `<select [(ngModel)]="..." [ngValue]="null">` inside `<sp-admin-form-field>` for fields
  with `number | null` option values. `sp-admin-select` only supports string `[value]` binding.
- Use `<sp-admin-select>` only for `string`-valued selects with static option lists.
- Use `<sp-admin-textarea>` for multi-line text (notes, goals, descriptions).

## Modal Conventions (Phase 10X-E / 10X-I)

- Use `<sp-admin-modal [open]="..." [title]="..." (closed)="...">` for all admin modals.
- Avoid page-local backdrop/modal CSS (`.sp-admin-modal-backdrop`, `.sp-admin-modal`, etc.).
- Use `maxWidth` input when the form needs more than the default 520px (e.g. `maxWidth="720px"`).
- Submit buttons must be inside the `<form>` element — do not use `slot="footer"` for form actions
  because a div in the footer slot is outside the `<form>` boundary and breaks form submission.
- The `slot="footer"` projection is appropriate for non-form confirm/action dialogs only.

## Layout Semantics (Phase 10X-I)

- `sp-admin-layout` wraps the content area in `<main>` (not a plain `<div>`).
- Playwright and accessibility tools locate the content area via `getByRole('main')`.
- Do not override this with a `<div>` wrapper inside the content slot.

### Form-field wrapper rule (Phase 10X-H — CVA now supported)

As of Phase 10X-H, `sp-admin-input`, `sp-admin-select`, and the new `sp-admin-textarea` implement
`ControlValueAccessor` via `NG_VALUE_ACCESSOR`. They can now two-way bind to a parent model.

Supported binding modes for all three wrappers:

- Template-driven: `<sp-admin-input [(ngModel)]="model" name="field" />`
- Reactive: `<sp-admin-input [formControl]="control" />` or `formControlName="field"`
- Disabled propagation: a disabled `FormControl` (or `[disabled]` input) disables the native control.
- Touched state: marked on blur, so `control.touched` and `ng-touched` work for validation display.

Wrapper inputs:

- `sp-admin-input`: `type`, `placeholder`, `autocomplete`, `readonly`, `required`, `invalid`.
- `sp-admin-select`: `options` (`{ value, label }[]`) and/or projected `<option>`, `placeholder`
  (disabled default option), `required`, `invalid`.
- `sp-admin-textarea`: `rows`, `placeholder`, `readonly`, `required`, `invalid`.

`sp-admin-form-field` supplies the label/hint/error/required-marker structure. Set `[required]="true"`
to render the red `*` marker. Wrap a CVA control or a native control inside it:

```html
<sp-admin-form-field label="Display name" hint="Shown to students" [required]="true" [error]="nameError">
  <sp-admin-input [(ngModel)]="displayName" name="displayName" />
</sp-admin-form-field>
```

#### When native controls are still allowed

Keep a native `.sp-input`/`.sp-ai-select` control inside `sp-admin-form-field` only when the CVA
wrapper cannot safely represent the control yet, for example:

- Multi-select, native `<datalist>`, file inputs, or controls with `multiple`.
- Selects whose option set is rebuilt by complex conditional ngModel logic where re-validating each
  field's two-way binding in a single migration pass would risk silent save regressions (AI Config
  dense provider-credentials grid, Integrations operational forms). These remain native pending a
  dedicated per-field migration pass. The CVA foundation now unblocks that work.

Do not introduce new long TailAdmin class lists for inputs in feature pages; use the wrappers.

## Folder Structure

```
src/LinguaCoach.Web/src/app/
  templates/
    README.md
    tailadmin/
      README.md                              <- version/license/source info
      free-angular-tailwind-dashboard/       <- vendored TailAdmin source (MIT)
        src/app/shared/layout/              <- Layout One shell reference
        src/app/shared/components/          <- UI primitive reference
        src/app/pages/                      <- demo pages — reference only, never import
        package.json                        <- TailAdmin deps — do NOT merge into SpeakPath
  admin/
    tokens/admin-tokens.css
    components/**                           <- sp-admin-* wrapper components
    services/**
    index.ts
```

`src/app/templates/tailadmin` is the vendor reference boundary.
`src/app/admin` is the SpeakPath adapter/wrapper layer.
No TailAdmin source is imported into SpeakPath app build paths.
Angular's compiler ignores `templates/` — it is not referenced in `tsconfig.app.json` or any import.

## Component Names

Canonical wrapper selectors use `sp-admin-*`.

Implemented foundation:

- `sp-admin-layout`
- `sp-admin-sidebar`
- `sp-admin-header`
- `sp-admin-page-header`
- `sp-admin-card`
- `sp-admin-stat-card`
- `sp-admin-button`
- `sp-admin-badge`
- `sp-admin-table`
- `sp-admin-empty-state`
- `sp-admin-loading-state`
- `sp-admin-error-state`
- `sp-admin-form-field`
- `sp-admin-input`
- `sp-admin-select`
- `sp-admin-pagination`
- `sp-admin-filter-bar`
- `sp-admin-modal`
- `sp-admin-drawer`
- `sp-admin-toast-outlet`

Existing adapter aliases remain for current pages:
`sp-admin-kpi-card`, `sp-admin-section-card`, and `sp-admin-data-table`.

## Design Tokens

Admin tokens live in `admin-tokens.css`.
Use CSS custom properties such as:

- `--sp-admin-bg`
- `--sp-admin-surface`
- `--sp-admin-border`
- `--sp-admin-text`
- `--sp-admin-text-muted`
- `--sp-admin-primary`
- `--sp-admin-green`
- `--sp-admin-amber`
- `--sp-admin-danger`
- `--sp-admin-sidebar-w`
- `--sp-admin-header-h`

Use these tokens inside admin wrappers.
Feature pages should prefer wrappers first.

## Services

Admin service facades live in `src/app/admin/services`.

- `AdminToastService` wraps the shared toast service.
- `AdminModalService` stores confirm-dialog requests.
- `AdminDrawerService` stores slide-in drawer state.

The modal and drawer services are foundations.
Richer orchestration remains deferred.

## Migration Checklist

For each admin page migration:

1. Replace page title blocks with `sp-admin-page-header`.
2. Replace common buttons with `sp-admin-button`.
3. Replace status pills with `sp-admin-badge`.
4. Replace repeated cards with `sp-admin-card` or `sp-admin-stat-card`.
5. Replace simple read-only tables with `sp-admin-table`.
6. Use `sp-admin-loading-state`, `sp-admin-error-state`, and `sp-admin-empty-state`.
7. Keep page-specific layout CSS small.
8. Do not move student UI into admin wrappers.

## Legacy Style Rule

Admin CSS that overrides or fights TailAdmin Layout One must not be added.
Avoid:
- Raw `background`, `color`, `padding` overrides on `.sp-admin-nav-item-active` outside the shell CSS.
- Repeated TailAdmin utility class lists in feature page templates.
- One-off component CSS for common UI that belongs in a wrapper.

When a page needs a visual fix, fix it in the wrapper component, shell, or tokens first.
Only use page-local CSS for truly unique content layout.

## Current Scope

Phase 10X-A migrated the admin shell plus proof pages.
Phase 10X-B migrated the core admin pages to the wrapper layer where feasible.
Phase 10X-C-F fixed the critical ViewEncapsulation bug, verified all gates,
and hardened TailAdmin Layout One alignment.

Phase 10X-G refactored the highest-legacy pages to wrappers and reduced page-local CSS.
Phase 10X-G-F finished the remaining wrapper consistency (Students table/badge wrappers,
Curriculum form-field wrappers) and verified the other priority pages were already migrated.

Migrated pages (post 10X-G-F):

- Students: row table now wrapped in `sp-admin-table` (projection mode); lifecycle, onboarding,
  and CEFR pills use the `sp-admin-badge` wrapper. Filter bar, pagination, sortable headers, and
  `sp-admin-table-actions` row menu in place. Obsolete page-local pagination/row-action/badge CSS removed.
- Curriculum: create/edit and routing-preview form fields use `sp-admin-form-field` for labels and
  hints. Native ngModel controls stay inside each field (the `sp-admin-input`/`sp-admin-select`
  wrappers have no ControlValueAccessor and cannot two-way bind to a parent model).
- Dashboard: KPI tiles now use `sp-admin-stat-card`; sections use `sp-admin-card`
  (including dashed placeholders); all status pills use `sp-admin-badge`. Removed the
  page-local KPI-card, status-card, badge, and table-card CSS now owned by wrappers.
- Students: wrapper page header, filter bar, and pagination; existing student management modals preserved.
- AI Config: page header, section cards, badges; duplicate in-card `<h2>` headings removed
  (card title is canonical); category Save/Test actions now use `sp-admin-button`.
- Curriculum: create/edit and routing-preview panels now use `sp-admin-card`; form and
  preview actions use `sp-admin-button` (replacing student-design-system `.sp-card`/`.sp-btn`).
- AI Usage: wrapper page header, stat cards, cards, table, badges, loading, empty, and error states.
- Prompts: wrapper page header, card, form field, table, badge, empty state, and button components.
- Exercise Types: wrapper page header, table, badges, buttons, and error state.
- Integrations: wrapper page header and section cards; existing operational forms preserved.
- Diagnostics: wrapper page header, loading, and error states from 10X-A remain.
- Curriculum: wrapper page header, filter bar, table, badges, buttons, loading, and error states for the list path.
- Usage Policies: wrapper page header, card, form field, table, badge, loading, empty, and error states.

Remaining legacy areas (post 10X-G):

- Dashboard keeps small page-local CSS for the action-card grid and analytics placeholder
  content layout (unique layout, not duplicated wrapper behavior).
- Student edit/reset/archive modals still use page-local modal markup (TODO-10X-D-MODAL).
- AI Config form controls still use page-local `.sp-ai-select` inputs and the dense
  provider-credentials grid. Migrating each field to `sp-admin-form-field`/`sp-admin-select`
  is deferred (high field count, ngModel-driven option logic) — see TODO-10X-G-AICONFIG-FORMS.
- Integrations still contains page-local operational forms and legacy table utility classes
  inside wrapper cards (TODO-10X-G-INTEGRATIONS-FORMS).
- Curriculum create/edit/preview still use page-local `.sp-input` form fields inside the
  new `sp-admin-card` (TODO-10X-G-CURRICULUM-FORMS).

Feature pages should not introduce new long TailAdmin class lists for common UI. Add or extend
`sp-admin-*` wrappers first, then use page-local CSS only for truly unique behavior.

---

## Phase 10X-J: Wrapper Variant API (2026-06-19)

All `sp-admin-*` wrappers now expose a robust variant/size/density/layout API.
Pages request design variations through typed inputs. TailAdmin class complexity stays inside wrappers.

### sp-admin-button

```html
<!-- variant: primary | secondary | success | danger | warning | info | neutral | ghost (deprecated) -->
<!-- appearance: solid | outline | soft | ghost | link -->
<!-- size: xs | sm | md | lg -->
<sp-admin-button variant="danger" appearance="solid" size="sm" [loading]="saving">Delete</sp-admin-button>
<sp-admin-button variant="primary" appearance="outline" [fullWidth]="true">Save</sp-admin-button>
<sp-admin-button variant="neutral" appearance="ghost" [iconOnly]="true" size="md">
  <svg leading .../>
</sp-admin-button>
```

Legacy: `variant="ghost"` continues to work (maps to `appearance="ghost" variant="neutral"`).

### sp-admin-badge

```html
<!-- tone: neutral | primary | success | warning | danger | info | purple -->
<!-- appearance: soft | solid | outline  (default: soft = TailAdmin light variant) -->
<!-- size: sm | md -->
<sp-admin-badge tone="success" appearance="soft" size="sm">Active</sp-admin-badge>
<sp-admin-badge tone="danger" appearance="solid" size="md">Error</sp-admin-badge>
<sp-admin-badge tone="warning" [dot]="true">Pending</sp-admin-badge>
```

### sp-admin-card

```html
<!-- variant: default | bordered | elevated | flat | metric | section -->
<!-- padding: none | sm | md | lg   radius: md | lg | xl | 2xl -->
<sp-admin-card title="AI Status" variant="metric" padding="md" [headerDivider]="true">
  <span slot="actions">...</span>
  Content
</sp-admin-card>
<sp-admin-card variant="elevated" [hover]="true" padding="lg">...</sp-admin-card>
<sp-admin-card [dashed]="true" variant="flat" padding="md">Placeholder</sp-admin-card>
```

### sp-admin-stat-card

```html
<!-- size: sm | md | lg -->
<!-- tone: indigo | green | violet | amber | teal | slate | primary | success | warning | danger | info | neutral -->
<sp-admin-stat-card tone="indigo" size="md" label="Students" [value]="count" [loading]="loading">
  <svg slot="icon" .../>
  <span slot="trend">↑ 12%</span>
</sp-admin-stat-card>
```

### sp-admin-table

```html
<!-- variant: basic | data | bordered | striped | simple | card -->
<!-- density: compact | comfortable | spacious -->
<sp-admin-table
  variant="data"
  density="compact"
  [columns]="cols"
  [rows]="rows"
  [hoverable]="true"
  [selectable]="true"
  [stickyHeader]="true"
  [hasActions]="true"
  (sortChange)="onSort($event)"
  (selectionChange)="onSelect($event)"
/>
```

Column definition: `{ key, label, sortable?, muted?, width?, align? }`.
Consumer is responsible for re-sorting rows on `(sortChange)`.

### sp-admin-filter-bar

```html
<!-- layout: inline | stacked | responsive   density: compact | comfortable -->
<sp-admin-filter-bar layout="responsive" density="comfortable">
  <input search placeholder="Search..." />
  <select filters>...</select>
  <sp-admin-button actions variant="secondary" appearance="outline" size="sm">Export</sp-admin-button>
</sp-admin-filter-bar>
```

### sp-admin-form-field

```html
<!-- layout: vertical | horizontal | inline   size: sm | md | lg -->
<sp-admin-form-field label="Email" layout="vertical" size="md" [required]="true" hint="Used for login" [error]="err">
  <sp-admin-input formControlName="email" />
</sp-admin-form-field>
<sp-admin-form-field label="Provider" layout="horizontal" size="md">
  <sp-admin-select formControlName="provider" [options]="providerOpts" />
</sp-admin-form-field>
```

### sp-admin-input / sp-admin-select / sp-admin-textarea

```html
<!-- size: sm | md | lg   state: default | error | success | disabled   fullWidth: boolean -->
<sp-admin-input formControlName="name" size="md" state="error" />
<sp-admin-select formControlName="tier" size="sm" [fullWidth]="false" [options]="opts" />
<sp-admin-textarea formControlName="notes" size="md" [rows]="6" state="success" />
```

CVA behavior (`[(ngModel)]`, `formControlName`, `setDisabledState`, touched-on-blur) preserved in all three.

### sp-admin-modal

```html
<!-- size: sm | md | lg | xl | full   variant: default | danger | form | confirm -->
<sp-admin-modal [open]="open" title="Delete student?" size="md" variant="danger"
  [closeOnBackdrop]="true" [showCloseButton]="true" (closed)="open = false">
  <p>This cannot be undone.</p>
  <sp-admin-button slot="footer" variant="danger" size="sm">Delete</sp-admin-button>
  <sp-admin-button slot="footer" variant="neutral" appearance="ghost" size="sm" (click)="open = false">Cancel</sp-admin-button>
</sp-admin-modal>
```

`maxWidth` input overrides the `size` preset if set explicitly (backward compat from 10X-I).

### sp-admin-drawer

```html
<!-- side: left | right   size: sm | md | lg | xl -->
<sp-admin-drawer [open]="open" title="Student detail" side="right" size="lg" (closed)="open = false">
  Content
</sp-admin-drawer>
```

---

## Raw class policy

**Not allowed in feature pages:**
- Raw Tailwind/TailAdmin class lists for components that have a wrapper equivalent.
- Example: `class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs"` for a badge.

**Allowed:**
- Layout-only utilities: `grid`, `flex`, `gap-*`, `col-span-*`, `w-full`, page-layout grids.
- Page-local CSS for unique non-component behavior (action card links, stat grids, etc.).
- `customClass` or `contentClass` escape hatches on wrappers — only when no typed input covers the need.

## Frontend Testing Rule

Frontend tests must not assert Tailwind, TailAdmin, BEM, wrapper implementation, border, radius,
spacing, inline style, or exact CSS class names unless that class is explicitly documented as a
public API.

Angular specs should test rendering, visible text, inputs and outputs, ControlValueAccessor
behavior, form values, disabled/loading state, emitted events, role/ARIA attributes, open/close
behavior, sorting, pagination, and modal/dropdown behavior.

Playwright tests should cover page behavior and smoke flows: page loads, visible text, roles,
ARIA attributes, form interaction, navigation, disabled states, modal/dropdown behavior, and basic
user flows. Do not assert Tailwind or TailAdmin class names in Playwright.

Visual appearance regressions should be handled by a dedicated visual regression baseline, not by
unit or Playwright assertions against implementation classes.

---

## Known gaps (tracked for future phases)

- `sp-admin-input-number` — number inputs with null/undefined remain native inside `sp-admin-form-field`.
- `sp-admin-select-object` — selects with non-string option values remain native inside `sp-admin-form-field`.
- `sp-admin-breadcrumb` — not yet wrapped (future phase).
- Dashboard mini-table still uses page-local `sp-admin-mini-table` CSS (migration to `sp-admin-table` pending).
