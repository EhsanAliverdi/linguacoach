import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
})
export class ResetPasswordComponent implements OnInit {
  newPassword = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');
  success = signal(false);

  private userId = '';
  private token = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService,
  ) {}

  ngOnInit(): void {
    this.userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
    this.token  = this.route.snapshot.queryParamMap.get('token')  ?? '';
    if (!this.userId || !this.token) {
      this.error.set('This reset link is invalid or has expired. Please request a new one.');
    }
  }

  onSubmit(): void {
    this.error.set('');

    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }
    if (this.newPassword.length < 8) {
      this.error.set('Password must be at least 8 characters.');
      return;
    }
    if (!this.userId || !this.token) {
      this.error.set('This reset link is invalid or has expired.');
      return;
    }

    this.loading.set(true);
    this.auth.resetPassword({
      userId: this.userId,
      token: this.token,
      newPassword: this.newPassword,
      confirmPassword: this.confirmPassword,
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not reset password. The link may have expired.');
      },
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
