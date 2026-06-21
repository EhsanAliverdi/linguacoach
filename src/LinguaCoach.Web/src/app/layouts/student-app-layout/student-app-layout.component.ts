import { Component, HostListener, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { NotificationDropdownComponent } from '../../shared/notifications/notification-dropdown/notification-dropdown.component';

const STORAGE_KEY = 'speakpath.sidebarCollapsed';

@Component({
  selector: 'app-student-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, NotificationDropdownComponent],
  templateUrl: './student-app-layout.component.html',
  styleUrls: ['./student-app-layout.component.css'],
})
export class StudentAppLayoutComponent implements OnInit {
  collapsed = signal(this.readCollapsed());
  profileMenuOpen = signal(false);
  streakDays = signal(0);

  firstName = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    const name = email.includes('@') ? email.split('@')[0] : email.split(' ')[0] || email;
    return name || 'Student';
  });

  avatarLetter = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'S';
  });

  greetingTime = computed(() => {
    const h = new Date().getHours();
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  });

  constructor(public auth: AuthService, private dashboard: DashboardService) {}

  ngOnInit(): void {
    this.dashboard.getDashboard().subscribe({
      next: d => this.streakDays.set(d.streakDays ?? 0),
      error: () => this.streakDays.set(0),
    });
  }

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try { localStorage.setItem(STORAGE_KEY, String(next)); } catch { /* ignore */ }
  }

  toggleProfileMenu(event: Event): void {
    event.stopPropagation();
    this.profileMenuOpen.update(v => !v);
  }

  @HostListener('document:click')
  closeProfileMenu(): void {
    this.profileMenuOpen.set(false);
  }

  logout(): void {
    this.auth.logout();
  }

  private readCollapsed(): boolean {
    try { return localStorage.getItem(STORAGE_KEY) === 'true'; } catch { return false; }
  }
}
