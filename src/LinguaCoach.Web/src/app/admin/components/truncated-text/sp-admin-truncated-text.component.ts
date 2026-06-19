import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-truncated-text',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-adm-trunc"
      [class.sp-adm-trunc-mono]="mono"
      [style.max-width]="maxWidth"
      [title]="value"
    >{{ display }}</span>
  `,
  styles: [`
    .sp-adm-trunc {
      display: inline-block;
      max-width: 240px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: bottom;
    }
    .sp-adm-trunc-mono {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
      font-size: 12px;
      color: #475467;
    }
  `],
})
export class SpAdminTruncatedTextComponent {
  @Input() value = '';
  @Input() maxLength = 0;
  @Input() maxWidth = '';
  @Input() mono = false;

  get display(): string {
    if (this.maxLength > 0 && this.value.length > this.maxLength) {
      return this.value.slice(0, this.maxLength) + '…';
    }
    return this.value;
  }
}
