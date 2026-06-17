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
      border-radius: var(--sp-admin-radius-sm);
      padding: 10px 14px;
      font-size: 13px;
      font-weight: 500;
      margin-bottom: 12px;
    }
    .sp-alert-error   { background: var(--sp-admin-danger-bg); color: var(--sp-admin-danger); }
    .sp-alert-success { background: var(--sp-admin-green-bg);  color: var(--sp-admin-green); }
    .sp-alert-info    { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-alert-warning { background: var(--sp-admin-amber-bg);  color: var(--sp-admin-amber); }
  `],
})
export class SpAdminAlertComponent {
  @Input() variant: AlertVariant = 'info';
}
