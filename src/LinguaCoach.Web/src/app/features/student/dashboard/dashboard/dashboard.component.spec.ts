import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../../../core/services/dashboard.service';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { PlacementService } from '../../../../core/services/placement.service';
import { SessionService } from '../../../../core/services/session.service';
import { PracticeGymSuggestionsService } from '../../../../core/services/practice-gym-suggestions.service';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';
import { DashboardResponse } from '../../../../core/models/dashboard.models';
import { TodaysSessionResponse } from '../../../../core/models/session.models';

const BASE_DASHBOARD: DashboardResponse = {
  studentName: 'Test User',
  careerProfile: 'Business Professional',
  cefrLevel: 'B1',
  message: 'Welcome!',
  lifecycleStage: 'CourseReady',
  learningPath: {
    pathId: 'path-1',
    title: 'Business English',
    modulesCompleted: 1,
    totalModules: 5,
    currentModule: {
      moduleId: 'mod-1',
      title: 'Meetings and Presentations',
      description: 'Learn to lead effective meetings.',
      order: 2,
      completedActivities: 3,
      totalActivities: 8,
      isCurrent: true,
      isCompleted: false,
      isReadyToComplete: false,
      averageScore: 72,
      latestScore: 75,
      focusSkill: 'speaking',
      reason: null,
      difficulty: 'intermediate',
    },
  },
  activityStats: { activitiesCompleted: 12, latestScore: 75, averageScore: 72 },
  currentFocus: null,
  nextRecommendedPractice: null,
  latestImprovement: null,
  streakDays: 3,
};

const TODAY_SESSION: TodaysSessionResponse = {
  sessionId: 'sess-1',
  title: 'Confident Meetings',
  topic: 'Business meetings',
  sessionGoal: 'Lead a team meeting',
  durationMinutes: 20,
  focusSkill: 'speaking',
  status: 'notStarted',
  isResuming: false,
  exercises: [],
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

function makeServices(overrides: {
  dashboard?: Partial<DashboardResponse> | null;
  session?: TodaysSessionResponse | null | 'error';
  suggestions?: object | 'error';
}) {
  const dashData = overrides.dashboard === null ? null : { ...BASE_DASHBOARD, ...overrides.dashboard };
  const dashboardService = { getDashboard: () => dashData ? of(dashData) : throwError(() => ({ error: { error: 'fail' } })) };

  const sessionData = overrides.session;
  const sessionService = {
    getToday: () =>
      sessionData === 'error' ? throwError(() => ({ status: 404 }))
      : sessionData === null ? throwError(() => ({ status: 404 }))
      : of(sessionData),
  };

  const suggestionsData = overrides.suggestions ?? EMPTY_SUGGESTIONS;
  const practiceGymService = {
    getSuggestions: () =>
      suggestionsData === 'error' ? throwError(() => ({})) : of(suggestionsData),
  };

  return {
    dashboardService,
    sessionService,
    practiceGymService,
    learningPathService: { getLearningMemory: () => of({ journeySummary: null, strongSkills: [], weakSkills: [], recurringMistakes: [], nextRecommendedFocus: [], coveredScenarioCount: 0, skillProfile: [] }) },
    placementService: { getResult: () => throwError(() => ({})) },
    authNotice: { consume: () => null },
  };
}

async function setup(overrides: Parameters<typeof makeServices>[0] = {}) {
  const svc = makeServices(overrides);
  await TestBed.configureTestingModule({
    imports: [DashboardComponent, RouterTestingModule],
    providers: [
      { provide: DashboardService, useValue: svc.dashboardService },
      { provide: SessionService, useValue: svc.sessionService },
      { provide: PracticeGymSuggestionsService, useValue: svc.practiceGymService },
      { provide: LearningPathService, useValue: svc.learningPathService },
      { provide: PlacementService, useValue: svc.placementService },
      { provide: AuthNoticeService, useValue: svc.authNotice },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('DashboardComponent', () => {

  it('loads dashboard after CourseReady', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-page"]')).toBeTruthy();
  });

  it('session 404 shows preparing state, not global error', async () => {
    const fixture = await setup({ session: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.sp-alert-error')).toBeNull();
    const lessonCard = el.querySelector('[data-testid="dashboard-todays-lesson"]');
    expect(lessonCard).toBeTruthy();
    expect(lessonCard!.textContent).toContain('being prepared');
  });

  it('session 404 does not replace dashboard with error state', async () => {
    const fixture = await setup({ session: 'error' });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="today-page"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="stat-streak"]')).toBeTruthy();
  });

  it('renders lesson card with real session data', async () => {
    const fixture = await setup({ session: TODAY_SESSION });
    const el: HTMLElement = fixture.nativeElement;
    const card = el.querySelector('[data-testid="dashboard-todays-lesson"]');
    expect(card).toBeTruthy();
    expect(card!.textContent).toContain('Confident Meetings');
  });

  it('lesson card shows Not started badge', async () => {
    const fixture = await setup({ session: TODAY_SESSION });
    const el: HTMLElement = fixture.nativeElement;
    const badge = el.querySelector('[data-testid="session-status-badge"]');
    expect(badge?.textContent?.trim()).toBe('Not started');
  });

  it('lesson card shows Completed badge', async () => {
    const fixture = await setup({ session: { ...TODAY_SESSION, status: 'completed' as const } });
    const el: HTMLElement = fixture.nativeElement;
    const badge = el.querySelector('[data-testid="session-status-badge"]');
    expect(badge?.textContent?.trim()).toBe('Completed');
  });

  it('lesson card shows In progress badge', async () => {
    const fixture = await setup({ session: { ...TODAY_SESSION, status: 'inProgress' as const } });
    const el: HTMLElement = fixture.nativeElement;
    const badge = el.querySelector('[data-testid="session-status-badge"]');
    expect(badge?.textContent?.trim()).toBe('In progress');
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
    const fixture = await setup({ session: 'error', suggestions: 'error' });
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
