import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, NEVER } from 'rxjs';
import { ActivityFeedbackPageComponent } from './activity-feedback-page.component';
import { ActivityService } from '../../../../core/services/activity.service';
import { ActivityFeedbackDto, WritingEvaluationDto } from '../../../../core/models/activity.models';

const baseFeedback: ActivityFeedbackDto = {
  attemptId: 'att-1',
  score: 75,
  coachSummary: 'Good work.',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: ['Clear structure.'],
  mainMistakes: [],
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  clarityIssues: [],
  grammarExplanation: null,
  toneExplanation: null,
  vocabularyToRemember: [],
  miniLesson: null,
  nextImprovementStep: null,
  rewriteChallenge: null,
  nextPracticeSuggestion: null,
  feedbackInSourceLanguage: null,
  questionFeedback: null,
  transcript: null,
  responseFeedback: null,
  speakingStrengths: null,
  speakingImprovements: null,
  missingExpectedPoints: null,
  suggestedImprovedResponse: null,
  patternEvaluation: null,
  feedbackPolicy: null,
};

function makeWritingEval(overrides: Partial<WritingEvaluationDto> = {}): WritingEvaluationDto {
  return {
    attemptId: 'att-1',
    status: 'Completed',
    feedbackText: 'Good attempt.',
    suggestedImprovement: null,
    correctedText: null,
    overallScore: 0.8,
    grammarScore: 0.9,
    vocabularyScore: null,
    coherenceScore: null,
    taskCompletionScore: null,
    completedAtUtc: '2026-07-01T10:00:00Z',
    failureReason: null,
    providerName: 'OpenAI',
    modelName: 'gpt-4o',
    ...overrides,
  };
}

describe('ActivityFeedbackPageComponent', () => {
  let activityService: jasmine.SpyObj<ActivityService>;

  beforeEach(() => {
    activityService = jasmine.createSpyObj('ActivityService', [
      'getAttemptEvaluation', 'getWritingEvaluation',
    ]);
    activityService.getAttemptEvaluation.and.returnValue(NEVER);
    activityService.getWritingEvaluation.and.returnValue(NEVER);

    TestBed.configureTestingModule({
      imports: [ActivityFeedbackPageComponent],
      providers: [{ provide: ActivityService, useValue: activityService }],
    });
  });

  function create(overrides: {
    feedback?: ActivityFeedbackDto;
    activityType?: string | null;
    activityId?: string | null;
    attemptId?: string | null;
    previousScore?: number | null;
    attemptCount?: number;
  } = {}) {
    const fixture = TestBed.createComponent(ActivityFeedbackPageComponent);
    const comp = fixture.componentInstance;
    comp.feedback = overrides.feedback ?? baseFeedback;
    comp.activityType = (overrides.activityType ?? 'writingScenario') as any;
    comp.activityId = overrides.activityId ?? 'act-1';
    comp.attemptId = overrides.attemptId ?? 'att-1';
    comp.previousScore = overrides.previousScore ?? null;
    comp.attemptCount = overrides.attemptCount ?? 1;
    comp.ngOnChanges();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the feedback page container', () => {
    const { nativeElement } = create();
    expect(nativeElement.querySelector('[data-testid="feedback-page"]')).toBeTruthy();
  });

  it('shows score ring with correct label', () => {
    const { nativeElement } = create();
    expect(nativeElement.textContent).toContain('75');
  });

  it('shows coachSummary text', () => {
    const { nativeElement } = create();
    expect(nativeElement.textContent).toContain('Good work.');
  });

  it('shows whatYouDidWell list items', () => {
    const { nativeElement } = create();
    expect(nativeElement.textContent).toContain('Clear structure.');
  });

  it('loads writing evaluation when activityType is writingScenario', () => {
    create({ activityType: 'writingScenario' });
    expect(activityService.getWritingEvaluation).toHaveBeenCalledWith('act-1', 'att-1');
  });

  it('does not load writing evaluation for speakingRolePlay', () => {
    create({ activityType: 'speakingRolePlay' });
    expect(activityService.getWritingEvaluation).not.toHaveBeenCalled();
  });

  it('shows writing eval component for writingScenario', () => {
    const { nativeElement } = create({ activityType: 'writingScenario' });
    expect(nativeElement.querySelector('[data-testid="writing-eval"]')).toBeTruthy();
  });

  it('renders completed writing eval scores', fakeAsync(() => {
    activityService.getWritingEvaluation.and.returnValue(of(makeWritingEval()));
    const fixture = create({ activityType: 'writingScenario' });
    tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="writing-eval-scores"]')).toBeTruthy();
  }));

  it('renders nextPracticeSuggestion when set', () => {
    const feedback = { ...baseFeedback, nextPracticeSuggestion: 'Practice more subjunctives.' };
    const { nativeElement } = create({ feedback });
    expect(nativeElement.querySelector('[data-testid="next-practice-suggestion"]')).toBeTruthy();
    expect(nativeElement.textContent).toContain('Practice more subjunctives.');
  });

  it('does not render nextPracticeSuggestion when null', () => {
    const { nativeElement } = create();
    expect(nativeElement.querySelector('[data-testid="next-practice-suggestion"]')).toBeNull();
  });

  it('renders context-aware next steps', () => {
    const { nativeElement } = create({ activityType: 'writingScenario' });
    expect(nativeElement.querySelector('[data-testid="feedback-next-steps"]')).toBeTruthy();
  });

  it('shows Improve button for writingScenario', () => {
    const { nativeElement } = create({ activityType: 'writingScenario' });
    expect(nativeElement.querySelector('[data-testid="btn-improve"]')).toBeTruthy();
  });

  it('hides Improve button for speakingRolePlay', () => {
    const { nativeElement } = create({ activityType: 'speakingRolePlay' });
    expect(nativeElement.querySelector('[data-testid="btn-improve"]')).toBeNull();
  });

  it('emits improveAnswer when Improve button clicked', () => {
    const fixture = create({ activityType: 'writingScenario' });
    let emitted = false;
    fixture.componentInstance.improveAnswer.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-improve"]').click();
    expect(emitted).toBeTrue();
  });

  it('emits nextActivity when Next activity button clicked', () => {
    const fixture = create();
    let emitted = false;
    fixture.componentInstance.nextActivity.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-next-activity"]').click();
    expect(emitted).toBeTrue();
  });

  it('shows support lang toggle when feedbackInSourceLanguage is set', () => {
    const feedback = { ...baseFeedback, feedbackInSourceLanguage: 'توضیح فارسی' };
    const { nativeElement } = create({ feedback });
    expect(nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]')).toBeTruthy();
  });

  it('hides support lang when feedbackInSourceLanguage is null', () => {
    const { nativeElement } = create();
    expect(nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]')).toBeNull();
  });

  it('support lang toggle label is generic — not Persian-specific', () => {
    const feedback = { ...baseFeedback, feedbackInSourceLanguage: 'Some text' };
    const { nativeElement } = create({ feedback });
    const btn: HTMLElement = nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]');
    expect(btn.textContent?.toLowerCase()).not.toContain('persian');
    expect(btn.textContent?.toLowerCase()).not.toContain('فارسی');
  });

  it('hasFeedbackContent returns true when coachSummary is set', () => {
    const fixture = create();
    expect(fixture.componentInstance.hasFeedbackContent).toBeTrue();
  });

  it('isWritingActivity returns true for writingScenario', () => {
    const fixture = create({ activityType: 'writingScenario' });
    expect(fixture.componentInstance.isWritingActivity).toBeTrue();
  });

  it('isSpeakingActivity returns true for speakingRolePlay', () => {
    const fixture = create({ activityType: 'speakingRolePlay' });
    expect(fixture.componentInstance.isSpeakingActivity).toBeTrue();
  });

  it('scoreImprovementMessage shows improvement when score increased', () => {
    const feedback = { ...baseFeedback, score: 80 };
    const fixture = create({ feedback, previousScore: 70 });
    expect(fixture.componentInstance.scoreImprovementMessage()).toContain('+10');
  });

  it('scoreImprovementMessage returns empty string when scores unavailable', () => {
    const fixture = create({ previousScore: null });
    expect(fixture.componentInstance.scoreImprovementMessage()).toBe('');
  });
});
