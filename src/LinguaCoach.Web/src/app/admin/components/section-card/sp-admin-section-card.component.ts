import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-section-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-sc-root" [class.sp-sc-dashed]="dashed">
      @if (title) {
        <div class="sp-sc-header">
          <ng-content select="[slot=header-icon]" />
          <span class="sp-sc-title">{{ title }}</span>
          <ng-content select="[slot=header-action]" />
        </div>
      }
      <ng-content />
    </div>
  `,
  styles: [`
    .sp-sc-root {
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-lg);
      padding: 18px;
    }
    .sp-sc-dashed {
      border: 1.5px dashed var(--sp-admin-border);
    }
    .sp-sc-header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 14px;
    }
    .sp-sc-title {
      font-size: 14px;
      font-weight: 800;
      color: var(--sp-admin-text);
      flex: 1;
    }
  `],
})
export class SpAdminSectionCardComponent {
  @Input() title = '';
  @Input() dashed = false;
}
