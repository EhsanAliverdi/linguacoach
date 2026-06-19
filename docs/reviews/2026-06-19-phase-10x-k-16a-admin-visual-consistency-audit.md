# Admin Visual Consistency Audit

**Date:** 2026-06-19
**Phase:** 10X-K-16A
**Type:** Visual consistency audit — frontend only
**Commit:** 926dce8

---

## Pages Reviewed

| Route | Component | Template |
|---|---|---|
| /admin | admin-dashboard | inline |
| /admin/students | admin-students | inline |
| /admin/ai-config | admin-ai-config | inline |
| /admin/prompts | admin-prompts | inline |
| /admin/usage (AI usage) | admin-ai-usage | .html |
| /admin/exercise-types | admin-exercise-types | inline |
| /admin/integrations | admin-integrations | .html |
| /admin/diagnostics | admin-diagnostics | .html |
| /admin/usage-policies | admin-usage-policies | .html |

---

## Existing Shared Admin Components

The admin barrel (`src/app/admin/index.ts`) exports:

**Layout:** `SpAdminLayoutComponent`, `SpAdminPageBodyComponent`, `SpAdminPageHeaderComponent`, `SpAdminSidebarComponent`, `SpAdminHeaderComponent`

**Cards:** `SpAdminCardComponent`, `SpAdminStatCardComponent`, `SpAdminActionCardComponent`

**Tables:** `SpAdminTableComponent`, `SpAdminTableActionsComponent`

**Forms:** `SpAdminFormFieldComponent`, `SpAdminInputComponent`, `SpAdminSelectComponent`, `SpAdminTextareaComponent`, `SpAdminFilterBarComponent`

**Feedback:** `SpAdminBadgeComponent`, `SpAdminButtonComponent`, `SpAdminAlertComponent`, `SpAdminLoadingStateComponent`, `SpAdminEmptyStateComponent`, `SpAdminErrorStateComponent`, `SpAdminPaginationComponent`, `SpAdminModalComponent`

**Text/data display:** `SpAdminTruncatedTextComponent`, `SpAdminCopyableTextComponent`, `SpAdminCodePillComponent`

**Legacy/possibly unused:** `SpAdminKpiCardComponent`, `SpAdminStatBadgeComponent`, `SpAdminSectionCardComponent`, `SpAdminDataTableComponent` — superseded by current components, need grep to confirm.

---

## Inconsistencies Found

### 1. Bare `<input type="number">` and `<select>` in forms (CRITICAL)

**What:** Several pages bypass the shared input wrappers and use raw browser inputs.

**Where:**
- `admin-integrations.html` — 11 number inputs (buffer sizes, timeouts, job counts) use `<input type="number" class="sp-adm-num-input">`
- `admin-integrations.html` — checkboxes use `<input type="checkbox" class="accent-blue-600 w-4 h-4">` with raw Tailwind
- `admin-ai-config` inline template — provider/model dropdowns use `<select class="sp-adm-native-select">`
- `admin-prompts` inline template — max token inputs use `<input class="sp-input">`
- `admin-usage-policies.html` — name, description, scope use `<input class="sp-input">` and `<select class="sp-input">`

**Impact:** Raw browser controls look visually different from styled wrappers. Number inputs especially look toy-like.

**Fix needed:** `sp-admin-number-input` (thin wrapper on `SpAdminInputComponent` with `type="number"`) and `sp-admin-checkbox` (styled checkbox+label). Native selects should use the existing `SpAdminSelectComponent`.

---

### 2. Tailwind utility classes mixed into admin page templates (CRITICAL)

**What:** Some pages use Tailwind (`grid gap-4 sm:grid-cols-2 lg:grid-cols-3`, `flex items-center gap-3`, `mt-4`, `mb-2`, `text-sm font-semibold text-gray-700`) directly in templates. Other pages use scoped `sp-admin-*` CSS class names.

**Where:**
- `admin-integrations.html` — pervasive Tailwind grid and flex utilities throughout
- `admin-ai-config` inline template — `grid gap-4 sm:grid-cols-2`, inline flex utilities
- `admin-usage-policies.html` — ad-hoc class names (`sp-admin-form-stack`, `sp-admin-actions`, `sp-admin-check`) not part of the shared system

**Consistent pages:** admin-dashboard, admin-diagnostics, admin-students, admin-prompts, admin-exercise-types, admin-ai-usage — all use scoped `sp-admin-*` CSS only.

**Impact:** Mixed strategy makes future theme changes fragile and produces subtle spacing differences.

**Fix needed:** Replace Tailwind layout blocks with scoped CSS or a new `sp-admin-form-grid` layout component.

---

### 3. `sp-admin-stat-card` repurposed as a system status display (HIGH)

**What:** Diagnostics uses `sp-admin-stat-card` for system health values (DB reachable, AI provider, uptime). Dashboard uses a hand-rolled `.sp-admin-status-rows` list with badges. Neither is a proper status card.

**Where:**
- `admin-diagnostics.html` — 8 stat cards in `.sp-diag-status-grid`
- `admin-dashboard` inline template — `.sp-admin-status-rows` ad-hoc list

**Impact:** Two different patterns for system health display. Stat cards have no health-tone border or background; the list has no card chrome.

**Fix needed:** New `sp-admin-status-card` component (label + value + health tone on card surface). New `sp-admin-status-grid` layout wrapper.

---

### 4. Intra-card section headers are inconsistent (HIGH)

**What:** Some pages divide card content with raw `<h3>` elements using Tailwind classes. Others use card `title` prop. No shared section header component exists.

**Where:**
- `admin-integrations.html` lines 151, 176: `<h3 class="text-sm font-semibold text-gray-700 mb-2">`
- `admin-ai-config` inline template: `<div class="text-sm font-bold text-slate-900">` and `<div class="text-xs font-mono text-slate-400">` for category card sub-headers

**Fix needed:** `sp-admin-section-header` component to normalize sub-card headings.

---

### 5. `sp-admin-alert` usage is inconsistent (HIGH)

**What:** Four different patterns for save feedback and form errors exist across pages.

**Where:**
- `admin-integrations.html` — `<sp-admin-alert variant="error|success">` (correct)
- `admin-usage-policies.html` — `<sp-admin-card padding="sm"><sp-admin-badge tone="success">...</sp-admin-badge></sp-admin-card>` (wrong)
- `admin-prompts` inline template — `<p class="sp-admin-text-error">` (raw paragraph)
- `admin-ai-config` inline template — `<span class="text-xs text-emerald-600">Saved</span>` / `<span class="text-xs text-red-500">{{ cs.error }}</span>` (raw spans per category card)

**Fix needed:** Standardize block-level feedback on `sp-admin-alert`. Document a separate pattern for inline per-card save confirmation.

---

### 6. Filter bar slot attribute usage is inconsistent (MEDIUM)

**What:** `SpAdminFilterBarComponent` uses `search` and `filters` attributes on child elements. Usage pattern varies.

**Where:**
- `admin-diagnostics.html` — uses `<sp-admin-form-field search>` and `<sp-admin-form-field filters>` (correct)
- `admin-students` inline template — uses `<sp-admin-input type="search">` directly without form-field wrapper; no label
- `admin-exercise-types` — uses `<sp-admin-input search>` and `<sp-admin-select filters>` directly, skipping form-field wrapper
- `admin-ai-usage.html` — uses `<sp-admin-select filters>` (correct)
- `admin-usage-policies.html` — no `sp-admin-filter-bar` at all; filters sit inside the card body

**Fix needed:** Document canonical slot usage in component. Optionally add `sp-admin-toolbar` as a named preset for simpler toolbar cases.

---

### 7. `sp-admin-table` density and minWidth are set ad hoc per page (MEDIUM)

**What:** All pages manually set `density="compact"` and choose an arbitrary `minWidth` (480px, 560px, 620px, 960px, 980px, 1080px).

**Where:** admin-dashboard, admin-students, admin-prompts, admin-exercise-types, admin-ai-usage, admin-diagnostics, admin-integrations.

**Fix needed:** Named presets on `SpAdminTableComponent` (`preset="narrow|standard|wide"`). Make `density="compact"` the default.

---

### 8. `sp-admin-page-body` missing in admin-usage-policies (MEDIUM)

**What:** `admin-usage-policies.html` renders page sections without a `<sp-admin-page-body>` wrapper. Every other reviewed page uses one.

**Impact:** Page body spacing and max-width differ from all other admin pages.

**Fix needed:** Add `<sp-admin-page-body>` wrapper. One-line change.

---

### 9. KPI stat card grids are defined per-page (LOW)

**What:** Each page with stat cards defines its own grid CSS locally with slightly different gap values and column counts.

**Where:** `.sp-admin-kpi-grid` (dashboard), `.sp-au-stat-grid` (ai-usage), `.sp-admin-integration-metrics` (integrations), `.sp-admin-metric-grid` (prompts).

**Fix needed:** A `sp-admin-stat-grid` layout component or shared CSS util.

---

### 10. AI config category keys displayed with raw Tailwind instead of `sp-admin-code-pill` (LOW)

**What:** LLM category cards show the category key as `<div class="text-xs font-mono text-slate-400">` rather than `<sp-admin-code-pill>`.

**Fix needed:** Replace with `<sp-admin-code-pill>`. Minor.

---

### 11. Legacy components remain in the barrel (LOW)

**What:** `SpAdminKpiCardComponent`, `SpAdminStatBadgeComponent`, `SpAdminSectionCardComponent`, `SpAdminDataTableComponent` are exported but appear superseded.

**Fix needed:** Grep to confirm zero usage. Remove if unused.

---

## Component Gap Analysis

| Component | Status | Priority |
|---|---|---|
| `sp-admin-number-input` | Missing — needed | Critical |
| `sp-admin-checkbox` | Missing — needed | Critical |
| `sp-admin-status-card` | Missing — needed | High |
| `sp-admin-status-grid` | Missing — needed | High |
| `sp-admin-section-header` | Missing — needed | High |
| `sp-admin-form-grid` | Missing — needed | High |
| `sp-admin-alert` consistency | Exists — needs adoption | High |
| `sp-admin-toolbar` / filter preset | Partially exists — needs docs/preset | Medium |
| `sp-admin-table` presets | Exists — needs preset API | Medium |
| `sp-admin-stat-grid` | Missing — low priority | Low |

---

## Prioritized Roadmap

### Phase 10X-K-17A — Critical form primitives

1. Create `sp-admin-number-input` — wraps `SpAdminInputComponent` with `type="number"` and `inputmode="numeric"`. Replaces 11 raw inputs in admin-integrations and token inputs in admin-prompts.
2. Create `sp-admin-checkbox` — styled checkbox+label wrapper. Replaces raw checkboxes in admin-integrations and admin-usage-policies.
3. Replace `<select class="sp-adm-native-select">` in admin-ai-config with `SpAdminSelectComponent`.
4. Add `<sp-admin-page-body>` to admin-usage-policies.
5. Standardize `sp-admin-alert` — replace badge-in-card (usage-policies) and raw spans (ai-config) with `sp-admin-alert`.

### Phase 10X-K-17B — Structural visual components

6. Create `sp-admin-status-card` — label + value + health tone on card surface. Replaces stat-card repurposing in diagnostics and ad-hoc status rows in dashboard.
7. Create `sp-admin-status-grid` — layout wrapper for system health card grids.
8. Create `sp-admin-section-header` — intra-card sub-heading component. Replaces raw `<h3>` in admin-integrations.
9. Create `sp-admin-form-grid` — layout wrapper replacing Tailwind `grid gap-4 sm:grid-cols-2 lg:grid-cols-3` blocks in admin-integrations and admin-ai-config.
10. Remove remaining Tailwind layout utilities from all admin page templates.

### Phase 10X-K-17C — Table and toolbar polish

11. Add `preset="narrow|standard|wide"` to `SpAdminTableComponent`. Make `density="compact"` the default.
12. Document `sp-admin-filter-bar` slot attribute usage. Add `sp-admin-toolbar` preset if helpful.

### Deferred

- `sp-admin-stat-grid` — shared KPI grid layout. Per-page CSS works; low visual impact. Defer to post-10X polish.
- Legacy component removal — after grep confirms zero usage.
- AI config category key code-pill fix — minor cosmetic.

---

## Decisions Made

- Audit scope is frontend visual patterns only. No backend, API, or product behavior changes.
- Phasing: 17A = input/form primitives, 17B = structural layout components, 17C = table/toolbar polish.
- `sp-admin-status-card` is a new component, not an extension of `sp-admin-stat-card`.
- `sp-admin-form-grid` is a layout wrapper only, not a form management abstraction.
- `SpAdminFilterBarComponent` is kept; `sp-admin-toolbar` would be a preset variant, not a replacement.

---

## Risks and Unresolved Questions

- Does `SpAdminSelectComponent` support `[ngValue]="null"` for the "inherit" option in AI config provider/model selects? If not, native select stays for that case.
- Legacy components need a grep before removal to confirm zero usage in all pages and specs.
- `admin-usage-policies` scope dropdown is a static 2-option enum. Confirm `SpAdminSelectComponent` handles static `[options]` arrays without an API dependency.

---

## Final Verdict

The admin shared component library is solid. Inconsistencies are concentrated and fixable in three focused phases.

The top three problems by visual impact:

1. **Raw form controls** — number inputs and checkboxes not yet wrapped (critical, Phase 17A).
2. **Alert/feedback patterns** — four different approaches in use across pages (high, Phase 17A).
3. **Status/health display** — no dedicated status card; stat-card is repurposed or ad-hoc lists are used (high, Phase 17B).

No redesign needed. No architecture changes. Three small focused phases close all remaining gaps.

---

## Confirmation

- No code changes made in this phase. Only this review document was created.
- No backend, API, or product behavior changed.
- No commit or push performed.
- No new components created.
- No pages redesigned.
