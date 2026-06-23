import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Styled checkbox with label. Supports ngModel and formControl.
 * Emits boolean values. Visually matches the admin design system.
 */
@Component({
  selector: 'sp-admin-checkbox',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminCheckboxComponent),
      multi: true,
    },
  ],
  template: `
    <label [class]="labelClasses">
      <span class="sp-adm-cb-track" [class.sp-adm-cb-checked]="checked" [class.sp-adm-cb-disabled]="isDisabled">
        <input
          type="checkbox"
          class="sp-adm-cb-input"
          [checked]="checked"
          [disabled]="isDisabled"
          (change)="onCheck($event)"
          (blur)="onBlur()"
        />
        <span class="sp-adm-cb-box" aria-hidden="true">
          @if (checked) {
            <svg width="10" height="8" viewBox="0 0 10 8" fill="none">
              <path d="M1 4L3.5 6.5L9 1" stroke="white" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          }
        </span>
      </span>
      @if (label) {
        <span class="sp-adm-cb-label" [class.sp-adm-cb-label-disabled]="isDisabled">{{ label }}</span>
      }
      @if (helper) {
        <span class="sp-adm-cb-helper">{{ helper }}</span>
      }
    </label>
  `,
  styles: [`
    :host { display:block; }
    .sp-adm-cb-wrap { display:flex; align-items:center; gap:8px; cursor:pointer; user-select:none; }
    .sp-adm-cb-wrap-disabled { cursor:not-allowed; }
    .sp-adm-cb-track { position:relative; display:inline-flex; align-items:center; flex-shrink:0; }
    .sp-adm-cb-input { position:absolute; opacity:0; width:0; height:0; pointer-events:none; }
    .sp-adm-cb-box {
      display:flex; align-items:center; justify-content:center;
      width:18px; height:18px; border-radius:4px;
      border:1.5px solid #d1d5db; background:#fff;
      transition:background .12s, border-color .12s, box-shadow .12s;
    }
    .sp-adm-cb-checked .sp-adm-cb-box {
      background:#4f46e5; border-color:#4f46e5;
    }
    .sp-adm-cb-disabled .sp-adm-cb-box {
      opacity:.5; cursor:not-allowed;
    }
    .sp-adm-cb-track:focus-within .sp-adm-cb-box {
      box-shadow:0 0 0 3px rgba(79,70,229,.2);
    }
    .sp-adm-cb-label { font-size:13px; color:var(--sp-admin-text,#0F172A); }
    .sp-adm-cb-label-disabled { opacity:.55; }
    .sp-adm-cb-helper { display:block; font-size:11.5px; color:#9ca3af; margin-top:1px; }
  `],
})
export class SpAdminCheckboxComponent implements ControlValueAccessor {
  @Input() label = '';
  @Input() helper = '';
  @Output() checkedChange = new EventEmitter<boolean>();

  checked = false;

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(value: boolean) { this._disabled = value; }

  private onChange: (value: boolean) => void = () => {};
  private onTouched: () => void = () => {};

  get isDisabled(): boolean { return this._disabled; }

  get labelClasses(): string {
    return 'sp-adm-cb-wrap' + (this.isDisabled ? ' sp-adm-cb-wrap-disabled' : '');
  }

  writeValue(value: boolean): void { this.checked = !!value; }
  registerOnChange(fn: (value: boolean) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }

  onCheck(event: Event): void {
    this.checked = (event.target as HTMLInputElement).checked;
    this.onChange(this.checked);
    this.checkedChange.emit(this.checked);
  }

  onBlur(): void { this.onTouched(); }
}
