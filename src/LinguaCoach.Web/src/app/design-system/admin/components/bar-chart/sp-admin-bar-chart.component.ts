import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-bar-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-bc-root" [attr.aria-label]="ariaLabel || 'Bar chart'">
      @if (!data || data.length === 0) {
        <div class="sp-bc-empty">{{ emptyMessage || 'No data for this period' }}</div>
      } @else {
        <svg [attr.viewBox]="viewBox()" style="width:100%;display:block" [attr.height]="height + 22">
          @for (bar of bars(); track bar.i) {
            <g>
              <rect [attr.x]="bar.x" [attr.y]="bar.y" [attr.width]="barW" [attr.height]="bar.h"
                rx="5" [attr.fill]="bar.isMax ? color : color + '55'" />
              @if (bar.v > 0) {
                <text
                  [attr.x]="bar.x + barW / 2"
                  [attr.y]="bar.y - 6"
                  text-anchor="middle"
                  [attr.font-size]="9.5"
                  [attr.font-weight]="bar.isMax ? '800' : '700'"
                  [attr.fill]="bar.isMax ? color : '#8B85A0'"
                  font-family="'Plus Jakarta Sans',sans-serif">{{ bar.v }}</text>
              }
              @if (bar.label) {
                <text
                  [attr.x]="bar.x + barW / 2"
                  [attr.y]="height + 16"
                  text-anchor="middle"
                  font-size="9.5"
                  fill="#8B85A0"
                  font-family="'Plus Jakarta Sans',sans-serif">{{ bar.label }}</text>
              }
            </g>
          }
        </svg>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-bc-empty {
      min-height: 120px; display: flex; align-items: center; justify-content: center;
      font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0);
      background: var(--sp-admin-bg, #F6F4FB); border-radius: 8px;
    }
  `],
})
export class SpAdminBarChartComponent {
  @Input() data: number[] = [];
  @Input() labels: string[] = [];
  @Input() color = '#5B4BE8';
  @Input() height = 160;
  @Input() ariaLabel = '';
  @Input() emptyMessage = '';

  readonly barW = 24;
  readonly gap = 8;

  viewBox(): string {
    const vw = this.data.length * (this.barW + this.gap) - this.gap + 4;
    return `0 0 ${vw} ${this.height + 22}`;
  }

  bars(): { i: number; v: number; x: number; y: number; h: number; isMax: boolean; label: string }[] {
    const max = Math.max(...this.data) || 1;
    return this.data.map((v, i) => {
      const h = Math.max(3, (v / max) * this.height);
      return {
        i,
        v,
        x: i * (this.barW + this.gap),
        y: this.height - h,
        h,
        isMax: v === max,
        label: this.labels?.[i] ?? '',
      };
    });
  }
}
