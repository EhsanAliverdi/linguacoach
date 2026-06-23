import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface BreakdownBarItem {
  label: string;
  value: number;
  /** 0–100 */
  pct: number;
  tone?: 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate' | 'danger';
  badge?: string;
}

@Component({
  selector: 'sp-admin-breakdown-bars',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-bdb-root" [attr.aria-label]="ariaLabel || title || 'Breakdown'">
      @if (title) {
        <div class="sp-bdb-title">{{ title }}</div>
      }
      @if (!items || items.length === 0) {
        <div class="sp-bdb-empty">No data</div>
      } @else {
        @for (item of items; track item.label) {
          <div class="sp-bdb-row">
            <div class="sp-bdb-meta">
              <span class="sp-bdb-label">{{ item.label }}</span>
              <span class="sp-bdb-value">{{ item.value }}</span>
            </div>
            <div class="sp-bdb-track">
              <div
                class="sp-bdb-fill sp-bdb-fill-{{ item.tone || 'indigo' }}"
                [style.width.%]="item.pct"
                [attr.aria-valuenow]="item.pct"
                [attr.aria-valuemin]="0"
                [attr.aria-valuemax]="100"
                role="progressbar"
                [attr.aria-label]="item.label + ': ' + item.pct + '%'"
              ></div>
            </div>
            @if (showPct) {
              <span class="sp-bdb-pct">{{ item.pct | number:'1.0-0' }}%</span>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .sp-bdb-root { display: block; min-width: 0; }
    .sp-bdb-title {
      font-size: 11px; font-weight: 800; color: var(--sp-admin-text-muted, #8B85A0);
      text-transform: uppercase; letter-spacing: .08em; margin-bottom: 10px;
    }
    .sp-bdb-empty { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); padding: 8px 0; }
    .sp-bdb-row { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
    .sp-bdb-row:last-child { margin-bottom: 0; }
    .sp-bdb-meta { display: flex; justify-content: space-between; width: 110px; flex-shrink: 0; gap: 4px; }
    .sp-bdb-label { font-size: 12px; font-weight: 600; color: var(--sp-admin-text-secondary, #4B4462); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .sp-bdb-value { font-size: 12px; font-weight: 700; color: var(--sp-admin-text, #211B36); flex-shrink: 0; }
    .sp-bdb-track { flex: 1; height: 6px; background: var(--sp-admin-border, #ECE9F5); border-radius: 99px; overflow: hidden; }
    .sp-bdb-fill { height: 100%; border-radius: 99px; transition: width .3s ease; min-width: 2px; }
    .sp-bdb-fill-indigo { background: var(--sp-admin-primary, #5B4BE8); }
    .sp-bdb-fill-green  { background: var(--sp-admin-green, #13B07C); }
    .sp-bdb-fill-violet { background: var(--sp-admin-violet, #7C3AED); }
    .sp-bdb-fill-amber  { background: var(--sp-admin-amber, #D97706); }
    .sp-bdb-fill-teal   { background: var(--sp-admin-teal, #0D9488); }
    .sp-bdb-fill-slate  { background: var(--sp-admin-slate, #475569); }
    .sp-bdb-fill-danger { background: var(--sp-admin-danger, #EF4444); }
    .sp-bdb-pct { font-size: 11px; font-weight: 700; color: var(--sp-admin-text-muted, #8B85A0); width: 32px; text-align: right; flex-shrink: 0; }
  `],
})
export class SpAdminBreakdownBarsComponent {
  @Input() items: BreakdownBarItem[] = [];
  @Input() title = '';
  @Input() ariaLabel = '';
  @Input() showPct = true;
}
