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
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #e5e7eb);
      border-radius: 16px;
      padding: 20px;
      display: flex;
      align-items: center;
      gap: 16px;
      box-shadow: var(--sp-admin-shadow-card, 0 1px 3px rgba(0,0,0,.06));
    }
    .sp-kpi-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sp-kpi-icon-indigo { background: var(--sp-admin-primary-bg, #ecf3ff); color: var(--sp-admin-primary, #465fff); }
    .sp-kpi-icon-green  { background: var(--sp-admin-green-bg, #ecfdf3);   color: var(--sp-admin-green, #16a34a); }
    .sp-kpi-icon-violet { background: var(--sp-admin-violet-bg, #f5f3ff);  color: var(--sp-admin-violet, #7c3aed); }
    .sp-kpi-icon-amber  { background: var(--sp-admin-amber-bg, #fffbeb);   color: var(--sp-admin-amber, #d97706); }
    .sp-kpi-icon-teal   { background: var(--sp-admin-teal-bg, #f0fdfa);    color: var(--sp-admin-teal, #0d9488); }
    .sp-kpi-icon-slate  { background: var(--sp-admin-slate-bg, #f2f4f7);   color: var(--sp-admin-slate, #475569); }
    .sp-kpi-body { min-width: 0; }
    .sp-kpi-label {
      font-size: 11px;
      font-weight: 600;
      color: var(--sp-admin-text-muted, #6b7280);
      text-transform: uppercase;
      letter-spacing: .05em;
      margin-bottom: 4px;
    }
    .sp-kpi-value {
      font-size: 24px;
      font-weight: 700;
      color: var(--sp-admin-text, #111827);
      line-height: 1.1;
    }
  `],
})
export class SpAdminKpiCardComponent {
  @Input() label = '';
  @Input() variant: KpiVariant = 'indigo';
}
