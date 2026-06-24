# Phase 10UI-PROMPTS-PARITY-1: Admin Prompts Content Rebuild from JSX

**Date:** 2026-06-25
**Sprint:** Phase 10 UI Parity
**Feature:** `/admin/prompts` content area visual rebuild
**Design reference:** `docs/design/speakpath/admin/pages/prompts.jsx`

---

## Files Reviewed

- `docs/design/speakpath/admin/pages/prompts.jsx` — design reference
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.ts` — rebuilt
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.spec.ts` — updated
- `src/LinguaCoach.Web/src/app/design-system/admin/components/kpi-card/sp-admin-kpi-card.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/stat-card/sp-admin-stat-card.component.ts`
- `src/LinguaCoach.Web/src/app/design-system/admin/components/slide-over/sp-admin-slide-over.component.ts`
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`

---

## Findings

### High priority — fixed

**KPI cards were using `layout='standard'` (circle icon, 30px value).**
JSX design uses tile layout: flush icon column with border-right, 24px value, no padding.
Fixed by adding `layout="tile"` to all four `sp-admin-kpi-card` elements.

**No slide-over for prompt detail or edit.**
Old component showed prompt content inline in an `sp-admin-card` block below the header.
JSX uses a SlideIn panel for viewing and editing prompts.
Fixed by replacing inline detail/form blocks with two `sp-admin-slide-over` panels:
- View slide-over: meta grid (category, status, token in/out), variable pills, dark-background prompt body.
- Edit/create slide-over: warning alert, token budget fields, prompt textarea (monospace).

**"latest" badge missing from table rows.**
JSX marks active + highest-version rows with a small teal-tinted "latest" badge beside the key pill.
Fixed with `isLatestVersion()` computed check and `sp-admin-badge tone="info"` (teal not available as `SpAdminBadgeTone`).

**`promptCategory()` only handled dot-delimited keys.**
Backend keys use underscore delimiters (e.g. `activity_evaluate_answer_short_q`).
Old code split on `.` only; `activity` prefix returned "Activity" without a mapping.
Fixed by splitting on `[._]` and mapping prefixes: `activity` → "Other", `system` → "Curriculum", `placement` → "Assessment", `memory` → "Memory".

**Token budget column not right-aligned or monospace.**
JSX shows `1,000 in / 800 out` right-aligned in `JetBrains Mono`.
Fixed with `.sp-adm-token-budget` class and `sp-admin-th-right` on the column header.

**Table rows not clickable.**
JSX wraps rows in `onClick={() => setViewRow(row)}`.
Fixed with `(click)="openView(p)"` on `<tr>` and `(click)="$event.stopPropagation()"` on the actions cell.

### Medium priority — fixed

**Page header subtitle used total version count, not template (key) count.**
JSX subtitle: "Manage and version AI prompt templates · 20 templates" where 20 = unique key count.
Fixed: subtitle now uses `uniqueKeyCount()` not `prompts().length`.

**Category filter options not filtered from backend data.**
Existing code was already correct. Verified and kept.

**"New version" button opened inline form.**
JSX opens a SlideIn panel. Fixed by using the edit slide-over with `editRow()=null` for create mode.

### Low priority — left as-is

**Version history selector in view slide-over.**
JSX renders a multi-version selector (v1/v2/v3 chips) in the view panel.
The Angular backend only returns one version per `PromptTemplateItem` row — each version is its own row in the table. There is no single-prompt multi-version API endpoint.
Decision: omit version history selector. The table already shows all versions as separate rows.

**Dark-syntax-highlighted prompt variables.**
JSX renders `{{variable}}` tokens in amber/orange inside the dark code block using string splitting.
Angular equivalent uses `pre` with `white-space: pre-wrap` — variables render as plain text in the dark block.
Decision: acceptable difference. Implementing syntax highlighting would require an additional pipe or directive not yet in the design system.

---

## Decisions Made

- Use `SpAdminKpiCardComponent` with `layout="tile"` (not `SpAdminStatCardComponent`) to match existing page KPI rows and the JSX tile style.
- Use `SpAdminSlideOverComponent` (already in design system) for both view and edit panels; `stackIndex=1` when edit opens over view.
- Keep menu item text "View content" (not "View") to preserve the existing wrapper migration test assertion.
- "latest" badge uses `tone="info"` (closest to teal available in `SpAdminBadgeTone`).
- `promptCategory()` now handles underscore-prefixed backend keys with an explicit prefix map.

---

## Tests

### Updated: `admin-prompts.component.spec.ts`

- 43 tests, all pass.
- New tests added:
  - `promptCategory` for underscore keys (activity, system, placement, memory)
  - `isLatestVersion` — highest version returns true, lower returns false
  - `promptVars` — extracts unique variable names; empty for content without variables
  - slide-over state: `closeView` clears signals; `closeEdit` clears signals
  - `openEdit` sets `editRow`; `openCreate` sets `editRow` to null
  - `createVersion` uses `editRow.key` on edit; uses `newKey` on create
  - separate formError tests for missing content vs missing key

### Full suite: 1336/1336 pass

---

## Build

```
npm run build -- --configuration production
```
Exit: 0 (warnings only, all pre-existing — not introduced by this change).

---

## Risks / Unresolved

- Inline variable syntax highlighting (amber tokens in dark block) not implemented.
- Version history selector in view slide-over not implemented (no backend multi-version detail endpoint).
- `SpAdminBadgeTone` does not include `teal`; "latest" badge uses `info` (blue-tinted). Consider adding `teal` to the badge tone type in a future design-system task.

---

## Visual Acceptance

Rebuilt to match JSX structure:
- Page title "Prompts" + subtitle with template count.
- "New version" top-right button opens create slide-over.
- 4 tile-layout KPI cards: Templates, Active versions, Total versions, Avg token budget.
- "Prompt library" card with search / category / status filter row + Refresh button.
- Table: KEY (code pill + latest badge), CATEGORY (badge), VERSION, STATUS (dot badge), TOKEN BUDGET (right-aligned monospace), ACTIONS (menu).
- Empty state when filters produce no rows.
- Loading and error states.
- View slide-over: meta grid, variable pills, dark prompt body, footer (Edit / Deactivate / Close).
- Edit/Create slide-over: warning alert (on edit), token budget fields, monospace textarea, footer (Save/Create / Cancel).
- Pagination shown when total pages > 1.

---

## Next Recommended Action

Run the app locally and open `/admin/prompts` to compare visually with `prompts.jsx`.
If variable syntax highlighting in the dark code block is required, create a `PromptBodyPipe` or directive in the design system.
