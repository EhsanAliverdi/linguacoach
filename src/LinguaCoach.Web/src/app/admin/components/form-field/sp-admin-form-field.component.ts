import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-form-field',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!--
      TailAdmin form label pattern (shared/components/form/label):
      block text-sm font-medium text-gray-700 mb-1.5
      hint: text-xs text-gray-400 mt-1
      error: text-xs text-error-500 mt-1
    -->
    <label class="sp-adm-field flex flex-col gap-1.5">
      @if (label) {
        <span class="sp-adm-field-label block text-sm font-medium text-gray-700 dark:text-gray-400">{{ label }}</span>
      }
      <ng-content />
      @if (hint && !error) {
        <span class="sp-adm-field-hint text-xs text-gray-400 dark:text-gray-500 mt-0.5">{{ hint }}</span>
      }
      @if (error) {
        <span class="sp-adm-field-error text-xs text-red-500 mt-0.5">{{ error }}</span>
      }
    </label>
  `,
  styles: [`/* TailAdmin-backed: block text-sm font-medium text-gray-700 label pattern */`],
})
export class SpAdminFormFieldComponent {
  @Input() label = '';
  @Input() hint = '';
  @Input() error = '';
}
