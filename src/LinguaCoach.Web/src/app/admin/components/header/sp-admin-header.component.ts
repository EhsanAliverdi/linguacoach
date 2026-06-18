import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminThemeToggleComponent } from '../theme-toggle/sp-admin-theme-toggle.component';

/**
 * Admin sticky header wrapper.
 * Adapts TailAdmin Layout One app-header pattern.
 * Source: shared/components/layout/app-header/app-header.component.html
 * Pattern: sticky top-0 flex w-full bg-white border-b border-gray-200 z-[99999]
 *   inner: flex items-center justify-between grow xl:flex-row xl:px-6
 *   left zone: [left] slot — breadcrumb, page title, sidebar toggle
 *   right zone: [actions] slot — user dropdown, notifications, theme toggle
 *   Theme toggle is always rendered in the right action zone unless hideThemeToggle=true.
 *
 * Usage:
 *   <sp-admin-header>
 *     <ng-container left>...</ng-container>
 *     <ng-container actions>...</ng-container>
 *   </sp-admin-header>
 */
@Component({
  selector: 'sp-admin-header',
  standalone: true,
  imports: [CommonModule, SpAdminThemeToggleComponent],
  template: `
    <header class="sp-admin-header sticky top-0 flex w-full bg-white dark:bg-gray-900 border-b border-gray-200 dark:border-gray-800 z-[99999]">
      <div class="sp-admin-header-inner flex items-center justify-between grow px-3 py-3 xl:px-6 xl:py-4 gap-2 sm:gap-4">
        <!-- Left zone: breadcrumb / sidebar toggle -->
        <div class="sp-admin-header-left flex items-center gap-2 min-w-0">
          <ng-content select="[left]" />
          <!-- Fallback: general content if no named slots used -->
          <ng-content />
        </div>

        <!-- Right action zone: theme toggle + projected actions -->
        <div class="sp-admin-header-actions flex items-center gap-1 sm:gap-2 shrink-0">
          <ng-content select="[actions]" />
          <sp-admin-theme-toggle />
        </div>
      </div>
    </header>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminHeaderComponent {}
