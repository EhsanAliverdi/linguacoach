import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  StudentListItem, UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName,
  AdminStudentLearningMemory, ResetStudentResponse,
} from '../../../core/models/admin.models';
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
  selector: 'app-admin-student-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="sp-admin-page-header">
      <div class="sp-admin-header-row">
        <div>
          <a routerLink="/admin/students" class="sp-admin-back-link">&larr; Back to students</a>
          @if (student(); as s) {
            <h1 class="sp-admin-page-title">{{ displayName(s) }}</h1>
            <p class="sp-admin-page-sub sp-safe-text">{{ s.email }}</p>
          }
        </div>
        @if (student(); as s) {
          <div class="sp-admin-row-actions">
            <button type="button" class="sp-admin-link-button" (click)="startEdit(s)">Edit</button>
            @if (s.lifecycleStage !== 'Archived') {
              <button type="button" class="sp-admin-link-button" (click)="startResetPassword(s)">Reset password</button>
              <button type="button" class="sp-admin-danger-link" (click)="startResetData(s)">Reset data</button>
              <button type="button" class="sp-admin-danger-link" (click)="confirmArchive(s)">Archive</button>
            }
          </div>
        }
      </div>
    </div>

    @if (loading()) {
      <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
    } @else if (error()) {
      <div class="sp-admin-alert-error">{{ error() }}</div>
    } @else if (student()) {
      @let s = student()!;
      <div class="sp-admin-detail-grid">
        <section class="sp-admin-table-card sp-admin-detail-card">
          <h2 class="sp-admin-card-title">Profile</h2>
          <dl class="sp-admin-detail-list">
            <div>
              <dt>Lifecycle stage</dt>
              <dd>
                <span class="sp-admin-badge"
                  [class.sp-admin-badge-slate]="s.lifecycleStage === 'Archived'"
                  [class.sp-admin-badge-indigo]="s.lifecycleStage !== 'Archived'">
                  {{ s.lifecycleStage }}
                </span>
              </dd>
            </div>
            <div>
              <dt>Onboarding</dt>
              <dd>
                <span class="sp-admin-badge"
                  [class.sp-admin-badge-green]="s.onboardingStatus === 'Complete'"
                  [class.sp-admin-badge-amber]="s.onboardingStatus !== 'Complete'">
                  {{ s.onboardingStatus }}
                </span>
              </dd>
            </div>
            <div>
              <dt>CEFR level</dt>
              <dd>{{ s.cefrLevel || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Career context</dt>
              <dd>{{ s.careerContext || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Learning goal</dt>
              <dd>{{ s.learningGoal || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Learning goal description</dt>
              <dd>{{ s.learningGoalDescription || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Difficult situations</dt>
              <dd>{{ s.difficultSituationsText || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Preferred session duration</dt>
              <dd>{{ s.preferredSessionDurationMinutes ? s.preferredSessionDurationMinutes + ' minutes' : 'Not set' }}</dd>
            </div>
            <div>
              <dt>Experience level</dt>
              <dd>{{ experienceLabel(s.professionalExperienceLevel) }}</dd>
            </div>
            <div>
              <dt>Role familiarity</dt>
              <dd>{{ familiarityLabel(s.roleFamiliarity) }}</dd>
            </div>
            <div>
              <dt>Joined</dt>
              <dd>{{ s.createdAt | date:'mediumDate' }}</dd>
            </div>
          </dl>
        </section>

        <section class="sp-admin-table-card sp-admin-detail-card">
          <h2 class="sp-admin-card-title">Learning memory</h2>
          @if (memoryLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (memoryError()) {
            <div class="sp-admin-alert-error">{{ memoryError() }}</div>
          } @else if (memory()) {
            @let mem = memory()!;
            <div class="sp-admin-memory">
              <div>
                <h3>Journey summary</h3>
                <p>{{ mem.journeySummary || 'No summary yet.' }}</p>
              </div>
              <div>
                <h3>Strong skills</h3>
                @if (mem.strongSkills.length) {
                  <ul>@for (skill of mem.strongSkills; track skill) { <li>{{ skill }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Weak skills</h3>
                @if (mem.weakSkills.length) {
                  <ul>@for (skill of mem.weakSkills; track skill) { <li>{{ skill }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Recurring mistakes</h3>
                @if (mem.recurringMistakes.length) {
                  <ul>@for (m of mem.recurringMistakes; track m) { <li>{{ m }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Next recommended focus</h3>
                @if (mem.nextRecommendedFocus.length) {
                  <ul>@for (f of mem.nextRecommendedFocus; track f) { <li>{{ f }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Covered scenarios</h3>
                <p>{{ mem.coveredScenarioCount }}</p>
              </div>
              <div class="sp-admin-wide">
                <h3>Skill profile</h3>
                @if (mem.skillProfile.length) {
                  <div class="sp-admin-skill-tags">
                    @for (skill of mem.skillProfile; track skill.skillKey) {
                      <span class="sp-admin-badge" [class.sp-admin-badge-amber]="skill.isWeak" [class.sp-admin-badge-green]="!skill.isWeak">
                        {{ skill.skillLabel }}
                      </span>
                    }
                  </div>
                } @else { <p class="sp-admin-table-empty">No skill profile yet.</p> }
              </div>
            </div>
          }
        </section>
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
    .sp-admin-header-row{display:flex;align-items:start;justify-content:space-between;gap:12px;flex-wrap:wrap;}
    .sp-admin-back-link{display:inline-block;margin-bottom:8px;font-size:13px;font-weight:700;color:#4338CA;text-decoration:none;}
    .sp-admin-row-actions{display:flex;align-items:center;gap:10px;white-space:nowrap;}
    .sp-admin-link-button,.sp-admin-danger-link{border:none;background:none;padding:0;font:inherit;font-size:12.5px;font-weight:800;cursor:pointer;}
    .sp-admin-link-button{color:#4338CA;}
    .sp-admin-danger-link{color:#DC2626;}
    .sp-admin-detail-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;}
    .sp-admin-detail-card{padding:20px;}
    .sp-admin-card-title{font-size:16px;font-weight:800;color:#0F172A;margin-bottom:14px;}
    .sp-admin-detail-list{display:grid;gap:12px;}
    .sp-admin-detail-list dt{font-size:12px;font-weight:800;color:#64748B;margin-bottom:2px;}
    .sp-admin-detail-list dd{font-size:14px;color:#0F172A;}
    .sp-admin-memory{display:grid;gap:16px;}
    .sp-admin-memory h3{font-size:12px;font-weight:800;color:#64748B;margin-bottom:6px;}
    .sp-admin-memory p{font-size:14px;color:#0F172A;}
    .sp-admin-memory ul{margin:0;padding-left:18px;font-size:14px;color:#0F172A;}
    .sp-admin-skill-tags{display:flex;flex-wrap:wrap;gap:8px;}
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.38);z-index:120;}
    .sp-admin-modal{position:fixed;right:24px;top:76px;bottom:24px;z-index:121;width:min(720px,calc(100vw - 48px));overflow:auto;background:#fff;border:1px solid #E2E8F0;border-radius:14px;box-shadow:0 20px 60px rgba(15,23,42,.22);}
    .sp-admin-modal-header{display:flex;align-items:start;justify-content:space-between;gap:12px;padding:20px 22px;border-bottom:1px solid #E2E8F0;}
    .sp-admin-modal-header h2{font-size:18px;font-weight:800;color:#0F172A;}
    .sp-admin-modal-header p{font-size:13px;color:#64748B;margin-top:2px;}
    .sp-admin-modal-header button{width:34px;height:34px;border-radius:9px;border:1px solid #E2E8F0;background:#fff;color:#64748B;cursor:pointer;display:grid;place-items:center;}
    .sp-admin-edit-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;padding:22px;}
    .sp-admin-edit-grid label span{display:block;margin-bottom:6px;font-size:12px;font-weight:800;color:#475569;}
    .sp-admin-modal-actions{display:flex;justify-content:flex-end;gap:10px;padding-top:8px;}
    @media(max-width:900px){
      .sp-admin-detail-grid{grid-template-columns:1fr;}
    }
    @media(max-width:720px){
      .sp-admin-modal{left:12px;right:12px;top:68px;bottom:12px;width:auto;}
      .sp-admin-edit-grid{grid-template-columns:1fr;padding:18px;}
    }
  `],
})
export class AdminStudentDetailComponent implements OnInit {
  student = signal<StudentListItem | null>(null);
  loading = signal(true);
  error = signal('');

  memory = signal<AdminStudentLearningMemory | null>(null);
  memoryLoading = signal(true);
  memoryError = signal('');

  editing = signal<StudentListItem | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  editForm: StudentEditForm = this.emptyForm();

  resetting = signal<StudentListItem | null>(null);
  savingReset = signal(false);
  resetError = signal('');
  resetSuccessPassword = signal('');
  resetForm = { newPassword: '', mustChangePassword: true };

  resettingData = signal<StudentListItem | null>(null);
  savingResetData = signal(false);
  resetDataError = signal('');
  resetDataResult = signal<ResetStudentResponse | null>(null);
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

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private adminApi: AdminApiService,
    private toast: ToastService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Missing student id.');
      this.loading.set(false);
      return;
    }
    this.loadStudent(id);
    this.loadMemory(id);
  }

  private loadStudent(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.listStudents(true).subscribe({
      next: students => {
        const found = students.find(s => s.studentProfileId === id);
        if (!found) {
          this.error.set('Student not found.');
        } else {
          this.student.set(found);
        }
        this.loading.set(false);
      },
      error: () => { this.error.set('Could not load student.'); this.loading.set(false); },
    });
  }

  private loadMemory(id: string): void {
    this.memoryLoading.set(true);
    this.memoryError.set('');
    this.adminApi.getStudentLearningMemory(id).subscribe({
      next: mem => { this.memory.set(mem); this.memoryLoading.set(false); },
      error: () => { this.memoryError.set('Could not load learning memory.'); this.memoryLoading.set(false); },
    });
  }

  displayName(student: StudentListItem): string {
    return student.displayName
      || [student.firstName, student.lastName].filter(Boolean).join(' ')
      || student.email;
  }

  experienceLabel(value: number | null): string {
    return this.experienceLevels.find(l => l.value === value)?.label ?? 'Not set';
  }

  familiarityLabel(value: number | null): string {
    return this.familiarityLevels.find(l => l.value === value)?.label ?? 'Not set';
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
        this.student.set(updated);
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
        this.student.set(updated);
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
        this.loadStudent(student.studentProfileId);
        this.loadMemory(student.studentProfileId);
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
