import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DonutSegment {
  label: string;
  pct: number;
  color: string;
}

export interface ComputedSegment extends DonutSegment {
  dashArray: string;
  dashOffset: number;
}

/**
 * SVG donut chart + legend. r=36, strokeWidth=14, viewBox 0 0 100 100.
 * Used for "AI cost by type" on the dashboard.
 */
@Component({
  selector: 'sp-admin-donut-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-donut-title">{{ title }}</div>
    <div class="sp-donut-body">
      <svg [attr.viewBox]="'0 0 100 100'" [attr.width]="size" [attr.height]="size" style="flex-shrink:0">
        @for (seg of computed; track seg.label) {
          <circle cx="50" cy="50" r="36" fill="none"
            [attr.stroke]="seg.color"
            stroke-width="14"
            [attr.stroke-dasharray]="seg.dashArray"
            [attr.stroke-dashoffset]="seg.dashOffset"/>
        }
        <circle cx="50" cy="50" r="22" fill="white"/>
      </svg>
      <div class="sp-donut-legend">
        @for (seg of computed; track seg.label) {
          <div class="sp-donut-legend-row">
            <div class="sp-donut-swatch" [style.background]="seg.color"></div>
            <span class="sp-donut-legend-label">{{ seg.label }}</span>
            <span class="sp-donut-legend-pct">{{ seg.pct }}%</span>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .sp-donut-title { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text,#211B36); margin-bottom: 14px; }
    .sp-donut-body { display: flex; align-items: center; gap: 16px; }
    .sp-donut-legend { flex: 1; display: flex; flex-direction: column; gap: 7px; }
    .sp-donut-legend-row { display: flex; align-items: center; gap: 7px; }
    .sp-donut-swatch { width: 9px; height: 9px; border-radius: 3px; flex-shrink: 0; }
    .sp-donut-legend-label { flex: 1; font-size: 12px; color: var(--sp-admin-text-secondary,#4B4462); }
    .sp-donut-legend-pct { font-size: 12px; font-weight: 800; color: var(--sp-admin-text,#211B36); }
  `],
})
export class SpAdminDonutChartComponent implements OnChanges {
  @Input() title = '';
  @Input() segments: DonutSegment[] = [];
  @Input() size = 80;

  computed: ComputedSegment[] = [];

  ngOnChanges(): void {
    const circ = 2 * Math.PI * 36;
    let cum = 0;
    this.computed = this.segments.map(s => {
      const offset = circ * (0.25 - cum);
      cum += s.pct / 100;
      return { ...s, dashArray: `${(s.pct / 100) * circ} ${circ}`, dashOffset: offset };
    });
  }
}
