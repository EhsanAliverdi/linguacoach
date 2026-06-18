import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';

export interface SpAdminTableColumn {
  key: string;
  label: string;
  muted?: boolean;
  sortable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
}

export type SortDirection = 'asc' | 'desc';
export interface SpAdminSortChange { column: string; direction: SortDirection; }
export type SpAdminTableVariant = 'basic' | 'data' | 'bordered' | 'striped' | 'simple' | 'card';
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
  imports: [CommonModule, SpAdminEmptyStateComponent, SpAdminLoadingStateComponent],
  template: `
    <div [class]="outerClasses">
      @if (loading) {
        <sp-admin-loading-state message="Loading records" />
      } @else if (columns.length === 0) {
        <div [class]="scrollClass">
          <ng-content />
        </div>
      } @else if (!rows.length) {
        <sp-admin-empty-state [message]="emptyMessage" />
      } @else {
        <div [class]="scrollClass">
          <table class="sp-adm-table w-full border-collapse" [class.sp-adm-table-bordered]="variant === 'bordered'">
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
                      [attr.aria-sort]="sortAriaLabel(column)"
                      (click)="column.sortable && onSortClick(column.key)"
                      (keydown.enter)="column.sortable && onSortClick(column.key)"
                      [attr.tabindex]="column.sortable ? 0 : null"
                      [attr.role]="column.sortable ? 'button' : null"
                    >
                      <span class="inline-flex items-center gap-1 select-none">
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
                <tr [class]="trClass($index)">
                  @if (selectable) {
                    <td class="sp-adm-td sp-adm-td-check">
                      <input type="checkbox" [checked]="isSelected($index)" (change)="onSelectRow($index, $event)" [attr.aria-label]="'Select row ' + ($index + 1)" />
                    </td>
                  }
                  @for (column of columns; track column.key) {
                    <td
                      [class]="tdClass(column)"
                      [style.text-align]="column.align || 'left'"
                    >{{ row[column.key] }}</td>
                  }
                  @if (hasActions) {
                    <td [class]="tdClass(null)">
                      <ng-content select="[rowActions]" />
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    /* TailAdmin basic-table-one: rounded-2xl border border-gray-200 bg-white */
    .sp-adm-table-card    { border-radius:16px; border:1px solid #e5e7eb; background:#fff; overflow:hidden; }
    .sp-adm-table-card.dark\\:border-gray-800 { border-color:#1f2937; }
    .sp-adm-table-data    { border-radius:16px; border:1px solid #e5e7eb; background:#fff; overflow:hidden; }
    .sp-adm-table-simple  { background:transparent; }
    .sp-adm-table-card-v  { border-radius:16px; border:1px solid #e5e7eb; background:#fff; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,.08); }
    .sp-adm-table-bordered-v { border-radius:16px; border:1px solid #e5e7eb; background:#fff; overflow:hidden; }
    .sp-adm-table-striped-v  { border-radius:16px; border:1px solid #e5e7eb; background:#fff; overflow:hidden; }

    .sp-adm-table-scroll  { overflow-x:auto; }

    /* Thead */
    .sp-adm-thead-basic  { }
    .sp-adm-thead-data   { }
    .sp-adm-thead-sticky { position:sticky; top:0; z-index:2; }

    .sp-adm-thead-row-basic  { border-bottom:1px solid #f1f5f9; }
    .sp-adm-thead-row-data   { border-bottom:2px solid #e5e7eb; }
    .sp-adm-thead-row-simple { border-bottom:1px solid #e5e7eb; }
    .sp-adm-thead-row-bordered { border-bottom:1px solid #e5e7eb; }
    .sp-adm-thead-row-striped  { border-bottom:1px solid #e5e7eb; }
    .sp-adm-thead-row-card     { border-bottom:1px solid #f1f5f9; }

    /* th — TailAdmin: px-5 py-3 text-xs font-medium text-gray-500 bg-gray-50 */
    .sp-adm-th        { text-align:left; font-size:11px; font-weight:600; color:#6b7280; white-space:nowrap; }
    .sp-adm-th-basic  { background:#f9fafb; }
    .sp-adm-th-data   { background:#f3f4f6; color:#374151; }
    .sp-adm-th-simple { background:transparent; }
    .sp-adm-th-card   { background:#f9fafb; }
    .sp-adm-th-bordered { background:#f9fafb; border-right:1px solid #e5e7eb; }
    .sp-adm-th-striped  { background:#f9fafb; }

    /* Density — th/td padding */
    .sp-adm-th-compact    { padding:6px 12px; }
    .sp-adm-th-comfortable{ padding:10px 20px; }
    .sp-adm-th-spacious   { padding:14px 24px; }
    .sp-adm-td-compact    { padding:6px 12px; font-size:12px; }
    .sp-adm-td-comfortable{ padding:14px 20px; font-size:13px; }
    .sp-adm-td-spacious   { padding:18px 24px; font-size:14px; }

    /* td */
    .sp-adm-td        { color:#374151; vertical-align:middle; border-bottom:1px solid #f1f5f9; }
    .sp-adm-td-bordered { border-right:1px solid #e5e7eb; }
    .sp-adm-table-muted { color:#94a3b8; font-size:11px; }

    /* Sortable */
    .sp-adm-th-sortable { cursor:pointer; user-select:none; }
    .sp-adm-th-sortable:hover { color:#1f2937; }
    .sp-adm-sort-icon { font-size:9px; opacity:.7; }

    /* Row states */
    .sp-adm-tr-hover { transition:background .1s; }
    .sp-adm-tr-hover:hover { background:#f9fafb; }
    .sp-adm-tr-stripe-odd  { background:#f9fafb; }
    .sp-adm-tr-stripe-even { background:#fff; }

    /* Borders */
    .sp-adm-table-bordered th, .sp-adm-table-bordered td { border:1px solid #e5e7eb; }

    /* Check column */
    .sp-adm-th-check, .sp-adm-td-check { width:40px; padding:0 12px; text-align:center; }
  `],
})
export class SpAdminTableComponent {
  @Input() columns: SpAdminTableColumn[] = [];
  @Input() rows: Record<string, unknown>[] = [];
  @Input() loading = false;
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
  @Output() sortChange = new EventEmitter<SpAdminSortChange>();
  @Output() selectionChange = new EventEmitter<number[]>();

  private selectedRows = new Set<number>();

  get outerClasses(): string {
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
    return 'sp-adm-table-scroll overflow-x-auto';
  }

  get theadClass(): string {
    return `sp-adm-thead-${this.variant}`;
  }

  get theadRowClass(): string {
    return `sp-adm-thead-row-${this.variant}`;
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
