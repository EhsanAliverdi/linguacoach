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
      gap: 3px;
      border-radius: var(--sp-admin-radius-sm);
      border: 1px solid #FECACA;
      background: var(--sp-admin-danger-bg);
      color: #991B1B;
      padding: 12px 14px;
      font-size: 13px;
    }
    .sp-adm-error strong { font-weight: 800; }
  `],
})
export class SpAdminErrorStateComponent {
  @Input() title = 'Something went wrong';
  @Input() message = 'Try again or contact support.';
}
