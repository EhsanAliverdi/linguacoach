import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SpAdminSelectOption {
  value: string;
  label: string;
}

/**
 * TailAdmin-backed select wrapper with ControlValueAccessor support.
 *
 * Supports both template-driven (`[(ngModel)]`) and reactive (`formControlName`)
 * forms. Options come from the `[options]` input or projected `<option>` content.
 * A `placeholder` renders a disabled default option. Disabled state propagates
 * from Angular forms. Touched state is marked on blur.
 */
@Component({
  selector: 'sp-admin-select',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminSelectComponent),
      multi: true,
    },
  ],
  template: `
    <!--
      TailAdmin select pattern (shared/components/form/select):
      h-11 rounded-lg border border-gray-200 bg-transparent px-4 py-2.5
      text-sm text-gray-800 focus:border-brand-300 focus:ring-2 focus:ring-blue-100
    -->
    <select
      class="sp-adm-select h-11 w-full rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm text-gray-800 shadow-sm focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-blue-100 dark:border-gray-800 dark:bg-gray-900 dark:text-white/90"
      [class.sp-adm-select-error]="invalid"
      [disabled]="disabled"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="invalid ? 'true' : null"
      [value]="value"
      (change)="onSelect($event)"
      (blur)="onBlur()"
    >
      @if (placeholder) {
        <option value="" disabled [selected]="!value">{{ placeholder }}</option>
      }
      @for (option of options; track option.value) {
        <option [value]="option.value">{{ option.label }}</option>
      }
      <ng-content />
    </select>
  `,
  styles: [`
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 select pattern */
    .sp-adm-select { box-sizing: border-box; }
    .sp-adm-select:disabled { opacity: 0.55; cursor: not-allowed; background: #f9fafb; }
    .sp-adm-select-error { border-color: #ef4444; }
  `],
})
export class SpAdminSelectComponent implements ControlValueAccessor {
  @Input() options: SpAdminSelectOption[] = [];
  @Input() placeholder = '';
  @Input() required = false;
  @Input() invalid = false;

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(value: boolean) { this._disabled = value; }

  value = '';

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string): void {
    this.value = value ?? '';
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this._disabled = isDisabled;
  }

  onSelect(event: Event): void {
    this.value = (event.target as HTMLSelectElement).value;
    this.onChange(this.value);
  }

  onBlur(): void {
    this.onTouched();
  }
}
