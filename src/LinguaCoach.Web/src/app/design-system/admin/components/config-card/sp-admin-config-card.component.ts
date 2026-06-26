import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminBadgeComponent, SpAdminBadgeTone } from '../badge/sp-admin-badge.component';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';
import { SpAdminIconComponent } from '../icon/sp-admin-icon.component';
import { SpAdminIconName } from '../icon/sp-admin-icon.component';

export type SpAdminConfigCardIconTone = 'green' | 'indigo' | 'amber' | 'purple' | 'orange' | 'slate' | 'teal' | 'danger';
export type SpAdminConfigCardState = 'default' | 'success' | 'warning' | 'neutral' | 'disabled';

const ICON_BG: Record<SpAdminConfigCardIconTone, string> = {
  green:  '#E0F6EE',
  indigo: '#EDEBFF',
  amber:  '#FFF1DC',
  purple: '#F2E9FF',
  orange: '#FFF1DC',
  slate:  '#F6F4FB',
  teal:   '#E0F6EE',
  danger: '#FEE2E2',
};

const ICON_COLOR: Record<SpAdminConfigCardIconTone, string> = {
  green:  '#13B07C',
  indigo: '#5B4BE8',
  amber:  '#F0982C',
  purple: '#B45CF0',
  orange: '#F0982C',
  slate:  '#8B85A0',
  teal:   '#0A7468',
  danger: '#EF4444',
};

@Component({
  selector: 'sp-admin-config-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, SpAdminBadgeComponent, SpAdminButtonComponent, SpAdminIconComponent],
  template: `
    <div class="sp-cc-card" [class]="cardClass">

      <!-- Header: icon + title + status badge -->
      <div class="sp-cc-header">
        <div class="sp-cc-icon"
             [style.background]="iconBg"
             [style.color]="iconColor">
          @if (icon) {
            <sp-admin-icon [name]="icon" size="md" [strokeWidth]="1.5" />
          }
        </div>
        <div class="sp-cc-meta">
          <span class="sp-cc-title">{{ title }}</span>
          @if (statusLabel) {
            <sp-admin-badge [tone]="statusTone" [dot]="statusDot" size="sm">{{ statusLabel }}</sp-admin-badge>
          }
        </div>
      </div>

      <!-- Description -->
      @if (description) {
        <p class="sp-cc-desc">{{ description }}</p>
      }

      <!-- Body slot — alerts, meta, callouts -->
      <ng-content />

      <!-- Action row — owned by component, matches sp-admin-config-category-card pattern -->
      @if (primaryLabel) {
        <div class="sp-cc-actions">
          <sp-admin-button
            variant="neutral"
            appearance="outline"
            [fullWidth]="true"
            [disabled]="primaryDisabled"
            (click)="primary.emit()">
            {{ primaryLabel }}
          </sp-admin-button>
          @if (secondaryLabel) {
            <sp-admin-button
              variant="neutral"
              appearance="outline"
              [loading]="secondaryBusy"
              [disabled]="secondaryDisabled || secondaryBusy"
              (click)="secondary.emit()">
              {{ secondaryLabel }}
            </sp-admin-button>
          }
        </div>
      }

    </div>
  `,
  styles: [`
    :host { display: contents; }

    .sp-cc-card {
      background: var(--sp-admin-surface, #fff);
      border: 1.5px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      box-shadow: var(--sp-admin-shadow-card, 0 1px 2px rgba(33,27,54,.06));
      min-height: 180px;
    }

    /* State variants */
    .sp-cc-card--success {
      border-color: var(--sp-admin-green-ring, #A8EDD4);
      background: linear-gradient(180deg, #F4FCF9 0%, var(--sp-admin-surface, #fff) 60px);
    }
    .sp-cc-card--warning {
      border-color: #FFD9A0;
    }
    .sp-cc-card--neutral {
      border-color: var(--sp-admin-border-subtle, #F4F2FC);
      background: var(--sp-admin-surface-subtle, #FBFAFE);
    }
    .sp-cc-card--disabled {
      border-color: var(--sp-admin-border-subtle, #F4F2FC);
      opacity: 0.6;
    }

    .sp-cc-header {
      display: flex;
      align-items: flex-start;
      gap: 14px;
    }
    .sp-cc-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .sp-cc-meta {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 5px;
      padding-top: 2px;
    }
    .sp-cc-title {
      font-size: 14px;
      font-weight: 700;
      color: var(--sp-admin-text, #211B36);
      line-height: 1.3;
    }
    .sp-cc-desc {
      font-size: 13px;
      color: var(--sp-admin-text-muted, #8B85A0);
      line-height: 1.5;
      margin: 0;
    }
    .sp-cc-actions {
      display: flex;
      gap: 8px;
      margin-top: auto;
    }
  `],
})
export class SpAdminConfigCardComponent {
  @Input({ required: true }) title = '';
  @Input() description = '';
  @Input() icon?: SpAdminIconName;
  @Input() iconTone: SpAdminConfigCardIconTone = 'indigo';
  @Input() state: SpAdminConfigCardState = 'default';
  @Input() statusLabel = '';
  @Input() statusTone: SpAdminBadgeTone = 'neutral';
  @Input() statusDot = false;

  // Primary action
  @Input() primaryLabel = '';
  @Input() primaryDisabled = false;
  @Output() primary = new EventEmitter<void>();

  // Secondary action (optional — e.g. "Test")
  @Input() secondaryLabel = '';
  @Input() secondaryBusy = false;
  @Input() secondaryDisabled = false;
  @Output() secondary = new EventEmitter<void>();

  get iconBg(): string { return ICON_BG[this.iconTone]; }
  get iconColor(): string { return ICON_COLOR[this.iconTone]; }
  get cardClass(): string {
    return this.state !== 'default' ? `sp-cc-card--${this.state}` : '';
  }
}
