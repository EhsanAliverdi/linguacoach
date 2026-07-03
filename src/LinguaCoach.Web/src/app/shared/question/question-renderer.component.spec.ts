import { TestBed } from '@angular/core/testing';
import { QuestionRendererComponent } from './question-renderer.component';
import { QuestionContent } from './question-content.models';

describe('QuestionRendererComponent', () => {
  function setup(content: QuestionContent) {
    TestBed.configureTestingModule({ imports: [QuestionRendererComponent] });
    const fixture = TestBed.createComponent(QuestionRendererComponent);
    fixture.componentRef.setInput('content', content);
    fixture.detectChanges();
    return fixture;
  }

  it('renders single_choice question text and choices', () => {
    const fixture = setup({
      type: 'single_choice',
      id: 'q1',
      questionText: 'Pick one',
      choices: [{ key: 'A', label: 'First' }, { key: 'B', label: 'Second' }],
    });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="question-text"]')?.textContent).toContain('Pick one');
    expect(el.querySelectorAll('[data-testid^="question-choice-"]').length).toBe(2);
  });

  it('selecting a single_choice choice updates the answers model', () => {
    const fixture = setup({
      type: 'single_choice',
      id: 'q1',
      questionText: 'Pick one',
      choices: [{ key: 'A', label: 'First' }, { key: 'B', label: 'Second' }],
    });
    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="question-choice-B"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.answers()).toEqual([{ questionId: 'q1', values: ['B'] }]);
  });

  it('toggling multiple_choice choices adds and removes values', () => {
    const fixture = setup({
      type: 'multiple_choice',
      id: 'q1',
      questionText: 'Pick any',
      choices: [{ key: 'A', label: 'First' }, { key: 'B', label: 'Second' }],
    });
    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="question-choice-A"]') as HTMLButtonElement).click();
    (el.querySelector('[data-testid="question-choice-B"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.getValues('q1').sort()).toEqual(['A', 'B']);

    (el.querySelector('[data-testid="question-choice-A"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.getValues('q1')).toEqual(['B']);
  });

  it('renders a listening_group stimulus and recurses into its sub-questions', () => {
    const fixture = setup({
      type: 'listening_group',
      id: 'g1',
      audioScript: 'Turn left.',
      questions: [
        { type: 'single_choice', id: 'q1', questionText: 'Which way?', choices: [{ key: 'A', label: 'left' }] },
      ],
    });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="question-group-stimulus"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="question-choice-A"]')).toBeTruthy();
  });

  it('renders a reading_group passage and its sub-questions', () => {
    const fixture = setup({
      type: 'reading_group',
      id: 'g1',
      passage: 'The cat sat on the mat.',
      questions: [
        { type: 'gap_fill', id: 'q1', questionText: 'Where did the cat sit?' },
      ],
    });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="question-passage"]')?.textContent).toContain('The cat sat on the mat.');
    expect(el.querySelector('[data-testid="question-gap-fill-input"]')).toBeTruthy();
  });

  it('supports a reading_group with more than one sub-question', () => {
    const fixture = setup({
      type: 'reading_group',
      id: 'g1',
      passage: 'The cat sat on the mat.',
      questions: [
        { type: 'gap_fill', id: 'q1', questionText: 'Where did the cat sit?' },
        { type: 'single_choice', id: 'q2', questionText: 'What animal is it?', choices: [{ key: 'A', label: 'cat' }] },
      ],
    });
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('[data-testid="question-gap-fill-input"]').length).toBe(1);
    expect(el.querySelectorAll('[data-testid="question-choice-A"]').length).toBe(1);
  });
});
