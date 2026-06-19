import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-error-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-error" role="alert">
      <strong>{{ title }}</strong>
      <span>{{ message }}</span>
    </div>
  `,
  styles: [`
    .sp-adm-error {
      display: flex;
      flex-direction: column;
      gap: 4px;
      border-radius: 10px;
      border: 1px solid #fecaca;
      border-left: 3px solid #ef4444;
      background: var(--sp-admin-danger-bg, #fef2f2);
      color: #991b1b;
      padding: 14px 16px;
      font-size: 13px;
      line-height: 1.5;
    }
    .sp-adm-error strong { font-weight: 600; color: #dc2626; }
  `],
})
export class SpAdminErrorStateComponent {
  @Input() title = 'Something went wrong';
  @Input() message = 'Try again or contact support.';
}
