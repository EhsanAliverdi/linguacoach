import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface EventFeedItem {
  id?: string;
  timestamp: string;
  title: string;
  message?: string;
  level?: 'Error' | 'Warning' | 'Information' | 'Debug';
  category?: string;
  correlationId?: string | null;
}

@Component({
  selector: 'sp-admin-event-feed',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-ef-root" [attr.aria-label]="ariaLabel || 'Event feed'">
      @if (title) {
        <div class="sp-ef-title">{{ title }}</div>
      }
      @if (loading) {
        <div class="sp-ef-empty">Loading…</div>
      } @else if (!items || items.length === 0) {
        <div class="sp-ef-empty">{{ emptyMessage || 'No events' }}</div>
      } @else {
        <div class="sp-ef-list">
          @for (item of items; track item.id || item.timestamp + $index) {
            <div class="sp-ef-item">
              <div class="sp-ef-dot-col">
                <div class="sp-ef-dot sp-ef-dot-{{ dotTone(item.level) }}"></div>
                <div class="sp-ef-line"></div>
              </div>
              <div class="sp-ef-body">
                <div class="sp-ef-row1">
                  <span class="sp-ef-message">{{ item.title }}</span>
                  @if (item.level && item.level !== 'Information') {
                    <span class="sp-ef-level sp-ef-level-{{ dotTone(item.level) }}">{{ item.level }}</span>
                  }
                </div>
                @if (item.message && item.message !== item.title) {
                  <div class="sp-ef-detail">{{ item.message }}</div>
                }
                <div class="sp-ef-meta">
                  <span class="sp-ef-ts">{{ item.timestamp | date:'HH:mm:ss' }}</span>
                  @if (item.category) {
                    <span class="sp-ef-cat">{{ item.category }}</span>
                  }
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .sp-ef-root { display: block; min-width: 0; }
    .sp-ef-title {
      font-size: 11px; font-weight: 800; color: var(--sp-admin-text-muted, #8B85A0);
      text-transform: uppercase; letter-spacing: .08em; margin-bottom: 10px;
    }
    .sp-ef-empty { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); padding: 8px 0; }
    .sp-ef-list { display: flex; flex-direction: column; }
    .sp-ef-item { display: flex; gap: 10px; }
    .sp-ef-dot-col { display: flex; flex-direction: column; align-items: center; flex-shrink: 0; width: 12px; padding-top: 3px; }
    .sp-ef-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
    .sp-ef-dot-danger  { background: var(--sp-admin-danger, #EF4444); }
    .sp-ef-dot-warning { background: var(--sp-admin-amber, #D97706); }
    .sp-ef-dot-info    { background: var(--sp-admin-primary, #5B4BE8); }
    .sp-ef-dot-neutral { background: var(--sp-admin-slate, #475569); }
    .sp-ef-line { flex: 1; width: 1px; background: var(--sp-admin-border, #ECE9F5); margin-top: 3px; min-height: 8px; }
    .sp-ef-item:last-child .sp-ef-line { display: none; }
    .sp-ef-body { flex: 1; min-width: 0; padding-bottom: 10px; }
    .sp-ef-row1 { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .sp-ef-message { font-size: 12px; font-weight: 600; color: var(--sp-admin-text, #211B36); line-height: 1.4; }
    .sp-ef-level { font-size: 10px; font-weight: 700; padding: 1px 6px; border-radius: 99px; }
    .sp-ef-level-danger  { background: var(--sp-admin-danger-bg, #FEF2F2); color: var(--sp-admin-danger, #EF4444); }
    .sp-ef-level-warning { background: var(--sp-admin-amber-bg, #FFFBEB); color: var(--sp-admin-amber, #D97706); }
    .sp-ef-level-info    { background: var(--sp-admin-primary-bg, #EDEBFF); color: var(--sp-admin-primary, #5B4BE8); }
    .sp-ef-detail { font-size: 11px; color: var(--sp-admin-text-secondary, #4B4462); margin-top: 1px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .sp-ef-meta { display: flex; gap: 8px; margin-top: 3px; }
    .sp-ef-ts  { font-size: 10px; color: var(--sp-admin-text-muted, #8B85A0); font-variant-numeric: tabular-nums; }
    .sp-ef-cat { font-size: 10px; color: var(--sp-admin-text-dim, #BDB8CC); }
  `],
})
export class SpAdminEventFeedComponent {
  @Input() items: EventFeedItem[] = [];
  @Input() title = '';
  @Input() ariaLabel = '';
  @Input() loading = false;
  @Input() emptyMessage = '';

  dotTone(level?: string): string {
    if (level === 'Error') return 'danger';
    if (level === 'Warning') return 'warning';
    if (level === 'Information') return 'info';
    return 'neutral';
  }
}
