import { Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface MiniBarItem {
  label: string;
  value: number;
  /** Optional ISO date string — shown in tooltip */
  date?: string;
}

export type MiniBarTone = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate';

@Component({
  selector: 'sp-admin-mini-bar-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-mbc-root" [attr.aria-label]="ariaLabel || title || 'Bar chart'">
      @if (title) {
        <div class="sp-mbc-title">{{ title }}</div>
      }
      @if (!items || items.length === 0) {
        <div class="sp-mbc-empty">No data</div>
      } @else {
        <div class="sp-mbc-bars" [style.height.px]="height">
          @for (bar of scaledBars(); track bar.label + $index) {
            <div
              class="sp-mbc-bar-col"
              [title]="bar.tooltip"
              [attr.aria-label]="bar.tooltip"
            >
              <div class="sp-mbc-bar-wrap">
                <div
                  class="sp-mbc-bar sp-mbc-bar-{{ tone }}"
                  [style.height.%]="bar.heightPct"
                ></div>
              </div>
              @if (showLabels) {
                <div class="sp-mbc-label">{{ bar.shortLabel }}</div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .sp-mbc-root { display: block; min-width: 0; width: 100%; }
    .sp-mbc-title {
      font-size: 11px; font-weight: 800; color: var(--sp-admin-text-muted, #8B85A0);
      text-transform: uppercase; letter-spacing: .08em; margin-bottom: 8px;
    }
    .sp-mbc-empty {
      font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0);
      padding: 12px 0; text-align: center;
    }
    .sp-mbc-bars {
      display: flex; align-items: flex-end; gap: 3px; width: 100%;
    }
    .sp-mbc-bar-col {
      flex: 1; display: flex; flex-direction: column; align-items: center; min-width: 0;
    }
    .sp-mbc-bar-wrap {
      width: 100%; flex: 1; display: flex; align-items: flex-end;
    }
    .sp-mbc-bar {
      width: 100%; min-height: 2px; border-radius: 3px 3px 0 0;
      transition: height .2s ease;
    }
    .sp-mbc-bar-indigo { background: var(--sp-admin-primary, #5B4BE8); opacity: .85; }
    .sp-mbc-bar-green  { background: var(--sp-admin-green, #13B07C); opacity: .85; }
    .sp-mbc-bar-violet { background: var(--sp-admin-violet, #7C3AED); opacity: .85; }
    .sp-mbc-bar-amber  { background: var(--sp-admin-amber, #D97706); opacity: .85; }
    .sp-mbc-bar-teal   { background: var(--sp-admin-teal, #0D9488); opacity: .85; }
    .sp-mbc-bar-slate  { background: var(--sp-admin-slate, #475569); opacity: .85; }
    .sp-mbc-label {
      font-size: 9px; color: var(--sp-admin-text-muted, #8B85A0);
      margin-top: 3px; text-align: center; white-space: nowrap;
      overflow: hidden; text-overflow: ellipsis; max-width: 100%;
    }
  `],
})
export class SpAdminMiniBarChartComponent {
  @Input() items: MiniBarItem[] = [];
  @Input() tone: MiniBarTone = 'indigo';
  @Input() height = 56;
  @Input() showLabels = true;
  @Input() title = '';
  @Input() ariaLabel = '';

  scaledBars() {
    if (!this.items || this.items.length === 0) return [];
    const max = Math.max(...this.items.map(i => i.value), 1);
    return this.items.map(item => ({
      label: item.label,
      shortLabel: item.label.length > 4 ? item.label.slice(-3) : item.label,
      heightPct: Math.max((item.value / max) * 100, item.value > 0 ? 4 : 0),
      tooltip: item.date
        ? `${item.date}: ${item.value}`
        : `${item.label}: ${item.value}`,
    }));
  }
}
