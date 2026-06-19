import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { AdminStudentDetailComponent } from './admin-student-detail.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { UsageGovernanceService, StudentEffectivePolicy, UsagePolicy } from '../../../core/services/usage-governance.service';
import { ToastService } from '../../../core/services/toast.service';
import { StudentListItem, AdminStudentLearningMemory, AdminActivityHistoryItem } from '../../../core/models/admin.models';

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
      'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.listStudents.and.returnValue(of([makeStudent()]));
    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
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
      'listStudents', 'getStudentLearningMemory', 'getActivityHistory',
      'updateStudent', 'archiveStudent', 'resetStudentPassword', 'resetStudent',
    ]);
    governance = jasmine.createSpyObj('UsageGovernanceService', [
      'getStudentEffectivePolicy', 'listUsagePolicies', 'assignStudentPolicy', 'removeStudentPolicy',
    ]);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    adminApi.getStudentLearningMemory.and.returnValue(of(makeMemory()));
    adminApi.getActivityHistory.and.returnValue(of([] as AdminActivityHistoryItem[]));
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
    adminApi.listStudents.and.returnValue(of([makeStudent()]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student preferences');
  });

  it('shows empty state when no preference fields are set', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent()]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student has not set any learning preferences yet.');
  });

  it('renders preferred name when present', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({ preferredName: 'Alex' })]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Alex');
  });

  it('renders support language when present', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({
      supportLanguageCode: 'es',
      supportLanguageName: 'Spanish',
    })]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Spanish');
  });

  it('renders focus areas when present', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({
      focusAreas: ['Presentations', 'Emails', 'Meetings'],
    })]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Presentations');
    expect(html.textContent).toContain('Emails');
  });

  it('renders custom focus area in slide-over when present', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({
      customFocusArea: 'Technical writing',
      focusAreas: ['Emails'],
    })]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Technical writing');
  });

  it('renders learning goals in slide-over when present', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({
      learningGoals: ['Improve fluency', 'Business vocabulary'],
      focusAreas: ['Emails'],
    })]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Improve fluency');
  });

  it('opens sp-admin-slide-over when View preferences is clicked', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({ preferredName: 'Jo' })]));
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
    adminApi.listStudents.and.returnValue(of([makeStudent({
      preferredName: 'Jo',
      supportLanguageName: 'French',
      difficultyPreference: 'Challenging',
      translationHelpPreference: 'Minimal',
      focusAreas: ['Meetings'],
      customFocusArea: 'Cold calls',
      learningGoals: ['Fluency'],
      customLearningGoal: 'Pass DELF B2',
    })]));
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
    adminApi.listStudents.and.returnValue(of([makeStudent()]));
    const fixture = TestBed.createComponent(AdminStudentDetailComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openPrefsSlideOver();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Student has not set any learning preferences yet.');
  });

  it('has no edit or save controls for student preferences', () => {
    adminApi.listStudents.and.returnValue(of([makeStudent({ preferredName: 'Alex' })]));
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
