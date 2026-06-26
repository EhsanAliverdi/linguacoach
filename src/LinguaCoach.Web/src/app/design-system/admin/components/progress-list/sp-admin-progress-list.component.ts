import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ProgressListTone = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'coral' | 'danger' | 'slate';

export interface ProgressListItem {
  label: string;
  count: number;
  pct: number;
  tone?: ProgressListTone;
}

const TONE_FILL: Record<ProgressListTone, string> = {
  indigo:  'var(--sp-admin-primary, #5B4BE8)',
  green:   'var(--sp-admin-green, #13B07C)',
  violet:  'var(--sp-admin-magenta, #B45CF0)',
  amber:   'var(--sp-admin-amber, #F0982C)',
  teal:    'var(--sp-admin-teal, #0A7468)',
  coral:   'var(--sp-admin-coral, #FF7A59)',
  danger:  'var(--sp-admin-danger, #EF4444)',
  slate:   'var(--sp-admin-slate, #8B85A0)',
};

@Component({
  selector: 'sp-admin-progress-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-pl-root" [attr.aria-label]="ariaLabel || null">
      @for (item of items; track item.label) {
        <div class="sp-pl-row">
          <div class="sp-pl-meta">
            <span class="sp-pl-label">{{ item.label }}</span>
            <span class="sp-pl-count">{{ item.count }} <span class="sp-pl-pct">· {{ item.pct }}%</span></span>
          </div>
          <div class="sp-pl-track" [style.height.px]="barHeight">
            <div
              class="sp-pl-fill"
              role="progressbar"
              [attr.aria-valuenow]="item.pct"
              [attr.aria-valuemin]="0"
              [attr.aria-valuemax]="100"
              [attr.aria-label]="item.label + ': ' + item.pct + '%'"
              [style.width.%]="item.pct"
              [style.background]="fillColor(item.tone)">
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-pl-root { display: flex; flex-direction: column; gap: 10px; }
    .sp-pl-row { display: flex; flex-direction: column; gap: 5px; }
    .sp-pl-meta { display: flex; justify-content: space-between; align-items: center; }
    .sp-pl-label { font-size: 12.5px; font-weight: 600; color: var(--sp-admin-text-secondary, #4B4462); }
    .sp-pl-count { font-size: 12.5px; font-weight: 800; color: var(--sp-admin-text, #211B36); white-space: nowrap; }
    .sp-pl-pct   { font-weight: 600; color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-pl-track { height: 8px; border-radius: 99px; background: var(--sp-admin-border, #ECE9F5); overflow: hidden; }
    .sp-pl-fill  { height: 100%; border-radius: 99px; transition: width .4s ease; min-width: 0; }
  `],
})
export class SpAdminProgressListComponent {
  @Input() items: ProgressListItem[] = [];
  @Input() ariaLabel = '';
  /** Track height in px. Default 8. Use 20 for score-distribution style. */
  @Input() barHeight = 8;

  fillColor(tone?: ProgressListTone): string {
    return TONE_FILL[tone ?? 'indigo'];
  }
}
