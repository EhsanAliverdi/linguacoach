import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export type SpAdminTextareaSize = 'sm' | 'md' | 'lg';
export type SpAdminTextareaState = 'default' | 'error' | 'success' | 'disabled';

/**
 * TailAdmin-backed textarea wrapper with ControlValueAccessor support.
 * Supports both template-driven ([(ngModel)]) and reactive (formControlName) forms.
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
      TailAdmin textarea (shared/components/form/input/text-area):
      rounded-lg border border-gray-200 bg-transparent px-4 py-2.5 text-sm
    -->
    <textarea
      [class]="textareaClasses"
      [rows]="rows"
      [placeholder]="placeholder"
      [disabled]="isDisabled"
      [readOnly]="readonly"
      [attr.required]="required ? '' : null"
      [attr.aria-invalid]="isInvalid ? 'true' : null"
      [value]="value"
      (input)="onInput($event)"
      (blur)="onBlur()"
    ></textarea>
  `,
  styles: [`
    :host { display:block; min-width:0; }
    /* TailAdmin-backed: rounded-lg border border-gray-200 textarea pattern */
    .sp-adm-textarea { box-sizing:border-box; width:100%; border-radius:8px; border:1px solid var(--sp-admin-border,#ECE9F5); background:transparent; color:var(--sp-admin-text,#0F172A); resize:vertical; transition:border-color .15s, box-shadow .15s; }
    .sp-adm-textarea::placeholder { color:var(--sp-admin-text-faint,#CBD5E1); }
    .sp-adm-textarea:focus { outline:none; border-color:var(--sp-admin-primary,#5B4BE8); box-shadow:var(--sp-admin-focus-ring,0 0 0 3px rgba(91,75,232,.15)); }
    .sp-adm-textarea:disabled { opacity:.55; cursor:not-allowed; background:var(--sp-admin-surface-subtle,#FBFAFE); }

    /* Sizes */
    .sp-adm-textarea-sm { padding:6px 12px;  font-size:12px; }
    .sp-adm-textarea-md { padding:10px 16px; font-size:13px; }
    .sp-adm-textarea-lg { padding:12px 20px; font-size:15px; }

    /* States */
    .sp-adm-textarea-error   { border-color:#ef4444; }
    .sp-adm-textarea-error:focus   { box-shadow:0 0 0 3px rgba(239,68,68,.15); }
    .sp-adm-textarea-success { border-color:#16a34a; }
    .sp-adm-textarea-success:focus { box-shadow:0 0 0 3px rgba(22,163,74,.15); }

    /* Width */
    .sp-adm-textarea-auto { width:auto; }
  `],
})
export class SpAdminTextareaComponent implements ControlValueAccessor {
  @Input() rows = 4;
  @Input() placeholder = '';
  @Input() readonly = false;
  @Input() required = false;
  @Input() invalid = false;
  @Input() size: SpAdminTextareaSize = 'md';
  @Input() state: SpAdminTextareaState = 'default';
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

  get textareaClasses(): string {
    const cls = ['sp-adm-textarea', `sp-adm-textarea-${this.size}`];
    const effectiveState = this.state !== 'default' ? this.state : (this.invalid ? 'error' : null);
    if (effectiveState && effectiveState !== 'disabled') cls.push(`sp-adm-textarea-${effectiveState}`);
    if (!this.fullWidth) cls.push('sp-adm-textarea-auto');
    return cls.join(' ');
  }

  writeValue(value: string): void { this.value = value ?? ''; }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }

  onInput(event: Event): void {
    this.value = (event.target as HTMLTextAreaElement).value;
    this.onChange(this.value);
  }

  onBlur(): void { this.onTouched(); }
}
