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
    <!--
      TailAdmin select pattern (shared/components/form/select):
      h-11 rounded-lg border border-gray-200 bg-transparent px-4 py-2.5
      text-sm text-gray-800 focus:border-brand-300 focus:ring-2 focus:ring-blue-100
    -->
    <select
      class="sp-adm-select h-11 w-full rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm text-gray-800 shadow-sm focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-blue-100 dark:border-gray-800 dark:bg-gray-900 dark:text-white/90"
      [disabled]="disabled"
      [(ngModel)]="value"
    >
      @for (option of options; track option.value) {
        <option [value]="option.value">{{ option.label }}</option>
      }
    </select>
  `,
  styles: [`
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 select pattern */
    .sp-adm-select { box-sizing: border-box; }
    .sp-adm-select:disabled { opacity: 0.55; cursor: not-allowed; background: #f9fafb; }
  `],
})
export class SpAdminSelectComponent {
  @Input() options: SpAdminSelectOption[] = [];
  @Input() value = '';
  @Input() disabled = false;
}
