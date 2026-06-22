import { Component, HostListener, OnDestroy, OnInit, ViewEncapsulation, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationStart, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { SpAdminHeaderComponent, SpAdminLayoutComponent, SpAdminSidebarComponent, SpAdminToastOutletComponent, SpAdminSidebarNavItemComponent, SpAdminSidebarSectionComponent, SpAdminUserMenuComponent } from '../../index';

const COLLAPSE_KEY = 'speakpath.adminSidebarCollapsed';

@Component({
  selector: 'app-admin-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet, SpAdminHeaderComponent, SpAdminLayoutComponent, SpAdminSidebarComponent, SpAdminToastOutletComponent, SpAdminSidebarNavItemComponent, SpAdminSidebarSectionComponent, SpAdminUserMenuComponent],
  templateUrl: './admin-app-layout.component.html',
  styleUrls: ['./admin-app-layout.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class AdminAppLayoutComponent implements OnInit, OnDestroy {
  collapsed = signal(this.readCollapsed());
  drawerOpen = signal(false);

  adminEmail = computed(() => this.auth.currentUser()?.email ?? '');
  adminInitial = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'A';
  });

  private touchStartX = 0;
  private touchCurrentX = 0;

  constructor(public auth: AuthService, router: Router) {
    router.events.pipe(filter(e => e instanceof NavigationStart)).subscribe(() => {
      this.closeDrawer();
    });
  }

  ngOnInit(): void { document.body.classList.add('admin-layout'); }
  ngOnDestroy(): void { document.body.classList.remove('admin-layout'); }

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try { localStorage.setItem(COLLAPSE_KEY, String(next)); } catch { /* ignore */ }
  }

  openDrawer(): void { this.drawerOpen.set(true); }
  closeDrawer(): void { this.drawerOpen.set(false); }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.drawerOpen()) this.closeDrawer();
  }

  onDrawerTouchStart(event: TouchEvent): void {
    this.touchStartX = event.touches[0].clientX;
    this.touchCurrentX = this.touchStartX;
  }

  onDrawerTouchMove(event: TouchEvent): void {
    this.touchCurrentX = event.touches[0].clientX;
  }

  onDrawerTouchEnd(): void {
    if (this.touchStartX - this.touchCurrentX > 60) {
      this.closeDrawer();
    }
  }

  logout(): void {
    this.auth.logout();
  }

  private readCollapsed(): boolean {
    try { return localStorage.getItem(COLLAPSE_KEY) === 'true'; } catch { return false; }
  }
}
