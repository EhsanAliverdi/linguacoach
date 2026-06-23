import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export type RingTone = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate' | 'danger';

@Component({
  selector: 'sp-admin-ring-metric',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-ring-root" [attr.aria-label]="ariaLabel || label">
      <svg
        class="sp-ring-svg"
        [attr.width]="size"
        [attr.height]="size"
        [attr.viewBox]="'0 0 ' + size + ' ' + size"
        role="img"
        [attr.aria-label]="label + ': ' + pct + '%'"
      >
        <!-- Track -->
        <circle
          class="sp-ring-track"
          [attr.cx]="cx"
          [attr.cy]="cy"
          [attr.r]="r"
          fill="none"
          stroke-width="5"
        />
        <!-- Fill — only when pct > 0 -->
        @if (pct > 0) {
          <circle
            class="sp-ring-fill sp-ring-fill-{{ tone }}"
            [attr.cx]="cx"
            [attr.cy]="cy"
            [attr.r]="r"
            fill="none"
            stroke-width="5"
            stroke-linecap="round"
            [attr.stroke-dasharray]="circumference"
            [attr.stroke-dashoffset]="dashOffset"
            [attr.transform]="'rotate(-90 ' + cx + ' ' + cy + ')'"
          />
        }
        <!-- Center value -->
        <text
          class="sp-ring-val"
          [attr.x]="cx"
          [attr.y]="cy + 5"
          text-anchor="middle"
          dominant-baseline="middle"
        >{{ displayValue }}</text>
      </svg>
      @if (label) {
        <div class="sp-ring-label">{{ label }}</div>
      }
      @if (sub) {
        <div class="sp-ring-sub">{{ sub }}</div>
      }
    </div>
  `,
  styles: [`
    .sp-ring-root { display: flex; flex-direction: column; align-items: center; gap: 6px; }
    .sp-ring-svg { display: block; overflow: visible; }
    .sp-ring-track { stroke: var(--sp-admin-border, #ECE9F5); }
    .sp-ring-fill-indigo { stroke: var(--sp-admin-primary, #5B4BE8); }
    .sp-ring-fill-green  { stroke: var(--sp-admin-green, #13B07C); }
    .sp-ring-fill-violet { stroke: var(--sp-admin-violet, #7C3AED); }
    .sp-ring-fill-amber  { stroke: var(--sp-admin-amber, #D97706); }
    .sp-ring-fill-teal   { stroke: var(--sp-admin-teal, #0D9488); }
    .sp-ring-fill-slate  { stroke: var(--sp-admin-slate, #475569); }
    .sp-ring-fill-danger { stroke: var(--sp-admin-danger, #EF4444); }
    .sp-ring-val {
      font-size: 13px; font-weight: 800;
      fill: var(--sp-admin-text, #211B36);
      font-family: inherit;
    }
    .sp-ring-label {
      font-size: 11px; font-weight: 700; color: var(--sp-admin-text-secondary, #4B4462);
      text-align: center; line-height: 1.3;
    }
    .sp-ring-sub {
      font-size: 10px; color: var(--sp-admin-text-muted, #8B85A0);
      text-align: center; margin-top: -4px;
    }
  `],
})
export class SpAdminRingMetricComponent {
  /** 0–100 percentage to fill */
  @Input() pct = 0;
  @Input() label = '';
  @Input() sub = '';
  @Input() tone: RingTone = 'indigo';
  @Input() size = 72;
  /** Override the center text (defaults to pct%) */
  @Input() displayValue = '';
  @Input() ariaLabel = '';

  get cx() { return this.size / 2; }
  get cy() { return this.size / 2; }
  get r() { return (this.size - 12) / 2; }
  get circumference() { return 2 * Math.PI * this.r; }
  get dashOffset() {
    const clamped = Math.max(0, Math.min(100, this.pct));
    return this.circumference * (1 - clamped / 100);
  }

  ngOnChanges() {
    if (!this.displayValue) {
      this.displayValue = Math.round(this.pct) + '%';
    }
  }
}
