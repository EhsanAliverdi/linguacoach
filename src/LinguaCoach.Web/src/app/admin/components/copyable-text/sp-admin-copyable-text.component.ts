import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-copyable-text',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="sp-adm-copy-wrap">
      <span
        class="sp-adm-copy-text"
        [class.sp-adm-copy-mono]="mono"
        [title]="value"
      >{{ displayValue || truncated }}</span>
      <button
        type="button"
        class="sp-adm-copy-btn"
        [attr.aria-label]="'Copy ' + (displayValue || truncated)"
        (click)="copy()"
      >{{ copied ? '✓' : '⎘' }}</button>
    </span>
  `,
  styles: [`
    .sp-adm-copy-wrap {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      max-width: 100%;
    }
    .sp-adm-copy-text {
      display: inline-block;
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: bottom;
      font-size: 13px;
      color: #344054;
    }
    .sp-adm-copy-mono {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
      font-size: 12px;
      color: #475467;
    }
    .sp-adm-copy-btn {
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 20px;
      height: 20px;
      border: none;
      background: transparent;
      cursor: pointer;
      color: #94a3b8;
      font-size: 13px;
      border-radius: 4px;
      padding: 0;
      transition: color 0.1s, background 0.1s;
    }
    .sp-adm-copy-btn:hover {
      color: #465fff;
      background: #f0f4ff;
    }
  `],
})
export class SpAdminCopyableTextComponent {
  @Input() value = '';
  @Input() displayValue = '';
  @Input() maxLength = 20;
  @Input() mono = true;

  copied = false;
  private resetTimer: ReturnType<typeof setTimeout> | null = null;

  get truncated(): string {
    if (this.maxLength > 0 && this.value.length > this.maxLength) {
      return this.value.slice(0, this.maxLength) + '…';
    }
    return this.value;
  }

  copy(): void {
    if (!this.value) return;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(this.value).then(() => this.showCopied(), () => {});
    } else {
      this.fallbackCopy(this.value);
    }
  }

  private showCopied(): void {
    this.copied = true;
    if (this.resetTimer) clearTimeout(this.resetTimer);
    this.resetTimer = setTimeout(() => { this.copied = false; }, 1500);
  }

  private fallbackCopy(text: string): void {
    try {
      const el = document.createElement('textarea');
      el.value = text;
      el.style.position = 'fixed';
      el.style.opacity = '0';
      document.body.appendChild(el);
      el.select();
      document.execCommand('copy');
      document.body.removeChild(el);
      this.showCopied();
    } catch {}
  }
}
