import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export type SpAdminInputSize = 'sm' | 'md' | 'lg';
export type SpAdminInputState = 'default' | 'error' | 'success' | 'disabled';

/**
 * TailAdmin-backed text input wrapper with ControlValueAccessor support.
 *
 * Supports both template-driven ([(ngModel)]) and reactive (formControlName) forms.
 * Disabled state propagates from Angular forms via setDisabledState.
 * Touched state is marked on blur. The [value] input is for one-way display use.
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
      TailAdmin input (shared/components/form/input):
      h-11 rounded-lg border border-gray-200 bg-transparent py-2.5 px-4
      text-sm text-gray-800 placeholder:text-gray-400
      focus:border-brand-300 focus:ring-3 focus:ring-brand-500/10
    -->
    <input
      [class]="inputClasses"
      [type]="type"
      [placeholder]="placeholder"
      [disabled]="isDisabled"
      [readOnly]="readonly"
      [attr.autocomplete]="autocomplete || null"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="isInvalid ? 'true' : null"
      [value]="value"
      (input)="onInput($event)"
      (blur)="onBlur()"
    />
  `,
  styles: [`
    :host { display:block; min-width:0; }
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 input pattern */
    .sp-adm-input { box-sizing:border-box; width:100%; border-radius:8px; border:1px solid #e5e7eb; background:transparent; color:#111827; transition:border-color .15s, box-shadow .15s; }
    .sp-adm-input::placeholder { color:#9ca3af; }
    .sp-adm-input:focus { outline:none; border-color:#93c5fd; box-shadow:0 0 0 3px rgba(147,197,253,.3); }
    .sp-adm-input:disabled { opacity:.55; cursor:not-allowed; background:#f9fafb; }

    /* Sizes */
    .sp-adm-input-sm { height:32px; padding:6px 12px; font-size:12px; }
    .sp-adm-input-md { height:44px; padding:10px 16px; font-size:13px; }
    .sp-adm-input-lg { height:52px; padding:12px 20px; font-size:15px; }

    /* States */
    .sp-adm-input-error   { border-color:#ef4444; }
    .sp-adm-input-error:focus   { box-shadow:0 0 0 3px rgba(239,68,68,.15); border-color:#ef4444; }
    .sp-adm-input-success { border-color:#16a34a; }
    .sp-adm-input-success:focus { box-shadow:0 0 0 3px rgba(22,163,74,.15); border-color:#16a34a; }

    /* Width */
    .sp-adm-input-auto { width:auto; }
  `],
})
export class SpAdminInputComponent implements ControlValueAccessor {
  @Input() type = 'text';
  @Input() placeholder = '';
  @Input() autocomplete = '';
  @Input() readonly = false;
  @Input() required = false;
  @Input() invalid = false;
  @Input() size: SpAdminInputSize = 'md';
  @Input() state: SpAdminInputState = 'default';
  @Input() fullWidth = true;

  @Input() set value(v: string) { this._value = v ?? ''; }
  get value(): string { return this._value; }

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(value: boolean) { this._disabled = value; }

  private _value = '';
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  get isDisabled(): boolean {
    return this._disabled || this.state === 'disabled';
  }

  get isInvalid(): boolean {
    return this.invalid || this.state === 'error';
  }

  get inputClasses(): string {
    const cls = ['sp-adm-input', `sp-adm-input-${this.size}`];
    const effectiveState = this.state !== 'default' ? this.state : (this.invalid ? 'error' : null);
    if (effectiveState && effectiveState !== 'disabled') cls.push(`sp-adm-input-${effectiveState}`);
    if (!this.fullWidth) cls.push('sp-adm-input-auto');
    return cls.join(' ');
  }

  writeValue(value: string): void { this._value = value ?? ''; }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }

  onInput(event: Event): void {
    this._value = (event.target as HTMLInputElement).value;
    this.onChange(this._value);
  }

  onBlur(): void { this.onTouched(); }
}
