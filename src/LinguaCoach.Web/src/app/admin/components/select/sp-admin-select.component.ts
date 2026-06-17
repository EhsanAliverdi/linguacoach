import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SpAdminSelectOption {
  value: string;
  label: string;
}

@Component({
  selector: 'sp-admin-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <select class="sp-adm-select" [disabled]="disabled" [(ngModel)]="value">
      @for (option of options; track option.value) {
        <option [value]="option.value">{{ option.label }}</option>
      }
    </select>
  `,
  styles: [`
    .sp-adm-select {
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
    .sp-adm-select:focus { outline: 3px solid var(--sp-admin-primary-focus); border-color: var(--sp-admin-primary); }
  `],
})
export class SpAdminSelectComponent {
  @Input() options: SpAdminSelectOption[] = [];
  @Input() value = '';
  @Input() disabled = false;
}
