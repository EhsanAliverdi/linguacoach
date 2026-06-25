import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminBadgeComponent } from '../badge/sp-admin-badge.component';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';

export interface ConfigCategoryField {
  label: string;
  value: string | null | undefined;
  mono?: boolean;
}

@Component({
  selector: 'sp-admin-config-category-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, SpAdminBadgeComponent, SpAdminButtonComponent],
  template: `
    <div class="sp-ccc-card" [class.sp-ccc-card--configured]="configured">

      <!-- Header: icon + meta + badge -->
      <div class="sp-ccc-header">
        <div class="sp-ccc-icon" [style.background]="iconBg">
          <ng-content select="[slot=icon]" />
        </div>
        <div class="sp-ccc-meta">
          <div class="sp-ccc-name">{{ name }}</div>
          <code class="sp-ccc-key-pill">{{ categoryKey }}</code>
        </div>
        @if (configured) {
          <sp-admin-badge tone="success">Configured</sp-admin-badge>
        } @else {
          <sp-admin-badge tone="warning">{{ notSetLabel }}</sp-admin-badge>
        }
      </div>

      <!-- Description -->
      @if (description) {
        <p class="sp-ccc-desc">{{ description }}</p>
      }

      <!-- Display fields (provider / model / voice) -->
      <div class="sp-ccc-fields" [class.sp-ccc-fields--3col]="fields.length >= 3">
        @for (f of fields; track f.label) {
          <div class="sp-ccc-field-group">
            <div class="sp-ccc-field-label">{{ f.label }}</div>
            <div class="sp-ccc-field-value" [class.sp-ccc-field-value--mono]="f.mono">
              {{ f.value || '—' }}
            </div>
          </div>
        }
      </div>

      <!-- Actions -->
      <div class="sp-ccc-actions">
        <sp-admin-button variant="neutral" appearance="outline" [fullWidth]="true" (click)="configure.emit()">
          Configure
        </sp-admin-button>
        <sp-admin-button variant="neutral" appearance="outline" [disabled]="testBusy" (click)="test.emit()">
          {{ testBusy ? '…' : testLabel }}
        </sp-admin-button>
      </div>

    </div>
  `,
  styles: [`
    :host { display: contents; }

    .sp-ccc-card {
      background: #fff;
      border: 1.5px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      box-shadow: 0 1px 3px rgba(33,27,54,.05);
    }
    .sp-ccc-card--configured {
      border-color: var(--sp-admin-green, #13B07C);
    }

    .sp-ccc-header {
      display: flex;
      align-items: flex-start;
      gap: 10px;
    }
    .sp-ccc-icon {
      width: 38px;
      height: 38px;
      border-radius: 10px;
      background: var(--sp-admin-primary-bg, #EDEBFF);
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .sp-ccc-meta {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .sp-ccc-name {
      font-size: 13.5px;
      font-weight: 700;
      color: var(--sp-admin-text, #211B36);
    }
    .sp-ccc-key-pill {
      display: inline-block;
      font-size: 11px;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      color: var(--sp-admin-magenta, #B45CF0);
      background: var(--sp-admin-magenta-bg, #F2E9FF);
      padding: 2px 7px;
      border-radius: 5px;
      font-style: normal;
    }

    .sp-ccc-desc {
      font-size: 12.5px;
      color: var(--sp-admin-text-muted, #8B85A0);
      line-height: 1.5;
      margin: 0;
    }

    .sp-ccc-fields {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    .sp-ccc-fields--3col { grid-template-columns: 1fr 1fr 1fr; }

    .sp-ccc-field-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .sp-ccc-field-label {
      font-size: 11px;
      font-weight: 800;
      color: var(--sp-admin-text-muted, #8B85A0);
      letter-spacing: .07em;
      text-transform: uppercase;
    }
    .sp-ccc-field-value {
      height: 32px;
      border: 1.5px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 8px;
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      display: flex;
      align-items: center;
      padding: 0 10px;
      font-size: 13px;
      font-weight: 600;
      color: var(--sp-admin-text, #211B36);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .sp-ccc-field-value--mono {
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 12px;
    }

    .sp-ccc-actions {
      display: flex;
      gap: 8px;
      margin-top: 4px;
    }
  `],
})
export class SpAdminConfigCategoryCardComponent {
  @Input() name = '';
  @Input() categoryKey = '';
  @Input() description = '';
  @Input() configured = false;
  @Input() notSetLabel = 'Not set';
  @Input() iconBg = 'var(--sp-admin-primary-bg, #EDEBFF)';
  @Input() fields: ConfigCategoryField[] = [];
  @Input() testBusy = false;
  @Input() testLabel = 'Test';

  @Output() configure = new EventEmitter<void>();
  @Output() test = new EventEmitter<void>();
}
