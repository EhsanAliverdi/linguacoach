import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KpiVariant } from '../kpi-card/sp-admin-kpi-card.component';

export type SpAdminStatCardTone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate';
export type SpAdminStatCardSize = 'sm' | 'md' | 'lg';

// TailAdmin stat/metric card: rounded-2xl border border-gray-200 bg-white
// icon: rounded-xl with brand/success/warning bg tones
@Component({
  selector: 'sp-admin-stat-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <article
      class="sp-adm-stat rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]"
      [class]="hostClasses"
    >
      @if (loading) {
        <div class="sp-adm-stat-skeleton" aria-busy="true">
          <div class="sp-adm-stat-icon-skel rounded-xl"></div>
          <div class="sp-adm-stat-text-skel"></div>
        </div>
      } @else {
        <div
          class="sp-adm-stat-icon rounded-xl flex items-center justify-center shrink-0"
          [class]="iconClass"
        >
          <ng-content select="[slot=icon]" />
        </div>
        <div class="sp-adm-stat-content">
          <div class="sp-adm-stat-label text-gray-500 dark:text-gray-400">{{ label }}</div>
          <div class="sp-adm-stat-value font-semibold text-gray-800 dark:text-white/90 leading-tight mt-0.5">{{ value }}</div>
          <ng-content select="[slot=trend]" />
        </div>
      }
    </article>
  `,
  styles: [`
    :host { display:block; min-width:0; }
    /* TailAdmin-backed: rounded-2xl border border-gray-200 bg-white stat card */

    /* Sizes */
    .sp-adm-stat-sm  { flex-direction:column; gap:8px; padding:14px; }
    .sp-adm-stat-sm .sp-adm-stat-icon  { width:36px; height:36px; }
    .sp-adm-stat-sm .sp-adm-stat-label { font-size:11px; }
    .sp-adm-stat-sm .sp-adm-stat-value { font-size:18px; }

    .sp-adm-stat-md  { flex-direction:row; gap:14px; padding:18px; }
    .sp-adm-stat-md .sp-adm-stat-icon  { width:44px; height:44px; }
    .sp-adm-stat-md .sp-adm-stat-label { font-size:13px; }
    .sp-adm-stat-md .sp-adm-stat-value { font-size:24px; }

    .sp-adm-stat-lg  { flex-direction:row; gap:18px; padding:24px; }
    .sp-adm-stat-lg .sp-adm-stat-icon  { width:56px; height:56px; }
    .sp-adm-stat-lg .sp-adm-stat-label { font-size:14px; }
    .sp-adm-stat-lg .sp-adm-stat-value { font-size:32px; }

    .sp-adm-stat { display:flex; align-items:center; }

    /* Icon tone backgrounds — maps to TailAdmin brand/success/warning/error tokens */
    .sp-adm-stat-icon-indigo  { background:#ecf3ff; color:#465fff; }
    .sp-adm-stat-icon-primary { background:#ecf3ff; color:#465fff; }
    .sp-adm-stat-icon-green   { background:#ecfdf3; color:#16a34a; }
    .sp-adm-stat-icon-success { background:#ecfdf3; color:#16a34a; }
    .sp-adm-stat-icon-violet  { background:#f5f3ff; color:#7c3aed; }
    .sp-adm-stat-icon-amber   { background:#fffbeb; color:#d97706; }
    .sp-adm-stat-icon-warning { background:#fffbeb; color:#d97706; }
    .sp-adm-stat-icon-teal    { background:#f0fdfa; color:#0d9488; }
    .sp-adm-stat-icon-info    { background:#f0f9ff; color:#0ba5ec; }
    .sp-adm-stat-icon-slate   { background:#f2f4f7; color:#475569; }
    .sp-adm-stat-icon-neutral { background:#f2f4f7; color:#475569; }
    .sp-adm-stat-icon-danger  { background:#fef2f2; color:#ef4444; }

    /* Skeleton */
    .sp-adm-stat-skeleton { display:flex; align-items:center; gap:14px; width:100%; }
    .sp-adm-stat-icon-skel { width:44px; height:44px; background:#f2f4f7; flex-shrink:0; animation:sp-adm-stat-pulse 1.4s ease infinite; }
    .sp-adm-stat-text-skel { height:36px; flex:1; border-radius:6px; background:#f2f4f7; animation:sp-adm-stat-pulse 1.4s ease infinite; }
    @keyframes sp-adm-stat-pulse { 0%,100% { opacity:1; } 50% { opacity:.5; } }
  `],
})
export class SpAdminStatCardComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() loading = false;
  @Input() size: SpAdminStatCardSize = 'md';

  // Accept both the legacy KpiVariant names and the new semantic tone names
  @Input() tone: SpAdminStatCardTone | KpiVariant = 'indigo';

  get hostClasses(): string {
    return `sp-adm-stat-${this.size}`;
  }

  get iconClass(): string {
    return `sp-adm-stat-icon-${this.tone}`;
  }
}
