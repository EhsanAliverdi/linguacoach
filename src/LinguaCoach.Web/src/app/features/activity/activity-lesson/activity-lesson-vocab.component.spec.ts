import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { ActivityLessonComponent } from './activity-lesson.component';
import { ActivityService } from '../../../core/services/activity.service';
import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';

const vocabActivity: ActivityDto = {
  activityId: 'vocab-act-1',
  activityType: 'vocabularyPractice',
  source: 'aiGenerated',
  title: 'Practise polite requests',
  difficulty: 'B1',
  situation: null,
  learningGoal: null,
  targetPhrases: [],
  targetVocabulary: [],
  exampleText: null,
  commonMistakeToAvoid: null,
  instructionInSourceLanguage: null,
  instructions: 'Fill in the blank with the most professional phrase.',
  practiceMode: 'fill_blank',
  vocabItems: [
    {
      vocabularyItemId: 'item-1',
      term: 'could you please',
      prompt: '_____ send me the updated file?',
      hint: 'Use a polite request phrase.',
      explanation: 'A polite way to make a workplace request.',
    },
    {
      vocabularyItemId: 'item-2',
      term: 'at your earliest convenience',
      prompt: 'Please respond _____.',
      hint: 'Use a formal phrase meaning "as soon as possible".',
      explanation: 'A formal phrase used in professional emails.',
    },
  ],
  scenario: null,
  speakerRole: null,
  listenerRole: null,
  transcriptAvailableAfterSubmit: null,
  listeningQuestions: null,
  responseTask: null,
  audioAvailable: null,
  audioUrl: null,
  audioContentType: null,
  audioDurationSeconds: null,
  audioUnavailableMessage: null,
  speakingScenario: null,
  studentRole: null,
  speakingListenerRole: null,
  speakingGoal: null,
  speakingPrompt: null,
  expectedPoints: null,
  suggestedPhrases: null,
  maxDurationSeconds: null,
  interactionMode: null,
  exercisePatternKey: null,
  contentJson: null,
};

const vocabFeedback: ActivityFeedbackDto = {
  attemptId: 'attempt-1',
  score: 100,
  coachSummary: 'Perfect — you got both correct!',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: ['Correct use of could you please'],
  mainMistakes: [],
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  clarityIssues: [],
  grammarExplanation: null,
  toneExplanation: null,
  vocabularyToRemember: [],
  miniLesson: 'Use modal verbs to soften requests.',
  nextImprovementStep: 'Try using these phrases in your next writing activity.',
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

describe('ActivityLessonComponent — VocabularyPractice', () => {
  let activityService: jasmine.SpyObj<ActivityService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    activityService = jasmine.createSpyObj('ActivityService', ['getNext', 'submitAttempt', 'submitVocabAttempt']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      imports: [ActivityLessonComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } }, queryParamMap: of({ get: () => null }) } },
        { provide: ActivityService, useValue: activityService },
        { provide: Router, useValue: router },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(ActivityLessonComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders VocabularyPractice learning state with instructions and item list', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Practise polite requests');
    expect(html).toContain('Fill in the blank with the most professional phrase.');
    expect(html).toContain('could you please');
    expect(html).toContain('at your earliest convenience');
    expect(html).toContain('Start practice');
  }));

  it('transitions to practice state on Start practice click', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('_____ send me the updated file?');
    expect(html).toContain('Check answers');
  }));

  it('shows hint when toggle clicked', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.detectChanges();

    fixture.componentInstance.toggleHint('item-1');
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Use a polite request phrase.');
  }));

  it('submit button disabled until all answers filled', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    const submitBtn = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: any) => b.textContent.includes('Check answers')) as HTMLButtonElement;
    expect(submitBtn.disabled).toBeTrue();
    expect(html).toContain('Check answers');
  }));

  it('submit button enabled when all answers filled', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.componentInstance.vocabAnswers['item-1'] = 'could you please';
    fixture.componentInstance.vocabAnswers['item-2'] = 'at your earliest convenience';
    fixture.detectChanges();

    const submitBtn = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: any) => b.textContent.includes('Check answers')) as HTMLButtonElement;
    expect(submitBtn.disabled).toBeFalse();
  }));

  it('calls submitVocabAttempt on submit and shows feedback', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    activityService.submitVocabAttempt.and.returnValue(of(vocabFeedback));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.componentInstance.vocabAnswers['item-1'] = 'could you please';
    fixture.componentInstance.vocabAnswers['item-2'] = 'at your earliest convenience';
    fixture.detectChanges();

    fixture.componentInstance.onSubmitVocab();
    tick();
    fixture.detectChanges();

    expect(activityService.submitVocabAttempt).toHaveBeenCalled();
    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Perfect — you got both correct!');
  }));

  it('does not show raw JSON in VocabularyPractice feedback', fakeAsync(() => {
    activityService.getNext.and.returnValue(of(vocabActivity));
    activityService.submitVocabAttempt.and.returnValue(of(vocabFeedback));
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startPractice();
    fixture.componentInstance.vocabAnswers['item-1'] = 'could you please';
    fixture.componentInstance.vocabAnswers['item-2'] = 'at your earliest convenience';
    fixture.componentInstance.onSubmitVocab();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).not.toContain('"vocabularyItemId"');
    expect(html).not.toContain('{"');
  }));
});
