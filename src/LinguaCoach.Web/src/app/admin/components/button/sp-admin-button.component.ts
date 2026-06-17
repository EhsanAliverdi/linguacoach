import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

@Component({
  selector: 'sp-admin-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      class="sp-adm-btn"
      [class.sp-adm-btn-primary]="variant === 'primary'"
      [class.sp-adm-btn-secondary]="variant === 'secondary'"
      [class.sp-adm-btn-ghost]="variant === 'ghost'"
      [class.sp-adm-btn-danger]="variant === 'danger'"
      [class.sp-adm-btn-sm]="size === 'sm'"
      [class.sp-adm-btn-block]="block"
      [disabled]="disabled || loading"
      [attr.type]="type"
    >
      @if (loading) {
        <span class="sp-adm-btn-spinner" aria-hidden="true"></span>
      }
      <ng-content />
    </button>
  `,
  styles: [`
    .sp-adm-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      min-height: 40px;
      border-radius: var(--sp-admin-radius-sm);
      border: 1px solid transparent;
      padding: 8px 16px;
      font: inherit;
      font-size: 13px;
      font-weight: 800;
      cursor: pointer;
      white-space: nowrap;
      transition: background var(--sp-admin-transition-fast), border-color var(--sp-admin-transition-fast), color var(--sp-admin-transition-fast), box-shadow var(--sp-admin-transition-fast);
    }
    .sp-adm-btn:focus-visible {
      outline: 3px solid var(--sp-admin-primary-focus);
      outline-offset: 2px;
    }
    .sp-adm-btn:disabled { opacity: .55; cursor: not-allowed; }
    .sp-adm-btn-primary { background: var(--sp-admin-primary); color: #fff; }
    .sp-adm-btn-primary:hover:not(:disabled) { background: var(--sp-admin-primary-hover); }
    .sp-adm-btn-secondary { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-adm-btn-ghost { background: var(--sp-admin-surface); border-color: var(--sp-admin-border); color: var(--sp-admin-text-secondary); }
    .sp-adm-btn-ghost:hover:not(:disabled) { border-color: var(--sp-admin-primary-focus); color: var(--sp-admin-primary); }
    .sp-adm-btn-danger { background: var(--sp-admin-danger); color: #fff; }
    .sp-adm-btn-sm { min-height: 32px; padding: 6px 12px; font-size: 12px; }
    .sp-adm-btn-block { width: 100%; }
    .sp-adm-btn-spinner {
      width: 14px;
      height: 14px;
      border-radius: 50%;
      border: 2px solid currentColor;
      border-right-color: transparent;
      animation: sp-adm-btn-spin .7s linear infinite;
    }
    @keyframes sp-adm-btn-spin { to { transform: rotate(360deg); } }
  `],
})
export class SpAdminButtonComponent {
  @Input() variant: SpAdminButtonVariant = 'primary';
  @Input() size: 'sm' | 'md' = 'md';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() disabled = false;
  @Input() loading = false;
  @Input() block = false;
}
