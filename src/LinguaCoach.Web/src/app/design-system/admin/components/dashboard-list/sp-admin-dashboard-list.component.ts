import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface DashboardListItem {
  id?: string;
  /** Text shown in first avatar square. If omitted, no avatar is shown. */
  avatarText?: string;
  /** CSS background for the avatar square. Defaults to indigo. */
  avatarBg?: string;
  /** CSS color for avatar text. Defaults to primary. */
  avatarColor?: string;
  /** Primary label line */
  title: string;
  /** Secondary/sub-label line */
  sub?: string;
  /** Sub-label CSS color */
  subColor?: string;
  /** Right-side value (large number / streak days) */
  value?: string | number;
  /** Small label below value (e.g. "days") */
  valueSub?: string;
  /** CSS color for value. Defaults to ink. */
  valueColor?: string;
  /** Emoji or text prefix before title (e.g. medal "🥇") */
  prefix?: string;
  /** If true, title is styled urgent/warning */
  urgent?: boolean;
  /** Router link path for the whole row */
  link?: string;
}

@Component({
  selector: 'sp-admin-dashboard-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="sp-dl-root" [attr.aria-label]="ariaLabel || null">
      @for (item of items; track item.id ?? item.title; let last = $last) {
        @if (item.link) {
          <a [routerLink]="item.link" class="sp-dl-row sp-dl-row--link" [class.sp-dl-row--last]="last">
            <ng-container *ngTemplateOutlet="rowContent; context: { item: item }" />
          </a>
        } @else {
          <div class="sp-dl-row" [class.sp-dl-row--last]="last">
            <ng-container *ngTemplateOutlet="rowContent; context: { item: item }" />
          </div>
        }
      }
    </div>

    <ng-template #rowContent let-item="item">
      @if (item.prefix) {
        <span class="sp-dl-prefix">{{ item.prefix }}</span>
      }
      @if (item.avatarText) {
        <div class="sp-dl-avatar"
          [style.background]="item.avatarBg || 'var(--sp-admin-primary-bg, #EDEBFF)'"
          [style.color]="item.avatarColor || 'var(--sp-admin-primary-hover, #3A2EA8)'">
          {{ item.avatarText }}
        </div>
      }
      <div class="sp-dl-body">
        <div class="sp-dl-title" [class.sp-dl-title--urgent]="item.urgent">{{ item.title }}</div>
        @if (item.sub) {
          <div class="sp-dl-sub" [style.color]="item.subColor || null">{{ item.sub }}</div>
        }
      </div>
      @if (item.value !== undefined) {
        <div class="sp-dl-value-wrap">
          <div class="sp-dl-value" [style.color]="item.valueColor || null">{{ item.value }}</div>
          @if (item.valueSub) {
            <div class="sp-dl-value-sub">{{ item.valueSub }}</div>
          }
        </div>
      }
    </ng-template>
  `,
  styles: [`
    :host { display: block; }
    .sp-dl-root { display: flex; flex-direction: column; }
    .sp-dl-row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 0;
      border-bottom: 1px solid var(--sp-admin-border, #ECE9F5);
      min-width: 0;
    }
    .sp-dl-row--last { border-bottom: none; }
    .sp-dl-row--link { text-decoration: none; color: inherit; cursor: pointer; transition: opacity .1s; }
    .sp-dl-row--link:hover { opacity: .75; }
    .sp-dl-prefix { font-size: 15px; width: 20px; flex-shrink: 0; }
    .sp-dl-avatar {
      width: 30px; height: 30px; border-radius: 8px;
      display: grid; place-items: center;
      font-size: 12px; font-weight: 800; flex-shrink: 0;
    }
    .sp-dl-body { flex: 1; min-width: 0; }
    .sp-dl-title {
      font-size: 13px; font-weight: 700;
      color: var(--sp-admin-text, #211B36);
      line-height: 1.2; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .sp-dl-title--urgent { color: var(--sp-admin-danger-ink, #DC2626); }
    .sp-dl-sub {
      font-size: 11.5px; font-weight: 600;
      color: var(--sp-admin-text-muted, #8B85A0);
      margin-top: 1px;
    }
    .sp-dl-value-wrap { text-align: right; flex-shrink: 0; }
    .sp-dl-value {
      font-size: 16px; font-weight: 800;
      color: var(--sp-admin-text, #211B36);
      letter-spacing: -.02em;
    }
    .sp-dl-value-sub {
      font-size: 10px;
      color: var(--sp-admin-text-muted, #8B85A0);
    }
  `],
})
export class SpAdminDashboardListComponent {
  @Input() items: DashboardListItem[] = [];
  @Input() ariaLabel = '';
}
