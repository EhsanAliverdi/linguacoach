# Phase 10X-K-17B — Admin Structural Visual Components Review

**Date:** 2026-06-19
**Phase:** 10X-K-17B
**Type:** Implementation review
**Based on audit:** docs/reviews/2026-06-19-phase-10x-k-16a-admin-visual-consistency-audit.md
**Commit base:** ad96d9c (phase 10x-k admin form primitives)

---

## Goal

Create four missing shared structural UI components — `sp-admin-status-card`, `sp-admin-status-grid`, `sp-admin-section-header`, `sp-admin-form-grid` — and apply them lightly to target admin pages to replace ad-hoc local CSS and raw heading patterns.

---

## Components Created

### `sp-admin-status-card`

**Location:** `src/app/admin/components/status-card/sp-admin-status-card.component.ts`

- Purpose: system health and operational status values (distinct from `sp-admin-stat-card` which is for numeric KPIs).
- Inputs: `label`, `value` (string | number), `tone` (success | warning | danger | info | neutral | primary), `helper`, `loading`.
- Renders a status dot using a CSS custom property per tone, a label in uppercase small caps style, and a bold value row.
- Loading skeleton: two pulse-animated bars (label + value width).
- Helper text: optional, shown below value.
- Does not duplicate `sp-admin-stat-card` — stat-card has an icon slot and trend slot for numeric KPIs; status-card is for operational state (reachable/unreachable, configured/not-set).

### `sp-admin-status-grid`

**Location:** `src/app/admin/components/status-grid/sp-admin-status-grid.component.ts`

- Layout wrapper for status cards.
- Inputs: `columns` (2 | 3 | 4 | 'auto'). Default: `'auto'`.
- Auto mode: `repeat(auto-fill, minmax(160px, 1fr))`.
- Fixed column modes collapse to 2-column below 640px.
- Used by diagnostics system status section.

### `sp-admin-section-header`

**Location:** `src/app/admin/components/section-header/sp-admin-section-header.component.ts`

- Normalizes intra-card/section sub-headings.
- Inputs: `title`, `description` (optional).
- Supports `[slot=actions]` content projection for right-side actions.
- Replaces raw `<h3 class="text-sm font-semibold text-gray-700 mb-2">` patterns.

### `sp-admin-form-grid`

**Location:** `src/app/admin/components/form-grid/sp-admin-form-grid.component.ts`

- Layout wrapper for form fields.
- Inputs: `columns` (1 | 2 | 3 | 4). Default: `2`.
- Responsive: single column below 768px; 2-column for 3/4 at 768–1023px.
- Replaces page-scoped `.sp-int-form-grid` CSS.

All four exported from `src/app/admin/index.ts`.

---

## Pages Changed

### admin-diagnostics

**Files:** `admin-diagnostics.component.html`, `admin-diagnostics.component.ts`

- Replaced 8 × `<sp-admin-stat-card size="sm">` system health items with `<sp-admin-status-card>` inside `<sp-admin-status-grid [columns]="4">`.
- Removed local `.sp-diag-status-grid` CSS rule (was: `repeat(auto-fill, minmax(190px, 1fr))`).
- Added `SpAdminStatusCardComponent`, `SpAdminStatusGridComponent` to imports.
- `SpAdminStatCardComponent` kept in imports (still used elsewhere — none in this file after change, but removal was safe to defer).

**Rationale:** System health items (Database: Reachable, AI provider: Configured, Uptime, Log level) are operational state, not numeric KPIs. The status-card pattern with a status dot and tone-colored border is semantically correct.

### admin-integrations

**Files:** `admin-integrations.component.html`, `admin-integrations.component.ts`

- Replaced 2 × `.sp-int-form-grid` divs with `<sp-admin-form-grid [columns]="3">`:
  - MinIO / Object Storage field grid (7 fields).
  - Lesson Buffer Settings field grid (11 fields).
- Replaced 2 × `<h3 class="text-sm font-semibold text-gray-700 mb-2">` with `<sp-admin-section-header title="..." />`:
  - "Ready lesson buffer per student"
  - "Recent batches"
- Removed `.sp-int-form-grid` local style rule.
- Added `SpAdminFormGridComponent`, `SpAdminSectionHeaderComponent` to imports array and named imports block.

---

## Pages Not Changed

### admin-dashboard

- AI System status section uses `sp-admin-badge [dot]="true"` rows (`.sp-admin-status-row`). This is a flat badge-list pattern, not a card grid. `sp-admin-status-card` would be visually heavier than appropriate here. Left as-is — it's a summary list, not a diagnostic grid.
- No raw `<h3>` or form grid patterns found.

### admin-ai-config

- Card-level section headers already use `sp-admin-card title="..."`.
- Intra-card sub-headings (LLM category display name, provider name) are data-driven inline divs inside `@for` loops — not standalone raw `<h3>` patterns. Replacing them with `sp-admin-section-header` inside a for loop would require the section-header to be imported and the data-bound pattern tested; risk is higher than benefit in this phase. Deferred.
- No raw `<h3>`/`<h4>` patterns found.

### admin-usage-policies

- Already cleaned up in 17A. `sp-admin-page-body`, `sp-admin-input`, `sp-admin-select`, `sp-admin-checkbox`, `sp-admin-alert` all in place. Form fields use `sp-admin-form-field` directly without a grid wrapper — layout is single-column, appropriate for a policy form. No grid change needed.

---

## Tests

### New spec files

- `src/app/admin/components/status-card/sp-admin-status-card.component.spec.ts` — 8 tests
  - label renders
  - string value renders
  - numeric value renders
  - helper text renders when provided
  - helper element absent when empty
  - loading skeleton shown when loading=true
  - skeleton absent when loading=false
  - all 6 tone values accepted without error

- `src/app/admin/components/status-grid/sp-admin-status-grid.component.spec.ts` — 4 tests
  - projected content renders
  - defaults to auto columns
  - columns=4 accepted without error
  - columns=2 accepted without error

- `src/app/admin/components/section-header/sp-admin-section-header.component.spec.ts` — 4 tests
  - title renders
  - description renders when provided
  - description element absent when empty
  - title element present without description

- `src/app/admin/components/form-grid/sp-admin-form-grid.component.spec.ts` — 4 tests
  - projected content renders
  - defaults to 2 columns
  - columns=3 accepted without error
  - columns=1 accepted without error

### Updated specs

- `admin-diagnostics.component.spec.ts` — updated 1 + added 2 tests
  - Updated: "renders 8 status cards inside status grid" (was: "renders system status stat cards" asserting sp-admin-stat-card)
  - Added: "status card shows database reachable value"
  - Added: "status card shows AI provider value"

- `admin-integrations.component.spec.ts` — added 2 tests
  - "renders section headers for buffer and batches sub-sections"
  - "renders form-grid wrappers for settings and storage fields"

---

## Build Result

Production build: **PASS** — `dist/lingua-coach.web` generated, no errors.

(One pre-existing WARNING: `*ngIf` in `onboarding-v2-welcome.component.ts` — not introduced by this phase, not touched.)

## Angular Test Result

**652 / 652 PASS** — 0 failures.

(Previous: 628. +24 new tests: 20 in new component specs + 2 in diagnostics + 2 in integrations.)

## Playwright Result

Not run — no Playwright tests directly cover the changed admin structural components.

---

## Status Cards Centralized

**Yes** — diagnostics system status section now uses `sp-admin-status-card` + `sp-admin-status-grid`. The 8 operational health items are no longer using the numeric-KPI `sp-admin-stat-card`.

## Form Grid Centralized

**Partially** — integrations uses `sp-admin-form-grid` for its two form sections. Admin-ai-config still uses Tailwind `grid gap-4 sm:grid-cols-2` — replacing those requires `SpAdminFormGridComponent` import into ai-config and is deferred as a separate cleanup pass once confirmed stable.

## Section Headers Centralized

**Partially** — integrations raw `<h3>` patterns replaced. Admin-ai-config intra-loop data-driven headings deferred (see above).

---

## Remaining Structural Inconsistencies After This Phase

| Location | Pattern | Status |
|---|---|---|
| `admin-ai-config` LLM/TTS category cards | Tailwind `grid gap-4 sm:grid-cols-2` | Deferred — needs `SpAdminFormGridComponent` imported to ai-config |
| `admin-ai-config` intra-card category heading divs | Raw `div.text-sm.font-bold` | Deferred — data-driven, inside `@for` loop, higher risk |
| `admin-ai-config` per-card inline save spans | `text-xs text-emerald-600` / `text-xs text-red-500` | Deferred from 17A, still pending |
| `admin-dashboard` AI System badge rows | Flat badge list | Intentionally left — not a card grid pattern |
| `admin-ai-config` LLM/TTS selects | `<select [ngValue]="null">` | Deferred from 17A — needs `sp-admin-select` object binding support |

---

## Decisions Made

- `sp-admin-status-card` is distinct from `sp-admin-stat-card`: status-card has a tone-colored border, a status dot, and no icon slot — appropriate for health/state. stat-card has an icon slot and trend slot — appropriate for numeric KPIs.
- `sp-admin-status-grid` default is `'auto'` columns, not a fixed number, to stay flexible.
- `sp-admin-form-grid` defaults to `2` columns — the most common admin form layout.
- `sp-admin-section-header` has no mandatory slot — `title` is the only required input, `description` and `[slot=actions]` are both optional.
- `ChangeDetectionStrategy.OnPush` was not used — consistent with all other admin shared components.
- Admin-ai-config was audited for raw h3 patterns; none found. The inline per-category headings are data-driven and were not replaced.

---

## Confirmation

- No backend, API, or product behavior changed.
- No new settings, fields, routes, services, or API calls added.
- No commit or push performed.
- No Playwright tests run.
- Build: PASS. Angular tests: 652/652 PASS.
