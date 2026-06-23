import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type GraphCardStatus = 'live' | 'partial' | 'unavailable' | 'loading';

@Component({
  selector: 'sp-admin-graph-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-gc-root">
      <div class="sp-gc-header">
        <div class="sp-gc-title-group">
          <span class="sp-gc-title">{{ title }}</span>
          @if (subtitle) {
            <span class="sp-gc-subtitle">{{ subtitle }}</span>
          }
        </div>
        <div class="sp-gc-header-right">
          @if (status && status !== 'loading') {
            <span class="sp-gc-status sp-gc-status--{{ status }}">
              @switch (status) {
                @case ('live')        { <span class="sp-gc-dot"></span>Live }
                @case ('partial')     { Partial }
                @case ('unavailable') { Unavailable }
              }
            </span>
          }
          @if (actionLabel) {
            <a class="sp-gc-action" [href]="actionHref || '#'">{{ actionLabel }}</a>
          }
        </div>
      </div>

      <div class="sp-gc-body">
        <ng-content />
      </div>

      @if (footerNote) {
        <div class="sp-gc-footer">{{ footerNote }}</div>
      }
    </div>
  `,
  styles: [`
    .sp-gc-root {
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 14px;
      overflow: hidden;
    }
    .sp-gc-header {
      display: flex; align-items: flex-start; justify-content: space-between;
      padding: 16px 18px 12px;
      border-bottom: 1px solid var(--sp-admin-border, #ECE9F5);
      gap: 8px;
    }
    .sp-gc-title-group { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .sp-gc-title {
      font-size: 13px; font-weight: 700;
      color: var(--sp-admin-text, #211B36);
      letter-spacing: -.01em;
    }
    .sp-gc-subtitle {
      font-size: 11px;
      color: var(--sp-admin-text-muted, #8B85A0);
    }
    .sp-gc-header-right { display: flex; align-items: center; gap: 10px; flex-shrink: 0; }
    .sp-gc-status {
      font-size: 10.5px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase;
      padding: 3px 8px; border-radius: 99px;
      display: flex; align-items: center; gap: 5px;
    }
    .sp-gc-status--live        { background: #DCFCE7; color: #15803D; }
    .sp-gc-status--partial     { background: #FEF9C3; color: #A16207; }
    .sp-gc-status--unavailable { background: var(--sp-admin-border, #ECE9F5); color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-gc-dot {
      width: 6px; height: 6px; border-radius: 50%;
      background: currentColor; display: inline-block; flex-shrink: 0;
    }
    .sp-gc-action {
      font-size: 12px; font-weight: 700;
      color: var(--sp-admin-primary, #5B4BE8);
      text-decoration: none;
    }
    .sp-gc-action:hover { color: var(--sp-admin-primary-hover, #3A2EA8); }
    .sp-gc-body { padding: 16px 18px; }
    .sp-gc-footer {
      padding: 8px 18px 12px;
      font-size: 10.5px;
      color: var(--sp-admin-text-dim, #BDB8CC);
      font-style: italic;
      border-top: 1px solid var(--sp-admin-border, #ECE9F5);
    }
  `],
})
export class SpAdminGraphCardComponent {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() status: GraphCardStatus | null = null;
  @Input() actionLabel = '';
  @Input() actionHref = '';
  @Input() footerNote = '';
}
