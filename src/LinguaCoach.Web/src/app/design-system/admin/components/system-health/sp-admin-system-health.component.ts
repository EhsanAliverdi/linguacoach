import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface SystemService {
  name: string;
  ms: number;
}

export interface SystemFooterRow {
  k: string;
  v: string;
}

@Component({
  selector: 'sp-admin-system-health',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="sp-syshealth">
      <div class="sp-syshealth-header">
        <span class="sp-syshealth-title">System health</span>
        <span class="sp-syshealth-status">
          <span class="sp-syshealth-dot"></span>All clear
        </span>
      </div>
      @for (svc of services; track svc.name; let last = $last) {
        <div class="sp-syshealth-row" [class.sp-syshealth-row--last]="last">
          <span class="sp-syshealth-svc-dot"></span>
          <span class="sp-syshealth-svc-name">{{ svc.name }}</span>
          <div class="sp-syshealth-bar-wrap">
            <div class="sp-syshealth-bar"
              [style.width]="barPct(svc.ms) + '%'"
              [style.background]="barColor(svc.ms)"></div>
          </div>
          <span class="sp-syshealth-ms">{{ svc.ms }}ms</span>
        </div>
      }
      @if (footer.length) {
        <div class="sp-syshealth-footer">
          @for (row of footer; track row.k) {
            <div class="sp-syshealth-footer-row">
              <span class="sp-syshealth-footer-k">{{ row.k }}</span>
              <span class="sp-syshealth-footer-v">{{ row.v }}</span>
            </div>
          }
        </div>
      }
      @if (diagnosticsLink) {
        <a [routerLink]="diagnosticsLink" class="sp-syshealth-link">View diagnostics →</a>
      }
    </div>
  `,
  styles: [`
    .sp-syshealth { display: flex; flex-direction: column; }
    .sp-syshealth-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px; }
    .sp-syshealth-title { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text, #211B36); }
    .sp-syshealth-status {
      font-size: 12px; font-weight: 700; color: var(--sp-dash-latency-good, #13B07C);
      display: flex; align-items: center; gap: 4px;
    }
    .sp-syshealth-dot {
      display: inline-block; width: 7px; height: 7px; border-radius: 50%;
      background: var(--sp-dash-latency-good, #13B07C);
      animation: sp-hpulse 2s infinite;
    }
    @keyframes sp-hpulse { 0%,100%{opacity:1} 50%{opacity:.4} }
    .sp-syshealth-svc-dot {
      display: inline-block; width: 7px; height: 7px; border-radius: 50%;
      background: var(--sp-dash-latency-good, #13B07C); flex-shrink: 0;
    }
    .sp-syshealth-row {
      display: flex; align-items: center; gap: 10px;
      padding: 8px 0; border-bottom: 1px solid var(--sp-admin-border, #ECE9F5);
    }
    .sp-syshealth-row--last { border-bottom: none; }
    .sp-syshealth-svc-name { flex: 1; font-size: 12.5px; font-weight: 600; color: var(--sp-admin-text, #211B36); }
    .sp-syshealth-bar-wrap {
      width: var(--sp-dash-latency-bar-w, 48px); height: var(--sp-dash-latency-bar-h, 4px);
      border-radius: 99px; background: #F0EEF8; overflow: hidden;
    }
    .sp-syshealth-bar { height: 100%; border-radius: 99px; }
    .sp-syshealth-ms { font-size: 11px; color: var(--sp-admin-text-muted, #8B85A0); font-weight: 700; width: 36px; text-align: right; }
    .sp-syshealth-footer {
      margin-top: 14px; padding-top: 14px;
      border-top: 1px solid var(--sp-admin-border, #ECE9F5);
      display: flex; flex-direction: column; gap: 8px;
    }
    .sp-syshealth-footer-row { display: flex; justify-content: space-between; }
    .sp-syshealth-footer-k { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-syshealth-footer-v { font-size: 12px; font-weight: 700; color: var(--sp-admin-text, #211B36); }
    .sp-syshealth-link {
      margin-top: 14px; font-size: 12.5px; font-weight: 700;
      color: var(--sp-admin-primary, #5B4BE8); text-decoration: none;
    }
    .sp-syshealth-link:hover { color: var(--sp-admin-primary-hover, #3A2EA8); }
  `],
})
export class SpAdminSystemHealthComponent {
  @Input() services: SystemService[] = [];
  @Input() footer: SystemFooterRow[] = [];
  @Input() diagnosticsLink: string | null = null;

  barPct(ms: number): number { return Math.min(100, Math.round((ms / 250) * 100)); }
  barColor(ms: number): string {
    return ms < 100
      ? 'var(--sp-dash-latency-good,#13B07C)'
      : ms < 200
      ? 'var(--sp-dash-latency-warn,#F0982C)'
      : 'var(--sp-dash-latency-bad,#EF4444)';
  }
}
