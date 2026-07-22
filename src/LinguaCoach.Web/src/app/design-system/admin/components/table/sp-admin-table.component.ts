import { Component, ContentChild, Input, Output, EventEmitter, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';
import { SpAdminErrorStateComponent } from '../error-state/sp-admin-error-state.component';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';
import { SpAdminSelectComponent, SpAdminSelectOption } from '../select/sp-admin-select.component';
import { SpAdminTableFooterComponent } from '../table-footer/sp-admin-table-footer.component';
import { SpAdminPaginationComponent } from '../pagination/sp-admin-pagination.component';
import { SpAdminInputComponent } from '../input/sp-admin-input.component';

/** A single dropdown filter rendered in the table's toolbar row (left side, next to actions). */
export interface SpAdminTableFilter {
  key: string;
  label: string;
  options: SpAdminSelectOption[];
  value: string;
  placeholder?: string;
}

export interface SpAdminTableColumn {
  key: string;
  label: string;
  muted?: boolean;
  sortable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  /** Marks this as the table's title column — bold by default (data-driven [columns]/[rows] mode only). */
  titleColumn?: boolean;
  /** Opts a titleColumn out of the default bold styling. */
  nobold?: boolean;
}

/** Column definition for projection-mode tables — drives colgroup and table-layout:fixed. */
export interface SpAdminColDef {
  width: string;
  align?: 'left' | 'center' | 'right';
}

export type SortDirection = 'asc' | 'desc';
export interface SpAdminSortChange { column: string; direction: SortDirection; }
export type SpAdminTableVariant = 'basic' | 'data' | 'bordered' | 'striped' | 'simple' | 'card';
export type SpAdminTableLayout = 'auto' | 'first-column-fluid';
export type SpAdminTableDensity = 'compact' | 'comfortable' | 'spacious';

/**
 * TailAdmin table wrapper — sortable, multi-variant, multi-density.
 * basic:   TailAdmin basic-table-one (rounded-2xl border, bg-gray-50 thead)
 * data:    TailAdmin data-table-one  (striped rows, accent header)
 * bordered: visible column borders
 * striped:  alternating row shading
 * simple:   no outer card shell, bare table
 * card:     elevated shadow card
 *
 * Consumer handles sorting — emits (sortChange) with {column, direction}.
 */
@Component({
  selector: 'sp-admin-table',
  standalone: true,
  imports: [CommonModule, FormsModule, SpAdminEmptyStateComponent, SpAdminErrorStateComponent, SpAdminLoadingStateComponent, SpAdminButtonComponent, SpAdminSelectComponent, SpAdminInputComponent, SpAdminTableFooterComponent, SpAdminPaginationComponent],
  template: `
    <div [class]="outerClasses">
      @if (loading) {
        <sp-admin-loading-state message="Loading records" />
      } @else if (error) {
        <sp-admin-error-state [title]="errorTitle" [message]="error" />
      } @else {
        @if (searchable || filters.length || bulkEditable) {
          <div class="sp-adm-toolbar-row">
            @if (searchable || filters.length) {
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
            }
            <div class="sp-adm-toolbar-actions">
              <ng-content select="[tableActions]" />
              @if (bulkEditable) {
                <sp-admin-button
                  size="sm"
                  [variant]="bulkEditMode ? 'primary' : 'neutral'"
                  [appearance]="bulkEditMode ? 'solid' : 'outline'"
                  (clicked)="toggleBulkEdit()"
                >
                  {{ bulkEditMode ? 'Done' : 'Bulk edit' }}
                </sp-admin-button>
              }
            </div>
          </div>
        }
        <div class="sp-adm-selection-row">
          <ng-content select="[selectionBar]" />
        </div>
        @if (columns.length === 0) {
          <div [class]="scrollClass" [style.--sp-admin-table-min-width]="minWidth" [class.sp-adm-fixed-layout]="fixedLayout" [class.sp-adm-fluid-layout]="layout === 'first-column-fluid'">
            <ng-content />
          </div>
          @if (paginationTotalPages !== undefined) {
            <sp-admin-table-footer>
              {{ paginationLabel }}
              <sp-admin-pagination slot="actions" [page]="paginationPage ?? 1" [totalPages]="paginationTotalPages" (pageChange)="paginationPageChange.emit($event)" />
            </sp-admin-table-footer>
          }
        } @else if (!rows.length) {
          <sp-admin-empty-state [message]="emptyMessage" />
        } @else {
          <div [class]="scrollClass" [style.--sp-admin-table-min-width]="minWidth" [class.sp-adm-fixed-layout]="fixedLayout" [class.sp-adm-fluid-layout]="layout === 'first-column-fluid'">
            <table class="sp-adm-table w-full border-collapse" [style.min-width]="minWidth || null" [class.sp-adm-table-bordered]="variant === 'bordered'">
              @if (showHeader) {
                <thead [class]="theadClass" [class.sp-adm-thead-sticky]="stickyHeader">
                  <tr [class]="theadRowClass">
                    @if (selectable) {
                      <th scope="col" class="sp-adm-th sp-adm-th-check" style="width:40px">
                        <input type="checkbox" [checked]="allSelected" (change)="onSelectAll($event)" aria-label="Select all" />
                      </th>
                    }
                    @for (column of columns; track column.key) {
                      <th
                        scope="col"
                        [class]="thClass(column)"
                        [style.width]="column.width || null"
                        [style.text-align]="effectiveAlign(column)"
                        [attr.aria-sort]="sortAriaLabel(column)"
                        (click)="column.sortable && onSortClick(column.key)"
                        (keydown.enter)="column.sortable && onSortClick(column.key)"
                        [attr.tabindex]="column.sortable ? 0 : null"
                        [attr.role]="column.sortable ? 'button' : null"
                      >
                        <span class="inline-flex items-center gap-1 select-none">
                          @if (bulkEditMode && column.titleColumn) {
                            <input type="checkbox" class="sp-adm-bulk-checkbox" [checked]="allSelected" (change)="onSelectAll($event)" (click)="$event.stopPropagation()" aria-label="Select all" />
                          }
                          {{ column.label }}
                          @if (column.sortable) {
                            <span class="sp-adm-sort-icon" aria-hidden="true">{{ sortIcon(column.key) }}</span>
                          }
                        </span>
                      </th>
                    }
                    @if (hasActions) {
                      <th scope="col" [class]="thClass(null)">Actions</th>
                    }
                  </tr>
                </thead>
              }
              <tbody>
                @for (row of rows; track $index) {
                  <tr [class]="trClass($index)" [class.sp-adm-tr-clickable]="rowClickable" (click)="rowClickable && rowClick.emit(row)">
                    @if (selectable) {
                      <td class="sp-adm-td sp-adm-td-check">
                        <input type="checkbox" [checked]="isSelected($index)" (change)="onSelectRow($index, $event)" (click)="$event.stopPropagation()" [attr.aria-label]="'Select row ' + ($index + 1)" />
                      </td>
                    }
                    @for (column of columns; track column.key) {
                    <td
                        [class]="tdClass(column)"
                        [style.text-align]="effectiveAlign(column)"
                        [style.width]="column.width || null"
                      >
                      <span class="inline-flex items-center gap-2">
                        @if (bulkEditMode && column.titleColumn) {
                          <input type="checkbox" class="sp-adm-bulk-checkbox" [checked]="isSelected($index)" (change)="onSelectRow($index, $event)" (click)="$event.stopPropagation()" [attr.aria-label]="'Select row ' + ($index + 1)" />
                        }
                        @if (cellTemplate) {
                          <ng-container *ngTemplateOutlet="cellTemplate; context: { $implicit: row, col: column }" />
                        } @else {
                          {{ $any(row)[column.key] }}
                        }
                      </span>
                    </td>
                    }
                    @if (hasActions) {
                      <td [class]="tdClass(null)">
                        <ng-content select="[rowActions]" />
                      </td>
                    }
                  </tr>
                  @if (rowDetailTemplate && isRowExpanded && isRowExpanded(row, $index)) {
                    <tr class="sp-adm-row-detail">
                      <td [attr.colspan]="detailColspan">
                        <ng-container *ngTemplateOutlet="rowDetailTemplate; context: { $implicit: row, index: $index }" />
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
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
  styles: [`
    /* SpeakPath-aligned table card shells */
    .sp-adm-table-card    { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }
    .sp-adm-table-data    { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }
    /* flush: no outer shell — table sits inside a parent card */
    .sp-adm-table-flush   { background:transparent; overflow:hidden; }
    .sp-adm-table-simple  { background:transparent; }
    .sp-adm-table-card-v  { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; box-shadow:var(--sp-admin-shadow-card,0 2px 8px rgba(60,48,140,.07)); }
    .sp-adm-table-bordered-v { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }
    .sp-adm-table-striped-v  { border-radius:14px; border:1px solid var(--sp-admin-border,#ECE9F5); background:#fff; overflow:hidden; }

    .sp-adm-table-scroll  { overflow-x:auto; width:100%; }
    .sp-adm-table-scroll::-webkit-scrollbar { height:8px; }
    .sp-adm-table-scroll::-webkit-scrollbar-thumb { background:#d1d5db; border-radius:999px; }

    :host ::ng-deep table {
      width:100%;
      min-width:var(--sp-admin-table-min-width, 720px);
      border-collapse:collapse;
      border-spacing:0;
      table-layout:auto;
    }
    :host ::ng-deep thead tr { border-bottom:1px solid #ECE9F5; }
    /* .adm-table th — exact Admin.html spec */
    :host ::ng-deep th {
      padding:10px 16px;
      background:transparent;
      color:#8B85A0;
      font-size:11px;
      font-weight:800;
      line-height:1.4;
      text-align:left;
      white-space:nowrap;
      vertical-align:middle;
      letter-spacing:.07em;
      text-transform:uppercase;
      border-bottom:1px solid #ECE9F5;
      user-select:none;
    }
    /* .adm-table td — exact Admin.html spec */
    :host ::ng-deep td {
      padding:12px 16px;
      color:#4B4462;
      font-size:13.5px;
      line-height:1.5;
      vertical-align:middle;
      border-bottom:1px solid #ECE9F5;
      min-width:0;
    }
    /* .adm-table tr:last-child td: no border-bottom */
    :host ::ng-deep tbody tr:last-child td { border-bottom:0; }
    /* .adm-table tbody tr:hover */
    :host ::ng-deep tbody tr:hover td { background:#FAFAFE; }
    :host ::ng-deep .sp-admin-table-muted,
    :host ::ng-deep .sp-admin-muted { color:#667085; }
    :host ::ng-deep .sp-admin-mono,
    :host ::ng-deep .sp-admin-table-mono {
      font-family:ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
      font-size:12px;
      color:#475467;
      word-break:break-all;
    }
    :host ::ng-deep .sp-admin-cap { text-transform:capitalize; }
    :host ::ng-deep .sp-admin-wide-cell { min-width:240px; max-width:360px; }
    :host ::ng-deep .sp-admin-table-truncate,
    :host ::ng-deep .sp-admin-truncate {
      max-width:260px;
      overflow:hidden;
      text-overflow:ellipsis;
      white-space:nowrap;
    }
    :host ::ng-deep .sp-admin-table-wrap,
    :host ::ng-deep .sp-admin-wrap {
      max-width:420px;
      white-space:normal;
      overflow-wrap:anywhere;
    }
    :host ::ng-deep .sp-admin-table-num,
    :host ::ng-deep .sp-admin-num {
      text-align:right;
      font-variant-numeric:tabular-nums;
      white-space:nowrap;
    }
    :host ::ng-deep .sp-adm-actions-wrap {
      display:flex;
      justify-content:flex-end;
    }
    /* Shared table cell helpers */
    :host ::ng-deep .sp-admin-version  { font-size:13.5px; font-weight:700; }
    :host ::ng-deep .sp-admin-mono-cell { font-family:ui-monospace,SFMono-Regular,Menlo,monospace; font-size:12.5px; color:var(--sp-admin-text-muted,#64748B); }
    :host ::ng-deep .sp-admin-cell-text { font-size:13px; font-weight:600; color:var(--sp-admin-text,#0F172A); }
    :host ::ng-deep .sp-admin-table-empty {
      color:#98a2b3;
    }

    :host ::ng-deep .sp-adm-table-density-compact th { padding:8px 12px; }
    :host ::ng-deep .sp-adm-table-density-compact td { padding:8px 12px; font-size:12px; }
    :host ::ng-deep .sp-adm-table-density-spacious th { padding:14px 24px; }
    :host ::ng-deep .sp-adm-table-density-spacious td { padding:18px 24px; font-size:14px; }

    /* Thead */
    .sp-adm-thead-basic  { }
    .sp-adm-thead-data   { }
    .sp-adm-thead-sticky { position:sticky; top:0; z-index:2; }

    /* Toolbar row — filters (left) + tableActions slot + Bulk edit toggle (right), same row.
       Sits above the table (or projected content) whenever filters or bulkEditable are set. */
    .sp-adm-toolbar-row {
      display:flex; align-items:flex-end; justify-content:space-between;
      gap:12px; flex-wrap:wrap; padding:12px 16px;
      border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC);
    }
    .sp-adm-toolbar-filters { display:flex; gap:12px; flex-wrap:wrap; align-items:flex-end; }
    .sp-adm-toolbar-filter { display:flex; flex-direction:column; gap:4px; min-width:140px; }
    .sp-adm-toolbar-search { min-width:220px; }
    .sp-adm-toolbar-filter-label {
      font-size:11px; font-weight:700; color:var(--sp-admin-text-muted,#8B85A0);
      text-transform:uppercase; letter-spacing:.04em;
    }
    .sp-adm-toolbar-actions { display:flex; gap:8px; align-items:center; flex-shrink:0; flex-wrap:wrap; }

    /* Selection/bulk-action row — sits directly under the toolbar row (filters + Bulk edit),
       above the table. Own top/bottom margin keeps it clear of the toolbar's border-bottom line
       above and the table's first row below. Empty (0 height) when the page projects nothing. */
    .sp-adm-selection-row { padding:0 16px; margin:12px 0; }
    .sp-adm-selection-row:empty { display:none; margin:0; }

    .sp-adm-bulk-checkbox { width:15px; height:15px; cursor:pointer; accent-color:var(--sp-admin-primary,#5B4BE8); flex-shrink:0; }

    /* Title column — bold by default (data-driven mode via column.titleColumn; projection-mode
       pages apply this class directly to their own <td>). Opt out with column.nobold. */
    :host ::ng-deep .sp-admin-td-title { font-weight:700; color:var(--sp-admin-text,#211B36); }

    .sp-adm-thead-row-basic  { border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }
    .sp-adm-thead-row-data   { border-bottom:2px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-thead-row-simple { border-bottom:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-thead-row-bordered { border-bottom:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-thead-row-striped  { border-bottom:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-thead-row-card     { border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }

    .sp-adm-th        { text-align:left; font-size:11px; font-weight:700; color:var(--sp-admin-text-muted,#64748B); white-space:nowrap; letter-spacing:0.06em; text-transform:uppercase; }
    .sp-adm-th-basic  { background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-adm-th-data   { background:var(--sp-admin-surface-subtle,#FBFAFE); color:var(--sp-admin-text-secondary,#334155); }
    .sp-adm-th-simple { background:transparent; }
    .sp-adm-th-card   { background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-adm-th-bordered { background:var(--sp-admin-surface-subtle,#FBFAFE); border-right:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-th-striped  { background:var(--sp-admin-surface-subtle,#FBFAFE); }

    /* Density — th/td padding */
    /* Density — comfortable matches .adm-table baseline exactly */
    .sp-adm-th-compact    { padding:6px 12px; font-size:11px; }
    .sp-adm-th-comfortable{ padding:10px 16px; font-size:11px; }
    .sp-adm-th-spacious   { padding:14px 20px; font-size:11px; }
    .sp-adm-td-compact    { padding:8px 12px;  font-size:12.5px; }
    .sp-adm-td-comfortable{ padding:12px 16px; font-size:13.5px; }
    .sp-adm-td-spacious   { padding:16px 20px; font-size:14px; }

    /* td */
    .sp-adm-td        { color:var(--sp-admin-text-secondary,#334155); vertical-align:middle; border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }
    .sp-adm-td-bordered { border-right:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-table-muted { color:#94a3b8; font-size:11px; }

    /* Sortable */
    .sp-adm-th-sortable { cursor:pointer; user-select:none; }
    .sp-adm-th-sortable:hover { color:#1f2937; }
    .sp-adm-sort-icon { font-size:9px; opacity:.7; }

    /* Row states */
    .sp-adm-tr-hover { transition:background .1s; }
    .sp-adm-tr-hover:hover { background:var(--sp-admin-bg,#F6F4FB); }
    .sp-adm-tr-stripe-odd  { background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-adm-tr-stripe-even { background:#fff; }
    .sp-adm-tr-clickable { cursor:pointer; }
    .sp-adm-row-detail > td { padding:0; background:var(--sp-admin-surface-subtle,#FBFAFE); }

    /* Borders */
    .sp-adm-table-bordered th, .sp-adm-table-bordered td { border:1px solid var(--sp-admin-border,#ECE9F5); }

    /* Check column */
    .sp-adm-th-check, .sp-adm-td-check { width:40px; padding:0 12px; text-align:center; }

    /* Fixed layout — applies table-layout:fixed to any projected <table> */
    :host ::ng-deep .sp-adm-fixed-layout table { table-layout:fixed !important; }

    /* Shared column alignment helpers */
    :host ::ng-deep .sp-admin-th-right { text-align:right !important; }
    :host ::ng-deep .sp-admin-th-center { text-align:center !important; }
    :host ::ng-deep .sp-admin-td-right  { text-align:right !important; }
    :host ::ng-deep .sp-admin-td-center { text-align:center !important; }

    /* first-column-fluid layout:
       table-layout:auto (browser sizes columns naturally).
       First th/td gets a large min-width so it wins the spare space race.
       Other cells stay nowrap so they shrink to content.
       sp-admin-fluid-col marks an explicit fluid column (Part B). */
    :host ::ng-deep .sp-adm-fluid-layout table {
      table-layout: auto;
      width: 100%;
    }
    :host ::ng-deep .sp-adm-fluid-layout table th,
    :host ::ng-deep .sp-adm-fluid-layout table td {
      white-space: nowrap;
    }
    /* First column is greedy — takes all spare width */
    :host ::ng-deep .sp-adm-fluid-layout table th:first-child,
    :host ::ng-deep .sp-adm-fluid-layout table td:first-child {
      width: 9999px;
      white-space: normal;
    }
    /* Explicit fluid column marker (overrides first-child) */
    :host ::ng-deep .sp-adm-fluid-layout table th.sp-admin-fluid-col,
    :host ::ng-deep .sp-adm-fluid-layout table td.sp-admin-fluid-col {
      width: 9999px;
      white-space: normal;
    }
    :host ::ng-deep .sp-adm-fluid-layout table tr:has(th.sp-admin-fluid-col) th:first-child,
    :host ::ng-deep .sp-adm-fluid-layout table tr:has(td.sp-admin-fluid-col) td:first-child {
      width: auto;
    }
    /* Actions column stays right-aligned and compact */
    :host ::ng-deep .sp-adm-fluid-layout table .sp-admin-actions {
      text-align: right;
      white-space: nowrap;
    }
  `],
})
export class SpAdminTableComponent {
  @ContentChild('cell') cellTemplate?: TemplateRef<{ $implicit: unknown; col: SpAdminTableColumn }>;
  @ContentChild('rowDetail') rowDetailTemplate?: TemplateRef<{ $implicit: unknown; index: number }>;
  /** When set, rows for which this returns true render `rowDetail` in a full-width row underneath (data-driven mode only). */
  @Input() isRowExpanded?: (row: unknown, index: number) => boolean;

  @Input() columns: SpAdminTableColumn[] = [];
  /** Any row shape — a concrete DTO array is fine (no index signature needed). Access fields via
   * `$any(row).fieldName` in a #cell template; the default (no cellTemplate) renderer does the
   * same internally for `column.key`. */
  @Input() rows: unknown[] = [];
  @Input() loading = false;
  @Input() error = '';
  @Input() errorTitle = 'Could not load data';
  @Input() emptyMessage = 'No records found.';
  @Input() sortColumn = '';
  @Input() sortDirection: SortDirection = 'asc';
  @Input() hasActions = false;
  @Input() variant: SpAdminTableVariant = 'basic';
  @Input() density: SpAdminTableDensity = 'comfortable';
  @Input() hoverable = true;
  @Input() selectable = false;
  @Input() showHeader = true;
  @Input() stickyHeader = false;
  @Input() minWidth = '720px';
  @Input() flush = false;
  @Input() fixedLayout = false;
  @Input() layout: SpAdminTableLayout = 'auto';
  /** Column definitions for projection-mode tables (width + optional align). */
  @Input() colDefs: SpAdminColDef[] = [];
  /** Dropdown filters rendered in the toolbar row, left side, next to the Bulk edit toggle/actions. */
  @Input() filters: SpAdminTableFilter[] = [];
  @Output() filterChange = new EventEmitter<{ key: string; value: string }>();
  /** Shows a search input as the first item in the toolbar's filter row — the table owns search
   *  the same way it owns dropdown filters, so pages no longer need their own search box outside
   *  the table. Two-way bindable via `searchValue`/`searchChange`. */
  @Input() searchable = false;
  @Input() searchValue = '';
  @Input() searchPlaceholder = 'Search…';
  @Output() searchChange = new EventEmitter<string>();
  /** Shows a "Bulk edit" toggle button above the table (data-driven or projected). */
  @Input() bulkEditable = false;
  /**
   * Bulk-edit toggle state — two-way bindable. In data-driven [columns]/[rows] mode, a checkbox
   * is automatically merged into the titleColumn's header/cells while this is true. Projection-mode
   * pages should bind this and render their own checkbox next to their title cell to match.
   */
  @Input() bulkEditMode = false;
  @Output() bulkEditModeChange = new EventEmitter<boolean>();
  @Output() sortChange = new EventEmitter<SpAdminSortChange>();
  @Output() selectionChange = new EventEmitter<number[]>();
  /** Data-driven mode only. When true, rows get a pointer cursor and emit (rowClick) on click — for
   * "click a row to open its detail" pages. Interactive cell content (checkboxes, action menus) must
   * stopPropagation() itself to avoid also triggering the row click, same as the built-in checkboxes do. */
  @Input() rowClickable = false;
  @Output() rowClick = new EventEmitter<unknown>();
  /** Data-driven mode only. When true, every column without its own explicit `align` is
   *  right-aligned except the titleColumn (which always stays left) — the common "title on
   *  the left, everything else lines up on the right" data-table convention. A column's own
   *  `align` always wins over this default. */
  @Input() rightAlignNonTitleColumns = false;

  /** Optional built-in pagination footer — set `paginationTotalPages` to enable it. Renders
   *  inside the table's own outer shell (same border as the table), so pages no longer need to
   *  wrap [flush] table + sp-admin-table-footer in their own sp-admin-card just to visually group
   *  them. Leave unset and keep using a manual <sp-admin-table-footer> sibling for anything beyond
   *  the standard "Page X of Y · N items" + pager. */
  @Input() paginationPage?: number;
  @Input() paginationTotalPages?: number;
  @Input() paginationLabel?: string;
  @Output() paginationPageChange = new EventEmitter<number>();

  private selectedRows = new Set<number>();

  toggleBulkEdit(): void {
    this.bulkEditMode = !this.bulkEditMode;
    this.bulkEditModeChange.emit(this.bulkEditMode);
    if (!this.bulkEditMode) {
      this.selectedRows.clear();
      this.selectionChange.emit([]);
    }
  }

  onFilterValueChange(filter: SpAdminTableFilter, value: string): void {
    filter.value = value;
    this.filterChange.emit({ key: filter.key, value });
  }

  onSearchValueChange(value: string): void {
    this.searchValue = value;
    this.searchChange.emit(value);
  }

  /** Clears row selection without leaving bulk-edit mode — call via a template ref after a bulk action succeeds. */
  clearSelection(): void {
    this.selectedRows.clear();
    this.selectionChange.emit([]);
  }

  /** Imperatively sets row selection (by row index) — call via a template ref for actions like
   * "Select all publishable" that pick a specific subset rather than toggling one row/all rows. */
  setSelection(indices: number[]): void {
    this.selectedRows = new Set(indices);
    this.selectionChange.emit([...this.selectedRows]);
  }

  get outerClasses(): string {
    if (this.flush) return 'sp-adm-table-flush';
    const variantMap: Record<SpAdminTableVariant, string> = {
      basic: 'sp-adm-table-card',
      data: 'sp-adm-table-data',
      bordered: 'sp-adm-table-bordered-v',
      striped: 'sp-adm-table-striped-v',
      simple: 'sp-adm-table-simple',
      card: 'sp-adm-table-card-v',
    };
    return variantMap[this.variant] ?? 'sp-adm-table-card';
  }

  get scrollClass(): string {
    return `sp-adm-table-scroll sp-adm-table-density-${this.density}`;
  }

  get theadClass(): string {
    return `sp-adm-thead-${this.variant}`;
  }

  get theadRowClass(): string {
    return `sp-adm-thead-row-${this.variant}`;
  }

  get detailColspan(): number {
    return this.columns.length + (this.selectable ? 1 : 0) + (this.hasActions ? 1 : 0);
  }

  effectiveAlign(column: SpAdminTableColumn): 'left' | 'center' | 'right' {
    if (column.align) return column.align;
    if (this.rightAlignNonTitleColumns && !column.titleColumn) return 'right';
    return 'left';
  }

  thClass(column: SpAdminTableColumn | null): string {
    const cls = [
      'sp-adm-th',
      `sp-adm-th-${this.variant}`,
      `sp-adm-th-${this.density}`,
    ];
    if (column?.sortable) cls.push('sp-adm-th-sortable');
    return cls.join(' ');
  }

  tdClass(column: SpAdminTableColumn | null): string {
    const cls = ['sp-adm-td', `sp-adm-td-${this.density}`];
    if (column?.muted) cls.push('sp-adm-table-muted');
    if (column?.titleColumn && !column?.nobold) cls.push('sp-admin-td-title');
    if (this.variant === 'bordered') cls.push('sp-adm-td-bordered');
    return cls.join(' ');
  }

  trClass(index: number): string {
    const cls: string[] = [];
    if (this.hoverable) cls.push('sp-adm-tr-hover');
    if (this.variant === 'striped') {
      cls.push(index % 2 === 0 ? 'sp-adm-tr-stripe-even' : 'sp-adm-tr-stripe-odd');
    }
    return cls.join(' ');
  }

  get allSelected(): boolean {
    return this.rows.length > 0 && this.selectedRows.size === this.rows.length;
  }

  isSelected(index: number): boolean {
    return this.selectedRows.has(index);
  }

  onSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      this.rows.forEach((_, i) => this.selectedRows.add(i));
    } else {
      this.selectedRows.clear();
    }
    this.selectionChange.emit([...this.selectedRows]);
  }

  onSelectRow(index: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      this.selectedRows.add(index);
    } else {
      this.selectedRows.delete(index);
    }
    this.selectionChange.emit([...this.selectedRows]);
  }

  onSortClick(key: string): void {
    const direction: SortDirection =
      this.sortColumn === key && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortChange.emit({ column: key, direction });
  }

  sortIcon(key: string): string {
    if (this.sortColumn !== key) return '↕';
    return this.sortDirection === 'asc' ? '▲' : '▼';
  }

  sortAriaLabel(column: SpAdminTableColumn): string | null {
    if (!column.sortable) return null;
    if (this.sortColumn !== column.key) return 'none';
    return this.sortDirection === 'asc' ? 'ascending' : 'descending';
  }
}
