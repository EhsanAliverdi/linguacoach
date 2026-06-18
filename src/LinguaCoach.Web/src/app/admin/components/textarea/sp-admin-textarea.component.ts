import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * TailAdmin-backed textarea wrapper with ControlValueAccessor support.
 *
 * Supports both template-driven (`[(ngModel)]`) and reactive (`formControlName`)
 * forms. Disabled state propagates from Angular forms. Touched state is marked
 * on blur. `rows` and `placeholder` control the textarea shape.
 */
@Component({
  selector: 'sp-admin-textarea',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminTextareaComponent),
      multi: true,
    },
  ],
  template: `
    <!--
      TailAdmin textarea pattern (shared/components/form/input/text-area):
      rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm
      focus:border-brand-300 focus:ring-2 focus:ring-blue-100
    -->
    <textarea
      class="sp-adm-textarea w-full rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm text-gray-800 shadow-sm placeholder:text-gray-400 focus:border-brand-300 focus:outline-none focus:ring-2 focus:ring-blue-100 dark:border-gray-800 dark:bg-gray-900 dark:text-white/90 dark:placeholder:text-white/30"
      [class.sp-adm-textarea-error]="invalid"
      [rows]="rows"
      [placeholder]="placeholder"
      [disabled]="disabled"
      [readOnly]="readonly"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="invalid ? 'true' : null"
      [value]="value"
      (input)="onInput($event)"
      (blur)="onBlur()"
    ></textarea>
  `,
  styles: [`
    /* TailAdmin-backed: rounded-lg border border-gray-200 textarea pattern */
    .sp-adm-textarea { box-sizing: border-box; resize: vertical; }
    .sp-adm-textarea:disabled { opacity: 0.55; cursor: not-allowed; background: #f9fafb; }
    .sp-adm-textarea-error { border-color: #ef4444; }
  `],
})
export class SpAdminTextareaComponent implements ControlValueAccessor {
  @Input() rows = 4;
  @Input() placeholder = '';
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
    this.value = (event.target as HTMLTextAreaElement).value;
    this.onChange(this.value);
  }

  onBlur(): void {
    this.onTouched();
  }
}
