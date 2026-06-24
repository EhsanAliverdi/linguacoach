import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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
    RouterLink,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
  ],
  templateUrl: './create-student.component.html',
  styles: [`
    .sp-cs-layout {
      display: grid;
      gap: 24px;
    }
    @media(min-width: 1100px) {
      .sp-cs-layout {
        grid-template-columns: 1fr 300px;
        align-items: start;
      }
    }

    .sp-cs-section {
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 16px;
      padding: 24px;
      margin-bottom: 16px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .sp-cs-section-header { display: flex; flex-direction: column; gap: 4px; }
    .sp-cs-section-title {
      font-size: 15px;
      font-weight: 800;
      color: var(--sp-admin-text, #211B36);
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .sp-cs-section-sub { font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0); }

    .sp-cs-optional-badge {
      font-size: 10px;
      font-weight: 700;
      letter-spacing: .06em;
      text-transform: uppercase;
      background: var(--sp-admin-primary-bg, #EDEBFF);
      color: var(--sp-admin-primary, #5B4BE8);
      padding: 2px 7px;
      border-radius: 99px;
    }

    .sp-cs-toggle-row {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      border-radius: 12px;
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      padding: 12px;
    }
    .sp-cs-toggle-checkbox {
      margin-top: 2px;
      width: 16px;
      height: 16px;
      accent-color: var(--sp-admin-primary, #5B4BE8);
      flex-shrink: 0;
      cursor: pointer;
    }
    .sp-cs-toggle-label {
      font-size: 13.5px;
      font-weight: 600;
      color: var(--sp-admin-text, #211B36);
      cursor: pointer;
    }
    .sp-cs-toggle-hint {
      margin: 2px 0 0;
      font-size: 12px;
      color: var(--sp-admin-text-muted, #8B85A0);
    }

    .sp-cs-security-note {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      font-size: 12px;
      color: #8B85A0;
      background: #fffbeb;
      border: 1px solid #fde68a;
      border-radius: 10px;
      padding: 10px 12px;
    }

    .sp-cs-toggle-btn {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 13.5px;
      font-weight: 600;
      color: var(--sp-admin-primary, #5B4BE8);
      background: none;
      border: none;
      cursor: pointer;
      padding: 0;
      width: 100%;
    }
    .sp-cs-toggle-chevron {
      width: 16px;
      height: 16px;
      transition: transform .2s;
    }
    .sp-cs-toggle-chevron--open { transform: rotate(180deg); }

    .sp-cs-optional-fields {
      display: flex;
      flex-direction: column;
      gap: 14px;
      border-radius: 12px;
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      padding: 16px;
    }

    .sp-cs-two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    @media(max-width: 600px) { .sp-cs-two-col { grid-template-columns: 1fr; } }

    .sp-cs-native-select {
      width: 100%;
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 8px;
      padding: 8px 12px;
      font-size: 13.5px;
      color: var(--sp-admin-text, #211B36);
      background: #fff;
    }

    .sp-cs-actions {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 12px;
      padding-top: 4px;
    }

    /* Aside panel */
    .sp-cs-aside-card {
      background: var(--sp-admin-surface, #fff);
      border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 16px;
      padding: 20px;
      position: sticky;
      top: 24px;
    }
    .sp-cs-aside-title {
      font-size: 13px;
      font-weight: 800;
      color: var(--sp-admin-text, #211B36);
      text-transform: uppercase;
      letter-spacing: .06em;
      margin-bottom: 16px;
    }
    .sp-cs-aside-steps {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .sp-cs-aside-steps li {
      display: flex;
      align-items: flex-start;
      gap: 12px;
    }
    .sp-cs-step-dot {
      width: 24px;
      height: 24px;
      border-radius: 50%;
      background: var(--sp-admin-primary-bg, #EDEBFF);
      color: var(--sp-admin-primary, #5B4BE8);
      font-size: 11px;
      font-weight: 800;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sp-cs-step-dot--done {
      background: var(--sp-admin-green-bg, #dcfce7);
      color: var(--sp-admin-green, #16a34a);
    }
    .sp-cs-step-label { font-size: 13px; font-weight: 700; color: var(--sp-admin-text, #211B36); }
    .sp-cs-step-desc { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); margin-top: 2px; }

    .sp-cs-aside-note {
      margin-top: 16px;
      font-size: 11.5px;
      color: #8B85A0;
      font-style: italic;
      border-top: 1px solid var(--sp-admin-border, #ECE9F5);
      padding-top: 12px;
    }
  `],
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
