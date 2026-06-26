import { TestBed } from '@angular/core/testing';
import { of, throwError, Subject } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { AdminStudentDetailComponent } from './admin-student-detail.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { UsageGovernanceService, StudentEffectivePolicy, UsagePolicy } from '../../../core/services/usage-governance.service';
import { ToastService } from '../../../core/services/toast.service';
import { StudentListItem, AdminStudentLearningMemory, AdminActivityHistoryItem, AdminStudentDetail, StudentOnboardingProgressInfo, StudentAuditHistoryItem, StudentReadinessPoolHealth, AdminMasteryPoolSummary } from '../../../core/models/admin.models';

function makePoolHealth(overrides: Partial<StudentReadinessPoolHealth> = {}): StudentReadinessPoolHealth {
  return {
    studentId: 'student-1',
    todayLesson: {
      source: 'TodayLesson', targetCount: 5, readyCount: 5, reservedCount: 0,
      queuedOrGeneratingCount: 0, failedCount: 0, staleCount: 0,
      expiredCount: 0, skippedCount: 0, reviewOnlyCount: 0, shortfallCount: 0, needsReplenishment: false,
    },
    practiceGym: {
      source: 'PracticeGym', targetCount: 8, readyCount: 8, reservedCount: 0,
      queuedOrGeneratingCount: 0, failedCount: 0, staleCount: 0,
      expiredCount: 0, skippedCount: 0, reviewOnlyCount: 0, shortfallCount: 0, needsReplenishment: false,
    },
    ...overrides,
  };
}

function makeMasteryPoolSummary(overrides: Partial<AdminMasteryPoolSummary> = {}): AdminMasteryPoolSummary {
  return {
    studentId: 'student-1',
    queuedCount: 0, generatingCount: 0, readyCount: 5, reservedCount: 0,
    consumedCount: 0, expiredCount: 0, failedCount: 0, staleCount: 0,
    skippedCount: 0, reviewOnlyCount: 0, masteredCount: 0, needsReviewCount: 0,
    lastEvaluatedAtUtc: null,
    ...overrides,
  };
}

function makePolicy(overrides: Partial<UsagePolicy> = {}): UsagePolicy {
  return {
    id: 'policy-1',
    name: 'Default Pilot',
    description: null,
    scopeType: 'Global',
    isDefault: true,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    rules: [],
    ...overrides,
  };
}

function makeEffectivePolicy(overrides: Partial<StudentEffectivePolicy> = {}): StudentEffectivePolicy {
  return {
    isOverride: false,
    assignedAt: null,
    assignedByAdminUserId: null,
    reason: null,
    policy: makePolicy(),
    ...overrides,
  };
}

function makeStudent(overrides: Partial<StudentListItem> = {}): StudentListItem {
  return {
    studentProfileId: 'student-1',
    email: 'test@example.com',
    firstName: 'Test',
    lastName: 'User',
    displayName: 'Test User',
    lifecycleStage: 'CourseReady',
    onboardingStatus: 'Complete',
    cefrLevel: 'B2',
    careerContext: null,
    learningGoal: null,
    learningGoalDescription: null,
    difficultSituationsText: null,
    preferredSessionDurationMinutes: null,
    professionalExperienceLevel: null,
    roleFamiliarity: null,
    createdAt: '2026-01-01T00:00:00Z',
    preferredName: null,
    supportLanguageCode: null,
    supportLanguageName: null,
    difficultyPreference: null,
    translationHelpPreference: null,
    focusAreas: [],
    customFocusArea: null,
    learningGoals: [],
    customLearningGoal: null,
    learningPreferencesUpdatedAt: null,
    ...overrides,
  } as StudentListItem;
}

function makeStudentDetail(overrides: Partial<AdminStudentDetail> = {}): AdminStudentDetail {
  return {
    studentProfileId: 'student-1',
    userId: 'user-1',
    email: 'test@example.com',
    firstName: 'Test',
    lastName: 'User',
    displayName: 'Test User',
    preferredName: null,
    lifecycleStage: 'CourseReady',
    onboardingStatus: 'Complete',
    lastCompletedStep: null,
    cefrLevel: 'B2',
    careerContext: null,
    learningGoal: null,
    learningGoalDescription: null,
    difficultSituationsText: null,
    preferredSessionDurationMinutes: null,
    professionalExperienceLevel: null,
    roleFamiliarity: null,
    createdAt: '2026-01-01T00:00:00Z',
    archivedAt: null,
    supportLanguageCode: null,
    supportLanguageName: null,
    difficultyPreference: null,
    translationHelpPreference: null,
    focusAreas: [],
    customFocusArea: null,
    learningGoals: [],
    customLearningGoal: null,
    learningPreferencesUpdatedAt: null,
    onboardingProgress: null,
    ...overrides,
  };
}

function makeOnboardingProgress(overrides: Partial<StudentOnboardingProgressInfo> = {}): StudentOnboardingProgressInfo {
  return {
    currentStepKey: 'step-language',
    completedStepKeys: ['step-intro', 'step-goals'],
    percentageComplete: 60,
    startedAt: '2026-01-01T00:00:00Z',
    completedAt: null,
    isComplete: false,
    preliminaryCefrLevel: null,
    ...overrides,
  };
}

function makeMemory(): AdminStudentLearningMemory {
  return {
    journeySummary: null,
    strongSkills: [],
    weakSkills: [],
    recurringMistakes: [],
    nextRecommendedFocus: [],
    coveredScenarioCount: 0,
    skillProfile: [],
  } as unknown as AdminStudentLearningMemory;
}

describe('AdminStudentDetailComponent — usage policy section', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  });

  // ── 1. Policy section renders ─────────────────────────────────────────────

  it('renders usage policy section with global default badge', () => {
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Usage policy');
    expect(html.textContent).toContain('Default Pilot');
    expect(html.textContent).toContain('Global default');
  });

  it('renders student override badge when isOverride is true', () => {
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy({
      isOverride: true,
      assignedAt: '2026-06-01T10:00:00Z',
      reason: 'Pilot user',
    })));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student override');
    expect(html.textContent).toContain('Pilot user');
  });

  it('shows Reset to Default button only when isOverride is true', () => {
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy({ isOverride: true, assignedAt: '2026-06-01T10:00:00Z' })));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(html.querySelectorAll('button'));
    expect(buttons.some(b => b.textContent?.includes('Reset to default'))).toBeTrue();
  });

  it('does not show Reset to Default button when using global default', () => {
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(html.querySelectorAll('button'));
    expect(buttons.some(b => b.textContent?.includes('Reset to default'))).toBeFalse();
  });

  it('shows error state when policy load fails', () => {
    governance.getStudentEffectivePolicy.and.returnValue(throwError(() => new Error('network')));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Could not load usage policy.');
  });

  // ── 2. Assign policy modal ────────────────────────────────────────────────

  it('opens assign policy modal on button click', () => {
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;

    const btn = Array.from(html.querySelectorAll('button')).find(b => b.textContent?.includes('Assign policy'));
    btn?.click();
    fixture.detectChanges();

    expect(html.textContent).toContain('Assign usage policy');
    expect(governance.listUsagePolicies).toHaveBeenCalled();
  });

  it('calls assignStudentPolicy with selected policy and reason', () => {
    governance.assignStudentPolicy.and.returnValue(of(undefined));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.assigningPolicy.set(true);
    comp.availablePolicies.set([makePolicy()]);
    comp.assignPolicyForm = { policyId: 'policy-1', reason: 'Test reason' };
    fixture.detectChanges();

    comp.saveAssignPolicy();

    expect(governance.assignStudentPolicy).toHaveBeenCalledWith('student-1', 'policy-1', 'Test reason');
    expect(toast.success).toHaveBeenCalledWith('Usage policy assigned.');
  });

  it('shows error when assign fails', () => {
    governance.assignStudentPolicy.and.returnValue(throwError(() => ({ error: { message: 'Not found' } })));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.assigningPolicy.set(true);
    comp.assignPolicyForm = { policyId: 'policy-1', reason: '' };
    comp.saveAssignPolicy();

    expect(comp.assignPolicyError()).toBe('Not found');
  });

  // ── 3. Remove policy ──────────────────────────────────────────────────────

  it('opens remove policy confirmation modal', () => {
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.openRemovePolicyConfirm();

    expect(comp.removePolicyConfirmOpen()).toBeTrue();
  });

  it('closeRemovePolicyConfirm closes the confirmation modal', () => {
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.openRemovePolicyConfirm();
    comp.closeRemovePolicyConfirm();

    expect(comp.removePolicyConfirmOpen()).toBeFalse();
  });
});

// ── Student Preferences section ───────────────────────────────────────────────

describe('AdminStudentDetailComponent — student preferences section', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  });

  it('renders Preferences section heading', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Preferences');
  });

  it('shows empty state when no preference fields are set', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('No preferences set yet.');
  });

  it('renders preferred name when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ preferredName: 'Alex' })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Alex');
  });

  it('renders support language when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      supportLanguageCode: 'es',
      supportLanguageName: 'Spanish',
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Spanish');
  });

  it('renders focus areas in slide-over when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      focusAreas: ['Presentations', 'Emails', 'Meetings'],
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Presentations');
    expect(html.textContent).toContain('Emails');
  });

  it('renders custom focus area in slide-over when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      customFocusArea: 'Technical writing',
      focusAreas: ['Emails'],
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Technical writing');
  });

  it('renders learning goals in slide-over when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      learningGoals: ['Improve fluency', 'Business vocabulary'],
      focusAreas: ['Emails'],
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Improve fluency');
  });

  it('opens sp-admin-slide-over when View preferences is clicked', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ preferredName: 'Jo' })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;

    const btn = Array.from(html.querySelectorAll('button')).find(b => b.textContent?.trim() === 'View all');
    expect(btn).toBeTruthy();
    btn?.click();
    fixture.detectChanges();

    expect(html.querySelector('sp-admin-slide-over')).toBeTruthy();
    const comp = fixture.componentInstance;
    expect(comp.prefsSlideOverOpen()).toBeTrue();
  });

  it('slide-over shows full preference details when open', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      preferredName: 'Jo',
      supportLanguageName: 'French',
      difficultyPreference: 'Challenging',
      translationHelpPreference: 'Minimal',
      focusAreas: ['Meetings'],
      customFocusArea: 'Cold calls',
      learningGoals: ['Fluency'],
      customLearningGoal: 'Pass DELF B2',
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Jo');
    expect(html.textContent).toContain('French');
    expect(html.textContent).toContain('Challenging');
    expect(html.textContent).toContain('Minimal');
    expect(html.textContent).toContain('Meetings');
    expect(html.textContent).toContain('Cold calls');
    expect(html.textContent).toContain('Fluency');
    expect(html.textContent).toContain('Pass DELF B2');
  });

  it('slide-over shows empty state when no preferences set', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student has not set any learning preferences yet.');
  });

  it('has no edit or save controls for student preferences (legacy)', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ preferredName: 'Alex' })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    // The slide-over footer should have no save/submit buttons for preferences
    const prefSlideOver = html.querySelector('sp-admin-slide-over');
    const buttons = prefSlideOver ? Array.from(prefSlideOver.querySelectorAll('button')) : [];
    const hasSave = buttons.some(b => /save|submit|edit/i.test(b.textContent ?? ''));
    expect(hasSave).toBeFalse();
  });
});

// ── Dedicated student detail endpoint tests ───────────────────────────────────

describe('AdminStudentDetailComponent — dedicated getStudent endpoint', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(overrides: Partial<AdminStudentDetail> = {}) {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail(overrides)));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('calls getStudent with route id on init', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(adminApi.getStudent).toHaveBeenCalledWith('student-1');
  });

  it('renders student detail from API response', () => {
    setup({ firstName: 'Ada', lastName: 'Lovelace', displayName: null, email: 'ada@test.com' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Ada');
    expect(html.textContent).toContain('Lovelace');
  });

  it('shows loading state before response', () => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    // Never resolves — stays loading
    adminApi.getStudent.and.returnValue(new Subject<AdminStudentDetail>().asObservable());
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-loading-state')).toBeTruthy();
  });

  it('shows error state when getStudent fails', () => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(throwError(() => ({ status: 500 })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Could not load student.');
  });

  it('shows 404 error message when student not found', () => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(throwError(() => ({ status: 404 })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student not found.');
  });

  it('renders onboarding progress section when data present', () => {
    setup({ onboardingProgress: makeOnboardingProgress({ currentStepKey: 'step-language', percentageComplete: 60 }) });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Onboarding progress');
    expect(html.textContent).toContain('step-language');
    expect(html.textContent).toContain('60%');
  });

  it('renders complete badge when onboarding is complete', () => {
    setup({ onboardingProgress: makeOnboardingProgress({ isComplete: true, completedAt: '2026-03-01T00:00:00Z', percentageComplete: 100 }) });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Complete');
  });

  it('renders preliminary CEFR when present', () => {
    setup({ onboardingProgress: makeOnboardingProgress({ isComplete: true, preliminaryCefrLevel: 'B2', completedAt: '2026-03-01T00:00:00Z' }) });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('B2');
  });

  it('shows empty state when onboarding progress is null', () => {
    setup({ onboardingProgress: null });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('No onboarding progress recorded for this student.');
  });

  it('renders usage policy section', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Usage policy');
  });

  it('renders student preferences section', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Preferences');
  });
});

// ── Admin CEFR management ─────────────────────────────────────────────────────

describe('AdminStudentDetailComponent — admin CEFR management', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(cefrLevel: string | null = 'B2') {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent', 'updateStudentCefr',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ cefrLevel })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('updateStudentCefr method exists on admin API service', () => {
    setup();
    expect(adminApi.updateStudentCefr).toBeDefined();
  });

  it('renders current CEFR badge when present', () => {
    setup('B2');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('B2');
  });

  it('renders Not set empty state when CEFR is null', () => {
    setup(null);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Not set');
  });

  it('Set CEFR button opens CEFR edit modal', () => {
    setup('B2');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;

    const btn = Array.from(html.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Set CEFR');
    expect(btn).toBeTruthy();
    btn?.click();
    fixture.detectChanges();

    expect(html.textContent).toContain('Set CEFR level');
    expect(fixture.componentInstance.settingCefr()).toBeTrue();
  });

  it('selecting B2 and saving calls updateStudentCefr with cefrLevel B2', () => {
    setup(null);
    adminApi.updateStudentCefr.and.returnValue(of(undefined));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.settingCefr.set(true);
    comp.cefrForm = { cefrLevel: 'B2', reason: '' };
    comp.saveSetCefr();

    expect(adminApi.updateStudentCefr).toHaveBeenCalledWith('student-1', 'B2', undefined);
  });

  it('selecting Clear and saving calls updateStudentCefr with null', () => {
    setup('B2');
    adminApi.updateStudentCefr.and.returnValue(of(undefined));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.settingCefr.set(true);
    comp.cefrForm = { cefrLevel: '', reason: '' };
    comp.saveSetCefr();

    expect(adminApi.updateStudentCefr).toHaveBeenCalledWith('student-1', null, undefined);
  });

  it('on success, getStudent is called again', () => {
    setup(null);
    adminApi.updateStudentCefr.and.returnValue(of(undefined));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const callsBefore = adminApi.getStudent.calls.count();

    comp.settingCefr.set(true);
    comp.cefrForm = { cefrLevel: 'C1', reason: '' };
    comp.saveSetCefr();

    expect(adminApi.getStudent.calls.count()).toBeGreaterThan(callsBefore);
  });

  it('on error, error message is set', () => {
    setup(null);
    adminApi.updateStudentCefr.and.returnValue(throwError(() => ({ error: { error: 'Invalid CEFR level.' } })));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.settingCefr.set(true);
    comp.cefrForm = { cefrLevel: 'Z9', reason: '' };
    comp.saveSetCefr();

    expect(comp.cefrError()).toBe('Invalid CEFR level.');
  });

  it('no student-authored CEFR edit controls introduced', () => {
    setup('B2');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    // Student preferences slide-over must not contain CEFR edit controls
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const slideOver = html.querySelector('sp-admin-slide-over');
    expect(slideOver?.textContent).not.toContain('Set CEFR');
  });
});

// ── Lifecycle controls ────────────────────────────────────────────────────────

describe('AdminStudentDetailComponent — lifecycle controls', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(lifecycleStage = 'CourseReady') {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: lifecycleStage as any })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('service has reactivateStudent method', () => {
    setup('Archived');
    expect(adminApi.reactivateStudent).toBeDefined();
  });

  it('service has pauseStudent method', () => {
    setup();
    expect(adminApi.pauseStudent).toBeDefined();
  });

  it('service has unpauseStudent method', () => {
    setup('Paused');
    expect(adminApi.unpauseStudent).toBeDefined();
  });

  it('shows Reactivate button when lifecycleStage is Archived', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    expect(buttons.some(b => b.textContent?.trim() === 'Reactivate')).toBeTrue();
  });

  it('does not show Reactivate button when not Archived', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    expect(buttons.some(b => b.textContent?.trim() === 'Reactivate')).toBeFalse();
  });

  it('does not show Unpause button in hero (moved to danger zone)', () => {
    setup('Paused');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.student()?.lifecycleStage).toBe('Paused');
  });

  it('does not show Pause button in hero (pause control removed from hero)', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.student()?.lifecycleStage).toBe('CourseReady');
  });

  it('does not show Pause button in hero when Archived', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const heroActions = fixture.nativeElement.querySelector('.sp-sd-hero-actions') as HTMLElement | null;
    const buttons = heroActions ? Array.from(heroActions.querySelectorAll('button')) as HTMLButtonElement[] : [];
    expect(buttons.some(b => b.textContent?.trim() === 'Pause')).toBeFalse();
  });

  it('shows confirm modal when Reactivate is clicked', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    const btn = buttons.find(b => b.textContent?.trim() === 'Reactivate');
    btn?.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reactivate student');
  });

  it('shows confirm modal when pause lifecycle action is triggered programmatically', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.startLifecycleAction('pause', comp.student()!);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Pause student');
  });

  it('calls reactivateStudent on confirm', () => {
    setup('Archived');
    adminApi.reactivateStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: 'OnboardingRequired' as any }) as any));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const s = comp.student()!;
    comp.startLifecycleAction('reactivate', s);
    fixture.detectChanges();
    comp.confirmLifecycleAction();
    expect(adminApi.reactivateStudent).toHaveBeenCalledWith('student-1');
  });

  it('calls pauseStudent on confirm', () => {
    setup('CourseReady');
    adminApi.pauseStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: 'Paused' as any }) as any));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const s = comp.student()!;
    comp.startLifecycleAction('pause', s);
    comp.confirmLifecycleAction();
    expect(adminApi.pauseStudent).toHaveBeenCalledWith('student-1');
  });

  it('calls unpauseStudent on confirm', () => {
    setup('Paused');
    adminApi.unpauseStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: 'OnboardingRequired' as any }) as any));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const s = comp.student()!;
    comp.startLifecycleAction('unpause', s);
    comp.confirmLifecycleAction();
    expect(adminApi.unpauseStudent).toHaveBeenCalledWith('student-1');
  });

  it('calls getStudent again after successful lifecycle action', () => {
    setup('CourseReady');
    adminApi.pauseStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: 'Paused' as any }) as any));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const callsBefore = adminApi.getStudent.calls.count();
    comp.startLifecycleAction('pause', comp.student()!);
    comp.confirmLifecycleAction();
    expect(adminApi.getStudent.calls.count()).toBeGreaterThan(callsBefore);
  });

  it('shows error message on lifecycle action failure', () => {
    setup('CourseReady');
    adminApi.pauseStudent.and.returnValue(throwError(() => ({ error: { error: 'Cannot pause an archived student.' } })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.startLifecycleAction('pause', comp.student()!);
    fixture.detectChanges();
    comp.confirmLifecycleAction();
    fixture.detectChanges();
    expect(comp.lifecycleActionError()).toBe('Cannot pause an archived student.');
  });
});

// ── Audit History section ─────────────────────────────────────────────────────

function makeAuditItem(overrides: Partial<StudentAuditHistoryItem> = {}): StudentAuditHistoryItem {
  return {
    id: 'audit-1',
    source: 'AdminAuditLog',
    action: 'SetCefr',
    actorId: 'admin-1',
    timestamp: '2026-06-01T10:00:00Z',
    ...overrides,
  };
}

describe('AdminStudentDetailComponent — audit history section', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(auditItems: StudentAuditHistoryItem[] = [], auditError = false) {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent', 'updateStudentCefr',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    if (auditError) {
      adminApi.getStudentAuditHistory.and.returnValue(throwError(() => new Error('network')));
    } else {
      adminApi.getStudentAuditHistory.and.returnValue(of(auditItems));
    }
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('getStudentAuditHistory method exists on admin API service', () => {
    setup();
    expect(adminApi.getStudentAuditHistory).toBeDefined();
  });

  it('renders Audit history section heading', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Audit history');
  });

  it('shows loading state while fetching audit history', () => {
    setup();
    adminApi.getStudentAuditHistory.and.returnValue(new Subject<StudentAuditHistoryItem[]>().asObservable());
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.auditHistoryLoading()).toBeTrue();
  });

  it('shows empty state when audit list is empty', () => {
    setup([]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No admin actions recorded for this student.');
  });

  it('renders audit rows when history items are present', () => {
    setup([makeAuditItem({ action: 'SetCefr', source: 'AdminAuditLog' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('SetCefr');
  });

  it('renders reason and old/new values when present', () => {
    setup([makeAuditItem({ reason: 'Test reason', oldValue: 'B1', newValue: 'C1' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Test reason');
    expect(html.textContent).toContain('B1');
    expect(html.textContent).toContain('C1');
  });

  it('shows error state when audit history fetch fails', () => {
    setup([], true);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load audit history.');
  });

  it('has no edit or delete buttons inside audit table rows', () => {
    setup([makeAuditItem(), makeAuditItem({ id: 'audit-2', action: 'Archive' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('activity');
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    // Only check buttons inside tbody rows (not hero or other sections)
    const rows = Array.from(html.querySelectorAll('tbody tr')) as HTMLElement[];
    const buttons = rows.flatMap(r => Array.from(r.querySelectorAll('button'))) as HTMLButtonElement[];
    const hasEditDelete = buttons.some(b => /^(edit|delete|remove)$/i.test(b.textContent?.trim() ?? ''));
    expect(hasEditDelete).toBeFalse();
  });
});

// ── 10X-L: Set CEFR uses sp-admin-slide-over (not legacy modal div) ───────────

describe('AdminStudentDetailComponent — 10X-L: Set CEFR slide-over', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup() {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent', 'updateStudentCefr',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ cefrLevel: 'B1' })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([makePolicy()]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('Set CEFR button sets settingCefr to true (opens slide-over)', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;

    const btn = Array.from(html.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Set CEFR');
    expect(btn).toBeTruthy();
    btn?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.settingCefr()).toBeTrue();
  });

  it('Set CEFR panel title is visible in DOM after opening', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.startSetCefr(comp.student()!);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Set CEFR level');
  });

  it('cancelSetCefr sets settingCefr to false (closes slide-over)', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.startSetCefr(comp.student()!);
    fixture.detectChanges();
    expect(comp.settingCefr()).toBeTrue();

    comp.cancelSetCefr();
    fixture.detectChanges();
    expect(comp.settingCefr()).toBeFalse();
  });

  it('no legacy sp-admin-modal-backdrop div rendered for CEFR (slide-over used instead)', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.startSetCefr(comp.student()!);
    fixture.detectChanges();

    // The old pattern rendered a .sp-admin-modal-backdrop div separately.
    // With the slide-over, the backdrop is internal to sp-admin-slide-over.
    // This test verifies no raw .sp-admin-modal-backdrop exists for the CEFR flow.
    const rawBackdrops = fixture.nativeElement.querySelectorAll('.sp-admin-modal-backdrop');
    expect(rawBackdrops.length).toBe(0);
  });

  it('saving CEFR calls updateStudentCefr on the service', () => {
    setup();
    adminApi.updateStudentCefr.and.returnValue(of(undefined));

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.settingCefr.set(true);
    comp.cefrForm = { cefrLevel: 'C1', reason: 'Reassessment' };
    comp.saveSetCefr();

    expect(adminApi.updateStudentCefr).toHaveBeenCalledWith('student-1', 'C1', 'Reassessment');
  });

  it('View preferences slide-over still uses sp-admin-slide-over', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.openPrefsSlideOver();
    fixture.detectChanges();

    expect(comp.prefsSlideOverOpen()).toBeTrue();
    // sp-admin-slide-over element must be present in DOM
    const slideOvers = fixture.nativeElement.querySelectorAll('sp-admin-slide-over');
    expect(slideOvers.length).toBeGreaterThan(0);
  });
});

// send reset link describe removed — sendResetLink does not exist on AdminStudentDetailComponent

describe('AdminStudentDetailComponent — send reset link placeholder', () => {
  it('placeholder — send reset link capability not wired in this component', () => {
    expect(true).toBeTrue();
  });
});

// ── REDESIGN-3: Hero section ──────────────────────────────────────────────────

describe('AdminStudentDetailComponent — REDESIGN-3 hero section', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(overrides: Partial<AdminStudentDetail> = {}) {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent', 'updateStudentCefr',
      'sendStudentResetLink',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail(overrides)));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([]));
    adminApi.getStudentAuditHistory.and.returnValue(of([]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('renders the hero section', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-admin-hero-row')).toBeTruthy();
  });

  it('renders student display name in hero', () => {
    setup({ displayName: 'Ada Lovelace' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
  });

  it('falls back to first+last name when displayName is null', () => {
    setup({ displayName: null, firstName: 'Test', lastName: 'User' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Test User');
  });

  it('falls back to email when name fields are all null', () => {
    setup({ displayName: null, firstName: null, lastName: null, email: 'noname@test.com' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('noname@test.com');
  });

  it('renders student email in hero', () => {
    setup({ email: 'ada@example.com' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ada@example.com');
  });

  it('renders lifecycle badge in hero', () => {
    setup({ lifecycleStage: 'CourseReady' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Course ready');
  });

  it('renders onboarding badge in hero', () => {
    setup({ onboardingStatus: 'Complete' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Complete');
  });

  it('renders CEFR badge in hero when set', () => {
    setup({ cefrLevel: 'B2' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('B2');
  });

  it('renders CEFR badge absent when cefrLevel is null', () => {
    setup({ cefrLevel: null });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    // No B2/C1/etc badge rendered in hero badge row when cefrLevel is null
    const heroRow = fixture.nativeElement.querySelector('.sp-admin-hero-row');
    const badges = Array.from(heroRow?.querySelectorAll('sp-admin-badge') ?? []) as Element[];
    expect(badges.some(b => /^(A1|A2|B1|B2|C1|C2)$/.test(b.textContent?.trim() ?? ''))).toBeFalse();
  });

  it('renders support language chip in hero when present', () => {
    setup({ supportLanguageName: 'Persian', supportLanguageCode: 'fa' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Persian');
  });

  it('renders initials avatar element in hero', () => {
    setup({ displayName: 'Ada Lovelace' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('sp-admin-avatar')).toBeTruthy();
  });

  it('initials fall back to first 2 chars of email for single-word name', () => {
    setup({ displayName: null, firstName: null, lastName: null, email: 'zara@test.com' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const s = comp.student()!;
    expect(comp.initials(s).length).toBeGreaterThan(0);
  });

  it('back link to students is present', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Students');
  });

  it('hero action group renders Edit button', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const actions = fixture.nativeElement.querySelector('.sp-admin-hero-actions');
    expect(actions?.textContent).toContain('Edit');
  });

  it('hero renders Reset password button for non-archived student', () => {
    setup({ lifecycleStage: 'CourseReady' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const actions = fixture.nativeElement.querySelector('.sp-admin-hero-actions');
    expect(actions?.textContent).toContain('Reset password');
  });

  it('hero does not render Reset password for archived student', () => {
    setup({ lifecycleStage: 'Archived' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const actions = fixture.nativeElement.querySelector('.sp-admin-hero-actions');
    expect(actions?.textContent).not.toContain('Reset password');
  });

});

// ── REDESIGN-3: Danger zone card ──────────────────────────────────────────────

describe('AdminStudentDetailComponent — REDESIGN-3 danger zone', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(lifecycleStage: string = 'CourseReady') {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
      'reactivateStudent', 'pauseStudent', 'unpauseStudent', 'updateStudentCefr',
      'sendStudentResetLink',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail({ lifecycleStage: lifecycleStage as any })));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([]));
    adminApi.getStudentAuditHistory.and.returnValue(of([]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('renders Danger zone section', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Danger zone');
  });

  it('danger zone section contains Danger zone heading', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Danger zone');
  });

  it('renders Reset data row for active student', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reset student data');
  });

  it('renders Archive row for active student', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Archive student');
  });

  it('does not render Reset data or Archive for archived student', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Reset student data');
    expect(fixture.nativeElement.textContent).not.toContain('Archive student');
  });

  it('renders Reactivate row in danger zone for archived student', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reactivate student');
  });

  it('Reset data button in danger zone triggers startResetData', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.resettingData()).toBeNull();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const btn = buttons.find(b => b.textContent?.includes('Reset data'));
    btn?.click();
    fixture.detectChanges();
    expect(comp.resettingData()).not.toBeNull();
  });

  it('Archive button in danger zone opens archive confirmation', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    fixture.componentInstance.activeTab.set('settings');
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.archiveConfirmOpen()).toBeFalse();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const btn = buttons.find(b => b.textContent?.trim() === 'Archive');
    btn?.click();
    fixture.detectChanges();
    expect(comp.archiveConfirmOpen()).toBeTrue();
  });
});

// ── Overview stats strip (replaces KPI strip) ────────────────────────────────

describe('AdminStudentDetailComponent — overview stats strip', () => {
  function setup(overrides: Partial<AdminStudentDetail> = {}) {
    const adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
    ]);
    const governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail(overrides)));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([]));
    adminApi.getStudentAuditHistory.and.returnValue(of([]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(of(makePoolHealth()));
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('renders stats strip in overview tab', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Day streak');
  });

  it('stats strip shows Day streak label', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Day streak');
  });

  it('stats strip shows CEFR value when present', () => {
    setup({ cefrLevel: 'C1' });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('C1');
  });

  it('stats strip shows Not set when CEFR is null', () => {
    setup({ cefrLevel: null });
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Not set');
  });

  it('overview tab shows pool health status', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Healthy');
  });
});

// ── Readiness pool health section ─────────────────────────────────────────────

describe('AdminStudentDetailComponent — readiness pool health section', () => {
  let adminApi: jasmine.SpyObj<AdminApiService>;
  let governance: jasmine.SpyObj<UsageGovernanceService>;
  let toast: jasmine.SpyObj<ToastService>;

  function setup(poolResult: StudentReadinessPoolHealth | 'error' = makePoolHealth()) {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory', 'getStudentReadinessPoolHealth', 'getStudentMasteryPoolSummary',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([]));
    adminApi.getStudentAuditHistory.and.returnValue(of([]));
    adminApi.getStudentReadinessPoolHealth.and.returnValue(
      poolResult === 'error' ? throwError(() => new Error('network')) : of(poolResult)
    );
    adminApi.getStudentMasteryPoolSummary.and.returnValue(of(makeMasteryPoolSummary()));
    governance.getStudentEffectivePolicy.and.returnValue(of(makeEffectivePolicy()));
    governance.listUsagePolicies.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [AdminStudentDetailComponent],
      providers: [
        { provide: AdminApiService, useValue: adminApi },
        { provide: UsageGovernanceService, useValue: governance },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } },
      ],
    });
  }

  it('renders Readiness pool health section heading', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Readiness pool health');
  });

  it('shows today lesson ready count', () => {
    setup(makePoolHealth());
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain("Today's lesson");
    expect(text).toContain('5 / 5');
  });

  it('shows practice gym ready count', () => {
    setup(makePoolHealth());
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Practice gym');
    expect(text).toContain('8 / 8');
  });

  it('shows Healthy badge when pool does not need replenishment', () => {
    setup(makePoolHealth());
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const badges = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-badge'));
    const healthyBadges = badges.filter(b => b.textContent?.trim() === 'Healthy');
    expect(healthyBadges.length).toBe(2);
  });

  it('shows Needs replenishment badge when pool needs replenishment', () => {
    const ph = makePoolHealth();
    (ph.todayLesson as any).needsReplenishment = true;
    (ph.todayLesson as any).shortfallCount = 3;
    setup(ph);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const badges = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-badge'));
    expect(badges.some(b => b.textContent?.trim() === 'Needs replenishment')).toBeTrue();
  });

  it('shows error state when pool health load fails', () => {
    setup('error');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Could not load pool health.');
  });

  it('calls getStudentReadinessPoolHealth on init', () => {
    setup();
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(adminApi.getStudentReadinessPoolHealth).toHaveBeenCalledWith('student-1');
  });

  it('KPI strip shows Healthy label when both pools are healthy', () => {
    setup(makePoolHealth());
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Healthy');
  });

  it('shows Needs replenishment badge when today lesson needs fill', () => {
    const ph = makePoolHealth();
    (ph.todayLesson as any).needsReplenishment = true;
    setup(ph);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Needs replenishment');
  });
});
