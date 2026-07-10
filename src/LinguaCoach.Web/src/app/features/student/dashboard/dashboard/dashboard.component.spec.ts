import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { DashboardComponent } from './dashboard.component';
import { DashboardSummaryService } from '../../../../core/services/dashboard-summary.service';
import { PlacementService } from '../../../../core/services/placement.service';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';
import { SessionService } from '../../../../core/services/session.service';
import { StudentDashboardSummary } from '../../../../core/models/dashboard-summary.models';
import { TodaysSessionResponse, DailyLessonModuleSection } from '../../../../core/models/session.models';

// ── Test fixtures ──────────────────────────────────────────────────────────────

const MODULE_SECTION: DailyLessonModuleSection = {
  selectedModules: [{
    moduleId: 'mod-1',
    title: 'Confident Meetings',
    description: 'Lead a team meeting',
    cefrLevel: 'B1',
    skill: 'speaking',
    subskill: null,
    difficultyBand: 2,
    estimatedMinutes: 20,
    reason: 'matched',
    linkedLessons: [],
    linkedExercises: [],
  }],
  fallbackRequired: false,
  fallbackReason: null,
  selectionReason: 'matched',
  targetCefrLevel: 'B1',
  totalEstimatedMinutes: 20,
  warnings: [],
};

const TODAY_AVAILABLE: TodaysSessionResponse = {
  available: true,
  moduleSection: MODULE_SECTION,
};

const TODAY_NOT_AVAILABLE: TodaysSessionResponse = {
  available: false,
  moduleSection: null,
};

const EMPTY_SUGGESTIONS = {
  suggestedItems: [],
  continueItems: [],
  reviewItems: [],
  readyCount: 0,
  reviewOnlyCount: 0,
  reservedCount: 0,
  isReplenishmentRecommended: false,
  generatedAtUtc: new Date().toISOString(),
};

const BASE_SUMMARY: StudentDashboardSummary = {
  profile: { displayName: 'Test User', cefrLevel: 'B1', supportLanguage: null },
  courseReadiness: {
    isLearningReady: true,
    lifecycleStatus: 'CourseReady',
    placementRequired: false,
    learningPlanExists: true,
  },
  todaySession: {
    status: 'NotAvailable',
    sessionId: null,
    title: null,
    topic: null,
    sessionGoal: null,
    focusSkill: null,
    durationMinutes: null,
    exerciseCount: null,
    actionLabel: "Start today's lesson",
  },
  learningPlan: {
    pathTitle: 'Business English',
    currentObjective: 'Meetings and Presentations',
    currentObjectiveDescription: 'Learn to lead effective meetings.',
    objectiveIndex: 2,
    totalObjectives: 5,
    modulesCompleted: 1,
    remainingObjectives: 3,
    completedActivities: 3,
    totalActivities: 8,
    progressPercent: 20,
  },
  practice: {
    status: 'Preparing',
    suggestedItem: null,
    reviewQueueCount: 0,
    weakestSkill: null,
  },
  progress: {
    skillProfile: [],
    strongSkills: [],
    weakSkills: [],
    nextRecommendedFocus: [],
    journeySummary: null,
    activitiesCompleted: 12,
    streakDays: 3,
  },
  quickStats: {
    currentCefr: 'B1',
    streakDays: 3,
    activitiesCompleted: 12,
    reviewQueueCount: 0,
  },
  warnings: {
    missingLearningPlan: false,
    missingTodaySession: false,
    practiceUnavailable: false,
    placementIncomplete: false,
  },
};

// ── Helpers ────────────────────────────────────────────────────────────────────

function makeServices(overrides: {
  dashboard?: unknown | null;
  today?: TodaysSessionResponse | 'error';
  suggestions?: object | 'error';
} = {}) {
  let practiceStatus: 'Ready' | 'Preparing' | 'NotAvailable' = 'Preparing';
  let reviewQueueCount = 0;

  if (overrides.suggestions === 'error') {
    practiceStatus = 'NotAvailable';
  } else if (overrides.suggestions && typeof overrides.suggestions === 'object') {
    const s = overrides.suggestions as any;
    reviewQueueCount = s.reviewItems?.length ?? 0;
    const readyCount = (s.suggestedItems?.length ?? 0) + (s.continueItems?.length ?? 0);
    practiceStatus = readyCount > 0 ? 'Ready' : 'Preparing';
  }

  const summary: StudentDashboardSummary = {
    ...BASE_SUMMARY,
    practice: { status: practiceStatus, suggestedItem: null, reviewQueueCount, weakestSkill: null },
    quickStats: { ...BASE_SUMMARY.quickStats, reviewQueueCount },
    warnings: { ...BASE_SUMMARY.warnings, practiceUnavailable: practiceStatus === 'NotAvailable' },
  };

  const summaryService = {
    getSummary: () =>
      overrides.dashboard === null
        ? throwError(() => ({ error: { error: 'fail' } }))
        : of(summary),
  };

  const sessionService = {
    getToday: () =>
      overrides.today === 'error'
        ? throwError(() => ({ error: { error: 'fail' } }))
        : of(overrides.today ?? TODAY_NOT_AVAILABLE),
  };

  return {
    summaryService,
    sessionService,
    placementService: { getAdaptiveCurrent: () => throwError(() => ({})) },
    authNotice: { consume: () => null },
  };
}

async function setup(overrides: Parameters<typeof makeServices>[0] = {}) {
  const svc = makeServices(overrides);
  await TestBed.configureTestingModule({
    imports: [DashboardComponent, RouterTestingModule],
    providers: [
      { provide: DashboardSummaryService, useValue: svc.summaryService },
      { provide: PlacementService, useValue: svc.placementService },
      { provide: AuthNoticeService, useValue: svc.authNotice },
      { provide: SessionService, useValue: svc.sessionService },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

// ── Tests ──────────────────────────────────────────────────────────────────────

describe('DashboardComponent', () => {

  it('loads dashboard after CourseReady', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-page"]')).toBeTruthy();
  });

  it('session error shows the nothing-available state, not a global error', async () => {
    const fixture = await setup({ today: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.sp-alert-error')).toBeNull();
    const card = el.querySelector('[data-testid="dashboard-todays-lesson"]');
    expect(card).toBeTruthy();
    expect(el.querySelector('[data-testid="today-not-available"]')).toBeTruthy();
  });

  it('session error does not replace dashboard with a global error state', async () => {
    const fixture = await setup({ today: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-page"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="stat-streak"]')).toBeTruthy();
  });

  it('renders the module card with real module data when available', async () => {
    const fixture = await setup({ today: TODAY_AVAILABLE });
    const el: HTMLElement = fixture.nativeElement;
    const card = el.querySelector('[data-testid="daily-lesson-module-card"]');
    expect(card).toBeTruthy();
    expect(card!.textContent).toContain('Confident Meetings');
  });

  it('shows the nothing-available state when Today has no module', async () => {
    const fixture = await setup({ today: TODAY_NOT_AVAILABLE });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-not-available"]')).toBeTruthy();
  });

  it('placement summary renders CEFR level', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const stat = el.querySelector('[data-testid="stat-cefr"]');
    expect(stat?.textContent).toContain('B1');
  });

  it('learning plan card renders real data', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const card = el.querySelector('[data-testid="today-learning-path"]');
    expect(card).toBeTruthy();
    expect(card!.textContent).toContain('Meetings and Presentations');
    expect(card!.textContent).toContain('Objective 2 of 5');
  });

  it('practice card shows preparing when suggestions empty', async () => {
    const fixture = await setup({ suggestions: EMPTY_SUGGESTIONS });
    const el: HTMLElement = fixture.nativeElement;
    const practiceCard = el.querySelector('[data-testid="dashboard-practice-card"]');
    expect(practiceCard).toBeTruthy();
    const empty = el.querySelector('[data-testid="practice-empty"]');
    expect(empty?.textContent).toContain('being prepared');
  });

  it('practice card shows review queue when items exist', async () => {
    const fixture = await setup({
      suggestions: {
        ...EMPTY_SUGGESTIONS,
        reviewItems: [{ title: 'Review item 1' }],
      },
    });
    const el: HTMLElement = fixture.nativeElement;
    const review = el.querySelector('[data-testid="practice-review"]');
    expect(review?.textContent).toContain('1');
  });

  it('practice card shows preparing when suggestions API fails', async () => {
    const fixture = await setup({ suggestions: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    const preparing = el.querySelector('[data-testid="practice-preparing"]');
    expect(preparing?.textContent).toContain('being prepared');
  });

  it('dashboard failure does not affect other widgets when session errors', async () => {
    const fixture = await setup({ today: 'error', suggestions: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-learning-path"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="stat-streak"]')).toBeTruthy();
    expect(el.querySelector('.sp-alert-error')).toBeNull();
  });

  it('global error only shows when dashboard API fails', async () => {
    const fixture = await setup({ dashboard: null });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.sp-alert-error')).toBeTruthy();
    expect(el.querySelector('[data-testid="today-page"]')).toBeNull();
  });

});
