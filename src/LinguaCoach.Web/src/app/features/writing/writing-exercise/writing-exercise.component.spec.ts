import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { WritingExerciseComponent } from './writing-exercise.component';
import { WritingService } from '../../../core/services/writing.service';
import { WritingExerciseDto, WritingFeedbackDto } from '../../../core/models/writing.models';

const SCENARIO_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

const exercise: WritingExerciseDto = {
  scenarioTitle: 'Follow-up email for a pending document approval',
  scenarioDescription: 'Ask a project manager to review a document.',
  learningGoal: 'Learn how to follow up professionally.',
  instructionInSourceLanguage: 'Write a professional follow-up email.',
  targetPhrases: ['could you please review'],
  targetVocabulary: ['pending approval'],
  exampleText: 'Dear Mr. Smith,\n\nI hope you are well.',
  commonMistakeToAvoid: 'Avoid sounding rude.',
};

const feedback: WritingFeedbackDto = {
  submissionId: 'submission-id',
  overallScore: 82,
  correctedEmail: 'Dear Sam, could you please review the latest revision?',
  feedbackInSourceLanguage: 'This is clear and polite.',
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  suggestedPhrases: ['could you please review'],
  mistakesToTrack: [],
  whatYouDidWell: ['Good formal greeting'],
  mainMistakes: [],
  grammarExplanation: 'Always add a comma after the salutation.',
  toneExplanation: 'Your tone was professional.',
  vocabularyToRemember: ['at your earliest convenience'],
  rewriteChallenge: 'Rewrite the opening with a warmer greeting.',
  nextPracticeSuggestion: 'Try writing an apology email next.',
};

describe('WritingExerciseComponent', () => {
  let writingService: jasmine.SpyObj<WritingService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    writingService = jasmine.createSpyObj('WritingService', ['getExercise', 'submitDraft']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    writingService.getExercise.and.returnValue(of(exercise));

    TestBed.configureTestingModule({
      imports: [WritingExerciseComponent],
      providers: [
        { provide: WritingService, useValue: writingService },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => SCENARIO_ID } } },
        },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(WritingExerciseComponent);
    fixture.detectChanges();
    return fixture;
  }

  // ── Initial state ─────────────────────────────────────────────────────────

  it('loads exercise and shows learning section on init', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(writingService.getExercise).toHaveBeenCalledWith(SCENARIO_ID);
    expect(fixture.componentInstance.state()).toBe('learning');
    expect(fixture.nativeElement.textContent).toContain('Today you will learn');
  }));

  it('shows example text in learning section', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Dear Mr. Smith');
  }));

  it('shows common mistake warning in learning section', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Avoid sounding rude.');
  }));

  it('transitions to exercise state when Start writing is clicked', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.startWriting();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('exercise');
    expect(fixture.nativeElement.textContent).toContain('Write your email draft');
  }));

  // ── Submission ────────────────────────────────────────────────────────────

  it('displays controlled AI unavailable message from the API', fakeAsync(() => {
    const message = 'AI feedback is not configured or is temporarily unavailable.';
    writingService.submitDraft.and.returnValue(throwError(() => ({
      error: {
        code: 'ai_unavailable',
        error: message,
      },
    })));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.draftText = 'Dear manager, could you please review the latest revision?';
    component.onSubmit();
    tick();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe(message);
    expect(fixture.nativeElement.textContent).toContain(message);
    expect(fixture.nativeElement.textContent).not.toContain('Failed to get feedback');
  }));

  it('shows feedback when the submit service returns a feedback payload', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.draftText = 'Please review this document.';
    component.onSubmit();
    tick();
    fixture.detectChanges();

    expect(component.state()).toBe('feedback');
    expect(fixture.nativeElement.textContent).toContain('Review your workplace message');
    expect(fixture.nativeElement.textContent).toContain('82');
  }));

  // ── Teaching feedback cards ───────────────────────────────────────────────

  it('shows whatYouDidWell section in feedback', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();
    fixture.componentInstance.draftText = 'Good email.';
    fixture.componentInstance.onSubmit();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('What you did well');
    expect(fixture.nativeElement.textContent).toContain('Good formal greeting');
  }));

  it('shows grammar explanation card in feedback', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();
    fixture.componentInstance.draftText = 'Good email.';
    fixture.componentInstance.onSubmit();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Grammar lesson');
    expect(fixture.nativeElement.textContent).toContain('Always add a comma after the salutation.');
  }));

  it('shows rewrite challenge in feedback', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();
    fixture.componentInstance.draftText = 'Good email.';
    fixture.componentInstance.onSubmit();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Challenge');
    expect(fixture.nativeElement.textContent).toContain('Rewrite the opening with a warmer greeting.');
  }));

  it('shows next practice suggestion in feedback', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();
    fixture.componentInstance.draftText = 'Good email.';
    fixture.componentInstance.onSubmit();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Try writing an apology email next.');
  }));

  // ── Navigation ────────────────────────────────────────────────────────────

  it('navigates to /writing when tryAnotherScenario is called', () => {
    const fixture = create();
    fixture.componentInstance.tryAnotherScenario();
    expect(router.navigate).toHaveBeenCalledWith(['/writing']);
  });

  it('navigates to /writing when tryAgain is called after feedback and then going back', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.componentInstance.draftText = 'test';
    fixture.componentInstance.onSubmit();
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('feedback');

    fixture.componentInstance.tryAgain();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('exercise');
    expect(fixture.componentInstance.draftText).toBe('');
  }));

  it('calls submitDraft with scenarioId', fakeAsync(() => {
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.startWriting();
    fixture.detectChanges();

    fixture.componentInstance.draftText = 'My draft.';
    fixture.componentInstance.onSubmit();
    tick();

    expect(writingService.submitDraft).toHaveBeenCalledWith('My draft.', SCENARIO_ID);
  }));
});
