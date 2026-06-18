import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

@Component({
  selector: 'sp-admin-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!--
      TailAdmin button pattern (shared/components/ui/button/button.component.html):
      inline-flex items-center justify-center gap-2 rounded-lg transition
      Primary: bg-brand-500 text-white shadow-theme-xs hover:bg-brand-600 disabled:bg-brand-300
      Outline: bg-white text-gray-700 ring-1 ring-inset ring-gray-300 hover:bg-gray-50
      Size sm: px-4 py-3 text-sm  |  md: px-5 py-3.5 text-sm
    -->
    <button
      class="sp-adm-btn inline-flex items-center justify-center gap-2 rounded-lg transition font-semibold text-sm"
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
    /* TailAdmin-backed: base structure uses TailAdmin rounded-lg transition pattern */
    .sp-adm-btn {
      min-height: 40px;
      border: 1px solid transparent;
      padding: 10px 20px;
      cursor: pointer;
      white-space: nowrap;
    }
    .sp-adm-btn:focus-visible {
      outline: 3px solid var(--sp-admin-primary-focus);
      outline-offset: 2px;
    }
    .sp-adm-btn:disabled { opacity: .55; cursor: not-allowed; }
    /* TailAdmin primary: bg-brand-500 text-white hover:bg-brand-600 */
    .sp-adm-btn-primary { background: #465fff; color: #fff; box-shadow: 0 1px 2px 0 rgba(0,0,0,.05); }
    .sp-adm-btn-primary:hover:not(:disabled) { background: #3641f5; }
    /* TailAdmin outline: bg-white text-gray-700 ring-1 ring-gray-300 */
    .sp-adm-btn-secondary { background: #fff; color: #344054; box-shadow: 0 0 0 1px #d0d5dd inset; }
    .sp-adm-btn-secondary:hover:not(:disabled) { background: #f9fafb; }
    .sp-adm-btn-ghost { background: var(--sp-admin-surface); box-shadow: 0 0 0 1px var(--sp-admin-border) inset; color: var(--sp-admin-text-secondary); }
    .sp-adm-btn-ghost:hover:not(:disabled) { box-shadow: 0 0 0 1px var(--sp-admin-primary-focus) inset; color: var(--sp-admin-primary); }
    .sp-adm-btn-danger { background: var(--sp-admin-danger); color: #fff; }
    .sp-adm-btn-danger:hover:not(:disabled) { background: var(--sp-admin-danger-hover); }
    /* TailAdmin size sm: px-4 py-3 text-sm */
    .sp-adm-btn-sm { min-height: 32px; padding: 8px 16px; font-size: 12px; }
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
