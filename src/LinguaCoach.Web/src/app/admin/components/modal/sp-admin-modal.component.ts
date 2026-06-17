import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (open) {
      <div class="sp-modal-backdrop" (click)="onBackdropClick()" aria-hidden="true"></div>
      <div
        class="sp-modal-panel"
        role="dialog"
        [attr.aria-label]="title"
        aria-modal="true"
      >
        <div class="sp-modal-header">
          <div>
            <div class="sp-modal-title">{{ title }}</div>
            @if (subtitle) {
              <div class="sp-modal-subtitle">{{ subtitle }}</div>
            }
          </div>
          <button class="sp-modal-close" (click)="close()" aria-label="Close dialog">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24">
              <path d="M18 6 6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="sp-modal-body">
          <ng-content />
        </div>
        <div class="sp-modal-footer">
          <ng-content select="[slot=footer]" />
        </div>
      </div>
    }
  `,
  styles: [`
    .sp-modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,.35);
      z-index: var(--sp-admin-z-modal);
    }
    .sp-modal-panel {
      position: fixed;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      z-index: calc(var(--sp-admin-z-modal) + 1);
      background: var(--sp-admin-surface);
      border-radius: var(--sp-admin-radius-lg);
      border: 1px solid var(--sp-admin-border);
      width: min(520px, calc(100vw - 32px));
      max-height: calc(100vh - 80px);
      overflow-y: auto;
      box-shadow: 0 8px 32px rgba(0,0,0,.14);
    }
    .sp-modal-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      padding: 20px 20px 0;
    }
    .sp-modal-title {
      font-size: 16px;
      font-weight: 800;
      color: var(--sp-admin-text);
      letter-spacing: -.01em;
    }
    .sp-modal-subtitle {
      font-size: 12.5px;
      color: var(--sp-admin-text-muted);
      margin-top: 2px;
    }
    .sp-modal-close {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      border-radius: var(--sp-admin-radius-sm);
      border: 1px solid var(--sp-admin-border);
      background: none;
      color: var(--sp-admin-text-dim);
      cursor: pointer;
      flex-shrink: 0;
    }
    .sp-modal-close:hover { background: var(--sp-admin-surface-subtle); }
    .sp-modal-body { padding: 16px 20px; }
    .sp-modal-footer {
      padding: 0 20px 20px;
      display: flex;
      gap: 8px;
      justify-content: flex-end;
    }
    .sp-modal-footer:empty { display: none; }
  `],
})
export class SpAdminModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() subtitle = '';
  @Input() closeOnBackdrop = true;
  @Output() closed = new EventEmitter<void>();

  close(): void { this.closed.emit(); }

  onBackdropClick(): void {
    if (this.closeOnBackdrop) this.close();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) this.close();
  }
}
