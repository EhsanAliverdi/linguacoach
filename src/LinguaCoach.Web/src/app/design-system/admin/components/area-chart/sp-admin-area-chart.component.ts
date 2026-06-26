import { Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-area-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-ac-root" [attr.aria-label]="ariaLabel || 'Area chart'">
      @if (!data || data.length < 2) {
        <div class="sp-ac-empty">{{ emptyMessage || 'No data for this period' }}</div>
      } @else {
        <svg [attr.viewBox]="viewBox()" style="width:100%;display:block;overflow:visible"
          [attr.height]="height">
          <defs>
            <linearGradient [id]="gradId" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" [attr.stop-color]="color" stop-opacity="0.18"/>
              <stop offset="100%" [attr.stop-color]="color" stop-opacity="0"/>
            </linearGradient>
          </defs>
          <!-- Y-axis gridlines and labels -->
          @for (tick of yTicks(); track tick.v) {
            <line [attr.x1]="padL" [attr.y1]="tick.y" [attr.x2]="VW - padR" [attr.y2]="tick.y"
              stroke="#ECE9F5" stroke-width="1"/>
            <text [attr.x]="padL - 6" [attr.y]="tick.y + 4" text-anchor="end" font-size="10"
              fill="#8B85A0" font-family="'Plus Jakarta Sans',sans-serif">{{ tick.label }}</text>
          }
          <!-- Area fill -->
          <path [attr.d]="areaPath()" [attr.fill]="'url(#' + gradId + ')'" />
          <!-- Line -->
          <path [attr.d]="linePath()" fill="none" [attr.stroke]="color" stroke-width="2.5"
            stroke-linecap="round" stroke-linejoin="round"/>
          <!-- End dot -->
          @if (endPt(); as ep) {
            <circle [attr.cx]="ep[0]" [attr.cy]="ep[1]" r="5"
              [attr.fill]="color" stroke="#fff" stroke-width="2.5"/>
          }
          <!-- X-axis labels -->
          @for (lbl of xLabels(); track lbl.i) {
            <text [attr.x]="lbl.x" [attr.y]="height - 4" text-anchor="middle" font-size="10"
              fill="#8B85A0" font-family="'Plus Jakarta Sans',sans-serif">{{ lbl.label }}</text>
          }
        </svg>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-ac-empty {
      min-height: 160px; display: flex; align-items: center; justify-content: center;
      font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0);
      background: var(--sp-admin-bg, #F6F4FB); border-radius: 8px;
    }
  `],
})
export class SpAdminAreaChartComponent {
  @Input() data: number[] = [];
  @Input() labels: string[] = [];
  @Input() color = '#5B4BE8';
  @Input() height = 200;
  @Input() prefix = '$';
  @Input() ariaLabel = '';
  @Input() emptyMessage = '';

  readonly gradId = 'sp-ac-grad-' + Math.random().toString(36).slice(2, 8);

  readonly VW = 600;
  readonly padL = 48;
  readonly padR = 16;
  readonly padT = 20;
  readonly padB = 28;

  viewBox() {
    return `0 0 ${this.VW} ${this.height}`;
  }

  private get plotW() { return this.VW - this.padL - this.padR; }
  private get plotH() { return this.height - this.padT - this.padB; }

  private get max() { return Math.max(...this.data) * 1.15 || 1; }

  private xS(i: number): number {
    return this.padL + (i / (this.data.length - 1)) * this.plotW;
  }

  private yS(v: number): number {
    return this.padT + this.plotH - (v / this.max) * this.plotH;
  }

  private get pts(): [number, number][] {
    return this.data.map((v, i) => [this.xS(i), this.yS(v)]);
  }

  linePath(): string {
    return this.pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
  }

  areaPath(): string {
    const line = this.linePath();
    const last = this.pts[this.pts.length - 1];
    return `${line} L${last[0].toFixed(1)},${(this.padT + this.plotH).toFixed(1)} L${this.padL},${(this.padT + this.plotH).toFixed(1)} Z`;
  }

  endPt(): [number, number] | null {
    if (!this.pts.length) return null;
    return this.pts[this.pts.length - 1];
  }

  yTicks(): { v: number; y: number; label: string }[] {
    const m = this.max;
    return [0, m * 0.25, m * 0.5, m * 0.75, m].map(v => ({
      v,
      y: this.yS(v),
      label: `${this.prefix}${v.toFixed(2)}`,
    }));
  }

  xLabels(): { i: number; x: number; label: string }[] {
    if (!this.labels?.length) return [];
    return this.labels
      .map((l, i) => ({ i, label: l, x: this.xS(i) }))
      .filter(item => !!item.label);
  }
}
