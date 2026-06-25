import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export type SpAdminNumberInputSize = 'sm' | 'md' | 'lg';

/**
 * Number input wrapper with ControlValueAccessor support.
 * Emits number | null (null when the field is empty).
 * Visually matches sp-admin-input.
 */
@Component({
  selector: 'sp-admin-number-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminNumberInputComponent),
      multi: true,
    },
  ],
  template: `
    <div [class]="wrapperClasses">
      @if (prefix) {
        <span class="sp-adm-prefix" aria-hidden="true">{{ prefix }}</span>
      }
      <input
        type="number"
        [class]="inputClasses"
        [placeholder]="placeholder"
        [disabled]="isDisabled"
        [attr.min]="min != null ? min : null"
        [attr.max]="max != null ? max : null"
        [attr.step]="step != null ? step : null"
        [attr.aria-label]="ariaLabel || null"
        [value]="displayValue"
        (input)="onInput($event)"
        (blur)="onBlur()"
      />
    </div>
  `,
  styles: [`
    :host { display:block; min-width:0; }
    .sp-adm-wrap { position:relative; display:flex; align-items:center; }
    .sp-adm-prefix { position:absolute; left:12px; font-size:13px; color:var(--sp-admin-text-muted,#8B85A0); pointer-events:none; z-index:1; line-height:1; }
    .sp-adm-input { box-sizing:border-box; width:100%; border-radius:8px; border:1px solid var(--sp-admin-border,#ECE9F5); background:transparent; color:var(--sp-admin-text,#0F172A); transition:border-color .15s, box-shadow .15s; }
    .sp-adm-input::placeholder { color:var(--sp-admin-text-faint,#CBD5E1); }
    .sp-adm-input:focus { outline:none; border-color:var(--sp-admin-primary,#5B4BE8); box-shadow:var(--sp-admin-focus-ring,0 0 0 3px rgba(91,75,232,.15)); }
    .sp-adm-input:disabled { opacity:.55; cursor:not-allowed; background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-adm-input-sm { height:32px; padding:6px 12px; font-size:12px; }
    .sp-adm-input-md { height:44px; padding:10px 16px; font-size:13px; }
    .sp-adm-input-lg { height:52px; padding:12px 20px; font-size:15px; }
    .sp-adm-has-prefix .sp-adm-input-sm { padding-left:22px; }
    .sp-adm-has-prefix .sp-adm-input-md { padding-left:22px; }
    .sp-adm-has-prefix .sp-adm-input-lg { padding-left:26px; }
    /* Remove browser spin buttons for a cleaner look, allow them for keyboard accessibility */
    .sp-adm-input[type=number] { -moz-appearance:textfield; }
    .sp-adm-input[type=number]::-webkit-inner-spin-button,
    .sp-adm-input[type=number]::-webkit-outer-spin-button { opacity:0.5; }
  `],
})
export class SpAdminNumberInputComponent implements ControlValueAccessor {
  @Input() placeholder = '';
  @Input() min: number | null = null;
  @Input() max: number | null = null;
  @Input() step: number | null = null;
  @Input() ariaLabel = '';
  @Input() size: SpAdminNumberInputSize = 'md';
  @Input() prefix = '';

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(value: boolean) { this._disabled = value; }

  private _value: number | null = null;
  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  get isDisabled(): boolean { return this._disabled; }

  get displayValue(): string {
    return this._value != null ? String(this._value) : '';
  }

  get wrapperClasses(): string {
    return this.prefix ? 'sp-adm-wrap sp-adm-has-prefix' : 'sp-adm-wrap';
  }

  get inputClasses(): string {
    return `sp-adm-input sp-adm-input-${this.size}`;
  }

  writeValue(value: number | null): void {
    this._value = value != null ? Number(value) : null;
  }

  registerOnChange(fn: (value: number | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }

  onInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const parsed = raw === '' ? null : Number(raw);
    this._value = parsed;
    this.onChange(parsed);
  }

  onBlur(): void { this.onTouched(); }
}
