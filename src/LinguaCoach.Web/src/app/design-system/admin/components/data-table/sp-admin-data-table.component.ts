import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminSpinnerComponent } from '../spinner/sp-admin-spinner.component';
import { SpAdminEmptyStateComponent } from '../empty-state/sp-admin-empty-state.component';

@Component({
  selector: 'sp-admin-data-table',
  standalone: true,
  imports: [CommonModule, SpAdminSpinnerComponent, SpAdminEmptyStateComponent],
  template: `
    <div class="sp-dt-card">
      @if (loading) {
        <div class="sp-dt-state"><sp-admin-spinner /></div>
      } @else if (!rows || rows.length === 0) {
        <sp-admin-empty-state [message]="emptyMessage" />
      } @else {
        <div class="sp-dt-scroll">
          <table class="sp-dt-table">
            <thead>
              <tr>
                @for (col of columns; track col.key) {
                  <th>{{ col.label }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows; track $index) {
                <tr>
                  @for (col of columns; track col.key) {
                    <td [class.sp-dt-muted]="col.muted">{{ row[col.key] }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
        <ng-content />
      }
    </div>
  `,
  styles: [`
    .sp-dt-card {
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-md);
      overflow: hidden;
    }
    .sp-dt-state { padding: 28px; text-align: center; }
    .sp-dt-scroll { overflow-x: auto; }
    .sp-dt-table {
      width: 100%;
      font-size: 13px;
      border-collapse: collapse;
    }
    .sp-dt-table th {
      text-align: left;
      padding: 10px 14px;
      font-size: 11px;
      font-weight: 800;
      color: var(--sp-admin-text-muted);
      text-transform: uppercase;
      letter-spacing: .08em;
      background: var(--sp-admin-surface-subtle);
      border-bottom: 1px solid var(--sp-admin-border);
    }
    .sp-dt-table td {
      padding: 10px 14px;
      border-bottom: 1px solid var(--sp-admin-border-subtle);
      color: var(--sp-admin-text-secondary);
    }
    .sp-dt-table tr:last-child td { border-bottom: none; }
    .sp-dt-table tr:hover td { background: var(--sp-admin-surface-subtle); }
    .sp-dt-muted { color: var(--sp-admin-text-dim); font-size: 12px; }
  `],
})
export class SpAdminDataTableComponent {
  @Input() columns: { key: string; label: string; muted?: boolean }[] = [];
  @Input() rows: Record<string, unknown>[] = [];
  @Input() loading = false;
  @Input() emptyMessage = 'No records found.';
}
