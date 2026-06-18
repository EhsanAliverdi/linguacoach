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
    <div class="min-h-screen xl:flex">
      <div>
        <ng-content select="[slot=sidebar]" />
      </div>
      <div
        class="flex-1 transition-all duration-300 ease-in-out"
        [ngClass]="collapsed ? 'xl:ml-[90px]' : 'xl:ml-[290px]'"
      >
        <ng-content select="[slot=header]" />
        <main class="p-4 mx-auto max-w-screen-2xl md:p-6">
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
