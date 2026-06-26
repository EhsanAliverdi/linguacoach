import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-heatmap',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-hm-root" [attr.aria-label]="ariaLabel || 'Activity heatmap'">
      @if (!data || data.length === 0) {
        <div class="sp-hm-empty"><ng-content /></div>
      } @else {
        <div class="sp-hm-scroll">
          <svg [attr.width]="totalW()" [attr.height]="totalH() + 4" style="display:block">
            @for (day of dayLabels; track day; let di = $index) {
              <text [attr.x]="0" [attr.y]="di * (cellSz + gap) + cellSz - 2"
                font-size="9.5" fill="#8B85A0" font-family="'Plus Jakarta Sans',sans-serif">{{ day }}</text>
            }
            @for (row of data; track $index; let di = $index) {
              @for (v of row; track $index; let wi = $index) {
                <rect
                  [attr.x]="labelW + wi * (cellSz + gap)"
                  [attr.y]="di * (cellSz + gap)"
                  [attr.width]="cellSz"
                  [attr.height]="cellSz"
                  rx="3"
                  [attr.fill]="v === 0 ? '#ECE9F5' : '#5B4BE8'"
                  [attr.opacity]="v === 0 ? 1 : (0.15 + (v / maxVal()) * 0.85)" />
              }
            }
          </svg>
        </div>
        <div class="sp-hm-footer">
          <span class="sp-hm-legend-label">12 weeks ago</span>
          <div class="sp-hm-legend">
            <span class="sp-hm-legend-label">Less</span>
            @for (swatch of legendSwatches; track swatch.opacity) {
              <div class="sp-hm-swatch"
                [style.background]="swatch.opacity === 0 ? '#ECE9F5' : '#5B4BE8'"
                [style.opacity]="swatch.opacity === 0 ? 1 : swatch.opacity"></div>
            }
            <span class="sp-hm-legend-label">More</span>
          </div>
          <span class="sp-hm-legend-label">This week</span>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-hm-empty { font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0); padding: 8px 0; }
    .sp-hm-root { display: flex; flex-direction: column; gap: 8px; }
    .sp-hm-scroll { overflow-x: auto; }
    .sp-hm-footer {
      display: flex; align-items: center; justify-content: space-between;
      font-size: 10px; color: #8B85A0; margin-top: 4px;
    }
    .sp-hm-legend { display: flex; align-items: center; gap: 4px; }
    .sp-hm-legend-label { font-size: 10px; color: #8B85A0; white-space: nowrap; }
    .sp-hm-swatch { width: 12px; height: 12px; border-radius: 3px; }
  `],
})
export class SpAdminHeatmapComponent {
  @Input() data: number[][] = [];
  @Input() ariaLabel = '';

  readonly dayLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly cellSz = 14;
  readonly gap = 3;
  readonly labelW = 28;
  readonly legendSwatches = [0, 0.2, 0.4, 0.65, 0.9].map(o => ({ opacity: o }));

  maxVal(): number {
    if (!this.data?.length) return 1;
    return Math.max(...this.data.flat()) || 1;
  }

  totalW(): number {
    const cols = this.data[0]?.length ?? 0;
    return this.labelW + cols * (this.cellSz + this.gap);
  }

  totalH(): number {
    return this.dayLabels.length * (this.cellSz + this.gap);
  }
}
