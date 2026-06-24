import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminCardVariant = 'default' | 'bordered' | 'elevated' | 'flat' | 'metric' | 'section';
export type SpAdminCardPadding = 'none' | 'sm' | 'md' | 'lg';
export type SpAdminCardRadius = 'md' | 'lg' | 'xl' | '2xl';

// TailAdmin card (shared/components/common/component-card):
// rounded-2xl border border-gray-200 bg-white
// header: px-6 py-5, body: p-4 border-t border-gray-100 sm:p-6
@Component({
  selector: 'sp-admin-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section
      class="sp-adm-card"
      [class]="hostClasses"
    >
      @if (loading) {
        <div class="sp-adm-card-loading" aria-busy="true">
          <span class="sp-adm-card-spinner" aria-hidden="true"></span>
        </div>
      }
      @if (title || hasActions) {
        <header class="sp-adm-card-header" [class.sp-adm-card-header-divider]="headerDivider">
          @if (title) {
            <h2 class="sp-adm-card-title">{{ title }}</h2>
          }
          <ng-content select="[slot=header]" />
          <ng-content select="[slot=actions]" />
        </header>
      }
      <div class="sp-adm-card-body" [class]="bodyClasses">
        <ng-content />
      </div>
    </section>
  `,
  styles: [`
    :host { display:block; min-width:0; }

    /* Base — matches .adm-card: bg white, border ECE9F5, radius 14px, shadow sh-xs */
    .sp-adm-card {
      min-width:0; position:relative; overflow:hidden;
      background: var(--sp-admin-surface, #FFFFFF);
    }

    /* Variant borders/shadows — all use the standalone card baseline */
    .sp-adm-card-default  { border:1px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-xs,0 1px 2px rgba(33,27,54,.06)); }
    .sp-adm-card-bordered { border:1.5px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-xs,0 1px 2px rgba(33,27,54,.06)); }
    .sp-adm-card-elevated { border:1px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-sm,0 2px 8px rgba(60,48,140,.07)); }
    .sp-adm-card-flat     { border:none; background:var(--sp-admin-surface-subtle,#FBFAFE) !important; box-shadow:none; }
    .sp-adm-card-metric   { border:1px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-xs,0 1px 2px rgba(33,27,54,.06)); }
    .sp-adm-card-section  { border:none; border-top:2px solid var(--sp-admin-primary,#5B4BE8); box-shadow:none; }
    .sp-adm-card-dashed   { border-style:dashed; }

    /* Radius — standalone card is 14px */
    .sp-adm-card-radius-md  { border-radius:14px; }
    .sp-adm-card-radius-lg  { border-radius:18px; }
    .sp-adm-card-radius-xl  { border-radius:22px; }
    .sp-adm-card-radius-2xl { border-radius:22px; }

    /* Hover */
    .sp-adm-card-hover { cursor:pointer; transition:box-shadow .15s,border-color .15s; }
    .sp-adm-card-hover:hover { box-shadow:var(--sp-admin-shadow-sm,0 2px 8px rgba(60,48,140,.07)); border-color:var(--sp-admin-primary-focus,#C0BAF9); }

    /* Header — matches .adm-card-header: flex, space-between, margin-bottom:16px, padding 20px */
    .sp-adm-card-header {
      display:flex; align-items:center; justify-content:space-between; gap:12px;
      padding:20px 20px 0;
      margin-bottom:16px;
    }
    .sp-adm-card-header-divider { border-bottom:1px solid var(--sp-admin-border,#ECE9F5); padding-bottom:16px; margin-bottom:0; }

    /* Title — matches .adm-card-title: 13.5px/700/ink */
    .sp-adm-card-title { margin:0; font-size:13.5px; font-weight:700; color:var(--sp-admin-text,#211B36); line-height:1.3; }

    /* Body padding — standalone .adm-card-p: padding 20px */
    .sp-adm-card-body-none { padding:0; }
    .sp-adm-card-body-sm   { padding:14px 16px; }
    .sp-adm-card-body-md   { padding:20px; }
    .sp-adm-card-body-lg   { padding:24px; }

    /* Body border when header present — use a 16px top gap not a border */
    .sp-adm-card-body-bordered { padding-top:0; }

    /* Loading overlay */
    .sp-adm-card-loading {
      position:absolute; inset:0; display:flex; align-items:center; justify-content:center;
      background:rgba(255,255,255,.75); z-index:10; border-radius:inherit;
    }
    .sp-adm-card-spinner {
      width:24px; height:24px; border-radius:50%;
      border:3px solid var(--sp-admin-border,#ECE9F5); border-top-color:var(--sp-admin-primary,#5B4BE8);
      animation:sp-adm-card-spin .7s linear infinite;
    }
    @keyframes sp-adm-card-spin { to { transform:rotate(360deg); } }
  `],
})
export class SpAdminCardComponent {
  @Input() title = '';
  @Input() variant: SpAdminCardVariant = 'default';
  @Input() padding: SpAdminCardPadding = 'md';
  @Input() radius: SpAdminCardRadius = 'md';
  @Input() headerDivider = false;
  @Input() hover = false;
  @Input() loading = false;
  @Input() dashed = false;
  /** @deprecated use variant='flat' or padding='sm' */
  @Input() set tight(v: boolean) { if (v) this.padding = 'sm'; }

  get hasActions(): boolean { return false; }

  get hostClasses(): string {
    const cls = [
      `sp-adm-card-${this.variant}`,
      `sp-adm-card-radius-${this.radius}`,
    ];
    if (this.hover) cls.push('sp-adm-card-hover');
    if (this.dashed) cls.push('sp-adm-card-dashed');
    return cls.join(' ');
  }

  get bodyClasses(): string {
    const cls = [`sp-adm-card-body-${this.padding}`];
    if (this.title) cls.push('sp-adm-card-body-bordered');
    return cls.join(' ');
  }
}
