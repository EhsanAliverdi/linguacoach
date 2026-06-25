import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Table footer row: summary text on the left, pagination/actions on the right.
 *
 * Slots:
 *   (default)       — left side (count/summary text)
 *   [slot=actions]  — right side (pagination, export, etc.)
 */
@Component({
  selector: 'sp-admin-table-footer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-tf-root">
      <div class="sp-adm-tf-left">
        <ng-content />
      </div>
      <div class="sp-adm-tf-right">
        <ng-content select="[slot=actions]" />
      </div>
    </div>
  `,
  styles: [`
    .sp-adm-tf-root {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-top: 8px;
      padding: 12px 20px;
      border-top: 1px solid var(--sp-admin-border, #ECE9F5);
      flex-wrap: wrap;
    }
    .sp-adm-tf-left {
      font-size: 13px;
      color: var(--sp-admin-text-muted, #8B85A0);
      font-weight: 600;
    }
    .sp-adm-tf-right {
      display: flex;
      align-items: center;
      gap: 8px;
    }
  `],
})
export class SpAdminTableFooterComponent {}
