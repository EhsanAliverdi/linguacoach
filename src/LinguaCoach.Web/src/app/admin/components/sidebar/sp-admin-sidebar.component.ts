import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-sidebar',
  standalone: true,
  imports: [CommonModule],
  template: `<aside class="sp-admin-sidebar" [class.sp-sidebar-collapsed]="collapsed"><ng-content /></aside>`,
  styles: [`:host { display: contents; }`],
})
export class SpAdminSidebarComponent {
  @Input() collapsed = false;
}
