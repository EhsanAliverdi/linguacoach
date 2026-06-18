import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KpiVariant } from '../kpi-card/sp-admin-kpi-card.component';

@Component({
  selector: 'sp-admin-stat-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!--
      TailAdmin stat/metric card pattern: rounded-2xl border border-gray-200 bg-white
      icon container: rounded-xl with brand/success/warning bg tones
      label: text-sm text-gray-500  value: text-title-sm font-semibold text-gray-800
    -->
    <article
      class="sp-adm-stat rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] flex items-center gap-4 p-5"
    >
      <div
        class="sp-adm-stat-icon rounded-xl flex items-center justify-center w-11 h-11 shrink-0"
        [class]="'sp-adm-stat-icon-' + tone"
      >
        <ng-content select="[slot=icon]" />
      </div>
      <div>
        <div class="sp-adm-stat-label text-sm text-gray-500 dark:text-gray-400">{{ label }}</div>
        <div class="sp-adm-stat-value text-2xl font-semibold text-gray-800 dark:text-white/90 leading-tight mt-0.5">{{ value }}</div>
      </div>
    </article>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    /* TailAdmin-backed: rounded-2xl border border-gray-200 bg-white stat card pattern */
    /* Icon tone bg/color — maps to TailAdmin brand/success/warning/error color tokens */
    .sp-adm-stat-icon-indigo { background: #ecf3ff; color: #465fff; }
    .sp-adm-stat-icon-green  { background: #ecfdf3; color: #16a34a; }
    .sp-adm-stat-icon-violet { background: #f5f3ff; color: #7c3aed; }
    .sp-adm-stat-icon-amber  { background: #fffbeb; color: #d97706; }
    .sp-adm-stat-icon-teal   { background: #f0fdfa; color: #0d9488; }
    .sp-adm-stat-icon-slate  { background: #f2f4f7; color: #475569; }
  `],
})
export class SpAdminStatCardComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() tone: KpiVariant = 'indigo';
}
