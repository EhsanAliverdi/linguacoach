import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

/**
 * Single sidebar navigation link.
 * Handles collapsed mode (icon-only + title tooltip) and mobile drawer usage.
 * Replaces the duplicated <a class="menu-item ..."> pattern in admin-app-layout.
 *
 * Usage:
 *   <sp-admin-sidebar-nav-item label="Dashboard" route="/admin" [exact]="true" [collapsed]="collapsed()">
 *     <svg>...</svg>
 *   </sp-admin-sidebar-nav-item>
 */
@Component({
  selector: 'sp-admin-sidebar-nav-item',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <a
      [routerLink]="route"
      routerLinkActive="sp-nav-active"
      [routerLinkActiveOptions]="exact ? { exact: true } : { exact: false }"
      class="sp-nav-item group"
      [class.justify-center]="collapsed"
      [class.px-0]="collapsed"
      [title]="collapsed ? label : ''"
      (click)="itemClick.emit()"
    >
      <span class="sp-nav-icon">
        <ng-content />
      </span>
      @if (!collapsed) {
        <span class="sp-nav-label">{{ label }}</span>
      }
    </a>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminSidebarNavItemComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) route = '';
  @Input() exact = false;
  @Input() collapsed = false;
  @Output() itemClick = new EventEmitter<void>();
}
