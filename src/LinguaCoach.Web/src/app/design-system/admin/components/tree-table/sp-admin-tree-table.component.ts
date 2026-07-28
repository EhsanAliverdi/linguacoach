import { Component, ContentChild, EventEmitter, Input, Output, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TreeTableModule } from 'primeng/treetable';
import type { TreeNode } from 'primeng/api';
import type { TreeTableLazyLoadEvent } from 'primeng/treetable';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';
import { SpAdminErrorStateComponent } from '../error-state/sp-admin-error-state.component';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';
import { SpAdminSelectComponent } from '../select/sp-admin-select.component';
import { SpAdminInputComponent } from '../input/sp-admin-input.component';
import { SpAdminTableFooterComponent } from '../table-footer/sp-admin-table-footer.component';
import { SpAdminPaginationComponent } from '../pagination/sp-admin-pagination.component';
import { SpAdminTableColumn, SpAdminTableFilter, SpAdminTableVariant, SpAdminTableDensity, SpAdminTableLayout } from '../table/sp-admin-table.component';

/**
 * Skill Graph rebuild Phase 4 (2026-07-27) — a reusable, design-system-native tree table: same
 * visual language and CSS classes as `SpAdminTableComponent` (this file's `styles` block is
 * deliberately a near-duplicate of that component's, not a shared import, to avoid coupling two
 * independently-evolving components to one stylesheet), with hierarchical rows (expand/collapse,
 * indentation) added.
 *
 * User correction (2026-07-27): a first pass used PrimeNG's TreeTable with its own default (Aura)
 * theme directly on the Skill Graph page — this looked nothing like the rest of the admin (flat
 * `SpAdminTableComponent` everywhere else), wasn't a real reusable design-system component, and
 * filters didn't apply to lazily-loaded children. This component keeps `p-treeTable` as the
 * underlying engine — "use the library" — for the parts worth trusting a library for (lazy-load
 * orchestration, and the internal expand/selection state machine via its own toggler/checkbox
 * sub-components, so expand-state bookkeeping is never hand-rolled and risk of desync bugs stays
 * with a well-tested library instead of new code here). Every header/cell is our own template via
 * `pTemplate="header"/"body"` using `SpAdminTableComponent`'s own CSS classes; PrimeNG's toggler/
 * checkbox sub-widgets are kept (for correctness) but their own default visuals are overridden via
 * `:host ::ng-deep` to match this design system's checkbox/icon sizing and color instead of Aura's.
 */
@Component({
  selector: 'sp-admin-tree-table',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TreeTableModule,
    SpAdminEmptyStateComponent, SpAdminErrorStateComponent, SpAdminLoadingStateComponent,
    SpAdminSelectComponent, SpAdminInputComponent, SpAdminTableFooterComponent, SpAdminPaginationComponent,
  ],
  template: `
    <div [class]="outerClasses">
      @if (loading) {
        <sp-admin-loading-state message="Loading records" />
      } @else if (error) {
        <sp-admin-error-state [title]="errorTitle" [message]="error" />
      } @else {
        @if (searchable || filters.length) {
          <div class="sp-adm-toolbar-row">
            <div class="sp-adm-toolbar-filters">
              @if (searchable) {
                <div class="sp-adm-toolbar-filter sp-adm-toolbar-search">
                  <sp-admin-input
                    [ngModel]="searchValue"
                    [ngModelOptions]="{standalone: true}"
                    (ngModelChange)="onSearchValueChange($event)"
                    [placeholder]="searchPlaceholder"
                    size="sm"
                  />
                </div>
              }
              @for (filter of filters; track filter.key) {
                <div class="sp-adm-toolbar-filter">
                  <label class="sp-adm-toolbar-filter-label">{{ filter.label }}</label>
                  <sp-admin-select
                    [options]="filter.options"
                    [placeholder]="filter.placeholder ?? ''"
                    size="sm"
                    [ngModel]="filter.value"
                    [ngModelOptions]="{standalone: true}"
                    (ngModelChange)="onFilterValueChange(filter, $event)"
                  />
                </div>
              }
            </div>
            <div class="sp-adm-toolbar-actions">
              <ng-content select="[tableActions]" />
            </div>
          </div>
        }
        <div class="sp-adm-selection-row">
          <ng-content select="[selectionBar]" />
        </div>
        @if (!rows.length) {
          <sp-admin-empty-state [message]="emptyMessage" />
        } @else {
          <div [class]="scrollClass" [style.--sp-admin-table-min-width]="minWidth" [class.sp-adm-fluid-layout]="layout === 'first-column-fluid'">
            <p-treeTable
              [value]="rows" dataKey="key"
              [lazy]="true" [lazyLoadOnInit]="false" (onLazyLoad)="lazyLoad.emit($event)"
              (onNodeExpand)="nodeExpand.emit($event.node)"
              [paginator]="false"
              [selectionMode]="selectable ? 'checkbox' : undefined"
              [selection]="selection" (selectionChange)="onPrimeSelectionChange($event)"
              tableStyleClass="sp-adm-table w-full border-collapse"
            >
              <ng-template pTemplate="header">
                <tr [class]="theadRowClass">
                  @if (selectable) {
                    <th scope="col" class="sp-adm-th sp-adm-th-check">
                      <p-treeTableHeaderCheckbox />
                    </th>
                  }
                  @for (column of columns; track column.key) {
                    <th scope="col" [class]="thClass(column)" [style.width]="column.width || null" [style.text-align]="column.align || null">
                      {{ column.label }}
                    </th>
                  }
                  @if (hasActions) { <th scope="col" class="sp-adm-th"></th> }
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-rowNode let-rowData="rowData">
                <tr [ttRow]="rowNode" [class]="trClass()">
                  @if (selectable) {
                    <td class="sp-adm-td sp-adm-td-check">
                      <p-treeTableCheckbox [value]="rowNode" />
                    </td>
                  }
                  @for (column of columns; track column.key) {
                    <td [class]="tdClass(column)" [style.text-align]="column.align || null">
                      <span class="inline-flex items-center gap-2">
                        @if (column.titleColumn) {
                          <span [style.paddingLeft.px]="rowNode.level * 20" class="sp-adm-tree-indent">
                            @if (!rowNode.node.leaf) {
                              <p-treeTableToggler [rowNode]="rowNode" />
                            }
                          </span>
                        }
                        @if (cellTemplate) {
                          <ng-container *ngTemplateOutlet="cellTemplate; context: { $implicit: rowData, col: column, node: rowNode.node }" />
                        } @else {
                          {{ rowData[column.key] }}
                        }
                      </span>
                    </td>
                  }
                  @if (hasActions) {
                    <td class="sp-adm-td">
                      <ng-container *ngTemplateOutlet="rowActionsTemplate ?? null; context: { $implicit: rowData, node: rowNode.node }" />
                    </td>
                  }
                </tr>
              </ng-template>
            </p-treeTable>
          </div>
          @if (paginationTotalPages !== undefined) {
            <sp-admin-table-footer>
              {{ paginationLabel }}
              <sp-admin-pagination slot="actions" [page]="paginationPage ?? 1" [totalPages]="paginationTotalPages" (pageChange)="paginationPageChange.emit($event)" />
            </sp-admin-table-footer>
          }
        }
      }
    </div>
  `,
  // Deliberately duplicated from SpAdminTableComponent's own `styles` block (same class names,
  // same values) rather than shared via styleUrls — see the class-level doc comment.
  styles: [`
    .sp-adm-table-card    { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }
    .sp-adm-table-data    { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }
    .sp-adm-table-flush   { background:transparent; overflow:hidden; }
    .sp-adm-table-scroll  { overflow-x:auto; width:100%; }
    .sp-adm-table-scroll::-webkit-scrollbar { height:8px; }
    .sp-adm-table-scroll::-webkit-scrollbar-thumb { background:#d1d5db; border-radius:999px; }

    :host ::ng-deep table { width:100%; min-width:var(--sp-admin-table-min-width, 720px); border-collapse:collapse; border-spacing:0; table-layout:auto; }
    :host ::ng-deep thead tr { border-bottom:1px solid #ECE9F5; }
    :host ::ng-deep th {
      padding:10px 16px; background:transparent; color:#8B85A0; font-size:11px; font-weight:800;
      line-height:1.4; text-align:left; white-space:nowrap; vertical-align:middle; letter-spacing:.07em;
      text-transform:uppercase; border-bottom:1px solid #ECE9F5; user-select:none;
    }
    :host ::ng-deep td { padding:12px 16px; color:#4B4462; font-size:13.5px; line-height:1.5; vertical-align:middle; border-bottom:1px solid #ECE9F5; min-width:0; }
    :host ::ng-deep tbody tr:last-child td { border-bottom:0; }
    :host ::ng-deep tbody tr:hover td { background:#FAFAFE; }

    .sp-adm-toolbar-row { display:flex; align-items:flex-end; justify-content:space-between; gap:12px; flex-wrap:wrap; padding:12px 16px; border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }
    .sp-adm-toolbar-filters { display:flex; gap:12px; flex-wrap:wrap; align-items:flex-end; }
    .sp-adm-toolbar-filter { display:flex; flex-direction:column; gap:4px; min-width:140px; }
    .sp-adm-toolbar-search { min-width:220px; }
    .sp-adm-toolbar-filter-label { font-size:11px; font-weight:700; color:var(--sp-admin-text-muted,#8B85A0); text-transform:uppercase; letter-spacing:.04em; }
    .sp-adm-toolbar-actions { display:flex; gap:8px; align-items:center; flex-shrink:0; flex-wrap:wrap; }

    .sp-adm-selection-row { padding:0 16px; margin:12px 0; }
    .sp-adm-selection-row:empty { display:none; margin:0; }

    .sp-adm-th        { text-align:left; font-size:11px; font-weight:700; color:var(--sp-admin-text-muted,#64748B); white-space:nowrap; letter-spacing:0.06em; text-transform:uppercase; background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-adm-td        { color:var(--sp-admin-text-secondary,#334155); vertical-align:middle; border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }
    .sp-adm-th-check, .sp-adm-td-check { width:40px; padding:0 12px; text-align:center; }
    .sp-adm-tr-hover { transition:background .1s; }
    .sp-adm-tr-hover:hover { background:var(--sp-admin-bg,#F6F4FB); }
    :host ::ng-deep .sp-admin-td-title { font-weight:700; color:var(--sp-admin-text,#211B36); }

    .sp-adm-tree-indent { display:inline-flex; align-items:center; }

    /* PrimeNG's own toggler/checkbox sub-widgets are kept for correct expand/selection behavior
       (see class doc comment) — these overrides make them match this design system's sizing/color
       instead of the Aura theme's, so they blend into an otherwise fully custom-styled table. */
    :host ::ng-deep .p-treetable-toggler {
      width:20px; height:20px; margin-right:2px; color:var(--sp-admin-text-muted,#8B85A0);
      border-radius:4px; background:transparent; border:none; cursor:pointer;
    }
    :host ::ng-deep .p-treetable-toggler:hover { background:var(--sp-admin-bg,#F6F4FB); color:var(--sp-admin-text,#211B36); }
    :host ::ng-deep .p-checkbox { width:16px; height:16px; }
    :host ::ng-deep .p-checkbox .p-checkbox-box {
      width:16px; height:16px; border-radius:4px; border:1.5px solid var(--sp-admin-border,#ECE9F5);
    }
    :host ::ng-deep .p-checkbox.p-checkbox-checked .p-checkbox-box,
    :host ::ng-deep .p-checkbox .p-checkbox-box.p-highlight {
      background:var(--sp-admin-primary,#5B4BE8); border-color:var(--sp-admin-primary,#5B4BE8);
    }

    /* first-column-fluid layout (ported from SpAdminTableComponent) — user-reported "column data
       overlaps/wraps": table-layout:auto lets every column's width float with its content, and
       with variable-width cells (badges, tags) that reflows unpredictably and can visually
       overlap. Fluid layout fixes the title column's width to take the spare space (wrapping
       normally there only) while every other column shrinks to its content and stays one line. */
    :host ::ng-deep .sp-adm-fluid-layout table { table-layout: auto; width: 100%; }
    :host ::ng-deep .sp-adm-fluid-layout table th,
    :host ::ng-deep .sp-adm-fluid-layout table td { white-space: nowrap; }
    :host ::ng-deep .sp-adm-fluid-layout table th:first-child,
    :host ::ng-deep .sp-adm-fluid-layout table td:first-child { width: 9999px; white-space: normal; }
    :host ::ng-deep .sp-adm-fluid-layout table th.sp-admin-fluid-col,
    :host ::ng-deep .sp-adm-fluid-layout table td.sp-admin-fluid-col { width: 9999px; white-space: normal; }
    :host ::ng-deep .sp-adm-fluid-layout table tr:has(th.sp-admin-fluid-col) th:first-child,
    :host ::ng-deep .sp-adm-fluid-layout table tr:has(td.sp-admin-fluid-col) td:first-child { width: auto; }
  `],
})
export class SpAdminTreeTableComponent<T = unknown> {
  @ContentChild('cell') cellTemplate?: TemplateRef<{ $implicit: T; col: SpAdminTableColumn; node: TreeNode<T> }>;
  @ContentChild('rowActions') rowActionsTemplate?: TemplateRef<{ $implicit: T; node: TreeNode<T> }>;

  @Input() columns: SpAdminTableColumn[] = [];
  @Input() rows: TreeNode<T>[] = [];
  @Input() loading = false;
  @Input() error = '';
  @Input() errorTitle = 'Could not load data';
  @Input() emptyMessage = 'No records found.';
  @Input() hasActions = false;
  @Input() variant: SpAdminTableVariant = 'data';
  @Input() density: SpAdminTableDensity = 'compact';
  @Input() minWidth = '720px';
  @Input() flush = false;
  /** 'first-column-fluid' (matches SpAdminTableComponent): the title column takes all spare width
   * and wraps normally; every other column shrinks to its content and stays on one line — fixes
   * the "columns overlap/wrap" look 'auto' layout gets once badges/tags vary row to row. */
  @Input() layout: SpAdminTableLayout = 'auto';

  @Input() selectable = false;
  @Input() selection: TreeNode<T>[] = [];
  @Output() selectionChange = new EventEmitter<TreeNode<T>[]>();

  @Input() searchable = false;
  @Input() searchValue = '';
  @Input() searchPlaceholder = 'Search…';
  @Output() searchChange = new EventEmitter<string>();

  @Input() filters: SpAdminTableFilter[] = [];
  @Output() filterChange = new EventEmitter<{ key: string; value: string }>();

  @Input() paginationPage?: number;
  @Input() paginationTotalPages?: number;
  @Input() paginationLabel?: string;
  @Output() paginationPageChange = new EventEmitter<number>();

  /** Fires on init/page change — caller re-fetches root rows and re-binds `[rows]`. */
  @Output() lazyLoad = new EventEmitter<TreeTableLazyLoadEvent>();
  /** Fires when a non-leaf row is expanded via PrimeNG's own toggler and has no children loaded
   * yet — caller fetches and sets `node.children`, then re-binds `[rows]` (a new array reference)
   * to trigger re-render. Matches `p-treeTable`'s own `onNodeExpand` contract exactly. */
  @Output() nodeExpand = new EventEmitter<TreeNode<T>>();

  get outerClasses(): string {
    if (this.flush) return 'sp-adm-table-flush';
    return this.variant === 'data' ? 'sp-adm-table-data' : 'sp-adm-table-card';
  }

  get scrollClass(): string {
    return `sp-adm-table-scroll sp-adm-table-density-${this.density}`;
  }

  get theadRowClass(): string {
    return 'sp-adm-thead-row';
  }

  thClass(column: SpAdminTableColumn): string {
    const cls = ['sp-adm-th'];
    if (column.titleColumn && this.layout === 'first-column-fluid') cls.push('sp-admin-fluid-col');
    return cls.join(' ');
  }

  tdClass(column: SpAdminTableColumn): string {
    const cls = ['sp-adm-td'];
    if (column.titleColumn && !column.nobold) cls.push('sp-admin-td-title');
    if (column.titleColumn && this.layout === 'first-column-fluid') cls.push('sp-admin-fluid-col');
    return cls.join(' ');
  }

  trClass(): string {
    return 'sp-adm-tr-hover';
  }

  onFilterValueChange(filter: SpAdminTableFilter, value: string): void {
    filter.value = value;
    this.filterChange.emit({ key: filter.key, value });
  }

  onSearchValueChange(value: string): void {
    this.searchValue = value;
    this.searchChange.emit(value);
  }

  /** `p-treeTable`'s (selectionChange) can emit a single node or null in some selection modes —
   * normalized to an array (empty when null) since this component's own contract is always an
   * array, matching `SpAdminTableComponent.selectionChange`'s row-indices-array convention. */
  onPrimeSelectionChange(event: TreeNode<T> | TreeNode<T>[] | null): void {
    this.selectionChange.emit(Array.isArray(event) ? event : event ? [event] : []);
  }
}
