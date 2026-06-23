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
      class="sp-adm-card bg-white dark:bg-white/[0.03]"
      [class]="hostClasses"
    >
      @if (loading) {
        <div class="sp-adm-card-loading" aria-busy="true">
          <span class="sp-adm-card-spinner" aria-hidden="true"></span>
        </div>
      }
      @if (title || hasActions) {
        <header class="sp-adm-card-header px-6 py-5" [class.sp-adm-card-header-divider]="headerDivider">
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

    /* Base */
    .sp-adm-card { min-width:0; position:relative; overflow:hidden; }

    /* Variant borders/shadows */
    .sp-adm-card-default   { border:1px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-card-bordered  { border:2px solid var(--sp-admin-border,#ECE9F5); }
    .sp-adm-card-elevated  { border:1px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-card,0 4px 12px rgba(60,48,140,.07)); }
    .sp-adm-card-flat      { border:none; background:var(--sp-admin-surface-subtle,#FBFAFE) !important; }
    .sp-adm-card-metric    { border:1px solid var(--sp-admin-border,#ECE9F5); box-shadow:var(--sp-admin-shadow-card,0 1px 3px rgba(60,48,140,.05)); }
    .sp-adm-card-section   { border:none; border-top:2px solid var(--sp-admin-primary,#5B4BE8); }
    .sp-adm-card-dashed    { border-style:dashed; }

    /* Radius */
    .sp-adm-card-radius-md  { border-radius:var(--sp-admin-radius-md,14px); }
    .sp-adm-card-radius-lg  { border-radius:var(--sp-admin-radius-lg,18px); }
    .sp-adm-card-radius-xl  { border-radius:var(--sp-admin-radius-xl,22px); }
    .sp-adm-card-radius-2xl { border-radius:var(--sp-admin-radius-xl,22px); }

    /* Hover */
    .sp-adm-card-hover { cursor:pointer; transition:box-shadow .15s,border-color .15s; }
    .sp-adm-card-hover:hover { box-shadow:0 4px 16px rgba(91,75,232,.12); border-color:var(--sp-admin-primary-focus,#C0BAF9); }

    /* Header */
    .sp-adm-card-header {
      display:flex; align-items:center; justify-content:space-between; gap:12px;
    }
    .sp-adm-card-header-divider { border-bottom:1px solid var(--sp-admin-border-subtle,#F4F2FC); }
    .sp-adm-card-title { margin:0; font-size:13.5px; font-weight:700; color:var(--sp-admin-text,#211B36); }

    /* Body padding */
    .sp-adm-card-body-none { padding:0; }
    .sp-adm-card-body-sm   { padding:12px 16px; }
    .sp-adm-card-body-md   { padding:16px 24px; }
    .sp-adm-card-body-lg   { padding:24px 32px; }

    /* Body border (when header present) */
    .sp-adm-card-body-bordered { border-top:1px solid var(--sp-admin-border-subtle,#F4F2FC); }

    /* Loading overlay */
    .sp-adm-card-loading {
      position:absolute; inset:0; display:flex; align-items:center; justify-content:center;
      background:rgba(255,255,255,.75); z-index:10;
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
