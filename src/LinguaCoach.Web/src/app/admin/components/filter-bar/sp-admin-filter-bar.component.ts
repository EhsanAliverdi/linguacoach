import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminFilterBarLayout = 'inline' | 'stacked' | 'responsive';
export type SpAdminFilterBarDensity = 'compact' | 'comfortable';

/**
 * Admin filter bar wrapper.
 * TailAdmin pattern: flex items-end justify-between gap-3 flex-wrap mb-4
 *
 * Named slots: [search] [filters] [actions]
 * Backward-compat: general <ng-content /> goes into left group.
 */
@Component({
  selector: 'sp-admin-filter-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-filter" [class]="hostClasses">
      <div class="sp-adm-filter-left flex items-end flex-wrap flex-1 min-w-0" [class.gap-2]="density === 'compact'" [class.gap-3]="density === 'comfortable'">
        <ng-content select="[search]" />
        <ng-content select="[filters]" />
        <ng-content />
      </div>
      <div class="sp-adm-filter-right flex items-center shrink-0" [class.gap-2]="density === 'compact'" [class.gap-2]="density === 'comfortable'">
        <ng-content select="[actions]" />
      </div>
    </div>
  `,
  styles: [`
    /* TailAdmin-backed: flex items-end justify-between gap-3 filter bar */
    .sp-adm-filter          { display:flex; align-items:flex-end; justify-content:space-between; flex-wrap:wrap; }
    .sp-adm-filter-compact  { gap:8px; margin-bottom:12px; }
    .sp-adm-filter-comfortable { gap:12px; margin-bottom:16px; }
    .sp-adm-filter-inline   { flex-direction:row; flex-wrap:nowrap; align-items:center; }
    .sp-adm-filter-stacked  { flex-direction:column; align-items:flex-start; }
    .sp-adm-filter-responsive { flex-direction:row; flex-wrap:wrap; }
  `],
})
export class SpAdminFilterBarComponent {
  @Input() layout: SpAdminFilterBarLayout = 'responsive';
  @Input() density: SpAdminFilterBarDensity = 'comfortable';

  get hostClasses(): string {
    return `sp-adm-filter-${this.density} sp-adm-filter-${this.layout}`;
  }
}
