import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { SpAdminBadgeComponent, SpAdminFilterBarComponent, SpAdminPageHeaderComponent, SpAdminPaginationComponent, SpAdminTableActionsComponent, SpAdminTableComponent } from '../../../admin';

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
  imports: [CommonModule, FormsModule, RouterLink, SpAdminBadgeComponent, SpAdminFilterBarComponent, SpAdminPageHeaderComponent, SpAdminPaginationComponent, SpAdminTableActionsComponent, SpAdminTableComponent],
  template: `
    <sp-admin-page-header title="Students" subtitle="Manage pilot student accounts">
      <a routerLink="../create-student" class="sp-admin-btn-primary">Create student</a>
    </sp-admin-page-header>

    <sp-admin-filter-bar>
      <label class="sp-admin-filter-toggle">
        <input type="checkbox" [(ngModel)]="includeArchived" (change)="load()" />
        <span>Show archived students</span>
      </label>
      <input
        type="search"
        class="sp-input sp-admin-search-input"
        placeholder="Search by email or name…"
        [(ngModel)]="searchTerm"
        (ngModelChange)="page.set(1)"
        aria-label="Search students" />
      <span class="sp-admin-table-muted">{{ filteredStudents().length }} shown</span>
    </sp-admin-filter-bar>

    @if (loading()) {
      <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
    } @else if (error()) {
      <div class="sp-admin-alert-error">{{ error() }}</div>
    } @else {
      <sp-admin-table>
        <table class="sp-admin-table">
          <thead>
            <tr>
              <th class="sp-admin-sortable" (click)="setSort('name')">Student{{ sortIndicator('name') }}</th>
              <th>Lifecycle</th>
              <th class="sp-admin-sortable" (click)="setSort('onboarding')">Onboarding{{ sortIndicator('onboarding') }}</th>
              <th>CEFR</th>
              <th>Profile</th>
              <th class="sp-admin-sortable" (click)="setSort('joined')">Joined{{ sortIndicator('joined') }}</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (s of pagedStudents(); track s.studentProfileId) {
              <tr [class.sp-admin-archived-row]="s.lifecycleStage === 'Archived'">
                <td>
                  <div class="sp-admin-student-name">{{ displayName(s) }}</div>
                  <div class="sp-admin-table-muted sp-safe-text">{{ s.email }}</div>
                </td>
                <td>
                  <sp-admin-badge [tone]="s.lifecycleStage === 'Archived' ? 'neutral' : 'primary'">{{ s.lifecycleStage }}</sp-admin-badge>
                </td>
                <td>
                  <sp-admin-badge [tone]="s.onboardingStatus === 'Complete' ? 'success' : 'warning'">{{ s.onboardingStatus }}</sp-admin-badge>
                </td>
                <td>
                  @if (s.cefrLevel) {
                    <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
                  } @else {
                    <span class="sp-admin-table-empty">-</span>
                  }
                </td>
                <td class="sp-admin-profile-cell">{{ s.careerContext || s.learningGoal || 'Not set' }}</td>
                <td class="sp-admin-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                <td>
                  <sp-admin-table-actions>
                    <a [routerLink]="[s.studentProfileId]" class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors block">View</a>
                    <button type="button" class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors" (click)="startEdit(s)">Edit</button>
                    @if (s.lifecycleStage !== 'Archived') {
                      <button type="button" class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors" (click)="startResetPassword(s)">Reset password</button>
                      <button type="button" class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-red-50 dark:hover:bg-red-900 transition-colors" (click)="startResetData(s)">Reset data</button>
                      <button type="button" class="sp-adm-action-item w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-red-50 dark:hover:bg-red-900 transition-colors" (click)="confirmArchive(s)">Archive</button>
                    }
                  </sp-admin-table-actions>
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (filteredStudents().length === 0) {
          <div class="sp-admin-empty-row">No students found.</div>
        }
      </sp-admin-table>
      @if (totalPages() > 1) {
        <sp-admin-pagination [page]="page()" [totalPages]="totalPages()" (pageChange)="page.set($event)" />
      }
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

    @if (resettingData(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelResetData()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="resetDataTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="resetDataTitle">Reset student data</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelResetData()" aria-label="Close reset data">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        @if (resetDataResult(); as result) {
          <div class="sp-admin-edit-grid">
            <div class="sp-admin-wide sp-admin-alert-success">
              Reset complete. New stage: {{ result.newStage }} (was {{ result.previousStage }}).
            </div>
            <div class="sp-admin-wide sp-admin-table-muted">
              Cleared: onboarding={{ result.clearedItems.onboardingAnswers }},
              placement={{ result.clearedItems.placementResults }},
              courses/sessions={{ result.clearedItems.coursesAndSessions }},
              attempts={{ result.clearedItems.activityAttempts }},
              vocabulary={{ result.clearedItems.vocabulary }},
              memory={{ result.clearedItems.learningMemory }},
              audio files deleted={{ result.clearedItems.audioFilesDeleted }},
              progress={{ result.clearedItems.progressData }}.
            </div>
            <div class="sp-admin-wide sp-admin-table-muted">Reset log: {{ result.resetLogId }}</div>
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-admin-btn-primary" (click)="cancelResetData()">Done</button>
            </div>
          </div>
        } @else {
          <form (ngSubmit)="saveResetData()" class="sp-admin-edit-grid">
            <label class="sp-admin-wide">
              <span>Preset</span>
              <select class="sp-input" [(ngModel)]="resetDataForm.preset" name="preset" (change)="applyResetPreset()">
                @for (preset of resetPresets; track preset.key) {
                  <option [ngValue]="preset.key">{{ preset.label }}</option>
                }
              </select>
            </label>

            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearOnboardingAnswers" name="clearOnboardingAnswers" style="margin:0;" />
              <span style="margin:0;">Clear onboarding answers</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearPlacementResults" name="clearPlacementResults" style="margin:0;" />
              <span style="margin:0;">Clear placement results</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearCoursesAndSessions" name="clearCoursesAndSessions" style="margin:0;" />
              <span style="margin:0;">Clear courses and sessions</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearActivityAttempts" name="clearActivityAttempts" style="margin:0;" />
              <span style="margin:0;">Clear activity attempts</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearVocabulary" name="clearVocabulary" style="margin:0;" />
              <span style="margin:0;">Clear vocabulary</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearLearningMemory" name="clearLearningMemory" style="margin:0;" />
              <span style="margin:0;">Clear learning memory</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearAudioFiles" name="clearAudioFiles" style="margin:0;" />
              <span style="margin:0;">Delete audio files</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearProgressData" name="clearProgressData" style="margin:0;" />
              <span style="margin:0;">Recalculate progress data</span>
            </label>

            <label class="sp-admin-wide">
              <span>Reason (required)</span>
              <textarea class="sp-input" rows="2" [(ngModel)]="resetDataForm.reason" name="reason"
                placeholder="Why is this reset being performed?"></textarea>
            </label>

            <label class="sp-admin-wide">
              <span>Type the student's email to confirm: {{ student.email }}</span>
              <input class="sp-input" [(ngModel)]="resetDataForm.confirmEmail" name="confirmEmail" autocomplete="off" />
            </label>

            @if (resetDataError()) {
              <div class="sp-admin-alert-error sp-admin-wide">{{ resetDataError() }}</div>
            }
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-button-ghost" (click)="cancelResetData()">Cancel</button>
              <button type="submit" class="sp-admin-btn-primary"
                [disabled]="savingResetData() || resetDataForm.confirmEmail !== student.email || !resetDataForm.reason.trim()">
                {{ savingResetData() ? 'Resetting...' : 'Reset data' }}
              </button>
            </div>
          </form>
        }
      </section>
    }
  `,
  styles: [`
    .sp-admin-header-row{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;}
    .sp-admin-students-toolbar{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:14px;flex-wrap:wrap;}
    .sp-admin-search-input{max-width:280px;flex:1;}
    .sp-admin-sortable{cursor:pointer;user-select:none;}
    .sp-admin-sortable:hover{color:#334155;}
    .sp-admin-filter-toggle{display:inline-flex;align-items:center;gap:8px;font-size:13px;font-weight:700;color:#475569;}
    .sp-admin-filter-toggle input{accent-color:#4338CA;}
    .sp-admin-table-scroll{overflow-x:auto;}
    .sp-admin-student-name{font-weight:800;color:#0F172A;}
    .sp-admin-profile-cell{max-width:260px;overflow-wrap:anywhere;}
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

  searchTerm = '';
  page = signal(1);
  readonly pageSize = 25;
  sortColumn = signal<'name' | 'onboarding' | 'joined'>('joined');
  sortDirection = signal<'asc' | 'desc'>('desc');

  filteredStudents = computed(() => {
    const term = this.searchTerm.trim().toLowerCase();
    let items = this.students();
    if (term) {
      items = items.filter(s =>
        s.email.toLowerCase().includes(term) ||
        this.displayName(s).toLowerCase().includes(term));
    }

    const column = this.sortColumn();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    items = [...items].sort((a, b) => {
      switch (column) {
        case 'name':
          return this.displayName(a).localeCompare(this.displayName(b)) * direction;
        case 'onboarding':
          return a.onboardingStatus.localeCompare(b.onboardingStatus) * direction;
        case 'joined':
        default:
          return (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()) * direction;
      }
    });

    return items;
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filteredStudents().length / this.pageSize)));

  pagedStudents = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredStudents().slice(start, start + this.pageSize);
  });

  setSort(column: 'name' | 'onboarding' | 'joined'): void {
    if (this.sortColumn() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortColumn.set(column);
      this.sortDirection.set('asc');
    }
    this.page.set(1);
  }

  sortIndicator(column: 'name' | 'onboarding' | 'joined'): string {
    if (this.sortColumn() !== column) return '';
    return this.sortDirection() === 'asc' ? ' ▲' : ' ▼';
  }

  resetting = signal<StudentListItem | null>(null);
  savingReset = signal(false);
  resetError = signal('');
  resetSuccessPassword = signal('');
  resetForm = { newPassword: '', mustChangePassword: true };

  editForm: StudentEditForm = this.emptyForm();

  resettingData = signal<StudentListItem | null>(null);
  savingResetData = signal(false);
  resetDataError = signal('');
  resetDataResult = signal<import('../../../core/models/admin.models').ResetStudentResponse | null>(null);
  resetDataForm = this.emptyResetDataForm();

  readonly resetPresets: { key: string; label: string; flags: Omit<ResetStudentRequest, 'reason'> }[] = [
    {
      key: 'fixPassword',
      label: 'Fix password',
      flags: { targetStage: 'PasswordChangeRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'restartOnboarding',
      label: 'Restart onboarding',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'restartPlacement',
      label: 'Restart placement',
      flags: { targetStage: 'PlacementRequired', clearOnboardingAnswers: false, clearPlacementResults: true, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'resetCourseOnly',
      label: 'Reset course only',
      flags: { targetStage: 'CourseReady', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: true, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'fullCleanReset',
      label: 'Full clean reset',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: true, clearCoursesAndSessions: true, clearActivityAttempts: true, clearVocabulary: true, clearLearningMemory: true, clearAudioFiles: true, clearProgressData: true },
    },
    {
      key: 'custom',
      label: 'Custom',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
  ];

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

  startResetData(student: StudentListItem): void {
    this.resettingData.set(student);
    this.resetDataError.set('');
    this.resetDataResult.set(null);
    this.resetDataForm = this.emptyResetDataForm();
  }

  cancelResetData(): void {
    this.resettingData.set(null);
    this.resetDataError.set('');
    this.resetDataResult.set(null);
  }

  applyResetPreset(): void {
    const preset = this.resetPresets.find(p => p.key === this.resetDataForm.preset);
    if (!preset) return;
    Object.assign(this.resetDataForm, preset.flags);
  }

  saveResetData(): void {
    const student = this.resettingData();
    if (!student) return;
    if (this.resetDataForm.confirmEmail !== student.email || !this.resetDataForm.reason.trim()) return;

    this.savingResetData.set(true);
    this.resetDataError.set('');

    const request: ResetStudentRequest = {
      targetStage: this.resetDataForm.targetStage,
      clearOnboardingAnswers: this.resetDataForm.clearOnboardingAnswers,
      clearPlacementResults: this.resetDataForm.clearPlacementResults,
      clearCoursesAndSessions: this.resetDataForm.clearCoursesAndSessions,
      clearActivityAttempts: this.resetDataForm.clearActivityAttempts,
      clearVocabulary: this.resetDataForm.clearVocabulary,
      clearLearningMemory: this.resetDataForm.clearLearningMemory,
      clearAudioFiles: this.resetDataForm.clearAudioFiles,
      clearProgressData: this.resetDataForm.clearProgressData,
      reason: this.resetDataForm.reason.trim(),
    };

    this.adminApi.resetStudent(student.studentProfileId, request).subscribe({
      next: result => {
        this.savingResetData.set(false);
        this.resetDataResult.set(result);
        this.toast.success(`Student data reset for ${student.email}`);
        this.load();
      },
      error: err => {
        this.savingResetData.set(false);
        this.resetDataError.set(err.error?.error ?? 'Could not reset student data.');
      },
    });
  }

  private emptyResetDataForm() {
    return {
      preset: 'restartOnboarding' as string,
      targetStage: 'OnboardingRequired' as StudentLifecycleStageName,
      clearOnboardingAnswers: true,
      clearPlacementResults: false,
      clearCoursesAndSessions: false,
      clearActivityAttempts: false,
      clearVocabulary: false,
      clearLearningMemory: false,
      clearAudioFiles: false,
      clearProgressData: false,
      reason: '',
      confirmEmail: '',
    };
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
