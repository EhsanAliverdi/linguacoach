import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminButtonVariant = 'primary' | 'secondary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'
  | 'ghost'; // @deprecated: use appearance='ghost' variant='neutral'
export type SpAdminButtonAppearance = 'solid' | 'outline' | 'soft' | 'ghost' | 'link';
export type SpAdminButtonSize = 'xs' | 'sm' | 'md' | 'lg';

@Component({
  selector: 'sp-admin-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!--
      TailAdmin button pattern (shared/components/ui/button/button.component.html):
      inline-flex items-center justify-center gap-2 rounded-lg transition
      Primary: bg-brand-500 text-white shadow-theme-xs hover:bg-brand-600
      Outline: bg-white text-gray-700 ring-1 ring-inset ring-gray-300 hover:bg-gray-50
    -->
    <button
      class="sp-adm-btn inline-flex items-center justify-center gap-2 rounded-lg transition font-semibold"
      [class]="hostClasses"
      [disabled]="disabled || loading"
      [attr.type]="type"
      [attr.aria-disabled]="disabled || loading ? 'true' : null"
      [attr.aria-busy]="loading ? 'true' : null"
    >
      @if (loading) {
        <span class="sp-adm-btn-spinner" aria-hidden="true"></span>
      }
      <ng-content select="[leading]" />
      <ng-content />
      <ng-content select="[trailing]" />
    </button>
  `,
  styles: [`
    .sp-adm-btn {
      border: 1px solid transparent;
      cursor: pointer;
      white-space: nowrap;
      line-height: 1.25;
    }
    .sp-adm-btn:focus-visible {
      outline: 3px solid var(--sp-admin-primary-focus);
      outline-offset: 2px;
    }
    .sp-adm-btn:disabled { opacity: .55; cursor: not-allowed; }

    /* --- Sizes (TailAdmin: sm px-4 py-3 text-sm | md px-5 py-3.5) --- */
    .sp-adm-btn-xs  { min-height: 28px; padding: 5px 12px; font-size: 11px; }
    .sp-adm-btn-sm  { min-height: 32px; padding: 8px 16px; font-size: 12px; }
    .sp-adm-btn-md  { min-height: 40px; padding: 10px 20px; font-size: 13px; }
    .sp-adm-btn-lg  { min-height: 48px; padding: 12px 28px; font-size: 15px; }

    /* --- Solid (TailAdmin primary bg-brand-500 / danger / etc.) --- */
    .sp-adm-btn-solid-primary   { background:#465fff; color:#fff; box-shadow:0 1px 2px rgba(0,0,0,.05); }
    .sp-adm-btn-solid-primary:hover:not(:disabled)   { background:#3641f5; }
    .sp-adm-btn-solid-secondary { background:#fff; color:#344054; box-shadow:0 0 0 1px #d0d5dd inset; }
    .sp-adm-btn-solid-secondary:hover:not(:disabled) { background:#f9fafb; }
    .sp-adm-btn-solid-success   { background:#16a34a; color:#fff; }
    .sp-adm-btn-solid-success:hover:not(:disabled)   { background:#15803d; }
    .sp-adm-btn-solid-danger    { background:#ef4444; color:#fff; }
    .sp-adm-btn-solid-danger:hover:not(:disabled)    { background:#dc2626; }
    .sp-adm-btn-solid-warning   { background:#f59e0b; color:#fff; }
    .sp-adm-btn-solid-warning:hover:not(:disabled)   { background:#d97706; }
    .sp-adm-btn-solid-info      { background:#0ba5ec; color:#fff; }
    .sp-adm-btn-solid-info:hover:not(:disabled)      { background:#0284c7; }
    .sp-adm-btn-solid-neutral   { background:#f2f4f7; color:#344054; box-shadow:0 0 0 1px #d0d5dd inset; }
    .sp-adm-btn-solid-neutral:hover:not(:disabled)   { background:#e5e7eb; }

    /* --- Outline --- */
    .sp-adm-btn-outline-primary   { background:transparent; color:#465fff; box-shadow:0 0 0 1px #465fff inset; }
    .sp-adm-btn-outline-primary:hover:not(:disabled)   { background:#ecf3ff; }
    .sp-adm-btn-outline-secondary { background:transparent; color:#344054; box-shadow:0 0 0 1px #d0d5dd inset; }
    .sp-adm-btn-outline-secondary:hover:not(:disabled) { background:#f9fafb; }
    .sp-adm-btn-outline-success   { background:transparent; color:#16a34a; box-shadow:0 0 0 1px #16a34a inset; }
    .sp-adm-btn-outline-success:hover:not(:disabled)   { background:#ecfdf3; }
    .sp-adm-btn-outline-danger    { background:transparent; color:#ef4444; box-shadow:0 0 0 1px #ef4444 inset; }
    .sp-adm-btn-outline-danger:hover:not(:disabled)    { background:#fef2f2; }
    .sp-adm-btn-outline-warning   { background:transparent; color:#d97706; box-shadow:0 0 0 1px #d97706 inset; }
    .sp-adm-btn-outline-warning:hover:not(:disabled)   { background:#fffbeb; }
    .sp-adm-btn-outline-info      { background:transparent; color:#0ba5ec; box-shadow:0 0 0 1px #0ba5ec inset; }
    .sp-adm-btn-outline-info:hover:not(:disabled)      { background:#f0f9ff; }
    .sp-adm-btn-outline-neutral   { background:transparent; color:#344054; box-shadow:0 0 0 1px #d0d5dd inset; }
    .sp-adm-btn-outline-neutral:hover:not(:disabled)   { background:#f2f4f7; }

    /* --- Soft --- */
    .sp-adm-btn-soft-primary   { background:#ecf3ff; color:#465fff; }
    .sp-adm-btn-soft-primary:hover:not(:disabled)   { background:#dde8ff; }
    .sp-adm-btn-soft-secondary { background:#f9fafb; color:#344054; }
    .sp-adm-btn-soft-secondary:hover:not(:disabled) { background:#f2f4f7; }
    .sp-adm-btn-soft-success   { background:#ecfdf3; color:#16a34a; }
    .sp-adm-btn-soft-success:hover:not(:disabled)   { background:#d1fae5; }
    .sp-adm-btn-soft-danger    { background:#fef2f2; color:#ef4444; }
    .sp-adm-btn-soft-danger:hover:not(:disabled)    { background:#fee2e2; }
    .sp-adm-btn-soft-warning   { background:#fffbeb; color:#d97706; }
    .sp-adm-btn-soft-warning:hover:not(:disabled)   { background:#fef3c7; }
    .sp-adm-btn-soft-info      { background:#f0f9ff; color:#0ba5ec; }
    .sp-adm-btn-soft-info:hover:not(:disabled)      { background:#e0f2fe; }
    .sp-adm-btn-soft-neutral   { background:#f2f4f7; color:#344054; }
    .sp-adm-btn-soft-neutral:hover:not(:disabled)   { background:#e5e7eb; }

    /* --- Ghost --- */
    .sp-adm-btn-ghost-primary   { background:transparent; color:#465fff; }
    .sp-adm-btn-ghost-primary:hover:not(:disabled)   { background:#ecf3ff; }
    .sp-adm-btn-ghost-secondary { background:transparent; color:#344054; }
    .sp-adm-btn-ghost-secondary:hover:not(:disabled) { background:#f2f4f7; }
    .sp-adm-btn-ghost-success   { background:transparent; color:#16a34a; }
    .sp-adm-btn-ghost-success:hover:not(:disabled)   { background:#ecfdf3; }
    .sp-adm-btn-ghost-danger    { background:transparent; color:#ef4444; }
    .sp-adm-btn-ghost-danger:hover:not(:disabled)    { background:#fef2f2; }
    .sp-adm-btn-ghost-warning   { background:transparent; color:#d97706; }
    .sp-adm-btn-ghost-warning:hover:not(:disabled)   { background:#fffbeb; }
    .sp-adm-btn-ghost-info      { background:transparent; color:#0ba5ec; }
    .sp-adm-btn-ghost-info:hover:not(:disabled)      { background:#f0f9ff; }
    .sp-adm-btn-ghost-neutral   { background:transparent; color:#344054; }
    .sp-adm-btn-ghost-neutral:hover:not(:disabled)   { background:#f2f4f7; }

    /* --- Link --- */
    .sp-adm-btn-link-primary   { background:transparent; color:#465fff; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-primary:hover:not(:disabled)   { color:#3641f5; }
    .sp-adm-btn-link-secondary { background:transparent; color:#344054; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-secondary:hover:not(:disabled) { color:#0f172a; }
    .sp-adm-btn-link-success   { background:transparent; color:#16a34a; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-success:hover:not(:disabled)   { color:#15803d; }
    .sp-adm-btn-link-danger    { background:transparent; color:#ef4444; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-danger:hover:not(:disabled)    { color:#dc2626; }
    .sp-adm-btn-link-warning   { background:transparent; color:#d97706; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-warning:hover:not(:disabled)   { color:#b45309; }
    .sp-adm-btn-link-info      { background:transparent; color:#0ba5ec; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-info:hover:not(:disabled)      { color:#0284c7; }
    .sp-adm-btn-link-neutral   { background:transparent; color:#344054; padding-left:0; padding-right:0; text-decoration:underline; }
    .sp-adm-btn-link-neutral:hover:not(:disabled)   { color:#0f172a; }

    /* --- Modifiers --- */
    .sp-adm-btn-block     { width:100%; }
    .sp-adm-btn-icon-only { aspect-ratio:1; }
    .sp-adm-btn-icon-only.sp-adm-btn-xs { padding:5px;  width:28px; }
    .sp-adm-btn-icon-only.sp-adm-btn-sm { padding:7px;  width:32px; }
    .sp-adm-btn-icon-only.sp-adm-btn-md { padding:9px;  width:40px; }
    .sp-adm-btn-icon-only.sp-adm-btn-lg { padding:11px; width:48px; }

    .sp-adm-btn-spinner {
      width:14px; height:14px; border-radius:50%;
      border:2px solid currentColor; border-right-color:transparent;
      animation:sp-adm-btn-spin .7s linear infinite; flex-shrink:0;
    }
    @keyframes sp-adm-btn-spin { to { transform:rotate(360deg); } }
  `],
})
export class SpAdminButtonComponent {
  @Input() variant: SpAdminButtonVariant = 'primary';
  @Input() appearance: SpAdminButtonAppearance = 'solid';
  @Input() size: SpAdminButtonSize = 'md';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() disabled = false;
  @Input() loading = false;
  @Input() fullWidth = false;
  @Input() iconOnly = false;
  /** @deprecated use fullWidth */
  @Input() block = false;

  get hostClasses(): string {
    // Legacy: variant='ghost' maps to appearance=ghost + variant=neutral
    const effectiveAppearance = this.variant === 'ghost' ? 'ghost' : this.appearance;
    const effectiveVariant = this.variant === 'ghost' ? 'neutral' : this.variant;
    const classes = [
      `sp-adm-btn-${this.size}`,
      `sp-adm-btn-${effectiveAppearance}-${effectiveVariant}`,
    ];
    if (this.fullWidth || this.block) classes.push('sp-adm-btn-block');
    if (this.iconOnly) classes.push('sp-adm-btn-icon-only');
    return classes.join(' ');
  }
}
