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
    .sp-kpi-card {
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-lg);
      padding: 18px;
      display: flex;
      align-items: center;
      gap: 14px;
      box-shadow: var(--sp-admin-shadow-card);
    }
    .sp-kpi-icon {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sp-kpi-icon-indigo { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-kpi-icon-green  { background: var(--sp-admin-green-bg);   color: var(--sp-admin-green); }
    .sp-kpi-icon-violet { background: var(--sp-admin-violet-bg);  color: var(--sp-admin-violet); }
    .sp-kpi-icon-amber  { background: var(--sp-admin-amber-bg);   color: var(--sp-admin-amber); }
    .sp-kpi-icon-teal   { background: var(--sp-admin-teal-bg);    color: var(--sp-admin-teal); }
    .sp-kpi-icon-slate  { background: var(--sp-admin-slate-bg);   color: var(--sp-admin-slate); }
    .sp-kpi-body { min-width: 0; }
    .sp-kpi-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--sp-admin-text-muted);
      text-transform: uppercase;
      letter-spacing: .04em;
      margin-bottom: 2px;
    }
    .sp-kpi-value {
      font-size: 26px;
      font-weight: 800;
      color: var(--sp-admin-text);
      line-height: 1;
    }
  `],
})
export class SpAdminKpiCardComponent {
  @Input() label = '';
  @Input() variant: KpiVariant = 'indigo';
}
