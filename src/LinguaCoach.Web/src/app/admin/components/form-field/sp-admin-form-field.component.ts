import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminFormFieldLayout = 'vertical' | 'horizontal' | 'inline';
export type SpAdminFormFieldSize = 'sm' | 'md' | 'lg';

// TailAdmin form label (shared/components/form/label):
// block text-sm font-medium text-gray-700 mb-1.5
// hint: text-xs text-gray-400 mt-1   error: text-xs text-error-500 mt-1
@Component({
  selector: 'sp-admin-form-field',
  standalone: true,
  imports: [CommonModule],
  template: `
    <label class="sp-adm-field" [class]="hostClasses">
      @if (label) {
        <span class="sp-adm-field-label font-medium text-gray-700 dark:text-gray-400" [class]="labelSizeClass">
          {{ label }}
          @if (required) {
            <span class="sp-adm-field-required text-red-500 ml-0.5" aria-hidden="true">*</span>
          }
        </span>
      }
      <ng-content />
      @if (hint && !error) {
        <span class="sp-adm-field-hint text-gray-400 dark:text-gray-500 mt-0.5" [class]="hintSizeClass">{{ hint }}</span>
      }
      @if (error) {
        <span class="sp-adm-field-error text-red-500 mt-0.5" [class]="hintSizeClass" role="alert">{{ error }}</span>
      }
    </label>
  `,
  styles: [`
    /* TailAdmin-backed: block text-sm font-medium text-gray-700 label pattern */

    /* Vertical (default): label above, hint/error below */
    .sp-adm-field-vertical { display:flex; flex-direction:column; gap:6px; min-width:0; }

    /* Horizontal: label left, control right */
    .sp-adm-field-horizontal { display:grid; grid-template-columns:160px minmax(0,1fr); align-items:start; gap:12px; min-width:0; }
    .sp-adm-field-horizontal .sp-adm-field-label { padding-top:10px; }
    .sp-adm-field-horizontal .sp-adm-field-hint,
    .sp-adm-field-horizontal .sp-adm-field-error  { grid-column:2; margin-top:2px; }

    /* Inline: label + control on same line */
    .sp-adm-field-inline { display:flex; align-items:center; gap:8px; flex-wrap:wrap; min-width:0; }
    .sp-adm-field-inline .sp-adm-field-hint,
    .sp-adm-field-inline .sp-adm-field-error { width:100%; margin-top:2px; }

    /* Label sizes */
    .sp-adm-field-label-sm { font-size:11px; }
    .sp-adm-field-label-md { font-size:13px; }
    .sp-adm-field-label-lg { font-size:15px; }

    /* Hint/error sizes */
    .sp-adm-field-hint-sm { font-size:10px; }
    .sp-adm-field-hint-md { font-size:11px; }
    .sp-adm-field-hint-lg { font-size:12px; }
  `],
})
export class SpAdminFormFieldComponent {
  @Input() label = '';
  @Input() hint = '';
  @Input() error = '';
  @Input() required = false;
  @Input() layout: SpAdminFormFieldLayout = 'vertical';
  @Input() size: SpAdminFormFieldSize = 'md';

  get hostClasses(): string {
    return `sp-adm-field-${this.layout}`;
  }

  get labelSizeClass(): string {
    return `sp-adm-field-label-${this.size}`;
  }

  get hintSizeClass(): string {
    return `sp-adm-field-hint-${this.size}`;
  }
}
