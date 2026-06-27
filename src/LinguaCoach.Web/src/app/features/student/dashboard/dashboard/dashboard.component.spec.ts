import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { DashboardComponent } from './dashboard.component';
import { DashboardSummaryService } from '../../../../core/services/dashboard-summary.service';
import { PlacementService } from '../../../../core/services/placement.service';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';
import {
  StudentDashboardSummary,
  DashboardSummaryTodaySession,
} from '../../../../core/models/dashboard-summary.models';
import { TodaysSessionResponse } from '../../../../core/models/session.models';

// ── Test fixtures ──────────────────────────────────────────────────────────────

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

const BASE_SUMMARY: StudentDashboardSummary = {
  profile: { displayName: 'Test User', cefrLevel: 'B1', supportLanguage: null },
  courseReadiness: {
    isLearningReady: true,
    lifecycleStatus: 'CourseReady',
    placementRequired: false,
    learningPlanExists: true,
  },
  todaySession: {
    status: 'Preparing',
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

function sessionToSection(
  session: TodaysSessionResponse | null | 'error'
): DashboardSummaryTodaySession {
  if (!session || session === 'error') {
    return {
      status: 'Preparing',
      sessionId: null, title: null, topic: null,
      sessionGoal: null, focusSkill: null,
      durationMinutes: null, exerciseCount: null,
      actionLabel: "Start today's lesson",
    };
  }
  const status =
    session.status === 'completed' ? 'Completed'
    : session.status === 'inProgress' ? 'InProgress'
    : 'Ready';
  const label =
    status === 'Completed' ? "Review today's lesson"
    : status === 'InProgress' ? 'Resume lesson'
    : "Start today's lesson";
  return {
    status,
    sessionId: session.sessionId,
    title: session.title,
    topic: session.topic,
    sessionGoal: session.sessionGoal,
    focusSkill: session.focusSkill,
    durationMinutes: session.durationMinutes,
    exerciseCount: session.exercises.length,
    actionLabel: label,
  };
}

function makeServices(overrides: {
  dashboard?: unknown | null;
  session?: TodaysSessionResponse | null | 'error';
  suggestions?: object | 'error';
} = {}) {
  const todaySession = sessionToSection(overrides.session ?? null);

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
    todaySession,
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

  return {
    summaryService,
    placementService: { getResult: () => throwError(() => ({})) },
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
