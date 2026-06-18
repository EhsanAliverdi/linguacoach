import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * TailAdmin-backed text input wrapper with ControlValueAccessor support.
 *
 * Supports both template-driven (`[(ngModel)]`) and reactive (`formControlName`)
 * forms. Disabled state propagates from Angular forms via `setDisabledState`.
 * Touched state is marked on blur. The legacy `[value]` input remains for
 * one-way display use but form binding is the supported path.
 */
@Component({
  selector: 'sp-admin-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminInputComponent),
      multi: true,
    },
  ],
  template: `
    <!--
      TailAdmin input pattern (shared/components/form/input):
      h-11 rounded-lg border border-gray-200 bg-transparent py-2.5 px-4
      text-sm text-gray-800 placeholder:text-gray-400
      focus:border-brand-300 focus:outline-hidden focus:ring-3 focus:ring-brand-500/10
    -->
    <input
      class="sp-adm-input h-11 w-full rounded-lg border border-gray-200 bg-transparent py-2.5 px-4 text-sm text-gray-800 shadow-sm placeholder:text-gray-400 focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-blue-100 dark:border-gray-800 dark:bg-gray-900 dark:text-white/90 dark:placeholder:text-white/30"
      [class.sp-adm-input-error]="invalid"
      [type]="type"
      [placeholder]="placeholder"
      [disabled]="disabled"
      [readOnly]="readonly"
      [attr.autocomplete]="autocomplete || null"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="invalid ? 'true' : null"
      [value]="value"
      (input)="onInput($event)"
      (blur)="onBlur()"
    />
  `,
  styles: [`
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 input pattern */
    .sp-adm-input { box-sizing: border-box; }
    .sp-adm-input:disabled { opacity: 0.55; cursor: not-allowed; background: #f9fafb; }
    .sp-adm-input-error { border-color: #ef4444; }
    .sp-adm-input-error:focus { box-shadow: 0 0 0 2px rgba(239, 68, 68, 0.15); }
  `],
})
export class SpAdminInputComponent implements ControlValueAccessor {
  @Input() type = 'text';
  @Input() placeholder = '';
  @Input() autocomplete = '';
  @Input() readonly = false;
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

  onInput(event: Event): void {
    this.value = (event.target as HTMLInputElement).value;
    this.onChange(this.value);
  }

  onBlur(): void {
    this.onTouched();
  }
}
