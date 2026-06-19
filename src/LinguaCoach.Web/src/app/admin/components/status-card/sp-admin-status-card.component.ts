import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminStatusCardTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'primary';

@Component({
  selector: 'sp-admin-status-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-sc-root" [class]="toneClass">
      @if (loading) {
        <div class="sp-sc-skeleton" aria-busy="true">
          <div class="sp-sc-skel-bar sp-sc-skel-label"></div>
          <div class="sp-sc-skel-bar sp-sc-skel-value"></div>
        </div>
      } @else {
        <div class="sp-sc-inner">
          <div class="sp-sc-top">
            @if (hasIcon) {
              <span class="sp-sc-icon" [class]="iconClass">
                <ng-content select="[slot=icon]" />
              </span>
            }
            <span class="sp-sc-label">{{ label }}</span>
            <span class="sp-sc-dot" [class]="dotClass"></span>
          </div>
          <div class="sp-sc-value">{{ value }}</div>
          @if (helper) {
            <div class="sp-sc-helper">{{ helper }}</div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .sp-sc-root {
      display: flex;
      flex-direction: column;
      border-radius: 12px;
      border: 1px solid var(--sp-sc-border, #e2e8f0);
      background: var(--sp-sc-bg, #fff);
      padding: 14px 16px 12px;
      min-width: 0;
    }

    /* Tone overrides */
    .sp-sc-success { --sp-sc-border: #bbf7d0; --sp-sc-dot-bg: #16a34a; }
    .sp-sc-warning { --sp-sc-border: #fde68a; --sp-sc-dot-bg: #d97706; }
    .sp-sc-danger  { --sp-sc-border: #fecaca; --sp-sc-dot-bg: #ef4444; }
    .sp-sc-info    { --sp-sc-border: #bae6fd; --sp-sc-dot-bg: #0284c7; }
    .sp-sc-primary { --sp-sc-border: #c7d2fe; --sp-sc-dot-bg: #4f46e5; }
    .sp-sc-neutral { --sp-sc-border: #e2e8f0; --sp-sc-dot-bg: #94a3b8; }

    .sp-sc-inner { display: flex; flex-direction: column; gap: 4px; }

    .sp-sc-top {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-bottom: 2px;
    }

    .sp-sc-label {
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      color: #64748b;
      flex: 1;
      min-width: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sp-sc-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--sp-sc-dot-bg, #94a3b8);
      flex-shrink: 0;
    }

    .sp-sc-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 18px;
      height: 18px;
      flex-shrink: 0;
      color: var(--sp-sc-dot-bg, #94a3b8);
    }

    .sp-sc-value {
      font-size: 15px;
      font-weight: 700;
      color: #1e293b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sp-sc-helper {
      font-size: 11px;
      color: #94a3b8;
      margin-top: 1px;
    }

    /* Skeleton */
    .sp-sc-skeleton { display: flex; flex-direction: column; gap: 8px; }
    .sp-sc-skel-bar {
      border-radius: 4px;
      background: #f1f5f9;
      animation: sp-sc-pulse 1.4s ease infinite;
    }
    .sp-sc-skel-label { height: 10px; width: 60%; }
    .sp-sc-skel-value { height: 18px; width: 80%; }
    @keyframes sp-sc-pulse { 0%,100% { opacity:1; } 50% { opacity:.45; } }
  `],
})
export class SpAdminStatusCardComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() tone: SpAdminStatusCardTone = 'neutral';
  @Input() helper = '';
  @Input() loading = false;

  get toneClass(): string { return `sp-sc-${this.tone}`; }
  get dotClass(): string { return ''; }
  get iconClass(): string { return ''; }
  get hasIcon(): boolean { return false; }
}
