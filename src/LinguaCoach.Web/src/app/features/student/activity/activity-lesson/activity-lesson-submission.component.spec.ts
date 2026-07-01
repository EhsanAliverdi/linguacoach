import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError, Subject, NEVER } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivityLessonComponent } from './activity-lesson.component';
import { ActivityService } from '../../../../core/services/activity.service';
import { ActivityDto, ActivityFeedbackDto } from '../../../../core/models/activity.models';

function makeActivity(overrides: Partial<ActivityDto> = {}): ActivityDto {
  return {
    activityId: 'act-1',
    activityType: 'writingScenario',
    source: 'aiGenerated',
    title: 'Test activity',
    difficulty: 'B1',
    situation: 'You need to write an update.',
    learningGoal: 'Write clearly.',
    targetPhrases: [],
    targetVocabulary: [],
    exampleText: null,
    commonMistakeToAvoid: null,
    instructionInSourceLanguage: null,
    instructions: null,
    practiceMode: null,
    vocabItems: null,
    scenario: null,
    speakerRole: null,
    listenerRole: null,
    transcriptAvailableAfterSubmit: null,
    listeningQuestions: null,
    responseTask: null,
    audioAvailable: false,
    audioUrl: null,
    audioContentType: null,
    audioDurationSeconds: null,
    audioUnavailableMessage: null,
    audioStatus: null,
    speakingScenario: null,
    studentRole: null,
    speakingListenerRole: null,
    speakingGoal: null,
    speakingPrompt: null,
    expectedPoints: null,
    suggestedPhrases: null,
    maxDurationSeconds: null,
    interactionMode: 'freeTextEntry',
    exercisePatternKey: 'free_text_response',
    contentJson: JSON.stringify({ situation: 'Write a short update.', prompt: 'Write 2-3 sentences.' }),
    stageContent: null,
    ...overrides,
  };
}

const baseFeedback: ActivityFeedbackDto = {
  attemptId: 'attempt-1',
  score: 80,
  coachSummary: 'Well done.',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: ['Clear writing.'],
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
};

describe('ActivityLessonComponent — submission and feedback loop', () => {
  let activityService: jasmine.SpyObj<ActivityService>;
  let router: jasmine.SpyObj<Router>;

  function setupBed(activityId: string | null = 'act-1', returnTo: string | null = null) {
    const paramGet = (k: string) => {
      if (k === 'activityId') return activityId;
      if (k === 'returnTo') return returnTo;
      return null;
    };
    TestBed.configureTestingModule({
      imports: [ActivityLessonComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: { get: paramGet } },
            queryParamMap: of({ get: paramGet }),
          },
        },
        { provide: ActivityService, useValue: activityService },
        { provide: Router, useValue: router },
      ],
    });
  }

  beforeEach(() => {
    activityService = jasmine.createSpyObj('ActivityService', [
      'getById', 'getNext', 'submitAttempt', 'submitVocabAttempt', 'submitListeningAttempt',
      'getAudioBlobUrl', 'getAttemptEvaluation', 'getWritingEvaluation',
    ]);
    activityService.getAttemptEvaluation.and.returnValue(NEVER);
    activityService.getWritingEvaluation.and.returnValue(NEVER);
    router = jasmine.createSpyObj('Router', ['navigate', 'navigateByUrl']);
  });

  function create() {
    const fixture = TestBed.createComponent(ActivityLessonComponent);
    fixture.detectChanges();
    return fixture;
  }

  // --- State: transitions through submitting → feedback ---

  it('transitions to submitting state while awaiting backend response', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    // Use a Subject so the observable doesn't complete synchronously.
    // This lets us assert the intermediate 'submitting' state before feedback arrives.
    const feedbackSubject = new Subject<ActivityFeedbackDto>();
    activityService.submitAttempt.and.returnValue(feedbackSubject.asObservable());

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'My update text here.' });
    expect(fixture.componentInstance.state()).toBe('submitting');

    feedbackSubject.next(baseFeedback);
    feedbackSubject.complete();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('feedback');
  }));

  it('shows feedback after successful submit', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'My update text here.' });
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.feedback()).toEqual(baseFeedback);
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Well done.');
  }));

  it('increments attemptCount after each submission', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.attemptCount()).toBe(0);
    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'First attempt.' });
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.attemptCount()).toBe(1);
  }));

  // --- Error handling ---

  it('shows error message and returns to writing state on backend error', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(
      throwError(() => new HttpErrorResponse({ error: { error: 'Invalid submission.' }, status: 400 })),
    );

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.detectChanges();
    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'Bad data.' });
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('writing');
    expect(fixture.componentInstance.errorMessage()).toContain('Invalid submission.');
  }));

  it('shows generic error when backend returns no message', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'Text.' });
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toBeTruthy();
    expect(fixture.componentInstance.state()).toBe('writing');
  }));

  // --- Payload shapes: multipleChoiceSingle ---

  it('serializes multipleChoiceSingle payload to { selectedOptionId }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'multipleChoice' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'multipleChoiceSingle', selectedOptionId: 'A' });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ selectedOptionId: 'A' }),
    );
  }));

  // --- Payload shapes: multipleChoiceMulti ---

  it('serializes multipleChoiceMulti payload to { selectedOptionIds }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'multipleChoiceMulti' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'multipleChoiceMulti', selectedOptionIds: ['A', 'B'] });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ selectedOptionIds: ['A', 'B'] }),
    );
  }));

  // --- Payload shapes: gapFill ---

  it('serializes gapFill payload to { answers: { gapId: value } }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'gapFill' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({
      kind: 'gapFill',
      answers: [{ gapId: 'gap_1', value: 'apologise' }, { gapId: 'gap_2', value: 'confirm' }],
    });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ answers: { gap_1: 'apologise', gap_2: 'confirm' } }),
    );
  }));

  // --- Payload shapes: reorderParagraphs ---

  it('serializes reorderParagraphs payload to { orderedIds }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'reorderParagraphs' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'reorderParagraphs', orderedIds: ['p2', 'p1', 'p3'] });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ orderedIds: ['p2', 'p1', 'p3'] }),
    );
  }));

  // --- Payload shapes: writeFromDictation ---

  it('serializes writeFromDictation payload to { items }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'writeFromDictation' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({
      kind: 'writeFromDictation',
      items: [{ itemId: 'item1', submittedText: 'Please submit the report by Friday.' }],
    });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ items: [{ itemId: 'item1', submittedText: 'Please submit the report by Friday.' }] }),
    );
  }));

  // --- Payload shapes: summarizeSpokenText ---

  it('serializes summarizeSpokenText payload to { summaryText }', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity({ interactionMode: 'summarizeSpokenText' })));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'summarizeSpokenText', summaryText: 'Teams may work remotely two days per week.' });
    tick();

    expect(activityService.submitAttempt).toHaveBeenCalledWith(
      'act-1',
      JSON.stringify({ summaryText: 'Teams may work remotely two days per week.' }),
    );
  }));

  // --- Continue / next activity navigation ---

  it('navigates to returnTo URL when nextActivity is called in lesson context', fakeAsync(() => {
    setupBed('act-1', '/lesson/sess-1');
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.nextActivity();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/lesson/sess-1');
  }));

  it('reloads the next activity when nextActivity is called without returnTo', fakeAsync(() => {
    setupBed('act-1', null);
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.nextActivity();
    tick();
    fixture.detectChanges();

    // With no returnTo, resets and calls getById again (since activityId is still set).
    expect(activityService.getById.calls.count()).toBeGreaterThan(1);
  }));

  // --- improveAnswer returns to writing state ---

  it('returns to writing state when improveAnswer is called', fakeAsync(() => {
    setupBed();
    activityService.getById.and.returnValue(of(makeActivity()));
    activityService.submitAttempt.and.returnValue(of(baseFeedback));

    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.onRendererSubmit({ kind: 'freeText', text: 'First attempt.' });
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('feedback');

    fixture.componentInstance.improveAnswer();
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('writing');
  }));
});
