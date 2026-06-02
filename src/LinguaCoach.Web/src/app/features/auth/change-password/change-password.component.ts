import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { AuthNoticeService } from '../../../core/services/auth-notice.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './change-password.component.html',
})
export class ChangePasswordComponent {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');
  notice = signal('');

  constructor(
    private auth: AuthService,
    private onboarding: OnboardingService,
    private router: Router,
    private authNotice: AuthNoticeService,
  ) {
    this.notice.set(this.authNotice.consume() ?? '');
  }

  onSubmit(): void {
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('New passwords do not match.');
      return;
    }
    if (this.newPassword.length < 8) {
      this.error.set('New password must be at least 8 characters.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.notice.set('');

    this.auth.changePassword({ currentPassword: this.currentPassword, newPassword: this.newPassword }).subscribe({
      next: () => {
        this.loading.set(false);
        const role = this.auth.currentUser()?.role;
        if (role === 'Admin') {
          this.router.navigate(['/admin']);
        } else {
          this.onboarding.getStatus().subscribe({
            next: s => this.router.navigate(s.isComplete ? ['/dashboard'] : ['/onboarding/resume']),
            error: () => this.router.navigate(['/onboarding/step-1']),
          });
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Failed to change password.');
      },
    });
  }
}
