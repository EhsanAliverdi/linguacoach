import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName,
  AdminStudentLearningMemory, ResetStudentResponse, AdminActivityHistoryItem,
  AdminStudentDetail, StudentAuditHistoryItem, StudentReadinessPoolHealth, AdminMasteryPoolSummary,
  AdminPlacementLatestResponse, AdminPlacementProgress, AdminStudentPracticeSummary,
  AdminLearningPlanProgress, AdminStudentProgressSummary, AdminStudentSpeakingAttemptsResult,
  AdminWritingEvaluationItemDto,
} from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { UsageGovernanceService, StudentEffectivePolicy, UsagePolicy } from '../../../core/services/usage-governance.service';
import {
  SpAdminAlertComponent,
  SpAdminAvatarComponent,
  SpAdminBadgeComponent,
  SpAdminBreakdownBarsComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminButtonGroupAction,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminFormGridComponent,
  SpAdminIconComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNativeSelectComponent,
  SpAdminNativeSelectOption,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminRingMetricComponent,
  SpAdminSlideOverComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import { BreakdownBarItem } from '../../../design-system/admin';
import { lifecycleLabel, lifecycleTone, onboardingLabel, onboardingTone } from '../../../design-system/admin/utils/admin-badge.utils';
import { SpAdminBadgeTone } from '../../../design-system/admin/components/badge/sp-admin-badge.component';

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
  imports: [
    CommonModule, FormsModule, RouterLink,
    SpAdminAlertComponent,
    SpAdminAvatarComponent,
    SpAdminBadgeComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminButtonComponent,
    SpAdminButtonGroupComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminIconComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminRingMetricComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-student-detail.component.html',
})
export class AdminStudentDetailComponent implements OnInit {
  student = signal<AdminStudentDetail | null>(null);
  loading = signal(true);
  error = signal('');
  activeTab = signal<'overview' | 'activity' | 'settings'>('overview');

  memory = signal<AdminStudentLearningMemory | null>(null);
  memoryLoading = signal(true);
  memoryError = signal('');

  history = signal<AdminActivityHistoryItem[]>([]);
  historyLoading = signal(true);
  historyError = signal('');

  auditHistory = signal<StudentAuditHistoryItem[]>([]);
  auditHistoryLoading = signal(true);
  auditHistoryError = signal('');
  auditDetailsSlideOverOpen = signal(false);
  auditDetailsItem = signal<StudentAuditHistoryItem | null>(null);

  savingEdit = signal(false);
  editError = signal('');
  editForm: StudentEditForm = this.emptyForm();

  resetting = signal<AdminStudentDetail | null>(null);
  savingReset = signal(false);
  resetError = signal('');
  resetSuccessPassword = signal('');
  resetForm = { newPassword: '', mustChangePassword: true };

  resettingData = signal<AdminStudentDetail | null>(null);
  savingResetData = signal(false);
  resetDataError = signal('');
  resetDataResult = signal<ResetStudentResponse | null>(null);
  resetDataForm = this.emptyResetDataForm();

  readonly resetPresets: { key: string; label: string; flags: Omit<ResetStudentRequest, 'reason'> }[] = [
    { key: 'fixPassword', label: 'Fix password', flags: { targetStage: 'PasswordChangeRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'restartOnboarding', label: 'Restart onboarding', flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'restartPlacement', label: 'Restart placement', flags: { targetStage: 'PlacementRequired', clearOnboardingAnswers: false, clearPlacementResults: true, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'resetCourseOnly', label: 'Reset course only', flags: { targetStage: 'CourseReady', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: true, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
    { key: 'fullCleanReset', label: 'Full clean reset', flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: true, clearCoursesAndSessions: true, clearActivityAttempts: true, clearVocabulary: true, clearLearningMemory: true, clearAudioFiles: true, clearProgressData: true } },
    { key: 'custom', label: 'Custom', flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false } },
  ];

  readonly resetPresetOptions = computed<SpAdminNativeSelectOption[]>(() =>
    this.resetPresets.map(p => ({ value: p.key, label: p.label }))
  );

  readonly sessionDurations = [15, 20, 30, 45, 60];
  readonly sessionDurationOptions: SpAdminNativeSelectOption[] = [
    { value: null, label: 'Not set' },
    ...this.sessionDurations.map(d => ({ value: d, label: `${d} minutes` })),
  ];

  readonly experienceLevels = [
    { value: 0, label: 'No professional experience' },
    { value: 1, label: 'Entry level or graduate' },
    { value: 2, label: 'Junior, 0-2 years' },
    { value: 3, label: 'Mid-level, 2-5 years' },
    { value: 4, label: 'Senior, 5-10 years' },
    { value: 5, label: 'Lead or manager, 10+ years' },
  ];
  readonly experienceLevelOptions: SpAdminNativeSelectOption[] = [
    { value: null, label: 'Not set' },
    ...this.experienceLevels.map(l => ({ value: l.value, label: l.label })),
  ];

  poolHealth = signal<StudentReadinessPoolHealth | null>(null);
  poolHealthLoading = signal(true);
  poolHealthError = signal('');

  masteryPoolSummary = signal<AdminMasteryPoolSummary | null>(null);
  masteryPoolSummaryLoading = signal(true);
  masteryPoolSummaryError = signal('');

  practiceSummary = signal<AdminStudentPracticeSummary | null>(null);
  practiceSummaryLoading = signal(true);
  practiceSummaryError = signal('');

  speakingAttempts = signal<AdminStudentSpeakingAttemptsResult | null>(null);
  speakingAttemptsLoading = signal(true);
  speakingAttemptsError = signal('');

  // Phase 17C — Writing evaluations
  writingEvaluations = signal<AdminWritingEvaluationItemDto[]>([]);
  writingEvaluationsLoading = signal(true);
  writingEvaluationsError = signal('');

  progressSummary = signal<AdminStudentProgressSummary | null>(null);
  progressSummaryLoading = signal(true);
  progressSummaryError = signal('');

  // Phase 15E — Learning Plan / Journey
  learningPlanProgress = signal<AdminLearningPlanProgress | null>(null);
  learningPlanProgressLoading = signal(true);
  learningPlanProgressError = signal('');

  // Phase 13A+13B — Adaptive Placement
  placementLatest = signal<AdminPlacementLatestResponse | null>(null);
  placementLoading = signal(true);
  placementError = signal('');
  placementProgress = signal<AdminPlacementProgress | null>(null);
  placementProgressLoading = signal(false);
  placementProgressError = signal('');
  startingPlacement = signal(false);
  completingPlacement = signal(false);
  abandoningPlacement = signal(false);
  expiringPlacement = signal(false);
  placementActionError = signal('');

  readonly lessonRingPct = computed(() => {
    const ph = this.poolHealth();
    if (!ph) return 0;
    const t = ph.todayLesson.targetCount;
    return t > 0 ? Math.round((ph.todayLesson.readyCount / t) * 100) : 0;
  });

  readonly gymRingPct = computed(() => {
    const ph = this.poolHealth();
    if (!ph) return 0;
    const t = ph.practiceGym.targetCount;
    return t > 0 ? Math.round((ph.practiceGym.readyCount / t) * 100) : 0;
  });

  readonly lessonPoolBreakdown = computed<BreakdownBarItem[]>(() => {
    const ph = this.poolHealth();
    if (!ph) return [];
    const l = ph.todayLesson;
    const tot = l.targetCount || 1;
    return ([
      { label: 'Ready',       value: l.readyCount,              pct: Math.round((l.readyCount              / tot) * 100), tone: 'green'  as const },
      { label: 'Review only', value: l.reviewOnlyCount,         pct: Math.round((l.reviewOnlyCount         / tot) * 100), tone: 'teal'   as const },
      { label: 'Queued',      value: l.queuedOrGeneratingCount, pct: Math.round((l.queuedOrGeneratingCount / tot) * 100), tone: 'indigo' as const },
      { label: 'Shortfall',   value: l.shortfallCount,          pct: Math.round((l.shortfallCount          / tot) * 100), tone: 'amber'  as const },
      { label: 'Skipped',     value: l.skippedCount,            pct: Math.round((l.skippedCount            / tot) * 100), tone: 'slate'   as const },
      { label: 'Failed',      value: l.failedCount,             pct: Math.round((l.failedCount             / tot) * 100), tone: 'danger' as const },
      { label: 'Stale',       value: l.staleCount,              pct: Math.round((l.staleCount              / tot) * 100), tone: 'slate'  as const },
    ] as BreakdownBarItem[]).filter(i => i.value > 0);
  });

  readonly gymPoolBreakdown = computed<BreakdownBarItem[]>(() => {
    const ph = this.poolHealth();
    if (!ph) return [];
    const g = ph.practiceGym;
    const tot = g.targetCount || 1;
    return ([
      { label: 'Ready',       value: g.readyCount,              pct: Math.round((g.readyCount              / tot) * 100), tone: 'green'  as const },
      { label: 'Review only', value: g.reviewOnlyCount,         pct: Math.round((g.reviewOnlyCount         / tot) * 100), tone: 'teal'   as const },
      { label: 'Queued',      value: g.queuedOrGeneratingCount, pct: Math.round((g.queuedOrGeneratingCount / tot) * 100), tone: 'indigo' as const },
      { label: 'Shortfall',   value: g.shortfallCount,          pct: Math.round((g.shortfallCount          / tot) * 100), tone: 'amber'  as const },
      { label: 'Skipped',     value: g.skippedCount,            pct: Math.round((g.skippedCount            / tot) * 100), tone: 'slate'   as const },
      { label: 'Failed',      value: g.failedCount,             pct: Math.round((g.failedCount             / tot) * 100), tone: 'danger' as const },
      { label: 'Stale',       value: g.staleCount,              pct: Math.round((g.staleCount              / tot) * 100), tone: 'slate'  as const },
    ] as BreakdownBarItem[]).filter(i => i.value > 0);
  });

  readonly lifecycleLabel = lifecycleLabel;
  readonly lifecycleTone = lifecycleTone;
  readonly onboardingLabel = onboardingLabel;
  readonly onboardingTone = onboardingTone;

  effectivePolicy = signal<StudentEffectivePolicy | null>(null);
  policyLoading = signal(true);
  policyError = signal('');

  availablePolicies = signal<UsagePolicy[]>([]);
  assigningPolicy = signal(false);
  savingAssignPolicy = signal(false);
  assignPolicyError = signal('');
  assignPolicyForm: { policyId: string; reason: string } = { policyId: '', reason: '' };

  readonly availablePolicyOptions = computed<SpAdminNativeSelectOption[]>(() =>
    this.availablePolicies().map(p => ({ value: p.id, label: p.name + (p.isDefault ? ' (default)' : '') }))
  );

  prefsSlideOverOpen = signal(false);

  settingCefr = signal(false);
  savingCefr = signal(false);
  cefrError = signal('');
  cefrForm: { cefrLevel: string; reason: string } = { cefrLevel: '', reason: '' };

  readonly cefrLevelOptions: SpAdminNativeSelectOption[] = [
    { value: '', label: 'Clear / Not set' },
    ...['A1', 'A2', 'B1', 'B2', 'C1', 'C2'].map(l => ({ value: l, label: l })),
  ];

  archiveConfirmOpen = signal(false);
  archiveTarget = signal<AdminStudentDetail | null>(null);
  archiveError = signal('');
  savingArchive = signal(false);

  removePolicyConfirmOpen = signal(false);
  removePolicyError = signal('');
  savingRemovePolicy = signal(false);

  lifecycleAction = signal<'reactivate' | 'pause' | 'unpause' | null>(null);
  savingLifecycleAction = signal(false);
  lifecycleActionError = signal('');

  constructor(
    private route: ActivatedRoute,
    private adminApi: AdminApiService,
    private toast: ToastService,
    private governance: UsageGovernanceService,
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
    this.loadHistory(id);
    this.loadAuditHistory(id);
    this.loadPolicy(id);
    this.loadPoolHealth(id);
    this.loadMasteryPoolSummary(id);
    this.loadPlacement(id);
    this.loadSpeakingAttempts(id);
    this.loadWritingEvaluations(id);
  }

  private loadStudent(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.getStudent(id).subscribe({
      next: detail => { this.student.set(detail); this.loading.set(false); this.startEdit(detail); },
      error: (err) => {
        this.error.set(err?.status === 404 ? 'Student not found.' : 'Could not load student.');
        this.loading.set(false);
      },
    });
  }

  private loadPoolHealth(id: string): void {
    this.poolHealthLoading.set(true);
    this.poolHealthError.set('');
    this.adminApi.getStudentReadinessPoolHealth(id).subscribe({
      next: ph => { this.poolHealth.set(ph); this.poolHealthLoading.set(false); },
      error: () => { this.poolHealthError.set('Could not load pool health.'); this.poolHealthLoading.set(false); },
    });

    this.adminApi.getStudentPracticeSummary(id).subscribe({
      next: ps => { this.practiceSummary.set(ps); this.practiceSummaryLoading.set(false); },
      error: () => { this.practiceSummaryError.set('Could not load practice summary.'); this.practiceSummaryLoading.set(false); },
    });

    this.adminApi.getLearningPlanProgress(id).subscribe({
      next: lp => { this.learningPlanProgress.set(lp); this.learningPlanProgressLoading.set(false); },
      error: () => { this.learningPlanProgressError.set('Could not load learning plan.'); this.learningPlanProgressLoading.set(false); },
    });

    this.adminApi.getStudentProgressSummary(id).subscribe({
      next: ps => { this.progressSummary.set(ps); this.progressSummaryLoading.set(false); },
      error: () => { this.progressSummaryError.set('Could not load progress summary.'); this.progressSummaryLoading.set(false); },
    });
  }

  private loadSpeakingAttempts(id: string): void {
    this.speakingAttemptsLoading.set(true);
    this.speakingAttemptsError.set('');
    this.adminApi.getStudentSpeakingAttempts(id).subscribe({
      next: r => { this.speakingAttempts.set(r); this.speakingAttemptsLoading.set(false); },
      error: () => { this.speakingAttemptsError.set('Could not load speaking submissions.'); this.speakingAttemptsLoading.set(false); },
    });
  }

  private loadWritingEvaluations(id: string): void {
    this.writingEvaluationsLoading.set(true);
    this.writingEvaluationsError.set('');
    this.adminApi.getStudentWritingEvaluations(id).subscribe({
      next: r => { this.writingEvaluations.set(r); this.writingEvaluationsLoading.set(false); },
      error: () => { this.writingEvaluationsError.set('Could not load writing evaluations.'); this.writingEvaluationsLoading.set(false); },
    });
  }

  writingStatusTone(status: string): SpAdminBadgeTone {
    if (status === 'Completed') return 'success';
    if (status === 'Failed') return 'danger';
    if (status === 'Evaluating') return 'info';
    return 'neutral';
  }

  private loadPlacement(id: string): void {
    this.placementLoading.set(true);
    this.placementError.set('');
    this.adminApi.getLatestPlacement(id).subscribe({
      next: r => {
        this.placementLatest.set(r);
        this.placementLoading.set(false);
        if (r.hasPlacement && r.assessmentId && r.status === 'InProgress') {
          this.loadPlacementProgress(id, r.assessmentId);
        }
      },
      error: () => { this.placementError.set('Could not load placement.'); this.placementLoading.set(false); },
    });
  }

  private loadPlacementProgress(studentId: string, assessmentId: string): void {
    this.placementProgressLoading.set(true);
    this.placementProgressError.set('');
    this.adminApi.getPlacementProgress(studentId, assessmentId).subscribe({
      next: p => { this.placementProgress.set(p); this.placementProgressLoading.set(false); },
      error: () => { this.placementProgressError.set('Could not load placement progress.'); this.placementProgressLoading.set(false); },
    });
  }

  startPlacement(): void {
    const id = this.student()?.studentProfileId;
    if (!id) return;
    this.startingPlacement.set(true);
    this.placementActionError.set('');
    this.adminApi.startPlacement(id).subscribe({
      next: () => { this.startingPlacement.set(false); this.loadPlacement(id); },
      error: (err: { error?: { error?: string } }) => {
        this.startingPlacement.set(false);
        this.placementActionError.set(err.error?.error ?? 'Could not start placement.');
      },
    });
  }

  completePlacement(): void {
    const id = this.student()?.studentProfileId;
    const assessmentId = this.placementLatest()?.assessmentId;
    if (!id || !assessmentId) return;
    this.completingPlacement.set(true);
    this.placementActionError.set('');
    this.adminApi.completePlacement(id, assessmentId).subscribe({
      next: () => { this.completingPlacement.set(false); this.loadPlacement(id); },
      error: (err: { error?: { error?: string } }) => {
        this.completingPlacement.set(false);
        this.placementActionError.set(err.error?.error ?? 'Could not complete placement.');
      },
    });
  }

  abandonPlacement(): void {
    const id = this.student()?.studentProfileId;
    const assessmentId = this.placementLatest()?.assessmentId;
    if (!id || !assessmentId) return;
    this.abandoningPlacement.set(true);
    this.placementActionError.set('');
    this.adminApi.abandonPlacement(id, assessmentId).subscribe({
      next: () => { this.abandoningPlacement.set(false); this.loadPlacement(id); },
      error: (err: { error?: { error?: string } }) => {
        this.abandoningPlacement.set(false);
        this.placementActionError.set(err.error?.error ?? 'Could not abandon placement.');
      },
    });
  }

  expirePlacement(): void {
    const id = this.student()?.studentProfileId;
    const assessmentId = this.placementLatest()?.assessmentId;
    if (!id || !assessmentId) return;
    this.expiringPlacement.set(true);
    this.placementActionError.set('');
    this.adminApi.expirePlacement(id, assessmentId).subscribe({
      next: () => { this.expiringPlacement.set(false); this.loadPlacement(id); },
      error: (err: { error?: { error?: string } }) => {
        this.expiringPlacement.set(false);
        this.placementActionError.set(err.error?.error ?? 'Could not expire placement.');
      },
    });
  }

  private loadMasteryPoolSummary(id: string): void {
    this.masteryPoolSummaryLoading.set(true);
    this.masteryPoolSummaryError.set('');
    this.adminApi.getStudentMasteryPoolSummary(id).subscribe({
      next: s => { this.masteryPoolSummary.set(s); this.masteryPoolSummaryLoading.set(false); },
      error: () => { this.masteryPoolSummaryError.set('Could not load mastery summary.'); this.masteryPoolSummaryLoading.set(false); },
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

  private loadHistory(id: string): void {
    this.historyLoading.set(true);
    this.historyError.set('');
    this.adminApi.getActivityHistory(id).subscribe({
      next: items => { this.history.set(items); this.historyLoading.set(false); },
      error: () => { this.historyError.set('Could not load activity history.'); this.historyLoading.set(false); },
    });
  }

  private loadAuditHistory(id: string): void {
    this.auditHistoryLoading.set(true);
    this.auditHistoryError.set('');
    this.adminApi.getStudentAuditHistory(id).subscribe({
      next: items => { this.auditHistory.set(items); this.auditHistoryLoading.set(false); },
      error: () => { this.auditHistoryError.set('Could not load audit history.'); this.auditHistoryLoading.set(false); },
    });
  }

  private loadPolicy(id: string): void {
    this.policyLoading.set(true);
    this.policyError.set('');
    this.governance.getStudentEffectivePolicy(id).subscribe({
      next: ep => { this.effectivePolicy.set(ep); this.policyLoading.set(false); },
      error: () => { this.policyError.set('Could not load usage policy.'); this.policyLoading.set(false); },
    });
  }

  openAuditDetails(item: StudentAuditHistoryItem): void {
    this.auditDetailsItem.set(item);
    this.auditDetailsSlideOverOpen.set(true);
  }

  closeAuditDetails(): void {
    this.auditDetailsSlideOverOpen.set(false);
    this.auditDetailsItem.set(null);
  }

  readonly closeFooterActions: SpAdminButtonGroupAction[] = [
    { id: 'close', label: 'Close', variant: 'neutral', appearance: 'outline' },
  ];

  displayName(student: AdminStudentDetail): string {
    return student.displayName
      || [student.firstName, student.lastName].filter(Boolean).join(' ')
      || student.email;
  }

  initials(student: AdminStudentDetail): string {
    const name = this.displayName(student);
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return name.slice(0, 2).toUpperCase();
  }

  scoreValue(item: AdminActivityHistoryItem): number | null {
    return item.score ?? item.percentage ?? null;
  }

  speakingStatusTone(status: string): SpAdminBadgeTone {
    if (status === 'Evaluated') return 'success';
    if (status === 'PendingEvaluation' || status === 'Submitted') return 'warning';
    if (status === 'EvaluationFailed') return 'danger';
    if (status === 'EvaluationUnavailable') return 'neutral';
    return 'neutral';
  }

  speakingStatusLabel(status: string): string {
    if (status === 'PendingEvaluation') return 'Pending evaluation';
    if (status === 'Submitted') return 'Submitted';
    if (status === 'Evaluated') return 'Evaluated';
    if (status === 'EvaluationFailed') return 'Evaluation failed';
    if (status === 'EvaluationUnavailable') return 'Unavailable';
    return status;
  }

  scoreToneClass(item: AdminActivityHistoryItem): string {
    const v = this.scoreValue(item);
    if (v === null) return 'sp-admin-score-none';
    if (v >= 75) return 'sp-admin-score-high';
    if (v >= 45) return 'sp-admin-score-mid';
    return 'sp-admin-score-low';
  }

  scoreLabel(item: AdminActivityHistoryItem): string {
    const v = this.scoreValue(item);
    return v !== null ? `${Math.round(v)}/100` : '—';
  }

  startEdit(student: AdminStudentDetail): void {
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

  editFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'save', label: this.savingEdit() ? 'Saving...' : 'Save changes', loading: this.savingEdit(), disabled: this.savingEdit() },
    ];
  }

  onEditFooterAction(actionId: string): void {
    if (actionId === 'save') this.saveEdit();
    else { const s = this.student(); if (s) this.startEdit(s); }
  }

  saveEdit(): void {
    const student = this.student();
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
      next: () => { this.savingEdit.set(false); this.loadStudent(student.studentProfileId); this.toast.success('Student updated successfully'); },
      error: err => { this.savingEdit.set(false); this.editError.set(err.error?.error ?? 'Could not update student.'); },
    });
  }

  // ── Archive ──────────────────────────────────────────────────────────────

  openArchiveConfirm(student: AdminStudentDetail): void {
    this.archiveTarget.set(student);
    this.archiveError.set('');
    this.savingArchive.set(false);
    this.archiveConfirmOpen.set(true);
  }

  closeArchiveConfirm(): void {
    this.archiveConfirmOpen.set(false);
    this.archiveError.set('');
  }

  archiveFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'confirm', label: this.savingArchive() ? 'Archiving...' : 'Archive student', variant: 'danger', appearance: 'solid', loading: this.savingArchive(), disabled: this.savingArchive() },
    ];
  }

  onArchiveFooterAction(actionId: string): void {
    if (actionId === 'confirm') this.doArchive();
    else this.closeArchiveConfirm();
  }

  private doArchive(): void {
    const target = this.archiveTarget();
    if (!target) return;
    this.savingArchive.set(true);
    this.archiveError.set('');
    this.adminApi.archiveStudent(target.studentProfileId).subscribe({
      next: () => {
        this.savingArchive.set(false);
        this.archiveConfirmOpen.set(false);
        this.loadStudent(target.studentProfileId);
        this.toast.success('Student archived');
      },
      error: err => { this.savingArchive.set(false); this.archiveError.set(err.error?.error ?? 'Could not archive student.'); },
    });
  }

  // ── Reset password ────────────────────────────────────────────────────────

  startResetPassword(student: AdminStudentDetail): void {
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
    for (let i = 0; i < 12; i++) result += chars[Math.floor(Math.random() * chars.length)];
    this.resetForm.newPassword = result;
  }

  resetPasswordFooterActions(): SpAdminButtonGroupAction[] {
    if (this.resetSuccessPassword()) {
      return [{ id: 'done', label: 'Done' }];
    }
    return [
      { id: 'generate', label: 'Generate password', variant: 'neutral', appearance: 'ghost' },
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'submit', label: this.savingReset() ? 'Saving...' : 'Reset password', loading: this.savingReset(), disabled: this.savingReset() || this.resetForm.newPassword.length < 8 },
    ];
  }

  onResetPasswordFooterAction(actionId: string): void {
    switch (actionId) {
      case 'generate': this.generateResetPassword(); break;
      case 'cancel':   this.cancelResetPassword(); break;
      case 'done':     this.cancelResetPassword(); break;
      case 'submit':   this.saveResetPassword(); break;
    }
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
      error: err => { this.savingReset.set(false); this.resetError.set(err.error?.error ?? 'Could not reset password.'); },
    });
  }

  // ── Reset data ────────────────────────────────────────────────────────────

  startResetData(student: AdminStudentDetail): void {
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

  resetDataCanSubmit(): boolean {
    const rd = this.resettingData();
    return !!(rd && this.resetDataForm.confirmEmail === rd.email && this.resetDataForm.reason.trim());
  }

  resetDataFooterActions(): SpAdminButtonGroupAction[] {
    if (this.resetDataResult()) {
      return [{ id: 'done', label: 'Done' }];
    }
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'submit', label: this.savingResetData() ? 'Resetting...' : 'Reset data', variant: 'danger', appearance: 'solid', loading: this.savingResetData(), disabled: this.savingResetData() || !this.resetDataCanSubmit() },
    ];
  }

  onResetDataFooterAction(actionId: string): void {
    if (actionId === 'submit') this.saveResetData();
    else this.cancelResetData();
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
      next: result => {
        this.savingResetData.set(false);
        this.resetDataResult.set(result);
        this.toast.success(`Student data reset for ${student.email}`);
        this.loadStudent(student.studentProfileId);
        this.loadMemory(student.studentProfileId);
      },
      error: err => { this.savingResetData.set(false); this.resetDataError.set(err.error?.error ?? 'Could not reset student data.'); },
    });
  }

  // ── Assign policy ─────────────────────────────────────────────────────────

  startAssignPolicy(): void {
    this.assignPolicyError.set('');
    this.assignPolicyForm = { policyId: this.effectivePolicy()?.policy.id ?? '', reason: '' };
    this.governance.listUsagePolicies().subscribe({
      next: policies => { this.availablePolicies.set(policies.filter(p => p.isActive)); this.assigningPolicy.set(true); },
      error: () => this.toast.error('Could not load policies.'),
    });
  }

  cancelAssignPolicy(): void { this.assigningPolicy.set(false); this.assignPolicyError.set(''); }

  assignPolicyFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'save', label: this.savingAssignPolicy() ? 'Saving...' : 'Assign policy', loading: this.savingAssignPolicy(), disabled: this.savingAssignPolicy() || !this.assignPolicyForm.policyId },
    ];
  }

  onAssignPolicyFooterAction(actionId: string): void {
    if (actionId === 'save') this.saveAssignPolicy();
    else this.cancelAssignPolicy();
  }

  saveAssignPolicy(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId || !this.assignPolicyForm.policyId) return;
    this.savingAssignPolicy.set(true);
    this.assignPolicyError.set('');
    this.governance.assignStudentPolicy(studentId, this.assignPolicyForm.policyId, this.assignPolicyForm.reason || null).subscribe({
      next: () => {
        this.savingAssignPolicy.set(false);
        this.assigningPolicy.set(false);
        this.toast.success('Usage policy assigned.');
        this.loadPolicy(studentId);
      },
      error: err => { this.savingAssignPolicy.set(false); this.assignPolicyError.set(err.error?.message ?? 'Could not assign policy.'); },
    });
  }

  // ── Remove policy ─────────────────────────────────────────────────────────

  openRemovePolicyConfirm(): void {
    this.removePolicyError.set('');
    this.savingRemovePolicy.set(false);
    this.removePolicyConfirmOpen.set(true);
  }

  closeRemovePolicyConfirm(): void { this.removePolicyConfirmOpen.set(false); this.removePolicyError.set(''); }

  removePolicyFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'confirm', label: this.savingRemovePolicy() ? 'Removing...' : 'Reset to default', variant: 'danger', appearance: 'solid', loading: this.savingRemovePolicy(), disabled: this.savingRemovePolicy() },
    ];
  }

  onRemovePolicyFooterAction(actionId: string): void {
    if (actionId === 'confirm') this.doRemovePolicy();
    else this.closeRemovePolicyConfirm();
  }

  private doRemovePolicy(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId) return;
    this.savingRemovePolicy.set(true);
    this.removePolicyError.set('');
    this.governance.removeStudentPolicy(studentId).subscribe({
      next: () => {
        this.savingRemovePolicy.set(false);
        this.removePolicyConfirmOpen.set(false);
        this.toast.success('Policy override removed. Student reverts to global default.');
        this.loadPolicy(studentId);
      },
      error: err => { this.savingRemovePolicy.set(false); this.removePolicyError.set(err.error?.message ?? 'Could not remove policy assignment.'); },
    });
  }

  // ── Preferences ───────────────────────────────────────────────────────────

  openPrefsSlideOver(): void { this.prefsSlideOverOpen.set(true); }
  closePrefsSlideOver(): void { this.prefsSlideOverOpen.set(false); }

  hasAnyPreference(s: AdminStudentDetail): boolean {
    return !!(
      s.preferredName || s.supportLanguageCode || s.supportLanguageName ||
      s.difficultyPreference || s.translationHelpPreference ||
      s.focusAreas?.length || s.customFocusArea ||
      s.learningGoals?.length || s.customLearningGoal
    );
  }

  // ── Set CEFR ──────────────────────────────────────────────────────────────

  startSetCefr(student: AdminStudentDetail): void {
    this.cefrForm = { cefrLevel: student.cefrLevel ?? '', reason: '' };
    this.cefrError.set('');
    this.settingCefr.set(true);
  }

  cancelSetCefr(): void { this.settingCefr.set(false); this.cefrError.set(''); }

  cefrFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'save', label: this.savingCefr() ? 'Saving...' : 'Save', loading: this.savingCefr(), disabled: this.savingCefr() },
    ];
  }

  onCefrFooterAction(actionId: string): void {
    if (actionId === 'save') this.saveSetCefr();
    else this.cancelSetCefr();
  }

  saveSetCefr(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId) return;
    this.savingCefr.set(true);
    this.cefrError.set('');
    const cefrLevel = this.cefrForm.cefrLevel.trim() || null;
    const reason = this.cefrForm.reason.trim() || undefined;
    this.adminApi.updateStudentCefr(studentId, cefrLevel, reason).subscribe({
      next: () => {
        this.savingCefr.set(false);
        this.settingCefr.set(false);
        this.loadStudent(studentId);
        this.toast.success(cefrLevel ? `CEFR level set to ${cefrLevel}` : 'CEFR level cleared');
      },
      error: (err: { error?: { error?: string } }) => { this.savingCefr.set(false); this.cefrError.set(err.error?.error ?? 'Could not update CEFR level.'); },
    });
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  lifecycleActionTitle(): string {
    switch (this.lifecycleAction()) {
      case 'reactivate': return 'Reactivate student';
      case 'pause':      return 'Pause student';
      case 'unpause':    return 'Unpause student';
      default:           return '';
    }
  }

  lifecycleActionDescription(): string {
    switch (this.lifecycleAction()) {
      case 'reactivate': return 'This will reactivate the student and set their lifecycle stage to Onboarding Required.';
      case 'pause':      return 'This will pause the student. They will not be able to progress until unpaused.';
      case 'unpause':    return 'This will unpause the student and set their lifecycle stage to Onboarding Required.';
      default:           return '';
    }
  }

  lifecycleFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
      { id: 'confirm', label: this.savingLifecycleAction() ? 'Saving...' : this.lifecycleActionTitle(), loading: this.savingLifecycleAction(), disabled: this.savingLifecycleAction() },
    ];
  }

  onLifecycleFooterAction(actionId: string): void {
    if (actionId === 'confirm') this.confirmLifecycleAction();
    else this.cancelLifecycleAction();
  }

  startLifecycleAction(action: 'reactivate' | 'pause' | 'unpause', _student: AdminStudentDetail): void {
    this.lifecycleAction.set(action);
    this.lifecycleActionError.set('');
    this.savingLifecycleAction.set(false);
  }

  cancelLifecycleAction(): void { this.lifecycleAction.set(null); this.lifecycleActionError.set(''); }

  confirmLifecycleAction(): void {
    const action = this.lifecycleAction();
    const studentId = this.student()?.studentProfileId;
    if (!action || !studentId) return;
    this.savingLifecycleAction.set(true);
    this.lifecycleActionError.set('');
    let call$;
    if (action === 'reactivate') call$ = this.adminApi.reactivateStudent(studentId);
    else if (action === 'pause') call$ = this.adminApi.pauseStudent(studentId);
    else call$ = this.adminApi.unpauseStudent(studentId);
    call$.subscribe({
      next: () => {
        this.savingLifecycleAction.set(false);
        this.lifecycleAction.set(null);
        this.loadStudent(studentId);
        this.toast.success(`Student ${action}d successfully`);
      },
      error: (err: { error?: { error?: string } }) => {
        this.savingLifecycleAction.set(false);
        this.lifecycleActionError.set(err.error?.error ?? `Could not ${action} student.`);
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
      firstName: '', lastName: '', displayName: '', careerContext: '',
      learningGoal: '', learningGoalDescription: '', difficultSituationsText: '',
      preferredSessionDurationMinutes: null, professionalExperienceLevel: null, roleFamiliarity: null,
    };
  }
}
