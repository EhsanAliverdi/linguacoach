import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ProgressComponent } from './progress.component';
import { ProgressService } from '../../../core/services/progress.service';
import { StudentProgressSummary } from '../../../core/models/student-progress-summary.models';

const emptyData: StudentProgressSummary = {
  learning: {
    currentCefrLevel: null,
    placementCompletedAt: null,
    currentLearningPhase: 'Onboarding',
    totalObjectives: 0,
    objectivesCompleted: 0,
    objectivesMastered: 0,
    objectivesInProgress: 0,
    objectivesRemaining: 0,
    completionPercentage: 0,
    currentObjectiveKey: null,
    currentObjectiveSkill: null,
    objectivesCompletedToday: 0,
  },
  skills: [],
  cefr: { startingCefrLevel: null, currentCefrLevel: null, cefrImproved: false, placementDate: null, note: null },
  mastery: { masteredObjectivesCount: 0, inProgressObjectivesCount: 0, reviewQueueCount: 0, weakSkillsCount: 0, weakSkillLabels: [] },
  recentActivity: [],
  focus: { recommendations: [], recurringMistakes: [], journeySummary: null },
};

const richData: StudentProgressSummary = {
  learning: {
    currentCefrLevel: 'B1',
    placementCompletedAt: '2026-05-01T00:00:00Z',
    currentLearningPhase: 'Active learning',
    totalObjectives: 20,
    objectivesCompleted: 5,
    objectivesMastered: 3,
    objectivesInProgress: 4,
    objectivesRemaining: 12,
    completionPercentage: 40,
    currentObjectiveKey: 'obj-1',
    currentObjectiveSkill: 'Writing',
    objectivesCompletedToday: 1,
  },
  skills: [
    { skillKey: 'grammar', skillLabel: 'Grammar', isWeak: false, scorePercent: 75 },
    { skillKey: 'vocabulary', skillLabel: 'Vocabulary', isWeak: true, scorePercent: 30 },
  ],
  cefr: {
    startingCefrLevel: 'A2',
    currentCefrLevel: 'B1',
    cefrImproved: true,
    placementDate: '2026-05-01T00:00:00Z',
    note: null,
  },
  mastery: {
    masteredObjectivesCount: 3,
    inProgressObjectivesCount: 4,
    reviewQueueCount: 2,
    weakSkillsCount: 1,
    weakSkillLabels: ['Vocabulary'],
  },
  recentActivity: [
    { eventType: 'LessonCompleted', description: 'Completed lesson 1', detail: 'Module A', occurredAt: '2026-06-20T10:00:00Z' },
    { eventType: 'PracticeCompleted', description: 'Practice session', detail: null, occurredAt: '2026-06-19T09:00:00Z' },
  ],
  focus: {
    recommendations: ['Practice formal writing', 'Review vocabulary'],
    recurringMistakes: ['Article usage'],
    journeySummary: 'You are making solid progress toward B1.',
  },
};

describe('ProgressComponent', () => {
  let progressService: jasmine.SpyObj<ProgressService>;

  beforeEach(() => {
    progressService = jasmine.createSpyObj('ProgressService', ['getProgressSummary']);

    TestBed.configureTestingModule({
      imports: [ProgressComponent, RouterModule.forRoot([])],
      providers: [
        { provide: ProgressService, useValue: progressService },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(ProgressComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows loading state before data resolves', () => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = TestBed.createComponent(ProgressComponent);
    expect(fixture.componentInstance.loading()).toBeTrue();
  });

  it('clears loading after data arrives', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.nativeElement.querySelector('[data-testid="progress-loading"]')).toBeNull();
  }));

  it('shows error state and retry button on API failure', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(
      throwError(() => ({ error: { error: 'Server error.' } })));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="progress-error"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="progress-retry"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Server error.');
  }));

  it('shows fallback message when error has no body', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(throwError(() => ({})));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Could not load your progress');
  }));

  it('renders learning summary heading and stat cards', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="learning-summary-heading"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="learning-summary"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="current-cefr"]').textContent.trim()).toBe('B1');
  }));

  it('shows learning plan progress bar when totalObjectives > 0', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="learning-plan-progress"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('40');  // completionPercentage
    expect(html).toContain('3 mastered');
  }));

  it('hides progress bar when no objectives', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="learning-plan-progress"]')).toBeNull();
  }));

  it('shows CEFR progress with start and current level', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('A2');
    expect(html).toContain('B1');
    expect(html).toContain('improved');
  }));

  it('shows CEFR placement prompt when no placement', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('placement assessment');
  }));

  it('shows skill progress bars for known skills', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="skill-progress"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Grammar');
    expect(html).toContain('Vocabulary');
    expect(html).toContain('needs work');
  }));

  it('shows skill-empty state when no skills', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="skill-progress-empty"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="skill-progress"]')).toBeNull();
  }));

  it('renders mastery summary stat grid', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="mastery-summary"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('mastered');
    expect(html).toContain('in progress');
    expect(html).toContain('need review');
  }));

  it('shows weak skill labels chip list', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="weak-skill-labels"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Vocabulary');
  }));

  it('hides weak skill labels when none present', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="weak-skill-labels"]')).toBeNull();
  }));

  it('shows focus recommendations section when data present', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="focus-recommendations"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('You are making solid progress toward B1.');
    expect(html).toContain('Practice formal writing');
    expect(html).toContain('Article usage');
  }));

  it('hides focus recommendations when none', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="focus-recommendations"]')).toBeNull();
  }));

  it('shows recent activity timeline', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="recent-activity"]')).toBeTruthy();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Completed lesson 1');
    expect(html).toContain('Practice session');
    expect(html).toContain('Module A');
  }));

  it('shows empty activity state when no events', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="recent-activity-empty"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="recent-activity"]')).toBeNull();
  }));

  it('does not render raw JSON anywhere', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).not.toContain('"skillKey"');
    expect(html).not.toContain('{"');
    expect(html).not.toContain('"isWeak"');
  }));

  it('hasFocus computed returns true when recommendations present', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(richData));
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.hasFocus()).toBeTrue();
  }));

  it('hasFocus computed returns false when focus is empty', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.hasFocus()).toBeFalse();
  }));

  it('eventColour returns correct colours', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(of(emptyData));
    const fixture = create();
    tick();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.eventColour('PlacementCompleted')).toBe('var(--sp-writing)');
    expect(comp.eventColour('LessonCompleted')).toBe('var(--sp-success)');
    expect(comp.eventColour('PracticeCompleted')).toBe('var(--sp-listening)');
    expect(comp.eventColour('ObjectiveMastered')).toBe('var(--sp-success)');
    expect(comp.eventColour('Unknown')).toBe('var(--sp-muted)');
  }));

  it('retry button re-fetches data', fakeAsync(() => {
    progressService.getProgressSummary.and.returnValue(
      throwError(() => ({ error: { error: 'Fail.' } })));
    const fixture = create();
    tick();
    fixture.detectChanges();

    progressService.getProgressSummary.and.returnValue(of(richData));
    const btn = fixture.nativeElement.querySelector('[data-testid="progress-retry"]');
    btn.click();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="progress-error"]')).toBeNull();
    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.data()).toBeTruthy();
  }));
});
