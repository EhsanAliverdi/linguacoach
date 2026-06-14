import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ProgressComponent } from './progress.component';
import { ProgressService } from '../../core/services/progress.service';
import { ProgressSummary } from '../../core/models/progress.models';
import { VocabularyService } from '../../core/services/vocabulary.service';

const emptyProgress: ProgressSummary = {
  summary: {
    activitiesCompleted: 0,
    totalAttempts: 0,
    retryAttempts: 0,
    averageScore: null,
    latestScore: null,
    bestScore: null,
    activitiesThisWeek: 0,
    modulesCompleted: 0,
    currentModuleProgress: null,
  },
  scoreTrend: [],
  skillProgress: { skills: [], topStrengths: [], weakestSkills: [] },
  learningFocus: null,
  moduleProgress: [],
};

const dataProgress: ProgressSummary = {
  summary: {
    activitiesCompleted: 5,
    totalAttempts: 8,
    retryAttempts: 3,
    averageScore: 76,
    latestScore: 82,
    bestScore: 91,
    activitiesThisWeek: 2,
    modulesCompleted: 1,
    currentModuleProgress: {
      moduleId: 'mod-1',
      title: 'Workplace Emails',
      completedActivities: 2,
      totalRequired: 3,
      averageScore: 76,
      latestScore: 82,
      isReadyToComplete: false,
    },
  },
  scoreTrend: [
    { attemptDate: '2026-06-07T10:00:00Z', score: 82, activityTitle: 'Polite request message', moduleTitle: 'Workplace Emails', attemptNumber: 2 },
    { attemptDate: '2026-06-06T09:00:00Z', score: 74, activityTitle: 'Delay explanation email', moduleTitle: 'Workplace Emails', attemptNumber: 1 },
  ],
  skillProgress: {
    skills: [
      { skillKey: 'grammar_accuracy', skillLabel: 'Grammar accuracy', isWeak: false, scorePercent: 65 },
      { skillKey: 'formal_tone', skillLabel: 'Formal tone', isWeak: true, scorePercent: 35 },
    ],
    topStrengths: ['Grammar accuracy'],
    weakestSkills: ['Formal tone'],
  },
  learningFocus: {
    journeySummary: 'You are making solid progress.',
    nextRecommendedFocus: ['Practise formal tone in requests'],
    recurringMistakes: ['Overly casual greetings'],
    weakSkills: ['Formal tone'],
    strongSkills: ['Grammar accuracy'],
  },
  moduleProgress: [
    { moduleId: 'mod-1', title: 'Workplace Emails', status: 'current', completedActivities: 2, totalRequired: 3, averageScore: 76, latestScore: 82, isReadyToComplete: false, completedAt: null },
    { moduleId: 'mod-2', title: 'Meeting Communication', status: 'upcoming', completedActivities: 0, totalRequired: 3, averageScore: null, latestScore: null, isReadyToComplete: false, completedAt: null },
  ],
};

describe('ProgressComponent', () => {
  let progressService: jasmine.SpyObj<ProgressService>;
  let vocabularyService: jasmine.SpyObj<VocabularyService>;

  beforeEach(() => {
    progressService = jasmine.createSpyObj('ProgressService', ['getProgress']);
    vocabularyService = jasmine.createSpyObj('VocabularyService', ['getVocabulary']);
    vocabularyService.getVocabulary.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [ProgressComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        { provide: ProgressService, useValue: progressService },
        { provide: VocabularyService, useValue: vocabularyService },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(ProgressComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows loading state initially', () => {
    progressService.getProgress.and.returnValue(of(emptyProgress));
    const fixture = TestBed.createComponent(ProgressComponent);
    // Before detectChanges: loading should be true
    expect(fixture.componentInstance.loading()).toBeTrue();
  });

  it('shows empty state when no attempts', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(emptyProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.isEmpty()).toBeTrue();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Your progress will appear here after you complete your first activity.');
    expect(html).toContain('Start practising');
  }));

  it('shows summary cards with real data', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.isEmpty()).toBeFalse();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('5');   // activitiesCompleted
    expect(html).toContain('76');  // averageScore
    expect(html).toContain('82');  // latestScore
    expect(html).toContain('2');   // activitiesThisWeek
    expect(html).toContain('1');   // modulesCompleted
    expect(html).toContain('3');   // retryAttempts
  }));

  it('shows score trend', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Polite request message');
    expect(html).toContain('Delay explanation email');
    expect(html).toContain('Attempt 2');
  }));

  it('shows skill strengths and areas to improve', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Grammar accuracy');
    expect(html).toContain('Formal tone');
    expect(html).toContain('Strengths');
    expect(html).toContain('Areas to improve');
  }));

  it('shows module progress with status labels', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Workplace Emails');
    expect(html).toContain('Meeting Communication');
    expect(html).toContain('In progress');
    expect(html).toContain('Upcoming');
  }));

  it('shows learning focus section', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('You are making solid progress.');
    expect(html).toContain('Practise formal tone in requests');
    expect(html).toContain('Overly casual greetings');
  }));

  it('shows friendly error message on API failure', fakeAsync(() => {
    progressService.getProgress.and.returnValue(throwError(() => ({ error: { error: 'Server error.' } })));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Server error.');
    expect(html).toContain('Try again');
  }));

  it('does not display raw JSON anywhere', fakeAsync(() => {
    progressService.getProgress.and.returnValue(of(dataProgress));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).not.toContain('"skillKey"');
    expect(html).not.toContain('"isWeak"');
    expect(html).not.toContain('{"');
    expect(html).not.toContain('"journeySummary"');
  }));

  it('hides sections cleanly when data is absent', fakeAsync(() => {
    const noFocusNoSkills: ProgressSummary = {
      ...dataProgress,
      skillProgress: { skills: [], topStrengths: [], weakestSkills: [] },
      learningFocus: null,
    };
    progressService.getProgress.and.returnValue(of(noFocusNoSkills));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).not.toContain('Your learning focus');
    expect(html).not.toContain('Skill progress');
  }));
});
