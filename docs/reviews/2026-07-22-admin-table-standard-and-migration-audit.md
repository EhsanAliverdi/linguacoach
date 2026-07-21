# Admin table standard — reference implementation + migration audit

**Date:** 2026-07-22
**Related:** Skill Graph page (`src/LinguaCoach.Web/src/app/features/admin/admin-skill-graph/`), `sp-admin-table` design-system component (`src/LinguaCoach.Web/src/app/design-system/admin/components/table/sp-admin-table.component.ts`)

## Context

Over several iterations on the Skill Graph page's "Nodes" table, `sp-admin-table` grew four capabilities that used to be hand-rolled per page:

1. **Bold title column** — `titleColumn: true` on a `SpAdminTableColumn` (data-driven `[columns]`/`[rows]` mode), or the `.sp-admin-td-title` CSS class applied directly to a `<td>` (projection mode, i.e. a hand-written `<table>` passed as content).
2. **Filters in the table's own toolbar** — `[filters]` (`SpAdminTableFilter[]`) + `(filterChange)`, rendered left-aligned in a toolbar row the table owns, instead of a `sp-admin-form-field`/`sp-admin-select` row a page builds above the table.
3. **Bulk edit toggle** — `[bulkEditable]`/`[bulkEditMode]`/`(bulkEditModeChange)`, a "Bulk edit" button that shows/hides a checkbox merged into the title column (in data-driven mode, automatically; in projection mode, the page renders its own checkbox conditioned on the same bound flag) — instead of an always-visible leading checkbox column.
4. **`selectionBar` slot** — a content-projection slot for the "N selected + action buttons" row, rendered directly under the toolbar row, above the table body — instead of a bulk-action bar sitting in page markup outside `<sp-admin-table>` entirely.

The user asked that this become the standard for every admin table in the app, not something redefined per page. Decision (via AskUserQuestion): convert the Nodes table to the *fully data-driven* form of this pattern as the reference implementation, then audit the rest of the admin app's tables against it — without migrating them yet.

## Reference implementation

`admin-skill-graph.component.html`'s Nodes table (`#nodesTableRef`) now uses:

```html
<sp-admin-table
  #nodesTableRef
  [columns]="nodesColumns" [rows]="nodes()" (selectionChange)="onNodesSelectionChange($event)"
  [filters]="nodesFilters()" (filterChange)="onNodesFilterChange($event)"
  [bulkEditable]="true" [bulkEditMode]="nodesBulkEditMode()" (bulkEditModeChange)="onNodesBulkEditModeChange($event)"
  emptyMessage="No nodes match the current filters. Draft some above.">
  @if (hasSelection()) {
    <div selectionBar class="sp-admin-form-actions"> ... reject/approve buttons ... </div>
  }
  <ng-template #cell let-row let-col="col">
    @switch (col.key) { @case ('tags') { ...badges... } @case ('reviewStatus') { ...badge... } @default { {{ row[col.key] }} } }
  </ng-template>
</sp-admin-table>
```

No hand-written `<table>`/`<thead>`/`<tbody>` remains on this page. The table owns structure; the page supplies data + the genuinely custom cells (tag badges, status badge, subskill fallback) via one shared `#cell` template. `(selectionChange)` emits row *indices* into the currently-bound `rows` array; the page maps those to real node ids for the batch-approve/reject API calls. A new public `clearSelection()` on `SpAdminTableComponent` (called via the `#nodesTableRef` template ref) lets a page clear checkmarks after a successful bulk action without leaving bulk-edit mode.

## Migration audit — other admin tables

19 other admin feature pages still use a hand-written `<table>` (projection mode). None of them use `.sp-admin-td-title`, `[filters]`, or `[bulkEditable]`/`[selectionBar]` yet — every filter dropdown and bulk-action bar in these pages is hand-built markup sitting outside `<sp-admin-table>`, and every bulk-selectable page uses an always-visible leading checkbox column rather than the toggleable pattern. Title-column bolding, where present at all, is done ad hoc (`<strong>`, or one-off classes like `sp-admin-cell-title` / `sp-admin-text-strong` / `sp-admin-identity-name`) instead of the shared `.sp-admin-td-title` marker.

| Page | Title col bolded? | Filters via table? | Bulk actions via table? | Divergence | Note |
|---|---|---|---|---|---|
| admin-ai-usage | N-A (no single title col; 4 summary tables) | N | N/A | High | 4 raw tables (provider/feature/trend/log), heavy badge cells |
| admin-resource-bank-unified | Y (`<strong>`, not class) | N | N | High | 10 cols incl. type/level/skill/tags/focus/counts, large migration |
| admin-exercises | Y (`<strong>`, not class) | N | N | Medium | 8 cols, moderate badges — straightforward columns/cellTemplate |
| admin-diagnostics | N-A | N | N/A | High | 6 raw tables (health checks, log stream, failure patterns x3, prompt versions) — most complex file |
| admin-student-detail | N-A (detail sub-tables) | N/A | N/A | Low-Med | Multiple small embedded tables; low priority, not the primary list pattern |
| admin-modules | Y (`<strong>`, not class) | N | N | Medium-High | 10 cols incl tags, counts, source/status badges |
| admin-dashboard | Y (`sp-admin-cell-title`) | N/A | N/A | Low | Small 4-col summary table — quick win |
| admin-notifications | Y (`sp-admin-cell-title`) | N | N/A | High | 3 raw tables, each with its own filter row outside the table |
| admin-content-import | N (no bold) | N/A | N/A | Low | Small 4-col run list — easy |
| admin-import-run-candidates | N-A (badges dominate) | N | N | Medium-High | 8 cols, multiple badges + reject modal |
| admin-exercise-types | Y (on Skill, not the title-like col) | N | N/A | Medium-High | Rich custom cells (status dots, generation stats, breakdowns) |
| admin-lessons | Y (`<strong>`, not class) | N | N | Medium | 7 cols, similar to admin-exercises |
| admin-ai-operations | N-A (metrics tables) | N/A | N/A | Low-Med | 3 small stat tables, straightforward |
| admin-onboarding | Y (`sp-admin-cell-title`) | N/A | N/A | Low | Simple 5-col table — quick win |
| admin-placement-items | **N — flag** ("Question" col plain text) | N | N/A | Low | Small 4-col table — easy |
| admin-security | N-A | N | N/A | Low-Med | 2 small tables |
| admin-usage-policies | Y (`sp-admin-text-strong`) | N/A | N/A | Medium-High | Nested table-in-row (policy + rules sub-table), expand/collapse complicates migration |
| admin-prompts | **N — flag** (key rendered as code pill, no bold) | N | N/A | Medium | 6 cols, code-pill + badge cells |
| admin-students | Y (`sp-admin-identity-name`) | N | N/A | Medium | 8 cols, sortable headers, identity-cell avatar composition |

**Quick wins (Low divergence, small tables):** admin-dashboard, admin-content-import, admin-onboarding, admin-placement-items.
**Two pages have a title-like column with no bold at all:** admin-placement-items ("Question"), admin-prompts (key/code pill) — worth fixing even before a full migration.
**Highest-value bulk-edit migrations** (currently always-visible checkbox columns): admin-exercises, admin-modules, admin-lessons, admin-resource-bank-unified, admin-import-run-candidates.
**Most complex** (multiple raw tables per page, most rework): admin-diagnostics, admin-ai-usage, admin-notifications.

## Decisions made

- Reference pattern locked: `[columns]`/`[rows]`/`[cellTemplate]` + `titleColumn` + `[filters]` + `[bulkEditable]`/`[selectionBar]`, demonstrated end-to-end on the Skill Graph Nodes table.
- Scope for this pass: audit only, no other page migrated yet (explicit AskUserQuestion choice).

## Risks / unresolved questions

- `admin-usage-policies`' nested table-in-row (expand/collapse) doesn't map cleanly onto `[columns]`/`[rows]` as-is — may need either a nested `#cell` template per row or to stay in projection mode with just the `.sp-admin-td-title`/`[filters]`/`[selectionBar]` pieces adopted.
- `admin-students`' sortable-header click handlers should map to `SpAdminTableColumn.sortable`/`(sortChange)`, already supported by the component — not called out as a blocker, just unverified against the real column data shape.
- Projection-mode pages get `.sp-admin-td-title`/`[filters]`/`[bulkEditable]`/`[selectionBar]` but not the *automatic* title-column checkbox merge (that's data-driven-mode-only) — each projection-mode migration still hand-wires its own checkbox conditioned on the bound `bulkEditMode` flag, same as the Nodes table did before its full conversion.

## Final verdict

Reference implementation proven on one real page end-to-end (bold title, filters, bulk-edit toggle, selection bar, all working live with no regressions). Audit complete for the remaining 19 pages, ranked by divergence and migration cost.

## Next recommended action

Start with the four Low-divergence quick wins (admin-dashboard, admin-content-import, admin-onboarding, admin-placement-items) to validate the pattern travels cleanly to simpler tables, then tackle the always-visible-checkbox-column pages (admin-exercises, admin-modules, admin-lessons) since those are the clearest functional upgrade (toggleable bulk edit vs. permanently-visible checkboxes). Treat each migration as its own small, reviewable change rather than one large sweep.
