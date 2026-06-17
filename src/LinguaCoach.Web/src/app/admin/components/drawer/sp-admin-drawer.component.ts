import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-drawer',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (open) {
      <div class="sp-adm-drawer-backdrop" (click)="closed.emit()" aria-hidden="true"></div>
      <aside class="sp-adm-drawer" role="dialog" aria-modal="true" [attr.aria-label]="title">
        <header class="sp-adm-drawer-header">
          <h2>{{ title }}</h2>
          <button type="button" (click)="closed.emit()" aria-label="Close drawer">×</button>
        </header>
        <div class="sp-adm-drawer-body"><ng-content /></div>
      </aside>
    }
  `,
  styles: [`
    .sp-adm-drawer-backdrop { position: fixed; inset: 0; z-index: var(--sp-admin-z-modal); background: rgba(15,23,42,.36); }
    .sp-adm-drawer {
      position: fixed;
      top: 0;
      right: 0;
      bottom: 0;
      z-index: calc(var(--sp-admin-z-modal) + 1);
      width: min(420px, 100vw);
      background: var(--sp-admin-surface);
      border-left: 1px solid var(--sp-admin-border);
      box-shadow: -18px 0 48px rgba(15,23,42,.18);
      overflow: auto;
    }
    .sp-adm-drawer-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 18px 20px;
      border-bottom: 1px solid var(--sp-admin-border);
    }
    .sp-adm-drawer-header h2 { margin: 0; font-size: 16px; font-weight: 800; color: var(--sp-admin-text); }
    .sp-adm-drawer-header button { border: 1px solid var(--sp-admin-border); background: var(--sp-admin-surface); border-radius: var(--sp-admin-radius-sm); width: 32px; height: 32px; cursor: pointer; }
    .sp-adm-drawer-body { padding: 20px; }
  `],
})
export class SpAdminDrawerComponent {
  @Input() open = false;
  @Input() title = '';
  @Output() closed = new EventEmitter<void>();
}
