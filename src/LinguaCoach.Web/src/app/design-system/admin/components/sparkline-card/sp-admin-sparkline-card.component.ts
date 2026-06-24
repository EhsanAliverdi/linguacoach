import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Metric card with inline SVG sparkline on the right.
 * Used for "AI spend (30d)" in the dashboard metric strip.
 */
@Component({
  selector: 'sp-admin-sparkline-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-sparkcard">
      <div class="sp-sparkcard-meta">
        <div class="sp-sparkcard-title">{{ title }}</div>
        <div class="sp-sparkcard-value">{{ value }}</div>
        @if (sub) {
          <div class="sp-sparkcard-sub">{{ sub }}</div>
        }
      </div>
      @if (linePath) {
        <svg [attr.width]="sparkW" [attr.height]="sparkH"
          [attr.viewBox]="'0 0 ' + sparkW + ' ' + sparkH"
          style="display:block;flex-shrink:0">
          <path [attr.d]="linePath" fill="none"
            [attr.stroke]="color" stroke-width="1.75" stroke-linecap="round"/>
          <circle [attr.cx]="lastPt[0]" [attr.cy]="lastPt[1]" r="2.5"
            [attr.fill]="color" stroke="white" stroke-width="1.5"/>
        </svg>
      }
    </div>
  `,
  styles: [`
    .sp-sparkcard { display: flex; align-items: center; gap: 14px; }
    .sp-sparkcard-meta { flex: 1; }
    .sp-sparkcard-title { font-size: 13px; font-weight: 700; color: var(--sp-admin-text,#211B36); margin-bottom: 5px; }
    .sp-sparkcard-value { font-size: 22px; font-weight: 800; color: var(--sp-admin-text,#211B36); letter-spacing: -.03em; }
    .sp-sparkcard-sub { font-size: 12px; color: var(--sp-admin-text-muted,#8B85A0); margin-top: 4px; }
  `],
})
export class SpAdminSparklineCardComponent implements OnChanges {
  @Input() title = '';
  @Input() value = '';
  @Input() sub: string | null = null;
  @Input() data: number[] = [];
  @Input() color = '#F0982C';
  @Input() sparkW = 80;
  @Input() sparkH = 32;

  linePath = '';
  lastPt: [number, number] = [0, 0];

  ngOnChanges(): void {
    const d = this.data;
    if (d.length < 2) { this.linePath = ''; return; }
    const min = Math.min(...d); const max = Math.max(...d); const r = max - min || 1;
    const W = this.sparkW; const H = this.sparkH;
    const pts: [number, number][] = d.map((v, i) => [
      (i / (d.length - 1)) * W,
      H - ((v - min) / r) * H * 0.8 - H * 0.1,
    ]);
    this.linePath = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)},${y.toFixed(1)}`).join('');
    this.lastPt = pts[pts.length - 1];
  }
}
