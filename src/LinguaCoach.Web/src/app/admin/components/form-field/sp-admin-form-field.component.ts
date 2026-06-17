import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-form-field',
  standalone: true,
  imports: [CommonModule],
  template: `
    <label class="sp-adm-field">
      <span class="sp-adm-field-label">{{ label }}</span>
      <ng-content />
      @if (hint) {
        <span class="sp-adm-field-hint">{{ hint }}</span>
      }
      @if (error) {
        <span class="sp-adm-field-error">{{ error }}</span>
      }
    </label>
  `,
  styles: [`
    .sp-adm-field { display: flex; flex-direction: column; gap: 6px; }
    .sp-adm-field-label { color: var(--sp-admin-text-secondary); font-size: 12px; font-weight: 800; }
    .sp-adm-field-hint { color: var(--sp-admin-text-dim); font-size: 11.5px; }
    .sp-adm-field-error { color: var(--sp-admin-danger); font-size: 12px; font-weight: 700; }
  `],
})
export class SpAdminFormFieldComponent {
  @Input() label = '';
  @Input() hint = '';
  @Input() error = '';
}
