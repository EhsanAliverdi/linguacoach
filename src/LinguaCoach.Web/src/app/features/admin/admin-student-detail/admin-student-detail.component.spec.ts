import { TestBed } from '@angular/core/testing';
import { of, throwError, Subject } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { AdminStudentDetailComponent } from './admin-student-detail.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { UsageGovernanceService, StudentEffectivePolicy, UsagePolicy } from '../../../core/services/usage-governance.service';
import { ToastService } from '../../../core/services/toast.service';
import { StudentListItem, AdminStudentLearningMemory, AdminActivityHistoryItem, AdminStudentDetail, StudentOnboardingProgressInfo, StudentAuditHistoryItem } from '../../../core/models/admin.models';

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
      'getStudentAuditHistory',
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

  it('calls removeStudentPolicy after confirmation', () => {
    governance.removeStudentPolicy.and.returnValue(of(undefined));
    spyOn(window, 'confirm').and.returnValue(true);

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.confirmRemovePolicy();

    expect(governance.removeStudentPolicy).toHaveBeenCalledWith('student-1');
    expect(toast.success).toHaveBeenCalled();
  });

  it('does not call removeStudentPolicy if confirmation is cancelled', () => {
    spyOn(window, 'confirm').and.returnValue(false);

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.confirmRemovePolicy();

    expect(governance.removeStudentPolicy).not.toHaveBeenCalled();
  });

  it('shows toast error if remove fails', () => {
    governance.removeStudentPolicy.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
    spyOn(window, 'confirm').and.returnValue(true);

    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.confirmRemovePolicy();

    expect(toast.error).toHaveBeenCalledWith('Server error');
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
      'getStudentAuditHistory',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
    adminApi.getStudentAuditHistory.and.returnValue(of([] as StudentAuditHistoryItem[]));
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

  it('renders Student preferences section heading', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student preferences');
  });

  it('shows empty state when no preference fields are set', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail()));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student has not set any learning preferences yet.');
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

  it('renders focus areas when present', () => {
    adminApi.getStudent.and.returnValue(of(makeStudentDetail({
      focusAreas: ['Presentations', 'Emails', 'Meetings'],
    })));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
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

    const btn = Array.from(html.querySelectorAll('button')).find(b => b.textContent?.trim() === 'View preferences');
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
      'getStudentAuditHistory',
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
      'getStudentAuditHistory',
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
    expect(html.querySelector('.sp-admin-spinner')).toBeTruthy();
  });

  it('shows error state when getStudent fails', () => {
    adminApi = jasmine.createSpyObj('AdminApiService', [
      'getStudent', 'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'getStudentAuditHistory',
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
      'getStudentAuditHistory',
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
    expect(html.textContent).toContain('Student preferences');
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
      'getStudentAuditHistory',
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
      'getStudentAuditHistory',
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

  it('shows Unpause button when lifecycleStage is Paused', () => {
    setup('Paused');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    expect(buttons.some(b => b.textContent?.trim() === 'Unpause')).toBeTrue();
  });

  it('shows Pause button when not Archived and not Paused', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    expect(buttons.some(b => b.textContent?.trim() === 'Pause')).toBeTrue();
  });

  it('does not show Pause button when Archived', () => {
    setup('Archived');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
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

  it('shows confirm modal when Pause is clicked', () => {
    setup('CourseReady');
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    const btn = buttons.find(b => b.textContent?.trim() === 'Pause');
    btn?.click();
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
      'getStudentAuditHistory',
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
    expect(fixture.nativeElement.textContent).toContain('No admin actions recorded for this student.');
  });

  it('renders audit rows when history items are present', () => {
    setup([makeAuditItem({ action: 'SetCefr', source: 'AdminAuditLog' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('SetCefr');
  });

  it('renders reason and old/new values when present', () => {
    setup([makeAuditItem({ reason: 'Test reason', oldValue: 'B1', newValue: 'C1' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
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
    expect(fixture.nativeElement.textContent).toContain('Could not load audit history.');
  });

  it('has no edit or delete controls on audit rows', () => {
    setup([makeAuditItem(), makeAuditItem({ id: 'audit-2', action: 'Archive' })]);
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    // Find the audit history section by aria-label
    const auditSection = html.querySelector('[aria-label="Audit history"]') as HTMLElement | null;
    expect(auditSection).toBeTruthy();
    const buttons = auditSection ? Array.from(auditSection.querySelectorAll('button')) as HTMLButtonElement[] : [];
    const hasEditDelete = buttons.some(b => /^(edit|delete|remove)$/i.test(b.textContent?.trim() ?? ''));
    expect(hasEditDelete).toBeFalse();
  });
});
