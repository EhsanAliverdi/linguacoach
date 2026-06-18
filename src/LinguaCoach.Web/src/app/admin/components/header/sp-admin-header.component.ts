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
    <header class="sticky top-0 flex w-full bg-white border-gray-200 z-[99999] dark:border-gray-800 dark:bg-gray-900 xl:border-b">
      <div class="flex flex-col items-center justify-between grow xl:flex-row xl:px-6">
        <div class="flex items-center justify-between w-full gap-2 px-3 py-3 border-b border-gray-200 dark:border-gray-800 sm:gap-4 xl:justify-normal xl:border-b-0 xl:px-0 lg:py-4">
          <ng-content />
        </div>
        <div class="flex items-center justify-between w-full gap-4 px-5 py-4 xl:flex shadow-theme-md xl:justify-end xl:px-0 xl:shadow-none">
          <div class="flex items-center gap-2 2xsm:gap-3">
            <sp-admin-theme-toggle />
            <ng-content select="[actions]" />
          </div>
          <ng-content select="[user]" />
        </div>
      </div>
    </header>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminHeaderComponent {}
