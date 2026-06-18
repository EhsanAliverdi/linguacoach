import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminBadgeTone = 'success' | 'warning' | 'info' | 'danger' | 'neutral' | 'primary';

// TailAdmin badge pattern (shared/components/ui/badge/badge.component.html):
// inline-flex items-center px-2.5 py-0.5 justify-center gap-1 rounded-full font-medium
// light/success: bg-success-50 text-success-600  light/error: bg-error-50 text-error-600
// light/warning: bg-warning-50 text-warning-600  light/primary: bg-brand-50 text-brand-500
@Component({
  selector: 'sp-admin-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="sp-adm-badge inline-flex items-center px-2.5 py-0.5 justify-center gap-1 rounded-full font-medium text-xs"
      [class]="'sp-adm-badge-' + tone"
    ><ng-content /></span>
  `,
  styles: [`
    /* TailAdmin-backed: uses TailAdmin badge light variant color mapping */
    .sp-adm-badge-success { background: #ecfdf3; color: #16a34a; }
    .sp-adm-badge-warning { background: #fffbeb; color: #d97706; }
    .sp-adm-badge-info    { background: #f0f9ff; color: #0ba5ec; }
    .sp-adm-badge-primary { background: #ecf3ff; color: #465fff; }
    .sp-adm-badge-danger  { background: #fef2f2; color: #ef4444; }
    .sp-adm-badge-neutral { background: #f2f4f7; color: #344054; }
  `],
})
export class SpAdminBadgeComponent {
  @Input() tone: SpAdminBadgeTone = 'neutral';
}
