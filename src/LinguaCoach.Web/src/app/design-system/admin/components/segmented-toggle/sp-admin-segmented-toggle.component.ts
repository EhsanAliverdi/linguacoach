import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SpAdminSegmentedOption {
  value: string;
  label: string;
}

/**
 * Two-or-more-option pill switcher (e.g. "Upload a file" / "Type it in"). Not a tab bar — used
 * inline within a form to toggle which input mode is shown, not to navigate between page
 * sections. See sp-admin-tab-bar (admin-tokens.css) for that different use case.
 */
@Component({
  selector: 'sp-admin-segmented-toggle',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-segmented" role="tablist" [attr.aria-label]="ariaLabel || null">
      @for (opt of options; track opt.value) {
        <button
          type="button"
          role="tab"
          class="sp-adm-segmented-btn"
          [class.sp-adm-segmented-btn--active]="opt.value === value"
          [attr.aria-selected]="opt.value === value"
          [disabled]="disabled"
          (click)="select(opt.value)">
          {{ opt.label }}
        </button>
      }
    </div>
  `,
  styles: [`
    :host { display: inline-block; }

    .sp-adm-segmented {
      display: inline-flex;
      background: var(--sp-admin-bg, #F6F4FB);
      border-radius: 9px;
      padding: 2px;
      gap: 2px;
    }

    .sp-adm-segmented-btn {
      appearance: none;
      border: none;
      border-radius: 7px;
      padding: 5px 14px;
      font-size: 12.5px;
      font-weight: 700;
      font-family: inherit;
      cursor: pointer;
      background: transparent;
      color: var(--sp-admin-text-muted, #8B85A0);
      transition: background var(--sp-admin-transition-fast, .12s ease), color var(--sp-admin-transition-fast, .12s ease);
      white-space: nowrap;
    }
    .sp-adm-segmented-btn:disabled { opacity: .55; cursor: not-allowed; }
    .sp-adm-segmented-btn--active {
      background: var(--sp-admin-surface, #fff);
      color: var(--sp-admin-text, #211B36);
      box-shadow: var(--sp-admin-shadow-xs, 0 1px 2px rgba(33,27,54,.08));
    }
  `],
})
export class SpAdminSegmentedToggleComponent {
  @Input() options: SpAdminSegmentedOption[] = [];
  @Input() value = '';
  @Input() disabled = false;
  @Input() ariaLabel = '';
  @Output() valueChange = new EventEmitter<string>();

  select(value: string): void {
    if (this.disabled || value === this.value) return;
    this.valueChange.emit(value);
  }
}
