import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'sp-admin-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <!--
      TailAdmin input pattern (shared/components/form/input):
      h-11 rounded-lg border border-gray-200 bg-transparent py-2.5 pl-4 pr-4
      text-sm text-gray-800 placeholder:text-gray-400
      focus:border-brand-300 focus:outline-hidden focus:ring-3 focus:ring-brand-500/10
      dark:border-gray-800 dark:bg-gray-900 dark:text-white/90
    -->
    <input
      class="sp-adm-input h-11 w-full rounded-lg border border-gray-200 bg-transparent py-2.5 px-4 text-sm text-gray-800 shadow-sm placeholder:text-gray-400 focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-blue-100 dark:border-gray-800 dark:bg-gray-900 dark:text-white/90 dark:placeholder:text-white/30"
      [attr.type]="type"
      [placeholder]="placeholder"
      [disabled]="disabled"
      [(ngModel)]="value"
    />
  `,
  styles: [`
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 input pattern */
    .sp-adm-input { box-sizing: border-box; }
    .sp-adm-input:disabled { opacity: 0.55; cursor: not-allowed; background: #f9fafb; }
  `],
})
export class SpAdminInputComponent {
  @Input() type = 'text';
  @Input() placeholder = '';
  @Input() disabled = false;
  @Input() value = '';
}
