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
    /* Base — .adm-badge: inline-flex, gap 5px, 11.5px/700, 3/10px padding, 99px radius */
    .sp-adm-badge    { white-space:nowrap; border-radius:99px; }
    .sp-adm-badge-sm { padding:3px 10px; font-size:11.5px; line-height:1.4; font-weight:700; }
    .sp-adm-badge-md { padding:4px 12px; font-size:12px;   line-height:1.4; font-weight:700; }

    /* Soft — matched to standalone adm-badge-* tones exactly */
    .sp-adm-badge-soft-success { background:#E0F6EE; color:#13B07C; }
    .sp-adm-badge-soft-warning { background:#FFF1DC; color:#B26410; }
    .sp-adm-badge-soft-info    { background:#EDEBFF; color:#5B4BE8; }
    .sp-adm-badge-soft-primary { background:#EDEBFF; color:#3A2EA8; }
    .sp-adm-badge-soft-danger  { background:#FEE2E2; color:#DC2626; }
    .sp-adm-badge-soft-neutral { background:#F6F4FB; color:#8B85A0; }
    .sp-adm-badge-soft-purple  { background:#F2E9FF; color:#B45CF0; }

    /* Solid */
    .sp-adm-badge-solid-success { background:#13B07C; color:#fff; }
    .sp-adm-badge-solid-warning { background:#F0982C; color:#fff; }
    .sp-adm-badge-solid-info    { background:#5B4BE8; color:#fff; }
    .sp-adm-badge-solid-primary { background:#5B4BE8; color:#fff; }
    .sp-adm-badge-solid-danger  { background:#EF4444; color:#fff; }
    .sp-adm-badge-solid-neutral { background:#8B85A0; color:#fff; }
    .sp-adm-badge-solid-purple  { background:#B45CF0; color:#fff; }

    /* Outline */
    .sp-adm-badge-outline-success { background:transparent; color:#13B07C; box-shadow:0 0 0 1px #13B07C inset; }
    .sp-adm-badge-outline-warning { background:transparent; color:#F0982C; box-shadow:0 0 0 1px #F0982C inset; }
    .sp-adm-badge-outline-info    { background:transparent; color:#5B4BE8; box-shadow:0 0 0 1px #5B4BE8 inset; }
    .sp-adm-badge-outline-primary { background:transparent; color:#5B4BE8; box-shadow:0 0 0 1px #5B4BE8 inset; }
    .sp-adm-badge-outline-danger  { background:transparent; color:#EF4444; box-shadow:0 0 0 1px #EF4444 inset; }
    .sp-adm-badge-outline-neutral { background:transparent; color:#8B85A0; box-shadow:0 0 0 1px #8B85A0 inset; }
    .sp-adm-badge-outline-purple  { background:transparent; color:#B45CF0; box-shadow:0 0 0 1px #B45CF0 inset; }

    /* Dot — 7px matches .adm-dot */
    .sp-adm-badge-dot { width:7px; height:7px; border-radius:50%; background:currentColor; flex-shrink:0; }
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
