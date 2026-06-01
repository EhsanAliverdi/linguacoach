import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';

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

  constructor(private adminService: AdminService, public auth: AuthService) {}

  onSubmit(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');

    this.adminService.createStudent({ email: this.email, temporaryPassword: this.temporaryPassword }).subscribe({
      next: res => {
        this.loading.set(false);
        this.success.set(`Student created. Share the temporary password with ${this.email}.`);
        this.email = '';
        this.temporaryPassword = '';
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
}
