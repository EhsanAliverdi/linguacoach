import { Component, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-admin-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-app-layout.component.html',
  styleUrls: ['./admin-app-layout.component.css'],
})
export class AdminAppLayoutComponent {
  adminEmail = computed(() => this.auth.currentUser()?.email ?? '');
  adminInitial = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'A';
  });

  constructor(public auth: AuthService) {}

  logout(): void {
    this.auth.logout();
  }
}
