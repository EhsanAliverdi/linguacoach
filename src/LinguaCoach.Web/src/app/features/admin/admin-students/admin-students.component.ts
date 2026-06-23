import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminStats, UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { SpAdminBadgeComponent, SpAdminButtonComponent, SpAdminCopyableTextComponent, SpAdminEmptyStateComponent, SpAdminErrorStateComponent, SpAdminFilterBarComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminKpiCardComponent, SpAdminLoadingStateComponent, SpAdminModalComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminPaginationComponent, SpAdminSelectComponent, SpAdminTableActionsComponent, SpAdminTableComponent, SpAdminTextareaComponent, SpAdminTruncatedTextComponent } from '../../../design-system/admin';
import type { SpAdminSelectOption } from '../../../design-system/admin';
import { lifecycleLabel, lifecycleTone, onboardingLabel, onboardingTone } from '../../../design-system/admin/utils/admin-badge.utils';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';

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
  imports: [CommonModule, FormsModule, RouterLink, SpAdminBadgeComponent, SpAdminButtonComponent, SpAdminCopyableTextComponent, SpAdminEmptyStateComponent, SpAdminErrorStateComponent, SpAdminFilterBarComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminKpiCardComponent, SpAdminLoadingStateComponent, SpAdminModalComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminPaginationComponent, SpAdminSelectComponent, SpAdminTableActionsComponent, SpAdminTableComponent, SpAdminTextareaComponent, SpAdminTruncatedTextComponent, SpAdminBreakdownBarsComponent],
  template: `
    <sp-admin-page-header title="Students" subtitle="Manage pilot student accounts">
      <sp-admin-button routerLink="../create-student">Create student</sp-admin-button>
    </sp-admin-page-header>

    <sp-admin-page-body>

    <!-- Summary strip (real data from stats + paged total) -->
    <div class="sp-stu-summary-row">
      <sp-admin-kpi-card label="Total students" variant="indigo">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        {{ loadingStats() ? '—' : (stats()?.totalStudents ?? 0) }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Onboarded" variant="green">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>
        {{ loadingStats() ? '—' : (stats()?.onboardedStudents ?? 0) }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Activities tracked" variant="amber">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        {{ loadingStats() ? '—' : (stats()?.totalActivityAttempts ?? 0) }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Showing (this page)" variant="slate">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
        {{ totalCount() }}
      </sp-admin-kpi-card>
    </div>

    <!-- Onboarding breakdown (real data from stats) -->
    @if (!loadingStats() && onboardingBreakdownItems().length > 0) {
      <div class="sp-stu-breakdown-row">
        <sp-admin-breakdown-bars
          [items]="onboardingBreakdownItems()"
          title="Onboarding progress"
          [showPct]="true"
          ariaLabel="Student onboarding breakdown" />
      </div>
    }

    <sp-admin-filter-bar>
      <label class="sp-admin-filter-toggle">
        <input type="checkbox" [(ngModel)]="includeArchived" (change)="onIncludeArchivedChange()" />
        <span>Show archived</span>
      </label>
      <sp-admin-input
        type="search"
        placeholder="Search by email or name"
        [ngModel]="searchTerm()"
        (ngModelChange)="onSearchChange($event)" />
      <sp-admin-select
        [options]="lifecycleStageOptions"
        placeholder="All lifecycle stages"
        size="sm"
        [fullWidth]="false"
        [ngModel]="filterLifecycleStage()"
        (ngModelChange)="onLifecycleStageChange($event)" />
      <sp-admin-select
        [options]="onboardingStatusOptions"
        placeholder="All onboarding statuses"
        size="sm"
        [fullWidth]="false"
        [ngModel]="filterOnboardingStatus()"
        (ngModelChange)="onOnboardingStatusChange($event)" />
      <sp-admin-select
        [options]="cefrLevelOptions"
        placeholder="All CEFR levels"
        size="sm"
        [fullWidth]="false"
        [ngModel]="filterCefrLevel()"
        (ngModelChange)="onCefrLevelChange($event)" />
      @if (hasActiveFilters()) {
        <sp-admin-button variant="ghost" size="sm" type="button" (click)="clearFilters()">Clear filters</sp-admin-button>
      }
      <span class="sp-stu-filter-spacer"></span>
      <label class="sp-stu-rows-label">
        Rows per page
        <select class="sp-stu-rows-select" [(ngModel)]="pageSize" (change)="onPageSizeChange()">
          @for (n of pageSizeOptions; track n) {
            <option [ngValue]="n">{{ n }}</option>
          }
        </select>
      </label>
    </sp-admin-filter-bar>

    @if (loading()) {
      <sp-admin-loading-state message="Loading students" />
    } @else if (error()) {
      <sp-admin-error-state title="Could not load students" [message]="error()" />
    } @else {
      @if (students().length === 0) {
        <sp-admin-empty-state message="No students found." />
      } @else {
        <sp-admin-table variant="data" density="compact" minWidth="980px">
          <table>
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
              @for (s of students(); track s.studentProfileId) {
                <tr [class.sp-admin-archived-row]="s.lifecycleStage === 'Archived'">
                  <td class="sp-admin-wide-cell">
                    <div class="sp-admin-student-name">{{ displayName(s) }}</div>
                    <sp-admin-copyable-text [value]="s.email" class="sp-admin-table-muted" />
                  </td>
                  <td>
                    <sp-admin-badge [tone]="lifecycleTone(s.lifecycleStage)">{{ lifecycleLabel(s.lifecycleStage) }}</sp-admin-badge>
                  </td>
                  <td>
                    <sp-admin-badge [tone]="onboardingTone(s.onboardingStatus)">{{ onboardingLabel(s.onboardingStatus) }}</sp-admin-badge>
                  </td>
                  <td>
                    @if (s.cefrLevel) {
                      <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
                    } @else {
                      <span class="sp-admin-table-empty">-</span>
                    }
                  </td>
                  <td class="sp-admin-profile-cell">
                    @if (s.careerContext || s.learningGoal) {
                      <sp-admin-truncated-text [value]="s.careerContext || s.learningGoal || ''" [maxLength]="60" [maxWidth]="'260px'" />
                    } @else {
                      <span class="sp-admin-table-empty">Not set</span>
                    }
                  </td>
                  <td class="sp-admin-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                  <td class="sp-admin-actions">
                    <sp-admin-table-actions>
                      <a role="menuitem" [routerLink]="[s.studentProfileId]" class="sp-adm-action-item">View</a>
                      <button role="menuitem" type="button" class="sp-adm-action-item" (click)="startEdit(s)">Edit</button>
                      @if (s.lifecycleStage !== 'Archived') {
                        <button role="menuitem" type="button" class="sp-adm-action-item" (click)="startResetPassword(s)">Reset password</button>
                        <button role="menuitem" type="button" class="sp-adm-action-item sp-adm-action-danger" (click)="startResetData(s)">Reset data</button>
                        <button role="menuitem" type="button" class="sp-adm-action-item sp-adm-action-danger" (click)="confirmArchive(s)">Archive</button>
                      }
                    </sp-admin-table-actions>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </sp-admin-table>
        @if (totalPages() > 1) {
          <sp-admin-pagination [page]="page()" [totalPages]="totalPages()" (pageChange)="onPageChange($event)" />
        }
      }
    }

    </sp-admin-page-body>

    <sp-admin-modal
      [open]="!!editing()"
      [title]="'Edit student'"
      [subtitle]="editing()?.email ?? ''"
      maxWidth="720px"
      (closed)="cancelEdit()"
    >
      <form (ngSubmit)="saveEdit()" class="sp-stu-edit-grid">
        <sp-admin-form-field label="First name">
          <sp-admin-input [(ngModel)]="editForm.firstName" name="firstName" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Last name">
          <sp-admin-input [(ngModel)]="editForm.lastName" name="lastName" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Display name" class="sp-stu-wide">
          <sp-admin-input [(ngModel)]="editForm.displayName" name="displayName" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Career or work context" class="sp-stu-wide">
          <sp-admin-input [(ngModel)]="editForm.careerContext" name="careerContext" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Learning goal" class="sp-stu-wide">
          <sp-admin-input [(ngModel)]="editForm.learningGoal" name="learningGoal" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Learning goal description" class="sp-stu-wide">
          <sp-admin-textarea [rows]="3" [(ngModel)]="editForm.learningGoalDescription" name="learningGoalDescription" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Difficult situations" class="sp-stu-wide">
          <sp-admin-textarea [rows]="3" [(ngModel)]="editForm.difficultSituationsText" name="difficultSituationsText" />
        </sp-admin-form-field>
        <sp-admin-form-field label="Preferred duration">
          <select class="sp-stu-select" [(ngModel)]="editForm.preferredSessionDurationMinutes" name="preferredSessionDurationMinutes">
            <option [ngValue]="null">Not set</option>
            @for (duration of sessionDurations; track duration) {
              <option [ngValue]="duration">{{ duration }} minutes</option>
            }
          </select>
        </sp-admin-form-field>
        <sp-admin-form-field label="Experience level">
          <select class="sp-stu-select" [(ngModel)]="editForm.professionalExperienceLevel" name="professionalExperienceLevel">
            <option [ngValue]="null">Not set</option>
            @for (level of experienceLevels; track level.value) {
              <option [ngValue]="level.value">{{ level.label }}</option>
            }
          </select>
        </sp-admin-form-field>
        <sp-admin-form-field label="Role familiarity">
          <select class="sp-stu-select" [(ngModel)]="editForm.roleFamiliarity" name="roleFamiliarity">
            <option [ngValue]="null">Not set</option>
            @for (level of familiarityLevels; track level.value) {
              <option [ngValue]="level.value">{{ level.label }}</option>
            }
          </select>
        </sp-admin-form-field>
        @if (editError()) {
          <div class="sp-admin-alert-error sp-stu-wide">{{ editError() }}</div>
        }
        <div class="sp-stu-wide flex justify-end gap-3 pt-2">
          <sp-admin-button variant="ghost" size="sm" type="button" (click)="cancelEdit()">Cancel</sp-admin-button>
          <sp-admin-button size="sm" type="submit" [loading]="savingEdit()" [disabled]="savingEdit()">
            {{ savingEdit() ? 'Saving...' : 'Save changes' }}
          </sp-admin-button>
        </div>
      </form>
    </sp-admin-modal>

    <sp-admin-modal
      [open]="!!resetting()"
      [title]="'Reset password'"
      [subtitle]="resetting()?.email ?? ''"
      (closed)="cancelResetPassword()"
    >
      @if (resetSuccessPassword()) {
        <div class="space-y-4">
          <div class="sp-admin-alert-success">
            Password reset. Share this temporary password with the student securely — it will not be shown again.
          </div>
          <sp-admin-form-field label="New temporary password">
            <sp-admin-input [value]="resetSuccessPassword()" [readonly]="true" />
          </sp-admin-form-field>
          <div class="flex justify-end">
            <sp-admin-button (click)="cancelResetPassword()">Done</sp-admin-button>
          </div>
        </div>
      } @else {
        <form (ngSubmit)="saveResetPassword()" class="space-y-4">
          <sp-admin-form-field label="New temporary password">
            <sp-admin-input
              [(ngModel)]="resetForm.newPassword"
              name="newPassword"
              placeholder="At least 8 characters, with a digit"
              autocomplete="off"
            />
          </sp-admin-form-field>
          <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="resetForm.mustChangePassword" name="mustChangePassword" class="accent-blue-600 w-4 h-4" />
            Require password change on next login
          </label>
          @if (resetError()) {
            <div class="sp-admin-alert-error">{{ resetError() }}</div>
          }
          <div class="flex justify-end gap-3 pt-2">
            <sp-admin-button variant="ghost" type="button" (click)="generateResetPassword()">Generate password</sp-admin-button>
            <sp-admin-button variant="ghost" type="button" (click)="cancelResetPassword()">Cancel</sp-admin-button>
            <sp-admin-button type="submit" [loading]="savingReset()" [disabled]="savingReset() || resetForm.newPassword.length < 8">
              {{ savingReset() ? 'Saving...' : 'Reset password' }}
            </sp-admin-button>
          </div>
        </form>
      }
    </sp-admin-modal>

    <sp-admin-modal
      [open]="!!resettingData()"
      [title]="'Reset student data'"
      [subtitle]="resettingData()?.email ?? ''"
      (closed)="cancelResetData()"
    >
      @if (resetDataResult(); as result) {
        <div class="space-y-3">
          <div class="sp-admin-alert-success">
            Reset complete. New stage: {{ result.newStage }} (was {{ result.previousStage }}).
          </div>
          <p class="text-xs text-gray-500">
            Cleared: onboarding={{ result.clearedItems.onboardingAnswers }},
            placement={{ result.clearedItems.placementResults }},
            courses/sessions={{ result.clearedItems.coursesAndSessions }},
            attempts={{ result.clearedItems.activityAttempts }},
            vocabulary={{ result.clearedItems.vocabulary }},
            memory={{ result.clearedItems.learningMemory }},
            audio files deleted={{ result.clearedItems.audioFilesDeleted }},
            progress={{ result.clearedItems.progressData }}.
          </p>
          <p class="text-xs text-gray-400">Reset log: {{ result.resetLogId }}</p>
          <div class="flex justify-end">
            <sp-admin-button (click)="cancelResetData()">Done</sp-admin-button>
          </div>
        </div>
      } @else {
        <form (ngSubmit)="saveResetData()" class="space-y-3">
          <sp-admin-form-field label="Preset">
            <select class="sp-stu-select" [(ngModel)]="resetDataForm.preset" name="preset" (change)="applyResetPreset()">
              @for (preset of resetPresets; track preset.key) {
                <option [ngValue]="preset.key">{{ preset.label }}</option>
              }
            </select>
          </sp-admin-form-field>

          <div class="flex flex-col gap-2">
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearOnboardingAnswers" name="clearOnboardingAnswers" class="accent-blue-600 w-4 h-4" />
              Clear onboarding answers
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearPlacementResults" name="clearPlacementResults" class="accent-blue-600 w-4 h-4" />
              Clear placement results
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearCoursesAndSessions" name="clearCoursesAndSessions" class="accent-blue-600 w-4 h-4" />
              Clear courses and sessions
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearActivityAttempts" name="clearActivityAttempts" class="accent-blue-600 w-4 h-4" />
              Clear activity attempts
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearVocabulary" name="clearVocabulary" class="accent-blue-600 w-4 h-4" />
              Clear vocabulary
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearLearningMemory" name="clearLearningMemory" class="accent-blue-600 w-4 h-4" />
              Clear learning memory
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearAudioFiles" name="clearAudioFiles" class="accent-blue-600 w-4 h-4" />
              Delete audio files
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearProgressData" name="clearProgressData" class="accent-blue-600 w-4 h-4" />
              Recalculate progress data
            </label>
          </div>

          <sp-admin-form-field label="Reason (required)">
            <sp-admin-textarea [rows]="2" [(ngModel)]="resetDataForm.reason" name="reason"
              placeholder="Why is this reset being performed?" />
          </sp-admin-form-field>

          @if (resettingData(); as student) {
            <sp-admin-form-field [label]="confirmEmailLabel(student.email)">
              <sp-admin-input [(ngModel)]="resetDataForm.confirmEmail" name="confirmEmail" autocomplete="off" />
            </sp-admin-form-field>
          }

          @if (resetDataError()) {
            <div class="sp-admin-alert-error">{{ resetDataError() }}</div>
          }
          @if (resettingData(); as student) {
            <div class="flex justify-end gap-3 pt-2">
              <sp-admin-button variant="ghost" type="button" (click)="cancelResetData()">Cancel</sp-admin-button>
              <sp-admin-button variant="danger" type="submit" [loading]="savingResetData()"
                [disabled]="savingResetData() || resetDataForm.confirmEmail !== student.email || !resetDataForm.reason.trim()">
                {{ savingResetData() ? 'Resetting...' : 'Reset data' }}
              </sp-admin-button>
            </div>
          }
        </form>
      }
    </sp-admin-modal>
  `,
  styles: [`
    .sp-stu-summary-row{display:grid;grid-template-columns:repeat(2,1fr);gap:14px;margin-bottom:4px;}
    @media(min-width:900px){.sp-stu-summary-row{grid-template-columns:repeat(4,1fr);}}
    .sp-stu-breakdown-row{max-width:480px;margin-bottom:4px;}

    .sp-stu-filter-spacer{flex:1;}
    .sp-stu-rows-label{display:inline-flex;align-items:center;gap:8px;font-size:13px;font-weight:600;color:#64748B;white-space:nowrap;flex-shrink:0;}
    .sp-stu-rows-select{height:32px;border:1px solid var(--sp-admin-border,#ECE9F5);border-radius:8px;padding:0 8px;font-size:13px;color:#0F172A;background:#fff;cursor:pointer;}

    .sp-admin-sortable{cursor:pointer;user-select:none;}
    .sp-admin-sortable:hover{color:#334155;}
    .sp-admin-filter-toggle{display:inline-flex;align-items:center;gap:8px;font-size:13px;font-weight:700;color:#475569;}
    .sp-admin-filter-toggle input{accent-color:var(--sp-admin-primary,#5B4BE8);}
    .sp-admin-student-name{font-weight:800;color:#0F172A;}
    .sp-admin-profile-cell{max-width:280px;}
    .sp-admin-archived-row td{background:#F8FAFC;color:#94A3B8;}
    .sp-stu-edit-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;}
    .sp-stu-wide{grid-column:1/-1;}
    .sp-stu-select{width:100%;height:44px;border:1px solid #E5E7EB;border-radius:8px;padding:0 16px;font-size:13px;background:#fff;color:#1A2130;box-sizing:border-box;}
    .sp-stu-select:focus{outline:none;border-color:#93C5FD;box-shadow:0 0 0 2px rgba(59,130,246,.1);}
    @media(max-width:640px){
      .sp-stu-edit-grid{grid-template-columns:1fr;}
    }
  `],
})
export class AdminStudentsComponent implements OnInit {
  // Server-driven list state
  students = signal<StudentListItem[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = 25;
  totalPages = signal(1);

  readonly pageSizeOptions = [10, 25, 50, 100];

  // Summary strip stats
  stats = signal<AdminStats | null>(null);
  loadingStats = signal(true);

  readonly onboardingBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const s = this.stats();
    if (!s || s.totalStudents === 0) return [];
    const total = s.totalStudents;
    const onboarded = s.onboardedStudents;
    const notOnboarded = total - onboarded;
    return [
      { label: 'Onboarded', value: onboarded, pct: Math.round((onboarded / total) * 100), tone: 'green' },
      { label: 'Pending', value: notOnboarded, pct: Math.round((notOnboarded / total) * 100), tone: 'amber' },
    ];
  });

  loading = signal(true);
  error = signal('');
  editing = signal<StudentListItem | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  includeArchived = false;

  searchTerm = signal('');
  sortColumn = signal<'name' | 'onboarding' | 'joined'>('joined');
  sortDirection = signal<'asc' | 'desc'>('desc');

  filterLifecycleStage = signal('');
  filterOnboardingStatus = signal('');
  filterCefrLevel = signal('');

  readonly lifecycleStageOptions: SpAdminSelectOption[] = [
    { value: 'Created', label: 'Created' },
    { value: 'PasswordChangeRequired', label: 'Password change required' },
    { value: 'OnboardingRequired', label: 'Onboarding required' },
    { value: 'OnboardingInProgress', label: 'Onboarding in progress' },
    { value: 'PlacementRequired', label: 'Placement required' },
    { value: 'PlacementInProgress', label: 'Placement in progress' },
    { value: 'PlacementCompleted', label: 'Placement completed' },
    { value: 'CourseReady', label: 'Course ready' },
    { value: 'InLesson', label: 'In lesson' },
    { value: 'ActiveLearning', label: 'Active learning' },
    { value: 'Paused', label: 'Paused' },
    { value: 'Archived', label: 'Archived' },
  ];

  readonly onboardingStatusOptions: SpAdminSelectOption[] = [
    { value: 'NotStarted', label: 'Not started' },
    { value: 'Pending', label: 'Pending' },
    { value: 'InProgress', label: 'In progress' },
    { value: 'Complete', label: 'Complete' },
  ];

  readonly cefrLevelOptions: SpAdminSelectOption[] = [
    { value: 'A1', label: 'A1 — Beginner' },
    { value: 'A2', label: 'A2 — Elementary' },
    { value: 'B1', label: 'B1 — Intermediate' },
    { value: 'B2', label: 'B2 — Upper intermediate' },
    { value: 'C1', label: 'C1 — Advanced' },
    { value: 'C2', label: 'C2 — Mastery' },
  ];

  hasActiveFilters(): boolean {
    return !!(this.searchTerm() || this.filterLifecycleStage() || this.filterOnboardingStatus() || this.filterCefrLevel());
  }

  onLifecycleStageChange(value: string): void {
    this.filterLifecycleStage.set(value);
    this.page.set(1);
    this.load();
  }

  onOnboardingStatusChange(value: string): void {
    this.filterOnboardingStatus.set(value);
    this.page.set(1);
    this.load();
  }

  onCefrLevelChange(value: string): void {
    this.filterCefrLevel.set(value);
    this.page.set(1);
    this.load();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.filterLifecycleStage.set('');
    this.filterOnboardingStatus.set('');
    this.filterCefrLevel.set('');
    this.page.set(1);
    this.load();
  }

  private sortByParam(): string {
    switch (this.sortColumn()) {
      case 'name': return 'name';
      case 'onboarding': return 'onboardingStatus';
      case 'joined': default: return 'createdAt';
    }
  }

  setSort(column: 'name' | 'onboarding' | 'joined'): void {
    if (this.sortColumn() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortColumn.set(column);
      this.sortDirection.set('asc');
    }
    this.page.set(1);
    this.load();
  }

  sortIndicator(column: 'name' | 'onboarding' | 'joined'): string {
    if (this.sortColumn() !== column) return '';
    return this.sortDirection() === 'asc' ? ' ▲' : ' ▼';
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
    this.load();
  }

  onIncludeArchivedChange(): void {
    this.page.set(1);
    this.load();
  }

  onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.load();
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

  onPageSizeChange(): void {
    this.page.set(1);
    this.load();
  }

  constructor(private adminApi: AdminApiService, private toast: ToastService) {}

  ngOnInit(): void {
    this.load();
    this.adminApi.getStats().subscribe({
      next: s => { this.stats.set(s); this.loadingStats.set(false); },
      error: () => { this.loadingStats.set(false); },
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.listStudents({
      page: this.page(),
      pageSize: this.pageSize,  // mutable — set by rows-per-page selector
      search: this.searchTerm() || undefined,
      includeArchived: this.includeArchived,
      sortBy: this.sortByParam(),
      sortDir: this.sortDirection(),
      lifecycleStage: this.filterLifecycleStage() || undefined,
      onboardingStatus: this.filterOnboardingStatus() || undefined,
      cefrLevel: this.filterCefrLevel() || undefined,
    }).subscribe({
      next: r => {
        this.students.set(r.items);
        this.totalCount.set(r.totalCount);
        this.totalPages.set(r.totalPages);
        this.loading.set(false);
      },
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
      next: () => {
        this.savingEdit.set(false);
        this.editing.set(null);
        this.toast.success('Student updated successfully');
        this.load();
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
      next: () => {
        this.toast.success('Student archived');
        this.load();
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

  readonly lifecycleLabel = lifecycleLabel;
  readonly lifecycleTone = lifecycleTone;
  readonly onboardingLabel = onboardingLabel;
  readonly onboardingTone = onboardingTone;

  confirmEmailLabel(email: string): string {
    return `Type the student email to confirm: ${email}`;
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
