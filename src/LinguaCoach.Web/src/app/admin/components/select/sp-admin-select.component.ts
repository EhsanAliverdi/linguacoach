import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SpAdminSelectOption {
  value: string;
  label: string;
}

export type SpAdminSelectSize = 'sm' | 'md' | 'lg';
export type SpAdminSelectState = 'default' | 'error' | 'success' | 'disabled';

/**
 * TailAdmin-backed select wrapper with ControlValueAccessor support.
 *
 * Supports both template-driven ([(ngModel)]) and reactive (formControlName) forms.
 * Options come from [options] input or projected <option> content.
 * Known gap: number|null / object values require native <select> — tracked for sp-admin-select-object.
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
      TailAdmin select (shared/components/form/select):
      h-11 rounded-lg border border-gray-200 bg-transparent px-4 py-2.5
      text-sm focus:border-brand-300 focus:ring-2 focus:ring-blue-100
    -->
    <select
      [class]="selectClasses"
      [disabled]="isDisabled"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="isInvalid ? 'true' : null"
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
    :host { display:block; min-width:0; }
    /* TailAdmin-backed: h-11 rounded-lg border border-gray-200 select pattern */
    .sp-adm-select { box-sizing:border-box; width:100%; border-radius:8px; border:1px solid #e5e7eb; background:transparent; color:#111827; transition:border-color .15s, box-shadow .15s; }
    .sp-adm-select:focus { outline:none; border-color:#93c5fd; box-shadow:0 0 0 3px rgba(147,197,253,.3); }
    .sp-adm-select:disabled { opacity:.55; cursor:not-allowed; background:#f9fafb; }

    /* Sizes */
    .sp-adm-select-sm { height:32px; padding:4px 12px; font-size:12px; }
    .sp-adm-select-md { height:44px; padding:10px 16px; font-size:13px; }
    .sp-adm-select-lg { height:52px; padding:12px 20px; font-size:15px; }

    /* States */
    .sp-adm-select-error   { border-color:#ef4444; }
    .sp-adm-select-error:focus   { box-shadow:0 0 0 3px rgba(239,68,68,.15); }
    .sp-adm-select-success { border-color:#16a34a; }
    .sp-adm-select-success:focus { box-shadow:0 0 0 3px rgba(22,163,74,.15); }

    /* Width */
    .sp-adm-select-auto { width:auto; }
  `],
})
export class SpAdminSelectComponent implements ControlValueAccessor {
  @Input() options: SpAdminSelectOption[] = [];
  @Input() placeholder = '';
  @Input() required = false;
  @Input() invalid = false;
  @Input() size: SpAdminSelectSize = 'md';
  @Input() state: SpAdminSelectState = 'default';
  @Input() fullWidth = true;

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(value: boolean) { this._disabled = value; }

  value = '';
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  get isDisabled(): boolean {
    return this._disabled || this.state === 'disabled';
  }

  get isInvalid(): boolean {
    return this.invalid || this.state === 'error';
  }

  get selectClasses(): string {
    const cls = ['sp-adm-select', `sp-adm-select-${this.size}`];
    const effectiveState = this.state !== 'default' ? this.state : (this.invalid ? 'error' : null);
    if (effectiveState && effectiveState !== 'disabled') cls.push(`sp-adm-select-${effectiveState}`);
    if (!this.fullWidth) cls.push('sp-adm-select-auto');
    return cls.join(' ');
  }

  writeValue(value: string): void { this.value = value ?? ''; }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }

  onSelect(event: Event): void {
    this.value = (event.target as HTMLSelectElement).value;
    this.onChange(this.value);
  }

  onBlur(): void { this.onTouched(); }
}
