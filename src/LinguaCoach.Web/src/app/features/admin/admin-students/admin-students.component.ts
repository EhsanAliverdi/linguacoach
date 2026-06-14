import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, UpdateStudentProfileRequest } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';

interface StudentEditForm {
  firstName: string;
  lastName: string;
  displayName: string;
  careerContext: string;
  learningGoal: string;
  learningGoalDescription: string;
  difficultSituationsText: string;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
}

@Component({
  selector: 'app-admin-students',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="sp-admin-page-header">
      <div class="sp-admin-header-row">
        <div>
          <h1 class="sp-admin-page-title">Students</h1>
          <p class="sp-admin-page-sub">Manage pilot student accounts</p>
        </div>
        <a routerLink="../create-student" class="sp-admin-btn-primary">Create student</a>
      </div>
    </div>

    <div class="sp-admin-students-toolbar">
      <label class="sp-admin-filter-toggle">
        <input type="checkbox" [(ngModel)]="includeArchived" (change)="load()" />
        <span>Show archived students</span>
      </label>
      <span class="sp-admin-table-muted">{{ students().length }} shown</span>
    </div>

    @if (loading()) {
      <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
    } @else if (error()) {
      <div class="sp-admin-alert-error">{{ error() }}</div>
    } @else {
      <div class="sp-admin-table-card sp-admin-table-scroll">
        <table class="sp-admin-table">
          <thead>
            <tr>
              <th>Student</th>
              <th>Lifecycle</th>
              <th>Onboarding</th>
              <th>CEFR</th>
              <th>Profile</th>
              <th>Joined</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (s of students(); track s.studentProfileId) {
              <tr [class.sp-admin-archived-row]="s.lifecycleStage === 'Archived'">
                <td>
                  <div class="sp-admin-student-name">{{ displayName(s) }}</div>
                  <div class="sp-admin-table-muted sp-safe-text">{{ s.email }}</div>
                </td>
                <td>
                  <span class="sp-admin-badge"
                    [class.sp-admin-badge-slate]="s.lifecycleStage === 'Archived'"
                    [class.sp-admin-badge-indigo]="s.lifecycleStage !== 'Archived'">
                    {{ s.lifecycleStage }}
                  </span>
                </td>
                <td>
                  <span class="sp-admin-badge"
                    [class.sp-admin-badge-green]="s.onboardingStatus === 'Complete'"
                    [class.sp-admin-badge-amber]="s.onboardingStatus !== 'Complete'">
                    {{ s.onboardingStatus }}
                  </span>
                </td>
                <td>
                  @if (s.cefrLevel) {
                    <span class="sp-admin-badge sp-admin-badge-indigo">{{ s.cefrLevel }}</span>
                  } @else {
                    <span class="sp-admin-table-empty">-</span>
                  }
                </td>
                <td class="sp-admin-profile-cell">{{ s.careerContext || s.learningGoal || 'Not set' }}</td>
                <td class="sp-admin-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                <td>
                  <div class="sp-admin-row-actions">
                    <button type="button" class="sp-admin-link-button" (click)="startEdit(s)">Edit</button>
                    @if (s.lifecycleStage !== 'Archived') {
                      <button type="button" class="sp-admin-link-button" (click)="startResetPassword(s)">Reset password</button>
                      <button type="button" class="sp-admin-danger-link" (click)="confirmArchive(s)">Archive</button>
                    }
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (students().length === 0) {
          <div class="sp-admin-empty-row">No students found.</div>
        }
      </div>
    }

    @if (editing(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelEdit()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="editStudentTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="editStudentTitle">Edit student</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelEdit()" aria-label="Close edit student">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        <form (ngSubmit)="saveEdit()" class="sp-admin-edit-grid">
          <label>
            <span>First name</span>
            <input class="sp-input" [(ngModel)]="editForm.firstName" name="firstName" />
          </label>
          <label>
            <span>Last name</span>
            <input class="sp-input" [(ngModel)]="editForm.lastName" name="lastName" />
          </label>
          <label class="sp-admin-wide">
            <span>Display name</span>
            <input class="sp-input" [(ngModel)]="editForm.displayName" name="displayName" />
          </label>
          <label class="sp-admin-wide">
            <span>Career or work context</span>
            <input class="sp-input" [(ngModel)]="editForm.careerContext" name="careerContext" />
          </label>
          <label class="sp-admin-wide">
            <span>Learning goal</span>
            <input class="sp-input" [(ngModel)]="editForm.learningGoal" name="learningGoal" />
          </label>
          <label class="sp-admin-wide">
            <span>Learning goal description</span>
            <textarea class="sp-input" rows="3" [(ngModel)]="editForm.learningGoalDescription" name="learningGoalDescription"></textarea>
          </label>
          <label class="sp-admin-wide">
            <span>Difficult situations</span>
            <textarea class="sp-input" rows="3" [(ngModel)]="editForm.difficultSituationsText" name="difficultSituationsText"></textarea>
          </label>
          <label>
            <span>Preferred duration</span>
            <select class="sp-input" [(ngModel)]="editForm.preferredSessionDurationMinutes" name="preferredSessionDurationMinutes">
              <option [ngValue]="null">Not set</option>
              @for (duration of sessionDurations; track duration) {
                <option [ngValue]="duration">{{ duration }} minutes</option>
              }
            </select>
          </label>
          <label>
            <span>Experience level</span>
            <select class="sp-input" [(ngModel)]="editForm.professionalExperienceLevel" name="professionalExperienceLevel">
              <option [ngValue]="null">Not set</option>
              @for (level of experienceLevels; track level.value) {
                <option [ngValue]="level.value">{{ level.label }}</option>
              }
            </select>
          </label>
          <label>
            <span>Role familiarity</span>
            <select class="sp-input" [(ngModel)]="editForm.roleFamiliarity" name="roleFamiliarity">
              <option [ngValue]="null">Not set</option>
              @for (level of familiarityLevels; track level.value) {
                <option [ngValue]="level.value">{{ level.label }}</option>
              }
            </select>
          </label>
          @if (editError()) {
            <div class="sp-admin-alert-error sp-admin-wide">{{ editError() }}</div>
          }
          <div class="sp-admin-modal-actions sp-admin-wide">
            <button type="button" class="sp-button-ghost" (click)="cancelEdit()">Cancel</button>
            <button type="submit" class="sp-admin-btn-primary" [disabled]="savingEdit()">
              {{ savingEdit() ? 'Saving...' : 'Save changes' }}
            </button>
          </div>
        </form>
      </section>
    }

    @if (resetting(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelResetPassword()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="resetPasswordTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="resetPasswordTitle">Reset password</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelResetPassword()" aria-label="Close reset password">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        @if (resetSuccessPassword()) {
          <div class="sp-admin-edit-grid">
            <div class="sp-admin-wide sp-admin-alert-success">
              Password reset. Share this temporary password with the student
              securely — it will not be shown again.
            </div>
            <label class="sp-admin-wide">
              <span>New temporary password</span>
              <input class="sp-input" [value]="resetSuccessPassword()" readonly />
            </label>
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-admin-btn-primary" (click)="cancelResetPassword()">Done</button>
            </div>
          </div>
        } @else {
          <form (ngSubmit)="saveResetPassword()" class="sp-admin-edit-grid">
            <label class="sp-admin-wide">
              <span>New temporary password</span>
              <input class="sp-input" [(ngModel)]="resetForm.newPassword" name="newPassword"
                placeholder="At least 8 characters, with a digit" autocomplete="off" />
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetForm.mustChangePassword" name="mustChangePassword" style="margin:0;" />
              <span style="margin:0;">Require password change on next login</span>
            </label>
            @if (resetError()) {
              <div class="sp-admin-alert-error sp-admin-wide">{{ resetError() }}</div>
            }
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-button-ghost" (click)="generateResetPassword()">Generate password</button>
              <button type="button" class="sp-button-ghost" (click)="cancelResetPassword()">Cancel</button>
              <button type="submit" class="sp-admin-btn-primary" [disabled]="savingReset() || resetForm.newPassword.length < 8">
                {{ savingReset() ? 'Saving...' : 'Reset password' }}
              </button>
            </div>
          </form>
        }
      </section>
    }
  `,
  styles: [`
    .sp-admin-header-row{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;}
    .sp-admin-students-toolbar{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:14px;}
    .sp-admin-filter-toggle{display:inline-flex;align-items:center;gap:8px;font-size:13px;font-weight:700;color:#475569;}
    .sp-admin-filter-toggle input{accent-color:#4338CA;}
    .sp-admin-table-scroll{overflow-x:auto;}
    .sp-admin-student-name{font-weight:800;color:#0F172A;}
    .sp-admin-profile-cell{max-width:260px;overflow-wrap:anywhere;}
    .sp-admin-row-actions{display:flex;align-items:center;gap:10px;white-space:nowrap;}
    .sp-admin-link-button,.sp-admin-danger-link{border:none;background:none;padding:0;font:inherit;font-size:12.5px;font-weight:800;cursor:pointer;}
    .sp-admin-link-button{color:#4338CA;}
    .sp-admin-danger-link{color:#DC2626;}
    .sp-admin-archived-row td{background:#F8FAFC;color:#94A3B8;}
    .sp-admin-modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.38);z-index:120;}
    .sp-admin-modal{position:fixed;right:24px;top:76px;bottom:24px;z-index:121;width:min(720px,calc(100vw - 48px));overflow:auto;background:#fff;border:1px solid #E2E8F0;border-radius:14px;box-shadow:0 20px 60px rgba(15,23,42,.22);}
    .sp-admin-modal-header{display:flex;align-items:start;justify-content:space-between;gap:12px;padding:20px 22px;border-bottom:1px solid #E2E8F0;}
    .sp-admin-modal-header h2{font-size:18px;font-weight:800;color:#0F172A;}
    .sp-admin-modal-header p{font-size:13px;color:#64748B;margin-top:2px;}
    .sp-admin-modal-header button{width:34px;height:34px;border-radius:9px;border:1px solid #E2E8F0;background:#fff;color:#64748B;cursor:pointer;display:grid;place-items:center;}
    .sp-admin-edit-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;padding:22px;}
    .sp-admin-edit-grid label span{display:block;margin-bottom:6px;font-size:12px;font-weight:800;color:#475569;}
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-modal-actions{display:flex;justify-content:flex-end;gap:10px;padding-top:8px;}
    @media(max-width:720px){
      .sp-admin-modal{left:12px;right:12px;top:68px;bottom:12px;width:auto;}
      .sp-admin-edit-grid{grid-template-columns:1fr;padding:18px;}
    }
  `],
})
export class AdminStudentsComponent implements OnInit {
  students = signal<StudentListItem[]>([]);
  loading = signal(true);
  error = signal('');
  editing = signal<StudentListItem | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  includeArchived = false;

  resetting = signal<StudentListItem | null>(null);
  savingReset = signal(false);
  resetError = signal('');
  resetSuccessPassword = signal('');
  resetForm = { newPassword: '', mustChangePassword: true };

  editForm: StudentEditForm = this.emptyForm();

  readonly sessionDurations = [15, 20, 30, 45, 60];
  readonly experienceLevels = [
    { value: 0, label: 'No professional experience' },
    { value: 1, label: 'Entry level or graduate' },
    { value: 2, label: 'Junior, 0-2 years' },
    { value: 3, label: 'Mid-level, 2-5 years' },
    { value: 4, label: 'Senior, 5-10 years' },
    { value: 5, label: 'Lead or manager, 10+ years' },
  ];
  readonly familiarityLevels = [
    { value: 0, label: 'New to role' },
    { value: 1, label: 'Understands basics' },
    { value: 2, label: 'Currently working in role' },
    { value: 3, label: 'Experienced in role' },
    { value: 4, label: 'Manages or trains others' },
  ];

  constructor(private adminApi: AdminApiService, private toast: ToastService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.listStudents(this.includeArchived).subscribe({
      next: s => { this.students.set(s); this.loading.set(false); },
      error: () => { this.error.set('Could not load students.'); this.loading.set(false); },
    });
  }

  displayName(student: StudentListItem): string {
    return student.displayName
      || [student.firstName, student.lastName].filter(Boolean).join(' ')
      || student.email;
  }

  startEdit(student: StudentListItem): void {
    this.editing.set(student);
    this.editError.set('');
    this.editForm = {
      firstName: student.firstName ?? '',
      lastName: student.lastName ?? '',
      displayName: student.displayName ?? '',
      careerContext: student.careerContext ?? '',
      learningGoal: student.learningGoal ?? '',
      learningGoalDescription: student.learningGoalDescription ?? '',
      difficultSituationsText: student.difficultSituationsText ?? '',
      preferredSessionDurationMinutes: student.preferredSessionDurationMinutes,
      professionalExperienceLevel: student.professionalExperienceLevel,
      roleFamiliarity: student.roleFamiliarity,
    };
  }

  cancelEdit(): void {
    this.editing.set(null);
    this.editError.set('');
  }

  saveEdit(): void {
    const student = this.editing();
    if (!student) return;

    this.savingEdit.set(true);
    this.editError.set('');

    const request: UpdateStudentProfileRequest = {
      firstName: this.nullIfBlank(this.editForm.firstName),
      lastName: this.nullIfBlank(this.editForm.lastName),
      displayName: this.nullIfBlank(this.editForm.displayName),
      careerContext: this.nullIfBlank(this.editForm.careerContext),
      learningGoal: this.nullIfBlank(this.editForm.learningGoal),
      learningGoalDescription: this.nullIfBlank(this.editForm.learningGoalDescription),
      difficultSituationsText: this.nullIfBlank(this.editForm.difficultSituationsText),
      preferredSessionDurationMinutes: this.editForm.preferredSessionDurationMinutes,
      professionalExperienceLevel: this.editForm.professionalExperienceLevel,
      roleFamiliarity: this.editForm.roleFamiliarity,
    };

    this.adminApi.updateStudent(student.studentProfileId, request).subscribe({
      next: updated => {
        this.students.update(items => items.map(item => item.studentProfileId === updated.studentProfileId ? updated : item));
        this.savingEdit.set(false);
        this.editing.set(null);
        this.toast.success('Student updated successfully');
      },
      error: err => {
        this.savingEdit.set(false);
        this.editError.set(err.error?.error ?? 'Could not update student.');
      },
    });
  }

  confirmArchive(student: StudentListItem): void {
    const confirmed = window.confirm(`Archive ${student.email}? They will be hidden from the active list and cannot sign in.`);
    if (!confirmed) return;

    this.adminApi.archiveStudent(student.studentProfileId).subscribe({
      next: updated => {
        if (this.includeArchived) {
          this.students.update(items => items.map(item => item.studentProfileId === updated.studentProfileId ? updated : item));
        } else {
          this.students.update(items => items.filter(item => item.studentProfileId !== updated.studentProfileId));
        }
        this.toast.success('Student archived');
      },
      error: err => this.toast.error(err.error?.error ?? 'Could not archive student.'),
    });
  }

  startResetPassword(student: StudentListItem): void {
    this.resetting.set(student);
    this.resetError.set('');
    this.resetSuccessPassword.set('');
    this.resetForm = { newPassword: '', mustChangePassword: true };
  }

  cancelResetPassword(): void {
    this.resetting.set(null);
    this.resetError.set('');
    this.resetSuccessPassword.set('');
  }

  generateResetPassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789';
    let result = '';
    for (let i = 0; i < 12; i++) {
      result += chars[Math.floor(Math.random() * chars.length)];
    }
    this.resetForm.newPassword = result;
  }

  saveResetPassword(): void {
    const student = this.resetting();
    if (!student || this.resetForm.newPassword.length < 8) return;

    this.savingReset.set(true);
    this.resetError.set('');

    this.adminApi.resetStudentPassword(student.studentProfileId, this.resetForm.newPassword, this.resetForm.mustChangePassword).subscribe({
      next: () => {
        this.savingReset.set(false);
        this.resetSuccessPassword.set(this.resetForm.newPassword);
        this.toast.success(`Password reset for ${student.email}`);
      },
      error: err => {
        this.savingReset.set(false);
        this.resetError.set(err.error?.error ?? 'Could not reset password.');
      },
    });
  }

  private nullIfBlank(value: string): string | null {
    const trimmed = value.trim();
    return trimmed ? trimmed : null;
  }

  private emptyForm(): StudentEditForm {
    return {
      firstName: '',
      lastName: '',
      displayName: '',
      careerContext: '',
      learningGoal: '',
      learningGoalDescription: '',
      difficultSituationsText: '',
      preferredSessionDurationMinutes: null,
      professionalExperienceLevel: null,
      roleFamiliarity: null,
    };
  }
}
