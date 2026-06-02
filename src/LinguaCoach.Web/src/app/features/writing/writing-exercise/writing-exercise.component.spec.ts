import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { WritingExerciseComponent } from './writing-exercise.component';
import { WritingService } from '../../../core/services/writing.service';
import { WritingExerciseDto, WritingFeedbackDto } from '../../../core/models/writing.models';

describe('WritingExerciseComponent', () => {
  let writingService: jasmine.SpyObj<WritingService>;
  let router: jasmine.SpyObj<Router>;

  const exercise: WritingExerciseDto = {
    scenarioTitle: 'Follow-up email for a pending document approval',
    scenarioDescription: 'Ask a project manager to review a document.',
    instructionInSourceLanguage: 'Write a professional follow-up email.',
    targetPhrases: ['could you please review'],
    targetVocabulary: ['pending approval'],
  };

  beforeEach(() => {
    writingService = jasmine.createSpyObj('WritingService', ['getExercise', 'submitDraft']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    writingService.getExercise.and.returnValue(of(exercise));

    TestBed.configureTestingModule({
      imports: [WritingExerciseComponent],
      providers: [
        { provide: WritingService, useValue: writingService },
        { provide: Router, useValue: router },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(WritingExerciseComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('displays controlled AI unavailable message from the API', fakeAsync(() => {
    const message = 'AI feedback is not configured or is temporarily unavailable.';
    writingService.submitDraft.and.returnValue(throwError(() => ({
      error: {
        code: 'ai_unavailable',
        error: message,
      },
    })));

    const fixture = create();
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
    };
    writingService.submitDraft.and.returnValue(of(feedback));

    const fixture = create();
    const component = fixture.componentInstance;
    component.draftText = 'Please review this document.';

    component.onSubmit();
    tick();
    fixture.detectChanges();

    expect(component.state()).toBe('feedback');
    expect(fixture.nativeElement.textContent).toContain('Review your workplace message');
    expect(fixture.nativeElement.textContent).toContain('82');
  }));
});
