import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminBadgeTone = 'success' | 'warning' | 'info' | 'danger' | 'neutral' | 'primary' | 'purple';
export type SpAdminBadgeAppearance = 'soft' | 'solid' | 'outline';
export type SpAdminBadgeSize = 'sm' | 'md';

// TailAdmin badge (shared/components/ui/badge): inline-flex rounded-full font-medium
// light/success: bg-success-50 text-success-600  light/error: bg-error-50 text-error-600
@Component({
  selector: 'sp-admin-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-adm-badge inline-flex items-center justify-center gap-1 rounded-full font-medium"
      [class]="hostClasses"
    >
      @if (dot) {
        <span class="sp-adm-badge-dot" aria-hidden="true"></span>
      }
      <ng-content />
    </span>
  `,
  styles: [`
    .sp-adm-badge    { white-space:nowrap; }
    .sp-adm-badge-sm { padding:2px 8px;  font-size:11px; line-height:1.4; font-weight:500; }
    .sp-adm-badge-md { padding:3px 10px; font-size:12px; line-height:1.4; font-weight:500; }

    /* Soft (TailAdmin light variant) */
    .sp-adm-badge-soft-success { background:#ecfdf3; color:#16a34a; }
    .sp-adm-badge-soft-warning { background:#fffbeb; color:#d97706; }
    .sp-adm-badge-soft-info    { background:#f0f9ff; color:#0ba5ec; }
    .sp-adm-badge-soft-primary { background:#ecf3ff; color:#465fff; }
    .sp-adm-badge-soft-danger  { background:#fef2f2; color:#ef4444; }
    .sp-adm-badge-soft-neutral { background:#f2f4f7; color:#344054; }
    .sp-adm-badge-soft-purple  { background:#f5f3ff; color:#7c3aed; }

    /* Solid */
    .sp-adm-badge-solid-success { background:#16a34a; color:#fff; }
    .sp-adm-badge-solid-warning { background:#f59e0b; color:#fff; }
    .sp-adm-badge-solid-info    { background:#0ba5ec; color:#fff; }
    .sp-adm-badge-solid-primary { background:#465fff; color:#fff; }
    .sp-adm-badge-solid-danger  { background:#ef4444; color:#fff; }
    .sp-adm-badge-solid-neutral { background:#344054; color:#fff; }
    .sp-adm-badge-solid-purple  { background:#7c3aed; color:#fff; }

    /* Outline */
    .sp-adm-badge-outline-success { background:transparent; color:#16a34a; box-shadow:0 0 0 1px #16a34a inset; }
    .sp-adm-badge-outline-warning { background:transparent; color:#d97706; box-shadow:0 0 0 1px #d97706 inset; }
    .sp-adm-badge-outline-info    { background:transparent; color:#0ba5ec; box-shadow:0 0 0 1px #0ba5ec inset; }
    .sp-adm-badge-outline-primary { background:transparent; color:#465fff; box-shadow:0 0 0 1px #465fff inset; }
    .sp-adm-badge-outline-danger  { background:transparent; color:#ef4444; box-shadow:0 0 0 1px #ef4444 inset; }
    .sp-adm-badge-outline-neutral { background:transparent; color:#344054; box-shadow:0 0 0 1px #344054 inset; }
    .sp-adm-badge-outline-purple  { background:transparent; color:#7c3aed; box-shadow:0 0 0 1px #7c3aed inset; }

    .sp-adm-badge-dot { width:6px; height:6px; border-radius:50%; background:currentColor; flex-shrink:0; }
  `],
})
export class SpAdminBadgeComponent {
  @Input() tone: SpAdminBadgeTone = 'neutral';
  @Input() appearance: SpAdminBadgeAppearance = 'soft';
  @Input() size: SpAdminBadgeSize = 'sm';
  @Input() dot = false;

  get hostClasses(): string {
    return `sp-adm-badge-${this.size} sp-adm-badge-${this.appearance}-${this.tone}`;
  }
}
