import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { KpiVariant } from '../kpi-card/sp-admin-kpi-card.component';

@Component({
  selector: 'sp-admin-action-card',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <a class="sp-ac-root" [routerLink]="routerLink">
      <div class="sp-ac-icon" [class]="'sp-ac-icon-' + variant">
        <ng-content select="[slot=icon]" />
      </div>
      <div>
        <div class="sp-ac-title">{{ title }}</div>
        @if (description) {
          <div class="sp-ac-desc">{{ description }}</div>
        }
      </div>
    </a>
  `,
  styles: [`
    .sp-ac-root {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px;
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-md);
      text-decoration: none;
      transition: box-shadow var(--sp-admin-transition-fast), border-color var(--sp-admin-transition-fast);
      cursor: pointer;
    }
    .sp-ac-root:hover {
      border-color: var(--sp-admin-primary-focus);
      box-shadow: var(--sp-admin-shadow-action);
    }
    .sp-ac-icon {
      width: 36px;
      height: 36px;
      border-radius: 9px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sp-ac-icon-indigo { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-ac-icon-green  { background: var(--sp-admin-green-bg);   color: var(--sp-admin-green); }
    .sp-ac-icon-violet { background: var(--sp-admin-violet-bg);  color: var(--sp-admin-violet); }
    .sp-ac-icon-amber  { background: var(--sp-admin-amber-bg);   color: var(--sp-admin-amber); }
    .sp-ac-icon-teal   { background: var(--sp-admin-teal-bg);    color: var(--sp-admin-teal); }
    .sp-ac-icon-slate  { background: var(--sp-admin-slate-bg);   color: var(--sp-admin-slate); }
    .sp-ac-title {
      font-size: 13px;
      font-weight: 700;
      color: var(--sp-admin-text);
    }
    .sp-ac-desc {
      font-size: 11.5px;
      color: var(--sp-admin-text-dim);
      margin-top: 1px;
    }
  `],
})
export class SpAdminActionCardComponent {
  @Input() title = '';
  @Input() description = '';
  @Input() variant: KpiVariant = 'indigo';
  @Input() routerLink: string | string[] = '/';
}
