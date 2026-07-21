import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminBadgeComponent, SpAdminBadgeTone } from '../badge/sp-admin-badge.component';

/**
 * Sprint 14.2 — standardizes the "title + subtitle + count badge" header shape already used
 * ad-hoc across the app (e.g. the "Pricing overrides"/per-provider bar and the plain
 * description-row headers) so every titled table/section can share one component instead of each
 * page hand-rolling its own markup. `count`/`countLabel` are optional — omit both to get a plain
 * title+description header.
 *
 * Two variants, matching the two visual patterns that already existed in this codebase before
 * this component did:
 * - `bar` (default) — the tinted bar with padding + bottom border (previously the hand-rolled
 *   `.sp-admin-provider-header` class), used as a `padding="none"` card's `[slot=header]` — the
 *   card itself gives that slot zero padding, so the header supplies its own.
 * - `plain` — no background/padding (previously `.sp-admin-section-header-row`), used as a
 *   floating description row above a section (not inside a padding="none" card header slot).
 */
@Component({
  selector: 'sp-admin-section-header',
  standalone: true,
  imports: [CommonModule, SpAdminBadgeComponent],
  template: `
    <div class="sp-sh-root" [class.sp-sh-root--bar]="variant === 'bar'">
      <div class="sp-sh-main">
        <span class="sp-sh-title" [class.sp-sh-title--bar]="variant === 'bar'">{{ title }}</span>
        @if (description) {
          <span class="sp-sh-desc">{{ description }}</span>
        }
      </div>
      <div class="sp-sh-actions">
        @if (count !== null) {
          <sp-admin-badge [tone]="countTone">{{ count }}{{ countLabel ? ' ' + countLabel : '' }}</sp-admin-badge>
        }
        <ng-content select="[slot=actions]" />
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-sh-root {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 14px;
    }
    .sp-sh-root--bar {
      align-items: center;
      margin-bottom: 0;
      padding: 13px 18px;
      background: var(--sp-admin-surface-subtle, #FAFAFE);
      border-bottom: 1px solid var(--sp-admin-border, #ECE9F5);
    }
    .sp-sh-main { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .sp-sh-title {
      font-size: 13px;
      font-weight: 600;
      color: var(--sp-admin-text,#0F172A);
      line-height: 1.35;
      letter-spacing: 0.01em;
    }
    .sp-sh-title--bar { font-size: 14px; font-weight: 800; }
    .sp-sh-desc {
      font-size: 12px;
      color: var(--sp-admin-text-muted,#64748B);
      line-height: 1.45;
    }
    .sp-sh-actions { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
  `],
})
export class SpAdminSectionHeaderComponent {
  @Input() title = '';
  @Input() description = '';
  /** Optional count badge, e.g. `[count]="overrides().length" countLabel="active"` → "3 active".
   *  Accepts a string too for ratio-style counts, e.g. `[count]="'5/8'" countLabel="configured"`. */
  @Input() count: number | string | null = null;
  @Input() countLabel = '';
  @Input() countTone: SpAdminBadgeTone = 'neutral';
  /** 'bar' (default) — tinted bar for a padding="none" card's [slot=header]. 'plain' — no
   *  background, for a floating description row above a section. */
  @Input() variant: 'bar' | 'plain' = 'bar';
}
