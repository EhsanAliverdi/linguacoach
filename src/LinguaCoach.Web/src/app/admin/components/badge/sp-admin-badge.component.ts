import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminBadgeTone = 'success' | 'warning' | 'info' | 'danger' | 'neutral' | 'primary';

@Component({
  selector: 'sp-admin-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="sp-adm-badge" [class]="'sp-adm-badge-' + tone"><ng-content /></span>`,
  styles: [`
    .sp-adm-badge {
      display: inline-flex;
      align-items: center;
      width: fit-content;
      border-radius: 999px;
      padding: 2px 8px;
      font-size: 11px;
      font-weight: 800;
      line-height: 1.6;
    }
    .sp-adm-badge-success { background: var(--sp-admin-green-bg); color: var(--sp-admin-green); }
    .sp-adm-badge-warning { background: var(--sp-admin-amber-bg); color: var(--sp-admin-amber); }
    .sp-adm-badge-info { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-adm-badge-primary { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-adm-badge-danger { background: var(--sp-admin-danger-bg); color: var(--sp-admin-danger); }
    .sp-adm-badge-neutral { background: var(--sp-admin-slate-bg); color: var(--sp-admin-slate); }
  `],
})
export class SpAdminBadgeComponent {
  @Input() tone: SpAdminBadgeTone = 'neutral';
}
