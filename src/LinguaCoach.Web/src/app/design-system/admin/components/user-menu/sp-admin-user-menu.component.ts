import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminDropdownComponent } from '../dropdown/sp-admin-dropdown.component';

/**
 * Admin header user menu.
 * Renders avatar initial, profile summary, and sign-out action.
 * Extracts the inline profile dropdown from admin-app-layout.
 *
 * Usage:
 *   <sp-admin-user-menu [email]="adminEmail()" [initial]="adminInitial()" (signOut)="logout()" />
 */
@Component({
  selector: 'sp-admin-user-menu',
  standalone: true,
  imports: [CommonModule, SpAdminDropdownComponent],
  template: `
    <sp-admin-dropdown align="right" width="auto" [closeOnMenuClick]="true">
      <button
        trigger
        class="sp-admin-avatar"
        aria-label="Profile menu"
      >{{ initial }}</button>

      <div menu class="sp-admin-profile-menu" role="menu">
        <div class="sp-admin-profile-summary">
          <div class="sp-admin-profile-avatar">{{ initial }}</div>
          <div class="sp-admin-profile-meta">
            <div class="sp-admin-profile-email">{{ email }}</div>
            <div class="sp-admin-profile-role">Admin</div>
          </div>
        </div>
        <div class="sp-admin-profile-divider"></div>
        <span class="sp-admin-profile-item sp-admin-profile-item-disabled" title="Profile settings not available">
          <svg width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
          Profile
        </span>
        <div class="sp-admin-profile-divider"></div>
        <button (click)="signOut.emit()" class="sp-admin-profile-item sp-admin-profile-signout" role="menuitem">
          <svg width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
          Sign out
        </button>
      </div>
    </sp-admin-dropdown>
  `,
  styles: [`:host { display: contents; }`],
})
export class SpAdminUserMenuComponent {
  @Input({ required: true }) email = '';
  @Input({ required: true }) initial = '';
  @Output() signOut = new EventEmitter<void>();
}
