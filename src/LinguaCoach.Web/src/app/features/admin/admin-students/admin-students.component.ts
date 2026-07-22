import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import {
  SpAdminAlertComponent,
  SpAdminAvatarComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminCheckboxComponent,
  SpAdminCopyableTextComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminFormGridComponent,
  SpAdminIdentityCellComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSlideOverComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import { SpAdminNativeSelectComponent, SpAdminNativeSelectOption } from '../../../design-system/admin/components/native-select/sp-admin-native-select.component';
import type { SpAdminSelectOption, SpAdminRowAction, SpAdminButtonGroupAction, SpAdminTableColumn, SpAdminTableFilter, SpAdminSortChange } from '../../../design-system/admin';
import { lifecycleLabel, lifecycleTone, onboardingLabel, onboardingTone } from '../../../design-system/admin/utils/admin-badge.utils';

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
  templateUrl: './admin-students.component.html',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminAvatarComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminButtonGroupComponent,
    SpAdminCheckboxComponent,
    SpAdminCopyableTextComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminIdentityCellComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSlideOverComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
})
export class AdminStudentsComponent implements OnInit {
  readonly studentColumns: SpAdminTableColumn[] = [
    { key: 'name', label: 'Student', sortable: true },
    { key: 'lifecycle', label: 'Lifecycle' },
    { key: 'onboarding', label: 'Onboarding', sortable: true },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'streak', label: 'Streak' },
    { key: 'minsPerWeek', label: 'Mins / WK' },
    { key: 'joined', label: 'Joined', sortable: true },
    { key: 'actions', label: 'Actions', align: 'right' },
  ];

  onSortChange(e: SpAdminSortChange): void {
    this.setSort(e.column as 'name' | 'onboarding' | 'joined');
  }

  students = signal<StudentListItem[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = 10;
  totalPages = signal(1);

  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly pageSizeSelectOptions: SpAdminNativeSelectOption[] = this.pageSizeOptions.map(n => ({ value: n, label: String(n) }));

  readonly showingFrom = computed(() => {
    if (this.totalCount() === 0) return 0;
    return (this.page() - 1) * this.pageSize + 1;
  });
  readonly showingTo = computed(() => Math.min(this.page() * this.pageSize, this.totalCount()));

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

  readonly sessionDurationOptions: SpAdminNativeSelectOption[] = [15, 20, 30, 45, 60].map(d => ({ value: d, label: `${d} minutes` }));
  readonly experienceLevelOptions: SpAdminNativeSelectOption[] = [
    { value: 0, label: 'No professional experience' },
    { value: 1, label: 'Entry level or graduate' },
    { value: 2, label: 'Junior, 0-2 years' },
    { value: 3, label: 'Mid-level, 2-5 years' },
    { value: 4, label: 'Senior, 5-10 years' },
    { value: 5, label: 'Lead or manager, 10+ years' },
  ];
  readonly familiarityLevelOptions: SpAdminNativeSelectOption[] = [
    { value: 0, label: 'New to role' },
    { value: 1, label: 'Understands basics' },
    { value: 2, label: 'Currently working in role' },
    { value: 3, label: 'Experienced in role' },
    { value: 4, label: 'Manages or trains others' },
  ];

  hasActiveFilters(): boolean {
    return !!(this.searchTerm() || this.filterLifecycleStage() || this.filterOnboardingStatus() || this.filterCefrLevel());
  }

  onLifecycleStageChange(value: string): void {
    this.filterLifecycleStage.set(value);
    this.page.set(1);
    this.load();
  }

  studentsFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'lifecycleStage', label: 'Lifecycle stage', options: this.lifecycleStageOptions, value: this.filterLifecycleStage(), placeholder: 'All lifecycle stages' },
  ]);

  onStudentsFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'lifecycleStage') this.onLifecycleStageChange(event.value);
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

  onArchivedChange(value: boolean): void {
    this.includeArchived = value;
    this.page.set(1);
    this.load();
  }

  onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.load();
  }

  onPageSizeSelectChange(value: number | string | null): void {
    this.pageSize = Number(value) || 10;
    this.page.set(1);
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
    { key: 'fixPassword',        label: 'Fix password',         flags: { targetStage: 'PasswordChangeRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'restartOnboarding',  label: 'Restart onboarding',   flags: { targetStage: 'OnboardingRequired',     clearOnboardingAnswers: true,  clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'restartPlacement',   label: 'Restart placement',    flags: { targetStage: 'PlacementRequired',      clearOnboardingAnswers: false, clearPlacementResults: true,  clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'resetCourseOnly',    label: 'Reset course only',    flags: { targetStage: 'CourseReady',            clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: true,  clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'fullCleanReset',     label: 'Full clean reset',     flags: { targetStage: 'OnboardingRequired',     clearOnboardingAnswers: true,  clearPlacementResults: true,  clearCoursesAndSessions: true,  clearActivityAttempts: true,  clearVocabulary: true,  clearLearningMemory: true,  clearAudioFiles: true,  clearProgressData: true  } },
    { key: 'custom',             label: 'Custom',               flags: { targetStage: 'OnboardingRequired',     clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
  ];

  readonly resetPresetOptions: SpAdminNativeSelectOption[] = this.resetPresets.map(p => ({ value: p.key, label: p.label }));

  // ── Row actions ──────────────────────────────────────────────────────────
  rowActions(s: StudentListItem): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [
      { id: 'view', label: 'View profile', icon: 'view' },
      { id: 'edit', label: 'Edit student', icon: 'edit' },
    ];
    if (s.lifecycleStage !== 'Archived') {
      actions.push({ id: 'reset-password', label: 'Reset password', icon: 'reset' });
      actions.push({ id: 'reset-data',     label: 'Reset data',     icon: 'reset' });
      actions.push({ id: 'archive',        label: 'Archive',        icon: 'archive', tone: 'danger', dividerBefore: true });
    }
    return actions;
  }

  onRowAction(actionId: string, student: StudentListItem): void {
    switch (actionId) {
      case 'view':           this.router.navigate([student.studentProfileId], { relativeTo: this.route }); break;
      case 'edit':           this.startEdit(student); break;
      case 'reset-password': this.startResetPassword(student); break;
      case 'reset-data':     this.startResetData(student); break;
      case 'archive':        this.confirmArchive(student); break;
    }
  }

  // ── Footer action arrays for button-group ─────────────────────────────────
  editFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel',       variant: 'neutral', appearance: 'outline' },
      { id: 'save',   label: this.savingEdit() ? 'Saving...' : 'Save changes', loading: this.savingEdit(), disabled: this.savingEdit() },
    ];
  }

  onEditFooterAction(id: string): void {
    if (id === 'cancel') this.cancelEdit();
    if (id === 'save') this.saveEdit();
  }

  resetPasswordFooterActions(): SpAdminButtonGroupAction[] {
    if (this.resetSuccessPassword()) {
      return [{ id: 'done', label: 'Done' }];
    }
    return [
      { id: 'generate', label: 'Generate password', variant: 'neutral', appearance: 'ghost' },
      { id: 'cancel',   label: 'Cancel',            variant: 'neutral', appearance: 'outline' },
      { id: 'submit',   label: this.savingReset() ? 'Saving...' : 'Reset password', loading: this.savingReset(), disabled: this.savingReset() || this.resetForm.newPassword.length < 8 },
    ];
  }

  onResetPasswordFooterAction(id: string): void {
    if (id === 'done')     this.cancelResetPassword();
    if (id === 'generate') this.generateResetPassword();
    if (id === 'cancel')   this.cancelResetPassword();
    if (id === 'submit')   this.saveResetPassword();
  }

  resetDataFooterActions(): SpAdminButtonGroupAction[] {
    if (this.resetDataResult()) {
      return [{ id: 'done', label: 'Done' }];
    }
    return [
      { id: 'cancel', label: 'Cancel',  variant: 'neutral', appearance: 'outline' },
      { id: 'submit', label: this.savingResetData() ? 'Resetting...' : 'Reset data', variant: 'danger', loading: this.savingResetData(), disabled: this.savingResetData() || !this.resetDataCanSubmit() },
    ];
  }

  onResetDataFooterAction(id: string): void {
    if (id === 'done')   this.cancelResetData();
    if (id === 'cancel') this.cancelResetData();
    if (id === 'submit') this.saveResetData();
  }

  resetDataCanSubmit(): boolean {
    const student = this.resettingData();
    return !!student && this.resetDataForm.confirmEmail === student.email && !!this.resetDataForm.reason.trim();
  }

  constructor(private adminApi: AdminApiService, private toast: ToastService, private router: Router, private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.listStudents({
      page: this.page(),
      pageSize: this.pageSize,
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

  avatarInitial(student: StudentListItem): string {
    const name = student.displayName || student.firstName || student.email || '?';
    return name[0].toUpperCase();
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

  cancelEdit(): void { this.editing.set(null); this.editError.set(''); }

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
      next: () => { this.savingEdit.set(false); this.editing.set(null); this.toast.success('Student updated successfully'); this.load(); },
      error: err => { this.savingEdit.set(false); this.editError.set(err.error?.error ?? 'Could not update student.'); },
    });
  }

  confirmArchive(student: StudentListItem): void {
    if (!window.confirm(`Archive ${student.email}? They will be hidden from the active list and cannot sign in.`)) return;
    this.adminApi.archiveStudent(student.studentProfileId).subscribe({
      next: () => { this.toast.success('Student archived'); this.load(); },
      error: err => this.toast.error(err.error?.error ?? 'Could not archive student.'),
    });
  }

  startResetPassword(student: StudentListItem): void {
    this.resetting.set(student);
    this.resetError.set('');
    this.resetSuccessPassword.set('');
    this.resetForm = { newPassword: '', mustChangePassword: true };
  }

  cancelResetPassword(): void { this.resetting.set(null); this.resetError.set(''); this.resetSuccessPassword.set(''); }

  generateResetPassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789';
    let result = '';
    for (let i = 0; i < 12; i++) result += chars[Math.floor(Math.random() * chars.length)];
    this.resetForm.newPassword = result;
  }

  saveResetPassword(): void {
    const student = this.resetting();
    if (!student || this.resetForm.newPassword.length < 8) return;
    this.savingReset.set(true);
    this.resetError.set('');
    this.adminApi.resetStudentPassword(student.studentProfileId, this.resetForm.newPassword, this.resetForm.mustChangePassword).subscribe({
      next: () => { this.savingReset.set(false); this.resetSuccessPassword.set(this.resetForm.newPassword); this.toast.success(`Password reset for ${student.email}`); },
      error: err => { this.savingReset.set(false); this.resetError.set(err.error?.error ?? 'Could not reset password.'); },
    });
  }

  startResetData(student: StudentListItem): void {
    this.resettingData.set(student);
    this.resetDataError.set('');
    this.resetDataResult.set(null);
    this.resetDataForm = this.emptyResetDataForm();
  }

  cancelResetData(): void { this.resettingData.set(null); this.resetDataError.set(''); this.resetDataResult.set(null); }

  applyResetPreset(): void {
    const preset = this.resetPresets.find(p => p.key === this.resetDataForm.preset);
    if (preset) Object.assign(this.resetDataForm, preset.flags);
  }

  saveResetData(): void {
    const student = this.resettingData();
    if (!student || !this.resetDataCanSubmit()) return;
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
      next: result => { this.savingResetData.set(false); this.resetDataResult.set(result); this.toast.success(`Student data reset for ${student.email}`); this.load(); },
      error: err => { this.savingResetData.set(false); this.resetDataError.set(err.error?.error ?? 'Could not reset student data.'); },
    });
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
    return { firstName: '', lastName: '', displayName: '', careerContext: '', learningGoal: '', learningGoalDescription: '', difficultSituationsText: '', preferredSessionDurationMinutes: null, professionalExperienceLevel: null, roleFamiliarity: null };
  }

  private emptyResetDataForm() {
    return { preset: 'restartOnboarding' as string, targetStage: 'OnboardingRequired' as StudentLifecycleStageName, clearOnboardingAnswers: true, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false, reason: '', confirmEmail: '' };
  }
}
