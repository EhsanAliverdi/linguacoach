import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type AlertVariant = 'error' | 'success' | 'info' | 'warning';

@Component({
  selector: 'sp-admin-alert',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-alert" [class]="'sp-alert-' + variant" role="alert">
      <ng-content />
    </div>
  `,
  styles: [`
    .sp-alert {
      border-radius: 12px;
      padding: 14px 16px;
      font-size: 13px;
      font-weight: 500;
      line-height: 1.5;
      margin-bottom: 12px;
      border-left-width: 3px;
      border-left-style: solid;
    }
    .sp-alert-error   {
      background: var(--sp-admin-danger-bg, #fef2f2);
      color: var(--sp-admin-danger, #dc2626);
      border-left-color: var(--sp-admin-danger, #dc2626);
    }
    .sp-alert-success {
      background: var(--sp-admin-green-bg, #ecfdf3);
      color: var(--sp-admin-green, #16a34a);
      border-left-color: var(--sp-admin-green, #16a34a);
    }
    .sp-alert-info    {
      background: var(--sp-admin-primary-bg, #EDEBFF);
      color: var(--sp-admin-primary, #5B4BE8);
      border-left-color: var(--sp-admin-primary, #5B4BE8);
    }
    .sp-alert-warning {
      background: var(--sp-admin-amber-bg, #fffbeb);
      color: var(--sp-admin-amber, #d97706);
      border-left-color: var(--sp-admin-amber, #d97706);
    }
  `],
})
export class SpAdminAlertComponent {
  @Input() variant: AlertVariant = 'info';
}
