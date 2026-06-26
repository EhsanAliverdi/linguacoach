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
  lifecycleStage: 'OnboardingInProgress',
  cefrLevel: 'A2',
};

const STUDENT_PLACEMENT: StudentListItem = {
  ...STUDENT,
  studentProfileId: 'sp-4',
  userId: 'user-4',
  email: 'dave@example.com',
  onboardingStatus: 'Completed',
  lifecycleStage: 'PlacementInProgress',
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

const EMPTY_ACTIVITY_TRENDS = { period: '30d', buckets: [] };
const EMPTY_SCORE_DIST = { period: '7d', totalScoredAttempts: 0, buckets: [], averageScore: null };
const EMPTY_AI_TRENDS = { period: '7d', buckets: [] };

const REAL_ACTIVITY_TRENDS = {
  period: '7d',
  buckets: [
    { date: '2026-06-18', activityCount: 3, completedCount: 2, failedCount: 1 },
    { date: '2026-06-19', activityCount: 5, completedCount: 4, failedCount: 0 },
  ],
};

const REAL_SCORE_DIST = {
  period: '7d',
  totalScoredAttempts: 10,
  buckets: [
    { label: '0–39', minScore: 0, maxScore: 39, count: 1 },
    { label: '40–59', minScore: 40, maxScore: 59, count: 2 },
    { label: '60–74', minScore: 60, maxScore: 74, count: 3 },
    { label: '75–89', minScore: 75, maxScore: 89, count: 3 },
    { label: '90–100', minScore: 90, maxScore: 100, count: 1 },
  ],
  averageScore: 67.5,
};

const REAL_AI_TRENDS = {
  period: '7d',
  buckets: [
    { date: '2026-06-18', requestCount: 10, successfulCalls: 9, failedCalls: 1, inputTokens: 500, outputTokens: 200, totalTokens: 700, cost: 0.012 },
    { date: '2026-06-19', requestCount: 5,  successfulCalls: 5, failedCalls: 0, inputTokens: 250, outputTokens: 100, totalTokens: 350, cost: 0.006 },
  ],
};

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
    getDashboardActivityTrends: jasmine.createSpy('getDashboardActivityTrends').and.returnValue(of(EMPTY_ACTIVITY_TRENDS)),
    getDashboardScoreDistribution: jasmine.createSpy('getDashboardScoreDistribution').and.returnValue(of(EMPTY_SCORE_DIST)),
    getAiUsageTrends: jasmine.createSpy('getAiUsageTrends').and.returnValue(of(EMPTY_AI_TRENDS)),
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

  it('renders total students KPI tile from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('TOTAL STUDENTS');
    expect(fixture.nativeElement.textContent).toContain('5');
  });

  it('renders active this week KPI tile from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('ACTIVE THIS WEEK');
    expect(fixture.nativeElement.textContent).toContain('3');
  });

  it('renders activities done KPI tile', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('ACTIVITIES DONE');
    expect(fixture.nativeElement.textContent).toContain('120');
  });

  it('renders admin actions section', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Admin actions');
    expect(fixture.nativeElement.textContent).toContain('System health');
  });

  it('renders recent students table with display name', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('Alice Smith');
  });

  it('renders active status label for completed+CEFR student', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('Active');
  });

  it('renders inactive status label for not-started student', async () => {
    await setup([STUDENT_NO_CEFR]);
    expect(fixture.nativeElement.textContent).toContain('Inactive');
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


  // Admin actions list — AI config status (surfaced as action card when unconfigured categories exist)
  it('shows no AI config action when all categories have a provider', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_CONFIGURED);
    expect(fixture.nativeElement.textContent).not.toContain('categories not set');
  });

  it('shows AI config action card when partial categories unconfigured', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_PARTIAL);
    expect(fixture.nativeElement.textContent).toContain('categories not set');
  });

  it('shows AI config action card when no categories configured', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_NONE_CONFIGURED);
    expect(fixture.nativeElement.textContent).toContain('categories not set');
  });

  it('shows no AI config action when categories list is empty', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_EMPTY);
    expect(fixture.nativeElement.textContent).not.toContain('categories not set');
  });

  it('shows no AI config action when AI config API errors', async () => {
    await setup([STUDENT], STATS, 'error');
    expect(fixture.nativeElement.textContent).not.toContain('categories not set');
  });

  // Admin actions — links to AI config page
  it('renders link to /admin/ai-config when categories partially unconfigured', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_PARTIAL);
    const link = fixture.nativeElement.querySelector('a[href="/admin/ai-config"]');
    expect(link).toBeTruthy();
  });

  it('no AI config action card when all categories configured', async () => {
    await setup([STUDENT], STATS, AI_CATEGORIES_CONFIGURED);
    expect(fixture.nativeElement.textContent).not.toContain('categories not set');
  });

  it('shows no AI config action when AI categories API errors', async () => {
    await setup([STUDENT], STATS, 'error');
    const text: string = fixture.nativeElement.textContent;
    expect(text).not.toContain('categories not set');
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
      expect(fixture.nativeElement.textContent).toContain('THIS WEEK');
    });

    it('shows THIS WEEK eyebrow and activity count', async () => {
      await setup([], STATS);
      expect(fixture.nativeElement.textContent).toContain('THIS WEEK');
    });

    it('shows ENGAGEMENT eyebrow and engagement %', async () => {
      await setup([], STATS);
      expect(fixture.nativeElement.textContent).toContain('ENGAGEMENT');
      expect(fixture.nativeElement.textContent).toContain('60%');
    });

    it('shows AVG SCORE eyebrow', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('AVG SCORE');
    });

    it('shows ACTION NEEDED eyebrow', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('ACTION NEEDED');
    });
  });

  describe('KPI icon tile row', () => {
    it('renders AI COST (7 DAYS) tile', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('AI COST (7 DAYS)');
    });

    it('renders ACTIVE THIS WEEK tile', async () => {
      await setup([], STATS);
      expect(fixture.nativeElement.textContent).toContain('ACTIVE THIS WEEK');
    });
  });

  describe('activity trends chart', () => {
    it('renders activities completed card', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Activities completed');
    });
  });

  describe('onboarding funnel card', () => {
    it('renders onboarding funnel section', async () => {
      await setup([STUDENT, STUDENT_NO_CEFR, STUDENT_IN_PROGRESS]);
      expect(fixture.nativeElement.textContent).toContain('Onboarding funnel');
    });

    it('funnel shows signed-up count matching students length', async () => {
      await setup([STUDENT_NO_CEFR], STATS);
      const stages = component.funnelStages();
      expect(stages[0].label).toBe('Signed up');
      expect(stages[0].count).toBe(1);
    });

    it('funnel in-progress students appear in not-onboarded stage', async () => {
      await setup([STUDENT_IN_PROGRESS], STATS);
      const stages = component.funnelStages();
      const onboarded = stages.find(s => s.label === 'Onboarded');
      expect(onboarded?.count).toBe(0);
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

    it('does not flag in-progress students as at-risk', async () => {
      await setup([STUDENT_IN_PROGRESS]);
      expect(component.atRiskStudents().length).toBe(0);
    });

    it('shows all-on-track message when no at-risk students', async () => {
      await setup([STUDENT]);
      expect(fixture.nativeElement.textContent).toContain('All students are engaged');
    });
  });

  describe('CEFR distribution card', () => {
    it('renders CEFR distribution section', async () => {
      await setup([STUDENT]);
      expect(fixture.nativeElement.textContent).toContain('CEFR distribution');
    });
  });

  describe('placeholder cards', () => {
    it('renders score distribution card', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Score distribution');
    });

    it('renders AI cost by type card', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('AI cost by type');
    });

    it('renders avg session card', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Avg session');
    });

    it('renders streak leaderboard card with placeholder', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Streak leaderboard');
    });

    it('renders live events card', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Live events');
    });
  });

  describe('cohort engagement card', () => {
    it('renders cohort engagement section', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Cohort engagement');
    });
  });

  describe('no fake data', () => {
    it('does not render Not implemented placeholder text', async () => {
      await setup([], { totalStudents: 0, onboardedStudents: 0, totalActivityAttempts: 0 });
      const text: string = fixture.nativeElement.textContent;
      expect(text).not.toContain('Not implemented');
    });
  });

  describe('hero KPI — activities this week', () => {
    it('calls getDashboardActivityTrends on init', async () => {
      await setup();
      expect(adminApi.getDashboardActivityTrends).toHaveBeenCalledTimes(1);
    });

    it('activityTrendTotal sums activityCount from real trend data', async () => {
      adminApi = makeAdminApi();
      adminApi.getDashboardActivityTrends.and.returnValue(of(REAL_ACTIVITY_TRENDS));
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
      expect(component.activityTrendTotal()).toBe(8); // 3 + 5
    });

    it('activityTrendTotal is 0 while loading', async () => {
      await setup();
      component.loadingActivityTrends.set(true);
      component.activityTrends.set(null);
      expect(component.activityTrendTotal()).toBe(0);
    });
  });

  describe('hero KPI — avg score', () => {
    it('calls getDashboardScoreDistribution on init', async () => {
      await setup();
      expect(adminApi.getDashboardScoreDistribution).toHaveBeenCalledTimes(1);
    });

    it('heroAvgScore returns averageScore from endpoint', async () => {
      adminApi = makeAdminApi();
      adminApi.getDashboardScoreDistribution.and.returnValue(of(REAL_SCORE_DIST));
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
      expect(component.heroAvgScore()).toBe(67.5);
    });

    it('heroAvgScore is null when no scored attempts', async () => {
      await setup();
      component.scoreDistribution.set({ period: '7d', totalScoredAttempts: 0, buckets: [], averageScore: null });
      expect(component.heroAvgScore()).toBeNull();
    });
  });

  describe('hero KPI — AI cost 7d', () => {
    it('calls getAiUsageTrends on init', async () => {
      await setup();
      expect(adminApi.getAiUsageTrends).toHaveBeenCalledTimes(1);
    });

    it('heroAiCost7d sums cost from AI usage trend buckets', async () => {
      adminApi = makeAdminApi();
      adminApi.getAiUsageTrends.and.returnValue(of(REAL_AI_TRENDS));
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
      expect(component.heroAiCost7d()).toBeCloseTo(0.018, 5);
    });

    it('heroAiCost7d is null while loading', async () => {
      await setup();
      component.loadingAiUsageTrends7d.set(true);
      component.aiUsageTrends7d.set(null);
      expect(component.heroAiCost7d()).toBeNull();
    });

    it('heroAiCost7d is 0 when no AI usage data', async () => {
      await setup();
      component.aiUsageTrends7d.set({ period: '7d', buckets: [] });
      component.loadingAiUsageTrends7d.set(false);
      expect(component.heroAiCost7d()).toBe(0);
    });
  });
});
