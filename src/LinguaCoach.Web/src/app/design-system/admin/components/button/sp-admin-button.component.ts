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
    /* Base — matches .adm-btn: inline-flex, gap 7px, 13.5px/700, 9px radius, transition opacity+shadow */
    .sp-adm-btn {
      border: none;
      cursor: pointer;
      white-space: nowrap;
      line-height: 1.25;
      font-size: 13.5px;
      font-weight: 700;
      font-family: inherit;
      transition: opacity .12s, box-shadow .12s;
    }
    .sp-adm-btn:hover:not(:disabled)  { opacity: 0.88; }
    .sp-adm-btn:active:not(:disabled) { transform: scale(0.98); }
    .sp-adm-btn:focus-visible {
      outline: 3px solid #C0BAF9;
      outline-offset: 2px;
    }
    .sp-adm-btn:disabled { opacity: .45; cursor: not-allowed; }

    /* --- Sizes — .adm-btn: 9/16px | .adm-btn-sm: 6/12px/12.5px | .adm-btn-xs: 4/10px/12px --- */
    .sp-adm-btn-xs  { padding: 4px 10px;  font-size: 12px;   border-radius: 6px; min-height: 26px; }
    .sp-adm-btn-sm  { padding: 6px 12px;  font-size: 12.5px; border-radius: 7px; min-height: 30px; }
    .sp-adm-btn-md  { padding: 9px 16px;  font-size: 13.5px; border-radius: 9px; min-height: 38px; }
    .sp-adm-btn-lg  { padding: 11px 22px; font-size: 14px;   border-radius: 9px; min-height: 44px; }

    /* --- Solid primary — .adm-btn-primary: coral→magenta→indigo gradient, indigo shadow --- */
    .sp-adm-btn-solid-primary   { background: linear-gradient(135deg,#FF7A59 0%,#B45CF0 52%,#5B4BE8 100%); color:#fff; box-shadow:0 4px 14px rgba(91,75,232,.25); border:none; }
    .sp-adm-btn-solid-primary:hover:not(:disabled) { opacity:0.88; }
    /* --- Solid indigo (secondary primary) — .adm-btn-indigo --- */
    .sp-adm-btn-solid-secondary { background:#5B4BE8; color:#fff; box-shadow:0 2px 8px rgba(91,75,232,.20); border:none; }
    .sp-adm-btn-solid-secondary:hover:not(:disabled) { opacity:0.88; }
    .sp-adm-btn-solid-success   { background:#13B07C; color:#fff; border:none; }
    .sp-adm-btn-solid-success:hover:not(:disabled)   { opacity:0.88; }
    .sp-adm-btn-solid-danger    { background:#EF4444; color:#fff; border:none; }
    .sp-adm-btn-solid-danger:hover:not(:disabled)    { opacity:0.88; }
    .sp-adm-btn-solid-warning   { background:#F0982C; color:#fff; border:none; }
    .sp-adm-btn-solid-warning:hover:not(:disabled)   { opacity:0.88; }
    .sp-adm-btn-solid-info      { background:#5B4BE8; color:#fff; border:none; }
    .sp-adm-btn-solid-info:hover:not(:disabled)      { opacity:0.88; }
    /* .adm-btn-ghost: white bg, border-2 border-color, ink text */
    .sp-adm-btn-solid-neutral   { background:#fff; color:#211B36; border:1.5px solid #E2DEF0; }
    .sp-adm-btn-solid-neutral:hover:not(:disabled)   { background:#F6F4FB; }

    /* --- Outline --- */
    .sp-adm-btn-outline-primary   { background:transparent; color:#5B4BE8; border:1.5px solid #5B4BE8; }
    .sp-adm-btn-outline-primary:hover:not(:disabled)   { background:#EDEBFF; }
    .sp-adm-btn-outline-secondary { background:#fff; color:#211B36; border:1.5px solid #E2DEF0; }
    .sp-adm-btn-outline-secondary:hover:not(:disabled) { background:#FBFAFE; }
    .sp-adm-btn-outline-success   { background:transparent; color:#13B07C; border:1.5px solid #13B07C; }
    .sp-adm-btn-outline-success:hover:not(:disabled)   { background:#E0F6EE; }
    .sp-adm-btn-outline-danger    { background:#FEE2E2; color:#DC2626; border:1.5px solid #FECACA; }
    .sp-adm-btn-outline-danger:hover:not(:disabled)    { background:#FEE2E2; }
    .sp-adm-btn-outline-warning   { background:transparent; color:#F0982C; border:1.5px solid #F0982C; }
    .sp-adm-btn-outline-warning:hover:not(:disabled)   { background:#FFF1DC; }
    .sp-adm-btn-outline-info      { background:transparent; color:#5B4BE8; border:1.5px solid #5B4BE8; }
    .sp-adm-btn-outline-info:hover:not(:disabled)      { background:#EDEBFF; }
    .sp-adm-btn-outline-neutral   { background:#fff; color:#211B36; border:1.5px solid #E2DEF0; }
    .sp-adm-btn-outline-neutral:hover:not(:disabled)   { background:#F6F4FB; }

    /* --- Soft --- */
    .sp-adm-btn-soft-primary   { background:#EDEBFF; color:#5B4BE8; border:none; }
    .sp-adm-btn-soft-primary:hover:not(:disabled)   { background:#C0BAF9; }
    .sp-adm-btn-soft-secondary { background:#FBFAFE; color:#211B36; border:none; }
    .sp-adm-btn-soft-secondary:hover:not(:disabled) { background:#F6F4FB; }
    .sp-adm-btn-soft-success   { background:#E0F6EE; color:#13B07C; border:none; }
    .sp-adm-btn-soft-success:hover:not(:disabled)   { background:#A8EDD4; }
    .sp-adm-btn-soft-danger    { background:#FEE2E2; color:#DC2626; border:none; }
    .sp-adm-btn-soft-danger:hover:not(:disabled)    { background:#FECACA; }
    .sp-adm-btn-soft-warning   { background:#FFF1DC; color:#B26410; border:none; }
    .sp-adm-btn-soft-warning:hover:not(:disabled)   { background:#FEF3C7; }
    .sp-adm-btn-soft-info      { background:#EDEBFF; color:#5B4BE8; border:none; }
    .sp-adm-btn-soft-info:hover:not(:disabled)      { background:#C0BAF9; }
    .sp-adm-btn-soft-neutral   { background:#F6F4FB; color:#211B36; border:none; }
    .sp-adm-btn-soft-neutral:hover:not(:disabled)   { background:#ECE9F5; }

    /* --- Ghost --- */
    .sp-adm-btn-ghost-primary   { background:transparent; color:#5B4BE8; border:none; }
    .sp-adm-btn-ghost-primary:hover:not(:disabled)   { background:#EDEBFF; }
    .sp-adm-btn-ghost-secondary { background:transparent; color:#211B36; border:none; }
    .sp-adm-btn-ghost-secondary:hover:not(:disabled) { background:#F6F4FB; }
    .sp-adm-btn-ghost-success   { background:transparent; color:#13B07C; border:none; }
    .sp-adm-btn-ghost-success:hover:not(:disabled)   { background:#E0F6EE; }
    .sp-adm-btn-ghost-danger    { background:transparent; color:#DC2626; border:none; }
    .sp-adm-btn-ghost-danger:hover:not(:disabled)    { background:#FEE2E2; }
    .sp-adm-btn-ghost-warning   { background:transparent; color:#F0982C; border:none; }
    .sp-adm-btn-ghost-warning:hover:not(:disabled)   { background:#FFF1DC; }
    .sp-adm-btn-ghost-info      { background:transparent; color:#5B4BE8; border:none; }
    .sp-adm-btn-ghost-info:hover:not(:disabled)      { background:#EDEBFF; }
    .sp-adm-btn-ghost-neutral   { background:transparent; color:#4B4462; border:none; }
    .sp-adm-btn-ghost-neutral:hover:not(:disabled)   { background:#F6F4FB; }

    /* --- Link --- */
    .sp-adm-btn-link-primary   { background:transparent; color:#5B4BE8; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-primary:hover:not(:disabled)   { color:#3A2EA8; }
    .sp-adm-btn-link-secondary { background:transparent; color:#4B4462; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-secondary:hover:not(:disabled) { color:#211B36; }
    .sp-adm-btn-link-success   { background:transparent; color:#13B07C; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-success:hover:not(:disabled)   { color:#0A7468; }
    .sp-adm-btn-link-danger    { background:transparent; color:#EF4444; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-danger:hover:not(:disabled)    { color:#DC2626; }
    .sp-adm-btn-link-warning   { background:transparent; color:#F0982C; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-warning:hover:not(:disabled)   { color:#B26410; }
    .sp-adm-btn-link-info      { background:transparent; color:#5B4BE8; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-info:hover:not(:disabled)      { color:#3A2EA8; }
    .sp-adm-btn-link-neutral   { background:transparent; color:#4B4462; padding-left:0; padding-right:0; text-decoration:underline; border:none; }
    .sp-adm-btn-link-neutral:hover:not(:disabled)   { color:#211B36; }

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
