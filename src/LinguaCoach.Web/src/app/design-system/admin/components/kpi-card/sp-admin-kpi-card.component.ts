import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

export type KpiVariant = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate' | 'coral';
export type KpiLayout = 'standard' | 'tile';
export type KpiIcon =
  | 'users' | 'user' | 'activity' | 'zap' | 'dollar' | 'document' | 'check'
  | 'plus' | 'clock' | 'alert' | 'shield' | 'database' | 'book'
  | 'microphone' | 'headphones' | 'target' | 'refresh';

const ICON_PATHS: Record<KpiIcon, string> = {
  users:       '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>',
  user:        '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  activity:    '<polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>',
  zap:         '<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>',
  dollar:      '<line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>',
  document:    '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/>',
  check:       '<polyline points="20 6 9 17 4 12"/>',
  plus:        '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/>',
  clock:       '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>',
  alert:       '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>',
  shield:      '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
  database:    '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>',
  book:        '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>',
  microphone:  '<path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><line x1="12" y1="19" x2="12" y2="23"/><line x1="8" y1="23" x2="16" y2="23"/>',
  headphones:  '<path d="M3 18v-6a9 9 0 0 1 18 0v6"/><path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z"/>',
  target:      '<circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>',
  refresh:     '<polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
};

@Component({
  selector: 'sp-admin-kpi-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-kpi-card" [class.sp-kpi-card--tile]="layout === 'tile'">
      <div class="sp-kpi-icon" [class]="'sp-kpi-icon-' + variant" [class.sp-kpi-icon--tile]="layout === 'tile'">
        @if (icon) {
          <span [innerHTML]="iconSvg" class="sp-kpi-icon-svg"></span>
        } @else {
          <ng-content select="[slot=icon]" />
        }
      </div>
      <div class="sp-kpi-body" [class.sp-kpi-body--tile]="layout === 'tile'">
        <div class="sp-kpi-label">{{ label }}</div>
        <div class="sp-kpi-value" [class.sp-kpi-value--tile]="layout === 'tile'">
          @if (loading) {
            <span class="sp-kpi-placeholder">—</span>
          } @else if (error) {
            <span class="sp-kpi-fallback">{{ fallback }}</span>
          } @else if (value !== null && value !== undefined) {
            {{ value }}
          } @else {
            <ng-content />
          }
        </div>
        @if (resolvedDelta) {
          <div class="sp-kpi-delta" [style.color]="deltaColor || null">{{ resolvedDelta }}</div>
        }
      </div>
    </div>
  `,
  styles: [`
    .sp-kpi-card {
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      align-items: center;
      gap: 16px;
      box-shadow: 0 1px 2px rgba(33,27,54,.06);
    }
    .sp-kpi-icon {
      width: 40px; height: 40px;
      border-radius: 11px;
      display: grid; place-items: center;
      flex-shrink: 0;
    }
    .sp-kpi-body { min-width: 0; flex: 1; }
    .sp-kpi-label {
      font-size: 10.5px; font-weight: 800;
      color: #8B85A0; text-transform: uppercase;
      letter-spacing: .09em; margin-bottom: 4px;
    }
    .sp-kpi-value {
      font-size: 30px; font-weight: 800;
      color: #211B36; letter-spacing: -.04em; line-height: 1;
    }
    .sp-kpi-delta { font-size: 11.5px; font-weight: 600; margin-top: 5px; color: #8B85A0; }
    .sp-kpi-icon-svg { display: flex; align-items: center; justify-content: center; line-height: 0; }
    .sp-kpi-placeholder { color: #BDB8CC; }
    .sp-kpi-fallback { font-size: 14px; font-style: italic; color: #8B85A0; }

    /* tile layout */
    .sp-kpi-card--tile { padding: 0; gap: 0; overflow: hidden; border-radius: 12px; align-items: stretch; }
    .sp-kpi-icon--tile {
      width: var(--sp-dash-kpi-tile-w, 56px);
      border-radius: 0;
      border-right: 1px solid var(--sp-admin-border, #ECE9F5);
      min-height: 72px; height: auto;
    }
    .sp-kpi-body--tile { padding: 13px 15px; }
    .sp-kpi-value--tile { font-size: 24px; }

    /* variants */
    .sp-kpi-icon-indigo { background: #EDEBFF; color: #5B4BE8; }
    .sp-kpi-icon-green  { background: #E0F6EE; color: #13B07C; }
    .sp-kpi-icon-violet { background: #F2E9FF; color: #B45CF0; }
    .sp-kpi-icon-amber  { background: #FFF1DC; color: #F0982C; }
    .sp-kpi-icon-teal   { background: #E0F6EE; color: #0A7468; }
    .sp-kpi-icon-slate  { background: #F6F4FB; color: #8B85A0; }
    .sp-kpi-icon-coral  { background: #FFEAE4; color: #FF7A59; }
  `],
})
export class SpAdminKpiCardComponent {
  constructor(private sanitizer: DomSanitizer) {}

  @Input() label = '';
  @Input() variant: KpiVariant = 'indigo';
  @Input() layout: KpiLayout = 'standard';
  @Input() delta: string | null = null;
  @Input() deltaColor: string | null = null;
  /** Alias for delta */
  @Input() subtitle: string | null = null;
  @Input() icon: KpiIcon | null = null;
  @Input() value: string | number | null = null;
  @Input() loading = false;
  @Input() error = false;
  @Input() fallback = 'N/A';

  get resolvedDelta(): string | null {
    return this.delta ?? this.subtitle ?? null;
  }

  get iconSvg(): SafeHtml | null {
    if (!this.icon) return null;
    const paths = ICON_PATHS[this.icon] ?? '';
    const size = this.layout === 'tile' ? 20 : 18;
    const svg = `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(svg);
  }
}
