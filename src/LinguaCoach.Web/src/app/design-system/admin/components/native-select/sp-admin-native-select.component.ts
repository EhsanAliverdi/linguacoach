import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SpAdminNativeSelectOption {
  value: string | number | null;
  label: string;
}

/**
 * Native <select> styled to match the admin design system.
 * Supports ngModel, formControl, and a simple [options] input.
 * Use for rows-per-page selectors, small edit-form selects, and
 * any case where sp-admin-select's custom dropdown is overkill.
 */
@Component({
  selector: 'sp-admin-native-select',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminNativeSelectComponent),
      multi: true,
    },
  ],
  template: `
    <select
      class="sp-adm-ns-select"
      [class.sp-adm-ns-sm]="size === 'sm'"
      [disabled]="isDisabled"
      [value]="value"
      (change)="onSelect($event)"
      (blur)="onTouched()"
    >
      @if (placeholder) {
        <option value="" disabled [selected]="value === null || value === undefined">{{ placeholder }}</option>
      }
      @for (opt of options; track opt.value) {
        <option [value]="opt.value" [selected]="opt.value === value">{{ opt.label }}</option>
      }
      <ng-content />
    </select>
  `,
  styles: [`
    :host { display: block; }
    .sp-adm-ns-select {
      display: block;
      width: 100%;
      height: 40px;
      padding: 0 12px;
      border: 1.5px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 8px;
      background: #fff;
      color: var(--sp-admin-text, #211B36);
      font-size: 13px;
      font-family: inherit;
      font-weight: 500;
      cursor: pointer;
      appearance: auto;
      box-sizing: border-box;
      transition: border-color .12s, box-shadow .12s;
    }
    .sp-adm-ns-select:focus {
      outline: none;
      border-color: var(--sp-admin-primary, #5B4BE8);
      box-shadow: 0 0 0 3px rgba(91, 75, 232, 0.12);
    }
    .sp-adm-ns-select:disabled {
      opacity: 0.55;
      cursor: not-allowed;
      background: var(--sp-admin-surface-subtle, #FBFAFE);
    }
    .sp-adm-ns-sm {
      height: 32px;
      font-size: 12px;
      padding: 0 8px;
      border-radius: 6px;
    }
  `],
})
export class SpAdminNativeSelectComponent implements ControlValueAccessor {
  @Input() options: SpAdminNativeSelectOption[] = [];
  @Input() placeholder = '';
  @Input() size: 'sm' | 'md' = 'md';

  value: string | number | null = null;
  isDisabled = false;

  private _onChange: (v: string | number | null) => void = () => {};
  onTouched: () => void = () => {};

  writeValue(v: string | number | null): void { this.value = v; }
  registerOnChange(fn: (v: string | number | null) => void): void { this._onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(d: boolean): void { this.isDisabled = d; }

  onSelect(event: Event): void {
    const el = event.target as HTMLSelectElement;
    const raw = el.value;
    // Coerce back to number when all options are numeric; null when empty/placeholder
    if (raw === '' || raw === 'null') {
      this.value = null;
      this._onChange(null);
      return;
    }
    const num = Number(raw);
    const val = !isNaN(num) && raw !== '' ? num : raw;
    this.value = val;
    this._onChange(val);
  }
}
