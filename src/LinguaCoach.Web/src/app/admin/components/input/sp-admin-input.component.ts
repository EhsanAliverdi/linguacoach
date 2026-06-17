import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'sp-admin-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <input
      class="sp-adm-input"
      [attr.type]="type"
      [placeholder]="placeholder"
      [disabled]="disabled"
      [(ngModel)]="value"
    />
  `,
  styles: [`
    .sp-adm-input {
      width: 100%;
      min-height: 38px;
      border: 1.5px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-sm);
      background: var(--sp-admin-surface);
      color: var(--sp-admin-text);
      padding: 8px 10px;
      font: inherit;
      font-size: 13px;
      box-sizing: border-box;
    }
    .sp-adm-input:focus { outline: 3px solid var(--sp-admin-primary-focus); border-color: var(--sp-admin-primary); }
    .sp-adm-input:disabled { background: var(--sp-admin-surface-subtle); color: var(--sp-admin-text-dim); }
  `],
})
export class SpAdminInputComponent {
  @Input() type = 'text';
  @Input() placeholder = '';
  @Input() disabled = false;
  @Input() value = '';
}
