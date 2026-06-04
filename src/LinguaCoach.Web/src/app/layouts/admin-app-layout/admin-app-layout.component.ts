import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

const STORAGE_KEY = 'speakpath.adminSidebarCollapsed';

@Component({
  selector: 'app-admin-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-app-layout.component.html',
  styleUrls: ['./admin-app-layout.component.css'],
})
export class AdminAppLayoutComponent {
  collapsed = signal(this.readCollapsed());

  adminEmail = computed(() => this.auth.currentUser()?.email ?? '');
  adminInitial = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'A';
  });

  constructor(public auth: AuthService) {}

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try { localStorage.setItem(STORAGE_KEY, String(next)); } catch { /* ignore */ }
  }

  logout(): void {
    this.auth.logout();
  }

  private readCollapsed(): boolean {
    try { return localStorage.getItem(STORAGE_KEY) === 'true'; } catch { return false; }
  }
}
