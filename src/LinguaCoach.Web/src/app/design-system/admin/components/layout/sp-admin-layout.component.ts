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
    <div class="min-h-screen" style="background:var(--sp-admin-bg,#F6F4FB)">
      <ng-content select="[slot=sidebar]" />
      <div
        class="flex flex-col min-h-screen transition-all duration-300 ease-in-out"
        [ngClass]="collapsed ? 'xl:ml-[64px]' : 'xl:ml-[240px]'"
      >
        <ng-content select="[slot=header]" />
        <main class="flex-1 w-full" style="padding:28px 32px 48px;">
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
