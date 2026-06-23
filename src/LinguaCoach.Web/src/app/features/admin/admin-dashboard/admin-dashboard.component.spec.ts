import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminDashboardComponent } from './admin-dashboard.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminStats, AiConfigCategoryItem } from '../../../core/models/admin.models';

const STATS: AdminStats = {
  totalStudents: 5,
  onboardedStudents: 3,
  totalActivityAttempts: 120,
};

const STUDENT: StudentListItem = {
  studentProfileId: 'sp-1',
  userId: 'user-1',
  email: 'alice@example.com',
  firstName: 'Alice',
  lastName: 'Smith',
  displayName: 'Alice Smith',
  onboardingStatus: 'Completed',
  lifecycleStage: 'CourseReady',
  cefrLevel: 'B2',
  careerContext: null,
  learningGoal: null,
  learningGoalDescription: null as any,
  difficultSituationsText: null as any,
  preferredSessionDurationMinutes: null,
  professionalExperienceLevel: null,
  roleFamiliarity: null,
  createdAt: '2026-01-15T00:00:00Z',
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
};

const STUDENT_NO_CEFR: StudentListItem = {
  ...STUDENT,
  studentProfileId: 'sp-2',
  userId: 'user-2',
  email: 'bob@example.com',
  displayName: 'Bob Jones',
  cefrLevel: null,
  onboardingStatus: 'NotStarted',
};

const STUDENT_IN_PROGRESS: StudentListItem = {
  ...STUDENT,
  studentProfileId: 'sp-3',
  userId: 'user-3',
  email: 'carol@example.com',
  onboardingStatus: 'InProgress',
  lifecycleStage: 'OnboardingPending',
  cefrLevel: 'A2',
};

const STUDENT_PLACEMENT: StudentListItem = {
  ...STUDENT,
  studentProfileId: 'sp-4',
  userId: 'user-4',
  email: 'dave@example.com',
  onboardingStatus: 'Completed',
  lifecycleStage: 'PlacementPending',
  cefrLevel: 'B1',
};

const AI_CATEGORIES_CONFIGURED: AiConfigCategoryItem[] = [
  { id: '1', categoryKey: 'writing', displayName: 'Writing activities', providerName: 'OpenAI', modelName: 'gpt-4o', voiceName: null },
  { id: '2', categoryKey: 'tts', displayName: 'Text to speech', providerName: 'OpenAI', modelName: 'tts-1', voiceName: 'nova' },
];

const AI_CATEGORIES_PARTIAL: AiConfigCategoryItem[] = [
  { id: '1', categoryKey: 'writing', displayName: 'Writing activities', providerName: 'OpenAI', modelName: 'gpt-4o', voiceName: null },
  { id: '2', categoryKey: 'tts', displayName: 'Text to speech', providerName: null, modelName: null, voiceName: null },
];

const AI_CATEGORIES_EMPTY: AiConfigCategoryItem[] = [];

const AI_CATEGORIES_NONE_CONFIGURED: AiConfigCategoryItem[] = [
  { id: '1', categoryKey: 'writing', displayName: 'Writing activities', providerName: null, modelName: null, voiceName: null },
];

function pagedOf(items: StudentListItem[]) {
  return { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1 };
}

function makeAdminApi(
  students: StudentListItem[] = [STUDENT],
  stats: AdminStats = STATS,
  aiCategories: AiConfigCategoryItem[] | 'error' = AI_CATEGORIES_CONFIGURED,
) {
  return {
    listStudents: jasmine.createSpy('listStudents').and.returnValue(of(pagedOf(students))),
    getStats: jasmine.createSpy('getStats').and.returnValue(of(stats)),
    listAiCategories: jasmine.createSpy('listAiCategories').and.returnValue(
      aiCategories === 'error' ? throwError(() => new Error('API error')) : of(aiCategories),
    ),
  };
}

describe('AdminDashboardComponent', () => {
  let fixture: ComponentFixture<AdminDashboardComponent>;
  let component: AdminDashboardComponent;
  let adminApi: ReturnType<typeof makeAdminApi>;

  async function setup(
    students: StudentListItem[] = [STUDENT],
    stats: AdminStats = STATS,
    aiCategories: AiConfigCategoryItem[] | 'error' = AI_CATEGORIES_CONFIGURED,
  ) {
    adminApi = makeAdminApi(students, stats, aiCategories);
    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  // ── Existing tests (preserved) ─────────────────────────────────────────────

  it('renders the dashboard', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Dashboard');
  });

  it('calls listStudents, getStats, and listAiCategories on init', async () => {
    await setup();
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    expect(adminApi.getStats).toHaveBeenCalledTimes(1);
    expect(adminApi.listAiCategories).toHaveBeenCalledTimes(1);
  });

  it('renders total students stat card from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Total students');
    expect(fixture.nativeElement.textContent).toContain('5');
  });

  it('renders onboarded stat card from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Onboarded');
    expect(fixture.nativeElement.textContent).toContain('3');
  });

  it('renders activities tracked stat card', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Activities tracked');
    expect(fixture.nativeElement.textContent).toContain('120');
  });

  it('renders quick action cards', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Add student');
    expect(fixture.nativeElement.textContent).toContain('Manage students');
    expect(fixture.nativeElement.textContent).toContain('AI Config');
    expect(fixture.nativeElement.textContent).toContain('Prompts');
  });

  it('renders recent students table', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('alice@example.com');
  });

  it('renders onboarding badge using shared label', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('Completed');
  });

  it('renders not-started badge for unboarded student', async () => {
    await setup([STUDENT_NO_CEFR]);
    expect(fixture.nativeElement.textContent).toContain('Not started');
  });

  it('renders CEFR badge when level present', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('B2');
  });

  it('renders em-dash when CEFR absent', async () => {
    await setup([STUDENT_NO_CEFR]);
    expect(fixture.nativeElement.textContent).toContain('—');
  });

  it('shows empty state when no students', async () => {
    await setup([]);
    expect(fixture.nativeElement.textContent).toContain('No students yet');
  });

  it('renders only up to 5 students in preview', async () => {
    const many = Array.from({ length: 8 }, (_, i) => ({
      ...STUDENT,
      userId: `user-${i}`,
      studentProfileId: `sp-${i}`,
      email: `student${i}@example.com`,
    }));
    await setup(many);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(5);
  });

  // AI provider stat card — live status
  it('shows "Configured" label when all categories have a provider', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_CONFIGURED);
    expect(fixture.nativeElement.textContent).toContain('AI provider');
    expect(fixture.nativeElement.textContent).toContain('Configured');
  });

  it('shows partial label when only some categories configured', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_PARTIAL);
    expect(fixture.nativeElement.textContent).toContain('1/2 configured');
  });

  it('shows "Not configured" label when no categories have a provider', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_NONE_CONFIGURED);
    expect(fixture.nativeElement.textContent).toContain('Not configured');
  });

  it('shows "Not configured" label when categories list is empty', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_EMPTY);
    expect(fixture.nativeElement.textContent).toContain('Not configured');
  });

  it('shows "Unknown" label when AI config API errors', async () => {
    await setup([STUDENT], STATS, 'error');
    expect(fixture.nativeElement.textContent).toContain('Unknown');
  });

  // AI System card — live categories
  it('renders AI System section with live category names', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_CONFIGURED);
    expect(fixture.nativeElement.textContent).toContain('AI System');
    expect(fixture.nativeElement.textContent).toContain('Writing activities');
    expect(fixture.nativeElement.textContent).toContain('Text to speech');
    expect(fixture.nativeElement.textContent).toContain('OpenAI');
  });

  it('shows "Not configured" badge for unconfigured category', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_PARTIAL);
    expect(fixture.nativeElement.textContent).toContain('Not configured');
  });

  it('shows "Action needed" when AI System has no configured categories', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_EMPTY);
    expect(fixture.nativeElement.textContent).toContain('Action needed');
  });

  it('shows "Unavailable" in AI System when API errors', async () => {
    await setup([STUDENT], STATS, 'error');
    expect(fixture.nativeElement.textContent).toContain('Unavailable');
  });

  it('renders link to /admin/ai-config', async () => {
    await setup();
    const link = fixture.nativeElement.querySelector('a[href="/admin/ai-config"]');
    expect(link).toBeTruthy();
  });

  it('does not display any API key or secret value', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_CONFIGURED);
    const text: string = fixture.nativeElement.textContent;
    expect(text).not.toContain('sk-');
    expect(text).not.toContain('apiKey');
    expect(text).not.toContain('API_KEY');
  });

  // ── New tests — REDESIGN-1 ──────────────────────────────────────────────────

  describe('weekly-snapshot hero banner', () => {
    it('renders the hero banner section', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Weekly snapshot');
    });

    it('shows onboarded count in hero from real stats', async () => {
      await setup([], STATS);
      expect(fixture.nativeElement.textContent).toContain('Students onboarded');
      expect(fixture.nativeElement.textContent).toContain('3');
    });

    it('shows total students count in hero from real stats', async () => {
      await setup([], STATS);
      expect(fixture.nativeElement.textContent).toContain('Total students');
      expect(fixture.nativeElement.textContent).toContain('5');
    });

    it('shows "Not implemented" for activities this week slot', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Activities this week');
      expect(fixture.nativeElement.textContent).toContain('Not implemented');
    });

    it('shows "Backend not available yet" for avg score slot', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Avg score');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });
  });

  describe('KPI icon tile row', () => {
    it('renders AI cost tile with Not implemented', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('AI cost');
      expect(fixture.nativeElement.textContent).toContain('Not implemented');
    });
  });

  describe('activity trends chart placeholder', () => {
    it('renders activity chart card with placeholder text', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Activity trends');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });
  });

  describe('onboarding funnel card', () => {
    it('renders onboarding funnel section', async () => {
      await setup([STUDENT, STUDENT_NO_CEFR, STUDENT_IN_PROGRESS]);
      expect(fixture.nativeElement.textContent).toContain('Onboarding funnel');
    });

    it('shows not-started count derived from students', async () => {
      await setup([STUDENT_NO_CEFR], STATS);
      expect(component.onboardingCounts().notStarted).toBe(1);
    });

    it('shows in-progress count derived from students', async () => {
      await setup([STUDENT_IN_PROGRESS], STATS);
      expect(component.onboardingCounts().inProgress).toBe(1);
    });

    it('shows total students from stats in funnel', async () => {
      await setup([], STATS);
      const text: string = fixture.nativeElement.textContent;
      expect(text).toContain('Onboarding funnel');
      expect(text).toContain('5');
    });
  });

  describe('at-risk students card', () => {
    it('renders at-risk card section', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('At-risk students');
    });

    it('shows empty state when no at-risk students', async () => {
      await setup([STUDENT]);
      expect(component.atRiskStudents().length).toBe(0);
    });

    it('identifies not-started students as at-risk', async () => {
      await setup([STUDENT_NO_CEFR]);
      expect(component.atRiskStudents().length).toBe(1);
      expect(component.atRiskStudents()[0].email).toBe('bob@example.com');
    });

    it('identifies in-progress students as at-risk', async () => {
      await setup([STUDENT_IN_PROGRESS]);
      expect(component.atRiskStudents().length).toBe(1);
    });

    it('shows note about aggregate risk score not available', async () => {
      await setup([STUDENT]);
      expect(fixture.nativeElement.textContent).toContain('backend not available yet');
    });
  });

  describe('CEFR distribution card', () => {
    it('renders CEFR distribution section', async () => {
      await setup([STUDENT]);
      expect(fixture.nativeElement.textContent).toContain('CEFR distribution');
    });

    it('derives CEFR counts from students list', async () => {
      await setup([STUDENT, STUDENT_IN_PROGRESS, STUDENT_PLACEMENT]);
      const dist = component.cefrDistribution();
      const b2 = dist.find(d => d.level === 'B2');
      const a2 = dist.find(d => d.level === 'A2');
      const b1 = dist.find(d => d.level === 'B1');
      expect(b2?.count).toBe(1);
      expect(a2?.count).toBe(1);
      expect(b1?.count).toBe(1);
    });

    it('excludes students with no CEFR level', async () => {
      await setup([STUDENT_NO_CEFR]);
      expect(component.cefrDistribution().length).toBe(0);
    });

    it('shows empty state when no CEFR data', async () => {
      await setup([STUDENT_NO_CEFR]);
      expect(fixture.nativeElement.textContent).toContain('No CEFR data yet');
    });
  });

  describe('placeholder cards', () => {
    it('renders score distribution card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Score distribution');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });

    it('renders AI spend by type card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('AI spend by type');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });

    it('renders avg session duration card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Avg session duration');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });

    it('renders streak leaderboard card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Streak leaderboard');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });

    it('renders live events feed card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Live events feed');
      expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
    });
  });

  describe('cohort engagement card', () => {
    it('renders cohort engagement section', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Cohort engagement');
    });

    it('derives course-ready count from students', async () => {
      await setup([STUDENT, STUDENT_PLACEMENT]);
      expect(component.lifecycleCounts().courseReady).toBe(1);
    });

    it('derives placement-pending count from students', async () => {
      await setup([STUDENT_PLACEMENT]);
      expect(component.lifecycleCounts().placementPending).toBe(1);
    });

    it('derives onboarding-pending count from students', async () => {
      await setup([STUDENT_IN_PROGRESS]);
      expect(component.lifecycleCounts().onboardingPending).toBe(1);
    });

    it('shows note about activity-based rate not available', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Activity-based engagement rate');
    });
  });

  describe('no fake data', () => {
    it('does not show fake production data', async () => {
      await setup([], { totalStudents: 0, onboardedStudents: 0, totalActivityAttempts: 0 });
      const text: string = fixture.nativeElement.textContent;
      expect(text).not.toContain('$');
      expect(text).not.toContain('All clear');
    });
  });
});
