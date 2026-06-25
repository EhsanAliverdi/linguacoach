import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

const PALETTE = ['#5B4BE8','#16a34a','#D97706','#0891b2','#7C3AED','#DC2626','#0F766E'];

const SIZE_MAP: Record<string, string> = {
  xs: '24px',
  sm: '30px',
  md: '36px',
  lg: '44px',
};

const FONT_MAP: Record<string, string> = {
  xs: '10px',
  sm: '11px',
  md: '13px',
  lg: '15px',
};

/**
 * Circular avatar showing initials. Deterministic colour from seed string.
 * Use seed="" with bg="" to override colour directly.
 *
 * Inputs:
 *   initials  — 1-2 character display text (required)
 *   seed      — string to derive colour from (email, id, name)
 *   size      — 'xs' | 'sm' | 'md' | 'lg'  (default: 'sm')
 *   ariaLabel — accessible label
 */
@Component({
  selector: 'sp-admin-avatar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-adm-av"
      [style.width]="sz"
      [style.height]="sz"
      [style.background]="bg"
      [style.font-size]="fs"
      [attr.aria-label]="ariaLabel || null"
      [attr.title]="ariaLabel || null"
    >{{ initials }}</span>
  `,
  styles: [`
    .sp-adm-av {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      font-weight: 800;
      color: #fff;
      line-height: 1;
      flex-shrink: 0;
      user-select: none;
    }
  `],
})
export class SpAdminAvatarComponent implements OnChanges {
  @Input() initials = '?';
  @Input() seed = '';
  @Input() size: 'xs' | 'sm' | 'md' | 'lg' = 'sm';
  @Input() ariaLabel = '';

  bg = PALETTE[0];

  get sz(): string { return SIZE_MAP[this.size] ?? '30px'; }
  get fs(): string { return FONT_MAP[this.size] ?? '11px'; }

  ngOnChanges(): void {
    const s = this.seed || this.initials;
    let h = 0;
    for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) & 0xFFFFFF;
    this.bg = PALETTE[Math.abs(h) % PALETTE.length];
  }
}
