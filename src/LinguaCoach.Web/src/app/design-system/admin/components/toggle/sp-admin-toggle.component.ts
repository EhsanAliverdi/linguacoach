import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'sp-admin-toggle',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminToggleComponent),
      multi: true,
    },
  ],
  template: `
    <label
      class="sp-tog-root"
      [class.sp-tog-disabled]="disabled"
      [attr.aria-label]="label || null"
    >
      <button
        type="button"
        role="switch"
        class="sp-tog-track"
        [class.sp-tog-track--on]="checked"
        [class.sp-tog-track--loading]="loading"
        [disabled]="disabled || loading || null"
        [attr.aria-checked]="checked"
        [attr.aria-disabled]="disabled || loading || null"
        (click)="toggle()"
        (keydown.Space)="$event.preventDefault(); toggle()"
        (keydown.Enter)="$event.preventDefault(); toggle()"
      >
        <span class="sp-tog-thumb" [class.sp-tog-thumb--on]="checked">
          @if (loading) {
            <span class="sp-tog-spinner" aria-hidden="true"></span>
          }
        </span>
      </button>
      @if (label) {
        <span class="sp-tog-label-group">
          <span class="sp-tog-label">{{ label }}</span>
          @if (description) {
            <span class="sp-tog-desc">{{ description }}</span>
          }
        </span>
      }
    </label>
  `,
  styles: [`
    .sp-tog-root {
      display: inline-flex; align-items: flex-start; gap: 10px;
      cursor: pointer; user-select: none;
    }
    .sp-tog-root.sp-tog-disabled { opacity: 0.5; cursor: not-allowed; pointer-events: none; }

    /* .adm-toggle: 38px x 22px, 99px radius, border none, indigo on, border-2 off */
    .sp-tog-track {
      flex-shrink: 0;
      width: 38px; height: 22px; border-radius: 99px;
      border: none; padding: 0; margin: 0;
      background: #E2DEF0;
      position: relative; cursor: pointer;
      transition: background 0.2s;
      display: flex; align-items: center;
    }
    .sp-tog-track:focus-visible {
      outline: 2px solid #5B4BE8;
      outline-offset: 2px;
    }
    .sp-tog-track--on {
      background: #5B4BE8;
    }
    .sp-tog-track--loading {
      opacity: 0.7; cursor: wait;
    }

    /* .adm-toggle::after: top 3px, left 3px/19px, 16px x 16px, shadow sh-xs */
    .sp-tog-thumb {
      position: absolute; left: 3px; top: 3px;
      width: 16px; height: 16px; border-radius: 50%;
      background: #fff;
      box-shadow: 0 1px 2px rgba(33,27,54,.06);
      transition: left 0.18s;
      display: flex; align-items: center; justify-content: center;
    }
    .sp-tog-thumb--on { left: 19px; }

    .sp-tog-spinner {
      width: 10px; height: 10px; border-radius: 50%;
      border: 1.5px solid var(--sp-admin-primary, #5B4BE8);
      border-top-color: transparent;
      animation: sp-tog-spin 0.7s linear infinite;
      display: block;
    }
    @keyframes sp-tog-spin { to { transform: rotate(360deg); } }

    .sp-tog-label-group { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .sp-tog-label { font-size: 13px; font-weight: 700; color: var(--sp-admin-text, #211B36); line-height: 1.4; }
    .sp-tog-desc  { font-size: 11.5px; color: var(--sp-admin-text-muted, #8B85A0); line-height: 1.4; }
  `],
})
export class SpAdminToggleComponent implements ControlValueAccessor {
  @Input() checked = false;
  @Input() disabled = false;
  @Input() loading = false;
  @Input() label = '';
  @Input() description = '';
  @Output() changed = new EventEmitter<boolean>();

  private onChange: (v: boolean) => void = () => {};
  private onTouched: () => void = () => {};

  toggle(): void {
    if (this.disabled || this.loading) return;
    this.checked = !this.checked;
    this.onChange(this.checked);
    this.onTouched();
    this.changed.emit(this.checked);
  }

  writeValue(value: boolean): void {
    this.checked = !!value;
  }

  registerOnChange(fn: (v: boolean) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }
}
