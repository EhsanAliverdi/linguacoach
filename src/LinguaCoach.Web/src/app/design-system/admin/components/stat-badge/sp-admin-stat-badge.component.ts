import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeTone = 'green' | 'amber' | 'indigo' | 'violet' | 'slate' | 'danger' | 'teal';

@Component({
  selector: 'sp-admin-stat-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="sp-badge" [class]="'sp-badge-' + tone"><ng-content /></span>`,
  styles: [`
    .sp-badge {
      display: inline-block;
      font-size: 11px;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 99px;
      white-space: nowrap;
    }
    .sp-badge-green  { background: var(--sp-admin-green-bg);   color: var(--sp-admin-green); }
    .sp-badge-amber  { background: var(--sp-admin-amber-bg);   color: var(--sp-admin-amber); }
    .sp-badge-indigo { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-badge-violet { background: var(--sp-admin-violet-bg);  color: var(--sp-admin-violet); }
    .sp-badge-slate  { background: var(--sp-admin-slate-bg);   color: var(--sp-admin-slate); }
    .sp-badge-danger { background: var(--sp-admin-danger-bg);  color: var(--sp-admin-danger); }
    .sp-badge-teal   { background: var(--sp-admin-teal-bg);    color: var(--sp-admin-teal); }
  `],
})
export class SpAdminStatBadgeComponent {
  @Input() tone: BadgeTone = 'slate';
}
