import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { OnboardingService } from '../../../../core/services/onboarding.service';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  email = '';
  password = '';
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
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.error.set('');
    this.notice.set('');

    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.mustChangePassword) {
          this.router.navigate(['/change-password']);
          return;
        }
        if (res.role === 'Admin') {
          this.router.navigate(['/admin']);
          return;
        }
        // Student: check onboarding status
        this.onboarding.getStatus().subscribe({
          next: status => {
            if (status.isComplete) {
              this.router.navigate(['/dashboard']);
            } else {
              this.router.navigate(['/onboarding/resume']);
            }
          },
          error: () => this.router.navigate(['/onboarding/v2']),
        });
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Login failed. Please check your credentials.');
      },
    });
  }
}

