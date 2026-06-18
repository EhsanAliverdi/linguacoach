import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Adapts TailAdmin Layout One shell structure.
 * Source: templates/tailadmin/.../shared/layout/app-layout/app-layout.component.html
 * Pattern: min-h-screen xl:flex, flex-1 with xl:ml-[290px]/xl:ml-[90px] transition.
 */
@Component({
  selector: 'sp-admin-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- TailAdmin Layout One: min-h-screen xl:flex outer shell -->
    <div class="sp-admin-shell min-h-screen xl:flex">
      <!-- Sidebar slot (fixed, handled by sp-admin-sidebar) -->
      <div>
        <ng-content select="[slot=sidebar]" />
      </div>
      <!-- Main content area: mirrors TailAdmin flex-1 transition-all with ml offset -->
      <div
        class="sp-admin-main flex-1 transition-all duration-300 ease-in-out"
        [class.sp-main-collapsed]="collapsed"
        [ngClass]="collapsed ? 'xl:ml-[90px]' : 'xl:ml-[290px]'"
      >
        <ng-content select="[slot=header]" />
        <main class="sp-admin-content p-4 mx-auto max-w-screen-2xl md:p-6">
          <ng-content />
        </main>
      </div>
    </div>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminLayoutComponent {
  @Input() collapsed = false;
}
