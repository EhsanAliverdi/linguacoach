import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-ph-root">
      <div class="sp-ph-text">
        <h1 class="sp-ph-title">{{ title }}</h1>
        @if (subtitle) {
          <p class="sp-ph-sub">{{ subtitle }}</p>
        }
      </div>
      <div class="sp-ph-actions">
        <ng-content />
      </div>
    </div>
  `,
  styles: [`
    .sp-ph-root {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 24px;
    }
    .sp-ph-text { min-width: 0; }
    .sp-ph-title {
      font-size: 22px;
      font-weight: 800;
      color: var(--sp-admin-text);
      letter-spacing: -.03em;
      margin: 0;
      line-height: 1.15;
    }
    .sp-ph-sub {
      font-size: 13px;
      color: var(--sp-admin-text-muted);
      margin: 3px 0 0;
      line-height: 1.5;
    }
    .sp-ph-actions {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }
  `],
})
export class SpAdminPageHeaderComponent {
  @Input() title = '';
  @Input() subtitle = '';
}
