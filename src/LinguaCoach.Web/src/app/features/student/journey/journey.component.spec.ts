import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { JourneyComponent } from './journey.component';
import { JourneyService } from '../../../core/services/journey.service';
import { StudentJourney, JourneyObjective } from '../../../core/models/journey.models';

// ── Fixtures ───────────────────────────────────────────────────────────────────

const OBJECTIVE_CURRENT: JourneyObjective = {
  objectiveKey: 'obj-speaking-b1',
  title: 'Speaking in meetings',
  skill: 'speaking',
  cefrLevel: 'B1',
  status: 'Current',
  sequenceNumber: 1,
  isReview: false,
  isBlocked: false,
  blockedByKey: null,
  lastEvaluatedAt: null,
  isMastered: false,
};

const OBJECTIVE_UPCOMING: JourneyObjective = {
  objectiveKey: 'obj-listening-b1',
  title: 'Listening for gist',
  skill: 'listening',
  cefrLevel: 'B1',
  status: 'Upcoming',
  sequenceNumber: 2,
  isReview: false,
  isBlocked: false,
  blockedByKey: null,
  lastEvaluatedAt: null,
  isMastered: false,
};

const OBJECTIVE_COMPLETED: JourneyObjective = {
  objectiveKey: 'obj-vocab-a2',
  title: 'Core vocabulary',
  skill: 'vocabulary',
  cefrLevel: 'A2',
  status: 'Completed',
  sequenceNumber: 0,
  isReview: false,
  isBlocked: false,
  blockedByKey: null,
  lastEvaluatedAt: '2026-05-15T10:00:00Z',
  isMastered: false,
};

const OBJECTIVE_MASTERED: JourneyObjective = {
  ...OBJECTIVE_COMPLETED,
  objectiveKey: 'obj-vocab-a2-mastered',
  isMastered: true,
};

const OBJECTIVE_REVIEW: JourneyObjective = {
  objectiveKey: 'obj-reading-a2',
  title: 'Reading short texts',
  skill: 'reading',
  cefrLevel: 'A2',
  status: 'Review',
  sequenceNumber: 3,
  isReview: true,
  isBlocked: false,
  blockedByKey: null,
  lastEvaluatedAt: '2026-05-01T10:00:00Z',
  isMastered: false,
};

const OBJECTIVE_BLOCKED: JourneyObjective = {
  objectiveKey: 'obj-writing-b1',
  title: 'Report writing',
  skill: 'writing',
  cefrLevel: 'B1',
  status: 'Blocked',
  sequenceNumber: 4,
  isReview: false,
  isBlocked: true,
  blockedByKey: 'obj-grammar-b1',
  lastEvaluatedAt: null,
  isMastered: false,
};

const FULL_JOURNEY: StudentJourney = {
  currentCefrLevel: 'B1',
  currentLearningPhase: 'Consolidating',
  totalObjectives: 5,
  completionPercentage: 20,
  lastCompletedAt: '2026-05-15T10:00:00Z',
  currentObjective: OBJECTIVE_CURRENT,
  upcomingObjectives: [OBJECTIVE_UPCOMING, OBJECTIVE_BLOCKED],
  completedObjectives: [OBJECTIVE_COMPLETED],
  reviewObjectives: [OBJECTIVE_REVIEW],
  milestones: [
    { type: 'placement_completed', label: 'Placement completed', occurredAt: '2026-04-01T09:00:00Z' },
    { type: 'first_objective_completed', label: '1st objective done', occurredAt: '2026-05-15T10:00:00Z' },
  ],
  planStatus: 'Active',
};

const EMPTY_JOURNEY: StudentJourney = {
  currentCefrLevel: 'A2',
  currentLearningPhase: 'Preparing',
  totalObjectives: 0,
  completionPercentage: 0,
  lastCompletedAt: null,
  currentObjective: null,
  upcomingObjectives: [],
  completedObjectives: [],
  reviewObjectives: [],
  milestones: [],
  planStatus: 'None',
};

// ── Setup helper ───────────────────────────────────────────────────────────────

async function setup(journey: StudentJourney | 'error' = FULL_JOURNEY) {
  const journeyService = {
    getJourney: () =>
      journey === 'error'
        ? throwError(() => new Error('network'))
        : of(journey),
  };

  await TestBed.configureTestingModule({
    imports: [JourneyComponent, RouterTestingModule],
    providers: [{ provide: JourneyService, useValue: journeyService }],
  }).compileComponents();

  const fixture = TestBed.createComponent(JourneyComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

// ── Tests ──────────────────────────────────────────────────────────────────────

describe('JourneyComponent', () => {

  it('renders page heading when journey loaded', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Your roadmap');
  });

  it('shows CEFR level and phase from journey data', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const cefr = el.querySelector('[data-testid="journey-cefr"]');
    expect(cefr?.textContent?.trim()).toBe('B1');
    const phase = el.querySelector('[data-testid="journey-phase"]');
    expect(phase?.textContent?.trim()).toBe('Consolidating');
  });

  it('renders progress percentage', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const pct = el.querySelector('[data-testid="journey-progress-pct"]');
    expect(pct?.textContent).toContain('20');
  });

  it('shows current objective card with Continue Lesson button', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const card = el.querySelector('[data-testid="current-objective"]');
    expect(card).toBeTruthy();
    expect(card!.textContent).toContain('Speaking in meetings');
    const btn = el.querySelector('[data-testid="continue-lesson-btn"]');
    expect(btn).toBeTruthy();
    expect((btn as HTMLAnchorElement).getAttribute('href')).toContain('dashboard');
  });

  it('shows completed objectives section', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const section = el.querySelector('[data-testid="completed-objectives"]');
    expect(section).toBeTruthy();
    expect(section!.textContent).toContain('Core vocabulary');
  });

  it('shows review queue when review objectives exist', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const section = el.querySelector('[data-testid="review-objectives"]');
    expect(section).toBeTruthy();
    expect(section!.textContent).toContain('Reading short texts');
    const empty = el.querySelector('[data-testid="review-queue-empty"]');
    expect(empty).toBeNull();
  });

  it('shows "up to date" message when review queue is empty', async () => {
    const noReview: StudentJourney = { ...FULL_JOURNEY, reviewObjectives: [] };
    const fixture = await setup(noReview);
    const el: HTMLElement = fixture.nativeElement;
    const empty = el.querySelector('[data-testid="review-queue-empty"]');
    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain("You're up to date");
    const section = el.querySelector('[data-testid="review-objectives"]');
    expect(section).toBeNull();
  });

  it('renders milestone chips', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const milestones = el.querySelector('[data-testid="milestones"]');
    expect(milestones).toBeTruthy();
    expect(milestones!.textContent).toContain('Placement completed');
  });

  it('shows upcoming timeline', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const list = el.querySelector('[data-testid="timeline-list"]');
    expect(list).toBeTruthy();
    expect(list!.textContent).toContain('Listening for gist');
  });

  it('shows blocked indicator in timeline', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const list = el.querySelector('[data-testid="timeline-list"]');
    expect(list!.textContent).toContain('Blocked by');
  });

  it('shows empty state when plan status is None', async () => {
    const fixture = await setup(EMPTY_JOURNEY);
    const el: HTMLElement = fixture.nativeElement;
    const preparing = el.querySelector('[data-testid="journey-preparing"]');
    expect(preparing).toBeTruthy();
    expect(preparing!.textContent).toContain('learning plan is being prepared');
    const heading = el.querySelector('[data-testid="current-objective"]');
    expect(heading).toBeNull();
  });

  it('shows error state and retry button on service failure', async () => {
    const fixture = await setup('error');
    const el: HTMLElement = fixture.nativeElement;
    const err = el.querySelector('[data-testid="journey-error"]');
    expect(err).toBeTruthy();
    expect(err!.textContent).toContain('Could not load');
    const retry = el.querySelector('[data-testid="journey-retry"]');
    expect(retry).toBeTruthy();
  });

  it('retries load when retry button clicked', async () => {
    let callCount = 0;
    const journeyService = {
      getJourney: () => {
        callCount++;
        return callCount === 1
          ? throwError(() => new Error('fail'))
          : of(FULL_JOURNEY);
      },
    };
    await TestBed.configureTestingModule({
      imports: [JourneyComponent, RouterTestingModule],
      providers: [{ provide: JourneyService, useValue: journeyService }],
    }).compileComponents();

    const fixture = TestBed.createComponent(JourneyComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const retryBtn = fixture.nativeElement.querySelector('[data-testid="journey-retry"]');
    expect(retryBtn).toBeTruthy();
    retryBtn.click();

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="journey-error"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="journey-cefr"]')).toBeTruthy();
  });

  it('mastered badge appears on mastered objectives', async () => {
    const withMastered: StudentJourney = {
      ...FULL_JOURNEY,
      completedObjectives: [OBJECTIVE_MASTERED],
    };
    const fixture = await setup(withMastered);
    const el: HTMLElement = fixture.nativeElement;
    const section = el.querySelector('[data-testid="completed-objectives"]');
    expect(section!.textContent).toContain('Mastered');
  });

  it('hides current objective card when none set', async () => {
    const noCurrent: StudentJourney = {
      ...FULL_JOURNEY,
      currentObjective: null,
    };
    const fixture = await setup(noCurrent);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="current-objective"]')).toBeNull();
  });

  it('shows timeline empty message when no upcoming objectives', async () => {
    const noUpcoming: StudentJourney = {
      ...FULL_JOURNEY,
      upcomingObjectives: [],
    };
    const fixture = await setup(noUpcoming);
    const el: HTMLElement = fixture.nativeElement;
    const empty = el.querySelector('[data-testid="timeline-empty"]');
    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain('No upcoming objectives');
  });

});
