import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';

export interface SpAdminTableColumn {
  key: string;
  label: string;
  muted?: boolean;
}

@Component({
  selector: 'sp-admin-table',
  standalone: true,
  imports: [CommonModule, SpAdminEmptyStateComponent, SpAdminLoadingStateComponent],
  template: `
    <div class="sp-adm-table-card">
      @if (loading) {
        <sp-admin-loading-state message="Loading records" />
      } @else if (columns.length === 0) {
        <div class="sp-adm-table-scroll">
          <ng-content />
        </div>
      } @else if (!rows.length) {
        <sp-admin-empty-state [message]="emptyMessage" />
      } @else {
        <div class="sp-adm-table-scroll">
          <table class="sp-adm-table">
            <thead>
              <tr>
                @for (column of columns; track column.key) {
                  <th scope="col">{{ column.label }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows; track $index) {
                <tr>
                  @for (column of columns; track column.key) {
                    <td [class.sp-adm-table-muted]="column.muted">{{ row[column.key] }}</td>
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
    .sp-adm-table-card {
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-md);
      overflow: hidden;
    }
    .sp-adm-table-scroll { overflow-x: auto; }
    :host ::ng-deep table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    :host ::ng-deep th {
      text-align: left;
      padding: 10px 14px;
      background: var(--sp-admin-surface-subtle);
      border-bottom: 1px solid var(--sp-admin-border);
      color: var(--sp-admin-text-dim);
      font-size: 11px;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: .05em;
      white-space: nowrap;
    }
    :host ::ng-deep td {
      padding: 10px 14px;
      border-bottom: 1px solid var(--sp-admin-border-subtle);
      color: var(--sp-admin-text-secondary);
      vertical-align: middle;
    }
    :host ::ng-deep tr:last-child td { border-bottom: none; }
    .sp-adm-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .sp-adm-table th {
      text-align: left;
      padding: 10px 14px;
      background: var(--sp-admin-surface-subtle);
      border-bottom: 1px solid var(--sp-admin-border);
      color: var(--sp-admin-text-dim);
      font-size: 11px;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: .05em;
      white-space: nowrap;
    }
    .sp-adm-table td {
      padding: 10px 14px;
      border-bottom: 1px solid var(--sp-admin-border-subtle);
      color: var(--sp-admin-text-secondary);
      vertical-align: middle;
    }
    .sp-adm-table tr:last-child td { border-bottom: none; }
    .sp-adm-table-muted { color: var(--sp-admin-text-dim); font-size: 12px; }
  `],
})
export class SpAdminTableComponent {
  @Input() columns: SpAdminTableColumn[] = [];
  @Input() rows: Record<string, unknown>[] = [];
  @Input() loading = false;
  @Input() emptyMessage = 'No records found.';
}
