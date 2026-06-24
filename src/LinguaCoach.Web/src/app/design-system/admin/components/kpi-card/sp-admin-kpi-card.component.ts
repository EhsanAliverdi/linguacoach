import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type KpiVariant = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate' | 'coral';

/**
 * layout='standard' (default): 40px icon, 30px value — existing pages.
 * layout='tile': 56px flush icon + border-right, 24px value — dashboard KPI row.
 */
export type KpiLayout = 'standard' | 'tile';

@Component({
  selector: 'sp-admin-kpi-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-kpi-card" [class.sp-kpi-card--tile]="layout === 'tile'">
      <div class="sp-kpi-icon" [class]="'sp-kpi-icon-' + variant" [class.sp-kpi-icon--tile]="layout === 'tile'">
        <ng-content select="[slot=icon]" />
      </div>
      <div class="sp-kpi-body" [class.sp-kpi-body--tile]="layout === 'tile'">
        <div class="sp-kpi-label">{{ label }}</div>
        <div class="sp-kpi-value" [class.sp-kpi-value--tile]="layout === 'tile'">
          <ng-content />
        </div>
        @if (delta) {
          <div class="sp-kpi-delta" [style.color]="deltaColor || null">{{ delta }}</div>
        }
      </div>
    </div>
  `,
  styles: [`
    .sp-kpi-card {
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      align-items: center;
      gap: 16px;
      box-shadow: 0 1px 2px rgba(33,27,54,.06);
    }
    .sp-kpi-icon {
      width: 40px; height: 40px;
      border-radius: 11px;
      display: grid; place-items: center;
      flex-shrink: 0;
    }
    .sp-kpi-body { min-width: 0; flex: 1; }
    .sp-kpi-label {
      font-size: 11px; font-weight: 800;
      color: #8B85A0; text-transform: uppercase;
      letter-spacing: .08em; margin-bottom: 4px;
    }
    .sp-kpi-value {
      font-size: 30px; font-weight: 800;
      color: #211B36; letter-spacing: -.04em; line-height: 1;
    }
    .sp-kpi-delta { font-size: 11.5px; font-weight: 600; margin-top: 5px; color: #8B85A0; }

    /* tile layout */
    .sp-kpi-card--tile { padding: 0; gap: 0; overflow: hidden; border-radius: 12px; align-items: stretch; }
    .sp-kpi-icon--tile {
      width: var(--sp-dash-kpi-tile-w, 56px);
      border-radius: 0;
      border-right: 1px solid var(--sp-admin-border, #ECE9F5);
      min-height: 72px; height: auto;
    }
    .sp-kpi-body--tile { padding: 13px 15px; }
    .sp-kpi-value--tile { font-size: 24px; }

    /* variants */
    .sp-kpi-icon-indigo { background: #EDEBFF; color: #5B4BE8; }
    .sp-kpi-icon-green  { background: #E0F6EE; color: #13B07C; }
    .sp-kpi-icon-violet { background: #F2E9FF; color: #B45CF0; }
    .sp-kpi-icon-amber  { background: #FFF1DC; color: #F0982C; }
    .sp-kpi-icon-teal   { background: #E0F6EE; color: #0A7468; }
    .sp-kpi-icon-slate  { background: #F6F4FB; color: #8B85A0; }
    .sp-kpi-icon-coral  { background: #FFEAE4; color: #FF7A59; }
  `],
})
export class SpAdminKpiCardComponent {
  @Input() label = '';
  @Input() variant: KpiVariant = 'indigo';
  @Input() layout: KpiLayout = 'standard';
  @Input() delta: string | null = null;
  @Input() deltaColor: string | null = null;
}
