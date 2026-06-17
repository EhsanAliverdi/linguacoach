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
      <p class="sp-empty-msg">{{ message }}</p>
      @if (ctaLabel && ctaRoute) {
        <a [routerLink]="ctaRoute" class="sp-empty-cta">{{ ctaLabel }}</a>
      }
    </div>
  `,
  styles: [`
    .sp-empty {
      padding: 32px 20px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      color: var(--sp-admin-text-dim);
      font-size: 13px;
    }
    .sp-empty-msg { margin: 0; }
    .sp-empty-cta {
      display: inline-block;
      font-size: 12px;
      font-weight: 700;
      padding: 6px 14px;
      border-radius: var(--sp-admin-radius-sm);
      background: var(--sp-admin-primary);
      color: #fff;
      text-decoration: none;
      margin-top: 4px;
    }
  `],
})
export class SpAdminEmptyStateComponent {
  @Input() message = 'No items found.';
  @Input() ctaLabel = '';
  @Input() ctaRoute = '';
}
