import { Component, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-student-app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './student-app-layout.component.html',
  styleUrls: ['./student-app-layout.component.css'],
})
export class StudentAppLayoutComponent {
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

  constructor(public auth: AuthService) {}

  logout(): void {
    this.auth.logout();
  }
}
