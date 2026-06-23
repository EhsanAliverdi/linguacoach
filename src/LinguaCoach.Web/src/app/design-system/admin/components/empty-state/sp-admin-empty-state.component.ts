import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'sp-admin-empty-state',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="sp-empty">
      <ng-content select="[slot=icon]" />
      @if (title) {
        <strong class="sp-empty-title">{{ title }}</strong>
      }
      <p class="sp-empty-msg">{{ message }}</p>
      @if (ctaLabel && ctaRoute) {
        <a [routerLink]="ctaRoute" class="sp-empty-cta">{{ ctaLabel }}</a>
      }
    </div>
  `,
  styles: [`
    .sp-empty {
      padding: 40px 24px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      color: var(--sp-admin-text-dim, #9ca3af);
      font-size: 13px;
    }
    .sp-empty-title {
      color: var(--sp-admin-text, #0F172A);
      font-size: 14px;
      font-weight: 600;
      margin-bottom: 2px;
    }
    .sp-empty-msg { margin: 0; color: var(--sp-admin-text-muted,#64748B); }
    .sp-empty-cta {
      display: inline-block;
      font-size: 12px;
      font-weight: 600;
      padding: 6px 16px;
      border-radius: 8px;
      background: var(--sp-admin-primary, #5B4BE8);
      color: #fff;
      text-decoration: none;
      margin-top: 8px;
    }
  `],
})
export class SpAdminEmptyStateComponent {
  @Input() title = '';
  @Input() message = 'No items found.';
  @Input() ctaLabel = '';
  @Input() ctaRoute = '';
}
