import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Adapts TailAdmin Layout One sticky header.
 * Source: templates/tailadmin/.../shared/layout/app-header/app-header.component.html
 * Pattern: sticky top-0 flex w-full bg-white border-b border-gray-200 z-[99999]
 *   inner: flex items-center justify-between grow xl:flex-row xl:px-6
 *   action zone: flex items-center gap-2 on right side.
 */
@Component({
  selector: 'sp-admin-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="sp-admin-header sticky top-0 flex w-full bg-white dark:bg-gray-900 border-b border-gray-200 dark:border-gray-800 z-[99999]">
      <div class="sp-admin-header-inner flex items-center justify-between grow px-3 py-3 xl:px-6 xl:py-4 gap-2 sm:gap-4">
        <ng-content />
      </div>
    </header>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminHeaderComponent {}
