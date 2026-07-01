import { TestBed } from '@angular/core/testing';
import { FeedbackWritingEvalComponent } from './feedback-writing-eval.component';
import { WritingEvaluationDto } from '../../../../core/models/activity.models';

function makeEval(overrides: Partial<WritingEvaluationDto> = {}): WritingEvaluationDto {
  return {
    attemptId: 'a1',
    status: 'Completed',
    feedbackText: 'Good work overall.',
    suggestedImprovement: 'Try varying sentence length.',
    correctedText: 'The corrected version.',
    overallScore: 0.82,
    grammarScore: 0.9,
    vocabularyScore: 0.75,
    coherenceScore: 0.8,
    taskCompletionScore: 0.85,
    completedAtUtc: '2026-07-01T10:00:00Z',
    failureReason: null,
    providerName: 'OpenAI',
    modelName: 'gpt-4o',
    ...overrides,
  };
}

describe('FeedbackWritingEvalComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackWritingEvalComponent] });
  });

  function create(eval_: WritingEvaluationDto | null, loading = false) {
    const fixture = TestBed.createComponent(FeedbackWritingEvalComponent);
    fixture.componentInstance.eval = eval_;
    fixture.componentInstance.loading = loading;
    fixture.detectChanges();
    return fixture;
  }

  it('shows loading indicator when loading and no eval', () => {
    const { nativeElement } = create(null, true);
    expect(nativeElement.querySelector('[data-testid="writing-eval-loading"]')).toBeTruthy();
  });

  it('shows nothing when eval is null and not loading', () => {
    const { nativeElement } = create(null, false);
    expect(nativeElement.querySelector('[data-testid="writing-eval"]')).toBeNull();
    expect(nativeElement.querySelector('[data-testid="writing-eval-loading"]')).toBeNull();
  });

  it('renders scores grid for Completed eval', () => {
    const { nativeElement } = create(makeEval());
    expect(nativeElement.querySelector('[data-testid="writing-eval-scores"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="score-overall"]')).toBeTruthy();
  });

  it('shows overall score as percentage', () => {
    const { nativeElement } = create(makeEval({ overallScore: 0.82 }));
    const el: HTMLElement = nativeElement.querySelector('[data-testid="score-overall"]');
    expect(el.textContent).toContain('82%');
  });

  it('shows grammar score', () => {
    const { nativeElement } = create(makeEval({ grammarScore: 0.9 }));
    expect(nativeElement.querySelector('[data-testid="score-grammar"]').textContent).toContain('90%');
  });

  it('hides grammar score when null', () => {
    const { nativeElement } = create(makeEval({ grammarScore: null }));
    expect(nativeElement.querySelector('[data-testid="score-grammar"]')).toBeNull();
  });

  it('shows feedbackText when present', () => {
    const { nativeElement } = create(makeEval({ feedbackText: 'Great effort.' }));
    expect(nativeElement.querySelector('[data-testid="writing-eval-feedback"]').textContent).toContain('Great effort.');
  });

  it('shows corrected text when present', () => {
    const { nativeElement } = create(makeEval({ correctedText: 'Better version here.' }));
    expect(nativeElement.querySelector('[data-testid="writing-eval-corrected"]').textContent).toContain('Better version here.');
  });

  it('shows suggested improvement when present', () => {
    const { nativeElement } = create(makeEval({ suggestedImprovement: 'Try being more concise.' }));
    expect(nativeElement.querySelector('[data-testid="writing-eval-suggestion"]').textContent).toContain('Try being more concise.');
  });

  it('includes AI disclaimer for Completed eval', () => {
    const { nativeElement } = create(makeEval());
    expect(nativeElement.querySelector('[data-testid="ai-disclaimer"]')).toBeTruthy();
  });

  it('shows pending state for Pending status', () => {
    const { nativeElement } = create(makeEval({ status: 'Pending' }));
    expect(nativeElement.querySelector('[data-testid="writing-eval"]')).toBeNull();
    expect(nativeElement.querySelector('[data-testid="feedback-pending-state"]')).toBeTruthy();
  });

  it('shows pending state for Failed status', () => {
    const { nativeElement } = create(makeEval({ status: 'Failed' }));
    expect(nativeElement.querySelector('[data-testid="feedback-pending-state"]')).toBeTruthy();
  });

  it('scorePercent returns -- for null', () => {
    const fixture = create(null);
    expect(fixture.componentInstance.scorePercent(null)).toBe('--');
  });

  it('scorePercent rounds to nearest percent', () => {
    const fixture = create(null);
    expect(fixture.componentInstance.scorePercent(0.756)).toBe('76%');
  });
});
