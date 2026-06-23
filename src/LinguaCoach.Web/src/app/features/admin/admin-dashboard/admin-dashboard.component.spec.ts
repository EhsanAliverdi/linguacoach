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
});
