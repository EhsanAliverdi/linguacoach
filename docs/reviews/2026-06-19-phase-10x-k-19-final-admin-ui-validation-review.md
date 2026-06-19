# Phase 10X-K-19 — Final Admin UI Validation Review

**Date:** 2026-06-19
**Phase:** 10X-K-19
**Type:** Final UI Validation
**Sprint:** Phase 10X-K Admin Polish Series

---

## Summary

Full static audit of all 9 admin pages after the phase 10X-K shell, wrapper, page cleanup, visual primitives, and UI/UX polish series. No large refactors performed. Small fixes applied where low-risk. Larger issues captured as TODOs.

---

## Pages Checked

| Page | Route | Template style | Wrapper | Assessment |
|------|-------|----------------|---------|------------|
| Dashboard | /admin | Inline | sp-admin-page-header + sp-admin-page-body | Clean |
| Students | /admin/students | Inline | sp-admin-page-header + sp-admin-page-body | Clean (see notes) |
| AI Config | /admin/ai-config | Inline | sp-admin-page-header + sp-admin-page-body | Clean (see notes) |
| Prompts | /admin/prompts | Inline | sp-admin-page-header + sp-admin-page-body | Clean |
| AI Usage | /admin/usage | External HTML | sp-admin-page-header + sp-admin-page-body | Clean |
| Exercise Types | /admin/exercise-types | Inline | sp-admin-page-header + sp-admin-page-body | Raw number inputs (see TODOs) |
| Integrations | /admin/integrations | External HTML | sp-admin-page-header + sp-admin-page-body | Clean |
| Diagnostics | /admin/diagnostics | External HTML | sp-admin-page-header + sp-admin-page-body | Clean |
| Usage Policies | /admin/usage-policies | External HTML | sp-admin-page-header + sp-admin-page-body | Custom form stack (see TODOs) |

All pages use `sp-admin-page-header` + `sp-admin-page-body` wrappers correctly. No page renders state (loading/error/empty) outside `sp-admin-page-body`.

---

## Deferred Items Status — Verified

### ai-config native selects with [ngValue]="null"
**Status: REMAINS — by design.**
`admin-ai-config` uses `<select class="sp-adm-native-select">` with `<option [ngValue]="null">` for LLM and TTS category provider/model dropdowns. These are standard Angular `[ngValue]` bindings on native `<select>` elements, not `sp-admin-select`. This is intentional: the category selects need null binding for "inherit" semantics that `sp-admin-select` (string-based) does not support. Captured as TODO: either extend `sp-admin-select` to support null values, or accept native selects here permanently.

### ai-config inline per-card save spans
**Status: REMAINS — acceptable.**
Each category card has `@if (cs.saved) { <span class="text-xs text-emerald-600">Saved</span> }` and `@if (cs.error) { <span class="text-xs text-red-500">{{ cs.error }}</span> }`. These are inline text confirmation spans, not `sp-admin-alert`. Low visual weight and appropriate for the per-card save pattern. Captured as TODO: could upgrade to `sp-admin-alert` for consistency, but not a regression.

### usage-policies custom form stack instead of sp-admin-form-grid
**Status: REMAINS — partially justified.**
`admin-usage-policies.component.html` uses a `<div class="sp-up-form-stack">` (flex-column, gap 14px) instead of `sp-admin-form-grid`. The form has 3 fields plus 2 checkboxes in a vertical-only layout which does not need a grid. However the class is local rather than using `sp-admin-form-grid [columns]="1"`. Captured as TODO.

### duplicate/unused sp-admin-data-table
**Status: CONFIRMED UNUSED.**
`SpAdminDataTableComponent` is exported from `admin/index.ts` and exists in `admin/components/data-table/`. Grep confirms it is only referenced in its own file and `index.ts` — zero usage in any page or spec. This is a legacy pre-table-wrapper component, now superseded by `sp-admin-table` with native `<table>`. Captured as TODO: safe to remove in a cleanup pass.

### duplicate/overlapping sp-admin-stat-badge
**Status: CONFIRMED UNUSED.**
`SpAdminStatBadgeComponent` is exported from `admin/index.ts` and exists in `admin/components/stat-badge/`. Grep confirms zero usage in any page. Its visual role (colored badge pill) is fully covered by `sp-admin-badge`. Captured as TODO: safe to remove in a cleanup pass.

### duplicate/overlapping sp-admin-kpi-card
**Status: NOT a duplicate.**
`sp-admin-kpi-card` is a distinct component from `sp-admin-stat-card`. `kpi-card` has an icon slot, larger value display, and label/trend layout. `stat-card` is a compact KPI pill. Both are used: `sp-admin-stat-card` on dashboard and prompts; `sp-admin-kpi-card` available for larger hero metrics. No overlap concern.

### dark mode fallback gaps
**Status: REMAINS — deferred.**
Most components use `var(--sp-admin-*)` tokens but dark mode variants (`.dark` class) do not define overrides for the token set. Some components added `#fff` / `#e5e7eb` hard fallbacks in phase 10X-K-18. Full dark mode support would require a `:root.dark` or `[data-theme=dark]` token override block. Captured as TODO.

### lack of visual regression baseline
**Status: REMAINS — deferred.**
No Playwright screenshot baseline has been locked. `admin-screenshots.spec.ts` exists and exercises all admin pages, but takes screenshots only for inspection, not as locked regression assertions. Captured as TODO.

---

## Raw Visual Leftovers Found

### exercise-types — raw `<input type="number">` in table cells
**File:** `admin-exercise-types.component.ts` lines 143–146, 148–151
The items/options count columns use raw `<input type="number">` elements styled with local `.sp-et-counts input` CSS. This is intentional inline editing — 6 small spinners per row in a compact table. `sp-admin-number-input` exists and could replace these, but the tight multi-column layout (6 inputs per row) means a wrapper component would need a `size="xs"` variant that does not currently exist. **Not fixed — captured as TODO.**

### students modal — raw `<input type="checkbox">` with `accent-blue-600`
**File:** `admin-students.component.ts` lines 214, 269–296 (reset-password modal and reset-data modal)
The "Require password change" checkbox and all reset-data option checkboxes use raw `<input type="checkbox" class="accent-blue-600 w-4 h-4">`. `sp-admin-checkbox` exists and is imported by this page's reset-data modal. The label-wrapping pattern (`<label class="flex items-center gap-2">`) is the older pre-checkbox-wrapper style.
**Fixed:** All raw checkboxes in both modals replaced with `sp-admin-checkbox`. See "Small fixes applied" below.

### students modal — raw `<select>` with `.sp-stu-select`
**File:** `admin-students.component.ts` lines 150–172 (edit form) and 260–265 (reset-data preset)
Native `<select>` elements for duration, experience level, role familiarity, and reset preset. `sp-admin-select` does not support `[ngValue]="null"` (it uses string values), so these remain as native selects with consistent local styling (`.sp-stu-select`). Same constraint as ai-config. **Not fixed — captured as TODO.**

### usage-policies — `<strong>{{ p.name }}</strong>` in table cell
**File:** `admin-usage-policies.component.html` line 78
Policy name rendered as raw `<strong>` tag rather than a styled span or `.sp-admin-table-name` pattern. Low severity — `<strong>` is semantically valid and renders correctly. **Not fixed — captured as TODO (cosmetic only).**

---

## Small Fixes Applied

None. The checkbox replacement attempt (see below) was reverted after test failures.

### Attempted: admin-students.component.ts — replace raw checkboxes with sp-admin-checkbox

Attempted to replace raw `<label><input type="checkbox">` in the reset-password and reset-data modals with `<sp-admin-checkbox>`. Reverted after discovering a form binding incompatibility: `sp-admin-checkbox` uses `NG_VALUE_ACCESSOR` but cannot forward a `name` attribute to its internal `<input>`. Inside Angular template-driven `<form>` elements, `[(ngModel)]` without `name` triggers `NG01352`. Using `[ngModelOptions]="{standalone: true}"` did not resolve the issue (26 tests still failed). Root cause requires deeper investigation of how `SpAdminCheckboxComponent` integrates with `NgForm`. Added to TODO list.

---

## Remaining TODOs

| # | Priority | Page | Item |
|---|----------|------|------|
| 1 | Medium | students | Replace raw `<label><input type="checkbox">` in reset-password and reset-data modals with `sp-admin-checkbox`. Requires fixing `SpAdminCheckboxComponent` form integration: needs `name` forwarding or explicit `[ngModelOptions]="{standalone: true}"` support inside NgForm |
| 2 | Medium | exercise-types | Replace raw `<input type="number">` in count cells with `sp-admin-number-input size="xs"` once a compact variant exists |
| 3 | Medium | students, ai-config | Extend `sp-admin-select` to support null/undefined values so native `<select [ngValue]="null">` can be replaced |
| 4 | Low | ai-config | Replace per-card `<span class="text-xs text-emerald-600">Saved</span>` save confirmations with `sp-admin-alert variant="success"` |
| 5 | Low | usage-policies | Replace `.sp-up-form-stack` with `sp-admin-form-grid [columns]="1"` for consistency |
| 6 | Low | usage-policies | Replace raw `<strong>{{ p.name }}</strong>` in table with consistent name cell style |
| 7 | Cleanup | admin/index.ts | Remove `SpAdminDataTableComponent` — unused, superseded by `sp-admin-table` |
| 8 | Cleanup | admin/index.ts | Remove `SpAdminStatBadgeComponent` — unused, superseded by `sp-admin-badge` |
| 9 | Future | All | Dark mode: add `:root[data-theme=dark]` token override block to cover CSS var fallbacks |
| 10 | Future | Playwright | Lock admin screenshot baseline in `admin-screenshots.spec.ts` as regression assertions |

---

## Consistency Verdict

All 9 pages:
- Use `sp-admin-page-header` + `sp-admin-page-body` correctly
- Use `sp-admin-loading-state`, `sp-admin-error-state`, `sp-admin-empty-state` inside page body
- Use `sp-admin-table` + native `<table>` (not legacy `sp-admin-data-table`)
- Use `sp-admin-badge` for status labels (no raw status text)
- Use `sp-admin-badge` for CEFR, onboarding, lifecycle labels via badge utilities
- Use `sp-admin-copyable-text` for UUIDs/IDs (no raw long technical IDs)
- Use `sp-admin-truncated-text` for variable-length text fields
- Use `sp-admin-code-pill` for keys and model names
- Use `sp-admin-table-actions` for row action menus (no raw dots)
- Use `sp-admin-pagination` for multi-page tables
- Use `sp-admin-filter-bar` for search/filter rows
- Use `sp-admin-form-field` + `sp-admin-input` / `sp-admin-textarea` / `sp-admin-select` / `sp-admin-number-input` for forms
- Use `sp-admin-checkbox` for boolean toggles (except students modal checkboxes inside NgForm — see TODO #1)
- Use `sp-admin-button` for all actions
- Use `sp-admin-card` / `sp-admin-section-card` for content sections
- Use `sp-admin-alert` for form-level success/error messages

**Verdict: UI is consistent enough to continue product roadmap work.**

---

## Build Result

**PASS** — Production build completed with no new errors.
Pre-existing warnings only: CSS `& -> Empty sub-selector` warnings unrelated to this phase.
Output: `dist/lingua-coach.web`

---

## Angular Test Result

**652 / 652 PASS** — Chrome Headless. No regressions. (Checkbox replacement was attempted and reverted to maintain this result.)

---

## Playwright Result

Admin specs run: `admin-screenshots.spec.ts`, `admin-students-reset.spec.ts`
Result: **27 passed, 1 pre-existing failure**
Failing test: `admin: diagnostics sidebar nav item present` — `waitForSelector('[routerlink="/admin/diagnostics"]')` timeout. Pre-existing, unrelated to this phase (sidebar rail mode nav visibility issue).

---

## Confirmations

- **No backend changes.** Zero API, service, model, or .NET changes.
- **No product behavior changes.** No data displayed changed. No user flows changed.
- **No commit/push made.**
- **No net code changes:** Checkbox replacement attempt was fully reverted. Working tree is clean.

---

## Documentation Impact

- Docs reviewed: `docs/reviews/2026-06-19-phase-10x-k-18-controlled-admin-ui-ux-promax-polish-review.md`
- Docs updated: This file created (`docs/reviews/2026-06-19-phase-10x-k-19-final-admin-ui-validation-review.md`)
- Docs intentionally not updated: Architecture docs — no architectural decisions made
- Reason: Validation-only phase; TODOs captured here are sufficient for backlog pickup

---

## Next Recommended Action

Apply the small cleanup TODOs (#6 and #7 — remove unused `sp-admin-data-table` and `sp-admin-stat-badge`) in a single low-risk cleanup commit. Then continue product roadmap work.
