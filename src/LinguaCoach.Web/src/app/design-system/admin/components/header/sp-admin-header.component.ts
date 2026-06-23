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
    <header class="sticky top-0 z-[999] flex w-full items-center bg-white px-4 dark:bg-gray-900 xl:px-6" style="height:var(--sp-admin-header-h);border-bottom:1px solid var(--sp-admin-border);">
      <div class="flex w-full items-center justify-between gap-3">
        <div class="flex items-center gap-2">
          <ng-content />
        </div>
        <div class="flex items-center gap-2">
          <sp-admin-theme-toggle />
          <ng-content select="[actions]" />
          <ng-content select="[user]" />
        </div>
      </div>
    </header>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminHeaderComponent {}
