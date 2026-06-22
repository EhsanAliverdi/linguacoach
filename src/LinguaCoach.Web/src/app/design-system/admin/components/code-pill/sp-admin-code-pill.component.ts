import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminCodePillTone = 'neutral' | 'primary' | 'info' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'sp-admin-code-pill',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-adm-code-pill"
      [class]="toneClass"
      [title]="value"
    >{{ truncated }}</span>
  `,
  styles: [`
    .sp-adm-code-pill {
      display: inline-block;
      max-width: 220px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: bottom;
      padding: 2px 8px;
      border-radius: 6px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
      font-size: 11px;
      font-weight: 500;
      line-height: 1.5;
    }
    .sp-adm-code-pill-neutral { background: #f2f4f7; color: #344054; }
    .sp-adm-code-pill-primary { background: #ecf3ff; color: #3538cd; }
    .sp-adm-code-pill-info    { background: #f0f9ff; color: #0369a1; }
    .sp-adm-code-pill-success { background: #ecfdf3; color: #15803d; }
    .sp-adm-code-pill-warning { background: #fffbeb; color: #b45309; }
    .sp-adm-code-pill-danger  { background: #fef2f2; color: #b91c1c; }
  `],
})
export class SpAdminCodePillComponent {
  @Input() value = '';
  @Input() tone: SpAdminCodePillTone = 'neutral';
  @Input() maxLength = 0;

  get truncated(): string {
    if (this.maxLength > 0 && this.value.length > this.maxLength) {
      return this.value.slice(0, this.maxLength) + '…';
    }
    return this.value;
  }

  get toneClass(): string {
    return `sp-adm-code-pill-${this.tone}`;
  }
}
