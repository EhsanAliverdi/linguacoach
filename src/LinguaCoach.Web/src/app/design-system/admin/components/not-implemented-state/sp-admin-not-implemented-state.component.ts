import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-not-implemented-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-nis-wrap" [attr.aria-label]="ariaLabel || null">
      <svg class="sp-nis-icon" width="20" height="20" viewBox="0 0 24 24" fill="none"
        stroke="currentColor" stroke-width="2" aria-hidden="true">
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
      <div>
        <div class="sp-nis-title">{{ title }}</div>
        <div class="sp-nis-body">
          <ng-content />
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-nis-wrap { display: flex; gap: 12px; align-items: flex-start; padding: 4px 0; }
    .sp-nis-icon { color: var(--sp-admin-text-muted, #8B85A0); flex-shrink: 0; }
    .sp-nis-title { font-size: 13px; font-weight: 600; color: var(--sp-admin-text-muted, #8B85A0); margin-bottom: 4px; }
    .sp-nis-body { font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0); line-height: 1.5; }
  `],
})
export class SpAdminNotImplementedStateComponent {
  @Input() title = 'Not yet implemented';
  @Input() ariaLabel = '';
}
