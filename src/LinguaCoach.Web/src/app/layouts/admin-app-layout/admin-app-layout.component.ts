import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

const COLLAPSE_KEY = 'speakpath.adminSidebarCollapsed';

@Component({
  selector: 'app-admin-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-app-layout.component.html',
  styleUrls: ['./admin-app-layout.component.css'],
})
export class AdminAppLayoutComponent {
  collapsed = signal(this.readCollapsed());
  drawerOpen = signal(false);

  adminEmail = computed(() => this.auth.currentUser()?.email ?? '');
  adminInitial = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'A';
  });

  constructor(public auth: AuthService) {}

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try { localStorage.setItem(COLLAPSE_KEY, String(next)); } catch { /* ignore */ }
  }

  openDrawer(): void { this.drawerOpen.set(true); }
  closeDrawer(): void { this.drawerOpen.set(false); }

  logout(): void {
    this.auth.logout();
  }

  private readCollapsed(): boolean {
    try { return localStorage.getItem(COLLAPSE_KEY) === 'true'; } catch { return false; }
  }
}
