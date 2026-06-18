import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KpiVariant } from '../kpi-card/sp-admin-kpi-card.component';

@Component({
  selector: 'sp-admin-stat-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <article class="sp-adm-stat">
      <div class="sp-adm-stat-icon" [class]="'sp-adm-stat-icon-' + tone">
        <ng-content select="[slot=icon]" />
      </div>
      <div>
        <div class="sp-adm-stat-label">{{ label }}</div>
        <div class="sp-adm-stat-value">{{ value }}</div>
      </div>
    </article>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .sp-adm-stat {
      display: flex;
      align-items: center;
      gap: 16px;
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-lg);
      padding: var(--sp-admin-card-pad);
      box-shadow: var(--sp-admin-shadow-card);
      min-width: 0;
      transition: box-shadow var(--sp-admin-transition-fast);
    }
    .sp-adm-stat:hover { box-shadow: var(--sp-admin-shadow-card-hover); }
    .sp-adm-stat-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .sp-adm-stat-icon-indigo { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-adm-stat-icon-green { background: var(--sp-admin-green-bg); color: var(--sp-admin-green); }
    .sp-adm-stat-icon-violet { background: var(--sp-admin-violet-bg); color: var(--sp-admin-violet); }
    .sp-adm-stat-icon-amber { background: var(--sp-admin-amber-bg); color: var(--sp-admin-amber); }
    .sp-adm-stat-icon-teal { background: var(--sp-admin-teal-bg); color: var(--sp-admin-teal); }
    .sp-adm-stat-icon-slate { background: var(--sp-admin-slate-bg); color: var(--sp-admin-slate); }
    .sp-adm-stat-label {
      color: var(--sp-admin-text-muted);
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: .04em;
    }
    .sp-adm-stat-value {
      color: var(--sp-admin-text);
      font-size: 24px;
      font-weight: 800;
      line-height: 1.1;
    }
  `],
})
export class SpAdminStatCardComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() tone: KpiVariant = 'indigo';
}
