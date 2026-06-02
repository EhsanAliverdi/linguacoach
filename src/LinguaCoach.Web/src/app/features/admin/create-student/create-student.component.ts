import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';

interface CreatedStudentCredentials {
  email: string;
  temporaryPassword: string;
}

@Component({
  selector: 'app-create-student',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './create-student.component.html',
})
export class CreateStudentComponent {
  email = '';
  temporaryPassword = '';
  loading = signal(false);
  error = signal('');
  success = signal('');
  emailError = signal('');
  passwordError = signal('');
  createdCredentials = signal<CreatedStudentCredentials | null>(null);

  constructor(private adminService: AdminService, public auth: AuthService) {}

  onSubmit(): void {
    if (!this.validate()) return;

    this.loading.set(true);
    this.error.set('');
    this.success.set('');
    this.createdCredentials.set(null);

    this.adminService.createStudent({ email: this.email, temporaryPassword: this.temporaryPassword }).subscribe({
      next: () => {
        this.loading.set(false);
        this.createdCredentials.set({
          email: this.email.trim(),
          temporaryPassword: this.temporaryPassword,
        });
        this.success.set('Student account created. Share these credentials privately with the student.');
      },
      error: err => {
        this.loading.set(false);
        if (err.status === 409) {
          this.error.set('A student with this email already exists.');
        } else {
          this.error.set(err.error?.error ?? 'Failed to create student.');
        }
      },
    });
  }

  startAnother(): void {
    this.email = '';
    this.temporaryPassword = '';
    this.success.set('');
    this.error.set('');
    this.emailError.set('');
    this.passwordError.set('');
    this.createdCredentials.set(null);
  }

  private validate(): boolean {
    this.emailError.set('');
    this.passwordError.set('');
    const email = this.email.trim();
    if (!email) {
      this.emailError.set('Enter the student email address.');
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.emailError.set('Enter a valid email address.');
    }

    if (!this.temporaryPassword) {
      this.passwordError.set('Enter a temporary password.');
    } else if (this.temporaryPassword.length < 8) {
      this.passwordError.set('Use at least 8 characters.');
    } else if (!/\d/.test(this.temporaryPassword)) {
      this.passwordError.set('Include at least one number.');
    }

    return !this.emailError() && !this.passwordError();
  }
}
