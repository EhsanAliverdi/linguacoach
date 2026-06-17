import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-admin-shell">
      <ng-content select="[slot=sidebar]" />
      <div class="sp-admin-main" [class.sp-main-collapsed]="collapsed">
        <ng-content select="[slot=header]" />
        <main class="sp-admin-content">
          <ng-content />
        </main>
      </div>
    </div>
  `,
})
export class SpAdminLayoutComponent {
  @Input() collapsed = false;
}
