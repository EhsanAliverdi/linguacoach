import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminDistributionTone =
  | 'green' | 'teal' | 'indigo' | 'violet' | 'amber' | 'orange' | 'red' | 'slate';

export interface SpAdminDistributionItem {
  key: string;
  label: string;
  value: number;
  total?: number;
  percent?: number;
  tone?: SpAdminDistributionTone;
  subtitle?: string;
}

const TONE_COLORS: Record<SpAdminDistributionTone, string> = {
  green:  '#13B07C',
  teal:   '#10B5A4',
  indigo: '#5B4BE8',
  violet: '#B45CF0',
  amber:  '#D97706',
  orange: '#FF7A59',
  red:    '#EF4444',
  slate:  '#64748B',
};

const TONE_BG: Record<SpAdminDistributionTone, string> = {
  green:  '#DFF6F2',
  teal:   '#CCFBF1',
  indigo: '#EDEBFF',
  violet: '#F2E9FF',
  amber:  '#FEF3C7',
  orange: '#FFEDD5',
  red:    '#FEF2F2',
  slate:  '#F1F5F9',
};

@Component({
  selector: 'sp-admin-distribution-breakdown',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-ddb-root">
      <!-- Header -->
      <div class="sp-ddb-header">
        <span class="sp-ddb-title">{{ title }}</span>
        @if (statusLabel) {
          <span class="sp-ddb-status-badge" [class]="'sp-ddb-status-' + (statusTone || 'green')">
            <span class="sp-ddb-status-dot"></span>
            {{ statusLabel }}
          </span>
        }
      </div>

      @if (!items || items.length === 0) {
        <div class="sp-ddb-empty">{{ emptyMessage || 'No data available.' }}</div>
      } @else {
        <!-- Stacked proportion bar -->
        <div class="sp-ddb-bar" role="img" [attr.aria-label]="title + ' distribution'">
          @for (item of items; track item.key) {
            <div
              class="sp-ddb-segment"
              [style.flex]="item.value"
              [style.background]="toneColor(item.tone)"
              [attr.title]="item.label + ': ' + item.value"
            ></div>
          }
        </div>

        <!-- Level cards -->
        <div class="sp-ddb-cards" [style.grid-template-columns]="'repeat(' + items.length + ', 1fr)'">
          @for (item of items; track item.key) {
            <div class="sp-ddb-card" [style.background]="toneBg(item.tone)">
              <div class="sp-ddb-card-label-row">
                <span class="sp-ddb-dot" [style.background]="toneColor(item.tone)"></span>
                <span class="sp-ddb-card-label" [style.color]="toneColor(item.tone)">{{ item.label }}</span>
              </div>
              <div class="sp-ddb-card-value">{{ item.value }}</div>
              <div class="sp-ddb-card-pct">{{ pct(item) }}% of total</div>
              <div class="sp-ddb-card-track">
                <div class="sp-ddb-card-fill" [style.background]="toneColor(item.tone)" [style.width.%]="pct(item)"></div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .sp-ddb-root {
      display: block;
    }

    .sp-ddb-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
    }

    .sp-ddb-title {
      font-size: 11px;
      font-weight: 800;
      letter-spacing: .07em;
      text-transform: uppercase;
      color: var(--sp-admin-text-muted, #8B85A0);
    }

    .sp-ddb-status-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 12px;
      font-weight: 700;
      padding: 4px 12px;
      border-radius: 99px;
    }

    .sp-ddb-status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
      flex-shrink: 0;
    }

    .sp-ddb-status-green {
      color: var(--sp-admin-success-ink, #065F46);
      background: var(--sp-admin-success-soft, #D1FAE5);
    }

    .sp-ddb-status-amber {
      color: #92400E;
      background: #FEF3C7;
    }

    .sp-ddb-status-slate {
      color: #475569;
      background: #F1F5F9;
    }

    .sp-ddb-empty {
      font-size: 13px;
      color: var(--sp-admin-text-muted, #8B85A0);
      padding: 16px 0;
      text-align: center;
    }

    /* Stacked bar */
    .sp-ddb-bar {
      display: flex;
      height: 8px;
      border-radius: 99px;
      overflow: hidden;
      margin-bottom: 20px;
      gap: 1.5px;
    }

    .sp-ddb-segment {
      min-width: 2px;
      transition: flex .5s cubic-bezier(.4,0,.2,1);
    }

    /* Level cards grid */
    .sp-ddb-cards {
      display: grid;
      gap: 10px;
    }

    .sp-ddb-card {
      padding: 13px 14px;
      border-radius: 10px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .sp-ddb-card-label-row {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .sp-ddb-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .sp-ddb-card-label {
      font-size: 12px;
      font-weight: 800;
      letter-spacing: .04em;
    }

    .sp-ddb-card-value {
      font-size: 26px;
      font-weight: 800;
      color: var(--sp-admin-text, #211B36);
      letter-spacing: -.04em;
      line-height: 1;
    }

    .sp-ddb-card-pct {
      font-size: 11px;
      font-weight: 600;
      color: var(--sp-admin-text-muted, #8B85A0);
    }

    .sp-ddb-card-track {
      height: 3px;
      border-radius: 99px;
      background: rgba(0,0,0,.09);
      overflow: hidden;
    }

    .sp-ddb-card-fill {
      height: 100%;
      border-radius: 99px;
      transition: width .3s ease;
    }
  `],
})
export class SpAdminDistributionBreakdownComponent {
  @Input() title = '';
  @Input() items: SpAdminDistributionItem[] = [];
  @Input() total?: number;
  @Input() statusLabel?: string;
  @Input() statusTone: 'green' | 'amber' | 'slate' = 'green';
  @Input() emptyMessage?: string;

  toneColor(tone?: SpAdminDistributionTone): string {
    return tone ? (TONE_COLORS[tone] ?? TONE_COLORS.indigo) : TONE_COLORS.indigo;
  }

  toneBg(tone?: SpAdminDistributionTone): string {
    return tone ? (TONE_BG[tone] ?? TONE_BG.indigo) : TONE_BG.indigo;
  }

  pct(item: SpAdminDistributionItem): number {
    if (item.percent != null) return item.percent;
    const t = this.total ?? this.items.reduce((s, i) => s + i.value, 0);
    return t > 0 ? Math.round((item.value / t) * 100) : 0;
  }
}
