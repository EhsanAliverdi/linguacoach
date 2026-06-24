import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type KpiVariant = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate';

@Component({
  selector: 'sp-admin-kpi-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-kpi-card">
      <div class="sp-kpi-icon" [class]="'sp-kpi-icon-' + variant">
        <ng-content select="[slot=icon]" />
      </div>
      <div class="sp-kpi-body">
        <div class="sp-kpi-label">{{ label }}</div>
        <div class="sp-kpi-value">
          <ng-content />
        </div>
      </div>
    </div>
  `,
  styles: [`
    /* Matches .adm-kpi: padding 20px, flex, gap 16px + .adm-card base styles */
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
    /* .adm-kpi-icon: 40px, radius 11px */
    .sp-kpi-icon {
      width: 40px;
      height: 40px;
      border-radius: 11px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    /* Exact standalone icon tones */
    .sp-kpi-icon-indigo  { background: #EDEBFF; color: #5B4BE8; }
    .sp-kpi-icon-green   { background: #E0F6EE; color: #13B07C; }
    .sp-kpi-icon-violet  { background: #F2E9FF; color: #B45CF0; }
    .sp-kpi-icon-amber   { background: #FFF1DC; color: #F0982C; }
    .sp-kpi-icon-teal    { background: #E0F6EE; color: #0A7468; }
    .sp-kpi-icon-slate   { background: #F6F4FB; color: #8B85A0; }
    .sp-kpi-body { min-width: 0; flex: 1; }
    /* .adm-kpi-label: 11px/800/muted/uppercase/0.08em */
    .sp-kpi-label {
      font-size: 11px;
      font-weight: 800;
      color: #8B85A0;
      text-transform: uppercase;
      letter-spacing: .08em;
      margin-bottom: 4px;
    }
    /* .adm-kpi-val: 30px/800/ink/-0.04em/lh1 */
    .sp-kpi-value {
      font-size: 30px;
      font-weight: 800;
      color: #211B36;
      letter-spacing: -.04em;
      line-height: 1;
    }
  `],
})
export class SpAdminKpiCardComponent {
  @Input() label = '';
  @Input() variant: KpiVariant = 'indigo';
}
