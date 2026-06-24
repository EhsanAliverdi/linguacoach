import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface HeroColumn {
  label: string;
  value: string;
  sub?: string;
  valueColor?: string;
  subColor?: string;
}

/**
 * Dark gradient 4-column banner used at the top of the admin dashboard.
 * Renders equal columns separated by rgba white dividers.
 */
@Component({
  selector: 'sp-admin-hero-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-hero">
      @for (col of columns; track col.label; let i = $index; let last = $last) {
        <div class="sp-hero-col" [class.sp-hero-col--last]="last">
          <div class="sp-hero-eyebrow">{{ col.label }}</div>
          <div class="sp-hero-value" [style.color]="col.valueColor || '#fff'">{{ col.value }}</div>
          @if (col.sub) {
            <div class="sp-hero-sub" [style.color]="col.subColor || 'rgba(255,255,255,.5)'">{{ col.sub }}</div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .sp-hero {
      background: linear-gradient(135deg, var(--sp-dash-hero-bg-start,#211B36) 0%, var(--sp-dash-hero-bg-end,#2D2455) 100%);
      border-radius: 16px;
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 0;
    }
    .sp-hero-col {
      padding: 18px 22px;
      border-right: 1px solid var(--sp-dash-hero-divider, rgba(255,255,255,.1));
    }
    .sp-hero-col--last { border-right: none; }
    .sp-hero-eyebrow {
      font-size: 11px; font-weight: 800;
      letter-spacing: .09em; text-transform: uppercase;
      color: var(--sp-dash-hero-eyebrow, rgba(255,255,255,.45));
      margin-bottom: 8px;
    }
    .sp-hero-value { font-size: 22px; font-weight: 800; letter-spacing: -.03em; line-height: 1; }
    .sp-hero-sub { font-size: 12px; margin-top: 6px; font-weight: 600; }
  `],
})
export class SpAdminHeroSummaryComponent {
  @Input() columns: HeroColumn[] = [];
}
