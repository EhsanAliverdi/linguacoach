import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Adapts TailAdmin Layout One fixed sidebar.
 * Source: templates/tailadmin/.../shared/layout/app-sidebar/app-sidebar.component.html
 * Pattern: fixed left-0 top-0 h-screen, w-[290px] expanded / w-[90px] collapsed,
 *   bg-white border-r border-gray-200, transition-all duration-300.
 *   Hidden on mobile (translate-x-full), visible on xl (xl:translate-x-0).
 */
@Component({
  selector: 'sp-admin-sidebar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <aside
      class="fixed flex flex-col top-0 left-0 h-screen bg-white dark:bg-gray-900 text-gray-900 border-r border-gray-200 dark:border-gray-800 transition-all duration-300 ease-in-out z-[99] overflow-hidden"
      [ngClass]="{
        'w-[290px]': !collapsed,
        'w-[90px]': collapsed,
        '-translate-x-full xl:translate-x-0': true
      }"
    >
      <ng-content />
    </aside>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminSidebarComponent {
  @Input() collapsed = false;
}
