import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';

export interface SpAdminTableColumn {
  key: string;
  label: string;
  muted?: boolean;
  sortable?: boolean;
}

export type SortDirection = 'asc' | 'desc';

export interface SpAdminSortChange {
  column: string;
  direction: SortDirection;
}

/**
 * TailAdmin table wrapper with sortable column support.
 * Source pattern: shared/components/ui/table basic-table-one
 * Outer:  rounded-2xl border border-gray-200 bg-white overflow-hidden
 * th:     px-5 py-3 text-xs font-medium text-gray-500 bg-gray-50 border-b border-gray-100 text-left
 * td:     px-5 py-4 text-sm text-gray-700 border-b border-gray-100
 *
 * Sortable columns: emit (sortChange) with { column, direction }.
 * Consumer is responsible for re-sorting rows — this component does not sort internally.
 * An optional "actions" column is appended when hasActions=true; use sp-admin-table-actions
 * inside a projected <ng-template #rowActions let-row> (not yet — row actions live in pages for now).
 */
@Component({
  selector: 'sp-admin-table',
  standalone: true,
  imports: [CommonModule, SpAdminEmptyStateComponent, SpAdminLoadingStateComponent],
  template: `
    <!--
      TailAdmin table pattern (shared/components/ui/table/basic-table-one):
      Outer: rounded-2xl border border-gray-200 bg-white overflow-hidden
      thead th: px-5 py-3 text-xs font-medium text-gray-500 bg-gray-50 border-b border-gray-100 text-left
      tbody td: px-5 py-4 text-sm text-gray-700 border-b border-gray-100 last:border-0
    -->
    <div class="sp-adm-table-card rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
      @if (loading) {
        <sp-admin-loading-state message="Loading records" />
      } @else if (columns.length === 0) {
        <div class="sp-adm-table-scroll overflow-x-auto">
          <ng-content />
        </div>
      } @else if (!rows.length) {
        <sp-admin-empty-state [message]="emptyMessage" />
      } @else {
        <div class="sp-adm-table-scroll overflow-x-auto">
          <table class="sp-adm-table w-full border-collapse">
            <thead>
              <tr class="border-b border-gray-100 dark:border-gray-800">
                @for (column of columns; track column.key) {
                  <th
                    scope="col"
                    class="px-5 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-white/[0.03] whitespace-nowrap"
                    [class.sp-adm-th-sortable]="column.sortable"
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
                  <th scope="col" class="px-5 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-white/[0.03] whitespace-nowrap">Actions</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows; track $index) {
                <tr class="border-b border-gray-100 dark:border-gray-800 last:border-0 hover:bg-gray-50 dark:hover:bg-white/[0.02] transition-colors">
                  @for (column of columns; track column.key) {
                    <td
                      class="px-5 py-4 text-sm text-gray-700 dark:text-gray-300 align-middle"
                      [class.sp-adm-table-muted]="column.muted"
                    >{{ row[column.key] }}</td>
                  }
                  @if (hasActions) {
                    <td class="px-5 py-4 text-sm align-middle">
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
    /* TailAdmin-backed: rounded-2xl border border-gray-200 bg-white table pattern */
    .sp-adm-table-muted { color: #94a3b8; font-size: 12px; }
    .sp-adm-th-sortable { cursor: pointer; user-select: none; }
    .sp-adm-th-sortable:hover { color: #334155; }
    .sp-adm-sort-icon { font-size: 10px; opacity: 0.7; }
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
  @Output() sortChange = new EventEmitter<SpAdminSortChange>();

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
