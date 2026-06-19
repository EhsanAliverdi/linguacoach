import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-section-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-sh-root">
      <div class="sp-sh-main">
        <span class="sp-sh-title">{{ title }}</span>
        @if (description) {
          <span class="sp-sh-desc">{{ description }}</span>
        }
      </div>
      <ng-content select="[slot=actions]" />
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-sh-root {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 10px;
    }
    .sp-sh-main { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .sp-sh-title {
      font-size: 13px;
      font-weight: 700;
      color: #374151;
      line-height: 1.3;
    }
    .sp-sh-desc {
      font-size: 11.5px;
      color: #6b7280;
      line-height: 1.4;
    }
  `],
})
export class SpAdminSectionHeaderComponent {
  @Input() title = '';
  @Input() description = '';
}
