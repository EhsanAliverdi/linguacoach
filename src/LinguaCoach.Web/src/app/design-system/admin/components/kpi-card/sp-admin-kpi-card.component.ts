import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

export type KpiVariant = 'indigo' | 'green' | 'violet' | 'amber' | 'teal' | 'slate' | 'coral';
export type KpiLayout = 'standard' | 'tile';
export type KpiIcon =
  | 'users' | 'user' | 'activity' | 'zap' | 'dollar' | 'document' | 'check'
  | 'plus' | 'clock' | 'alert' | 'shield' | 'database' | 'book'
  | 'microphone' | 'headphones' | 'target' | 'refresh'
  | 'layers' | 'bar-chart' | 'bell' | 'mail' | 'phone' | 'send' | 'key' | 'cpu';

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
  layers:      '<polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/>',
  'bar-chart': '<line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>',
  bell:        '<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/>',
  mail:        '<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/>',
  phone:       '<path d="M10.5 1.5H8.25A2.25 2.25 0 0 0 6 3.75v16.5a2.25 2.25 0 0 0 2.25 2.25h7.5A2.25 2.25 0 0 0 18 20.25V3.75a2.25 2.25 0 0 0-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 18.75h3"/>',
  send:        '<line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/>',
  key:         '<path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/>',
  cpu:         '<rect x="4" y="4" width="16" height="16" rx="2"/><rect x="9" y="9" width="6" height="6"/><line x1="9" y1="1" x2="9" y2="4"/><line x1="15" y1="1" x2="15" y2="4"/><line x1="9" y1="20" x2="9" y2="23"/><line x1="15" y1="20" x2="15" y2="23"/><line x1="20" y1="9" x2="23" y2="9"/><line x1="20" y1="14" x2="23" y2="14"/><line x1="1" y1="9" x2="4" y2="9"/><line x1="1" y1="14" x2="4" y2="14"/>',
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
        <div class="sp-kpi-label" [class.sp-kpi-label--tile]="layout === 'tile'">{{ label }}</div>
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
    /* Icon wrapper — normalizes both built-in and projected icons to 20px */
    .sp-kpi-icon-svg { display: flex; align-items: center; justify-content: center; line-height: 0; }
    .sp-kpi-icon-svg svg { width: 20px; height: 20px; flex-shrink: 0; }
    /* Normalize projected slot=icon content to same size */
    :host ::ng-deep .sp-kpi-icon svg { width: 20px !important; height: 20px !important; flex-shrink: 0; }
    .sp-kpi-placeholder { color: #BDB8CC; }
    .sp-kpi-fallback { font-size: 14px; font-style: italic; color: #8B85A0; }

    /* tile layout — grid ensures icon strip always fills full card height */
    .sp-kpi-card--tile {
      display: grid;
      grid-template-columns: var(--sp-dash-kpi-tile-w, 56px) 1fr;
      align-items: stretch;
      gap: 0;
      padding: 0;
      overflow: hidden;
      border-radius: 12px;
      min-height: 80px;
      height: 100%;
    }
    .sp-kpi-icon--tile {
      grid-column: 1;
      width: auto;
      height: auto;
      align-self: stretch;
      border-radius: 0;
      border-right: 1px solid var(--sp-admin-border, #ECE9F5);
      display: grid;
      place-items: center;
    }
    .sp-kpi-body--tile { grid-column: 2; padding: 13px 15px; min-width: 0; align-self: center; }
    .sp-kpi-value--tile {
      font-size: 24px;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      max-width: 100%;
    }
    /* label in tile: same as standard but fits narrower body */
    .sp-kpi-label--tile { margin-bottom: 6px; }
    .sp-kpi-delta {
      font-size: 11.5px; font-weight: 600; margin-top: 4px; color: #8B85A0;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;
    }

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
    // Size is controlled by CSS (.sp-kpi-icon-svg svg); attrs are set to 20 as fallback
    const svg = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(svg);
  }
}
