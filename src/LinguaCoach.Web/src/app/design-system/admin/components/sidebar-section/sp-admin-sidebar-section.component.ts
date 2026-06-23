import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Sidebar section heading label.
 * Hides in collapsed mode to avoid layout overflow.
 * Replaces the duplicated <p class="text-xs font-semibold uppercase ..."> pattern.
 *
 * Usage:
 *   <sp-admin-sidebar-section label="Menu" [collapsed]="collapsed()" />
 */
@Component({
  selector: 'sp-admin-sidebar-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (!collapsed) {
      <p class="mb-2 px-3 text-[9.5px] font-extrabold uppercase tracking-[.1em]" style="color:var(--sp-admin-text-faint)">{{ label }}</p>
    }
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminSidebarSectionComponent {
  @Input({ required: true }) label = '';
  @Input() collapsed = false;
}
