import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-create-student',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
  ],
  templateUrl: './create-student.component.html',
})
export class CreateStudentComponent {
  // Required fields
  email = '';
  temporaryPassword = '';
  mustChangePassword = true;

  // Optional profile fields
  showOptional = false;
  firstName = '';
  lastName = '';
  displayName = '';
  careerContext = '';
  learningGoal = '';
  preferredSessionDurationMinutes: number | null = null;
  professionalExperienceLevel: number | null = null;
  roleFamiliarity: number | null = null;

  loading = signal(false);
  error = signal('');
  success = signal('');
  emailError = signal('');
  passwordError = signal('');

  readonly experienceLevels = [
    { value: 0, label: 'Entry level' },
    { value: 1, label: 'Mid-level' },
    { value: 2, label: 'Senior' },
    { value: 3, label: 'Lead / Principal' },
  ];

  readonly familiarityLevels = [
    { value: 0, label: 'New to role' },
    { value: 1, label: 'Familiar' },
    { value: 2, label: 'Very experienced' },
  ];

  readonly sessionDurations = [15, 20, 30, 45, 60];

  constructor(
    private adminService: AdminService,
    public auth: AuthService,
    private router: Router,
    private toast: ToastService) {}

  onSubmit(): void {
    if (!this.validate()) return;

    this.loading.set(true);
    this.error.set('');
    this.success.set('');

    const request: Parameters<AdminService['createStudent']>[0] = {
      email: this.email.trim(),
      temporaryPassword: this.temporaryPassword,
      mustChangePassword: this.mustChangePassword,
    };

    if (this.showOptional) {
      if (this.firstName.trim()) request.firstName = this.firstName.trim();
      if (this.lastName.trim()) request.lastName = this.lastName.trim();
      if (this.displayName.trim()) request.displayName = this.displayName.trim();
      if (this.careerContext.trim()) request.careerContext = this.careerContext.trim();
      if (this.learningGoal.trim()) request.learningGoal = this.learningGoal.trim();
      if (this.preferredSessionDurationMinutes) request.preferredSessionDurationMinutes = this.preferredSessionDurationMinutes;
      if (this.professionalExperienceLevel !== null) request.professionalExperienceLevel = this.professionalExperienceLevel;
      if (this.roleFamiliarity !== null) request.roleFamiliarity = this.roleFamiliarity;
    }

    this.adminService.createStudent(request).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Student created successfully');
        this.router.navigate(['/admin/students']);
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
    this.mustChangePassword = true;
    this.showOptional = false;
    this.firstName = '';
    this.lastName = '';
    this.displayName = '';
    this.careerContext = '';
    this.learningGoal = '';
    this.preferredSessionDurationMinutes = null;
    this.professionalExperienceLevel = null;
    this.roleFamiliarity = null;
    this.success.set('');
    this.error.set('');
    this.emailError.set('');
    this.passwordError.set('');
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
