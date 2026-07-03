import { TestBed } from '@angular/core/testing';
import { QuestionEditorComponent } from './question-editor.component';
import { QuestionContent, ReadingGroupQuestion, SingleChoiceQuestion } from './question-content.models';

describe('QuestionEditorComponent', () => {
  function setup(content: QuestionContent) {
    TestBed.configureTestingModule({ imports: [QuestionEditorComponent] });
    const fixture = TestBed.createComponent(QuestionEditorComponent);
    fixture.componentRef.setInput('content', content);
    fixture.detectChanges();
    return fixture;
  }

  it('emits an updated single_choice question when text changes', () => {
    const fixture = setup({
      type: 'single_choice', id: 'q1', questionText: 'Old', choices: [{ key: 'A', label: 'a' }, { key: 'B', label: 'b' }], correctAnswerKey: 'A',
    });
    let emitted: QuestionContent | undefined;
    fixture.componentInstance.contentChange.subscribe(v => (emitted = v));

    fixture.componentInstance.updateQuestionText('New text');

    expect((emitted as SingleChoiceQuestion).questionText).toBe('New text');
  });

  it('addChoice appends a new choice with the next letter key', () => {
    const fixture = setup({
      type: 'single_choice', id: 'q1', questionText: 'Q', choices: [{ key: 'A', label: 'a' }, { key: 'B', label: 'b' }], correctAnswerKey: 'A',
    });
    let emitted: SingleChoiceQuestion | undefined;
    fixture.componentInstance.contentChange.subscribe(v => (emitted = v as SingleChoiceQuestion));

    fixture.componentInstance.addChoice();

    expect(emitted!.choices.length).toBe(3);
    expect(emitted!.choices[2].key).toBe('C');
  });

  it('removeChoice refuses to go below 2 choices', () => {
    const fixture = setup({
      type: 'single_choice', id: 'q1', questionText: 'Q', choices: [{ key: 'A', label: 'a' }, { key: 'B', label: 'b' }], correctAnswerKey: 'A',
    });
    const spy = jasmine.createSpy();
    fixture.componentInstance.contentChange.subscribe(spy);

    fixture.componentInstance.removeChoice(0);

    expect(spy).not.toHaveBeenCalled();
  });

  it('onTypeChange to gap_fill produces a GapFillQuestion preserving questionText', () => {
    const fixture = setup({
      type: 'single_choice', id: 'q1', questionText: 'Keep me', choices: [{ key: 'A', label: 'a' }], correctAnswerKey: 'A',
    });
    let emitted: QuestionContent | undefined;
    fixture.componentInstance.contentChange.subscribe(v => (emitted = v));

    fixture.componentInstance.onTypeChange('gap_fill');

    expect(emitted!.type).toBe('gap_fill');
    expect((emitted as any).questionText).toBe('Keep me');
  });

  it('addSubQuestion appends a new sub-question to a reading_group', () => {
    const content: ReadingGroupQuestion = {
      type: 'reading_group', id: 'g1', passage: 'Text',
      questions: [{ type: 'gap_fill', id: 'q1', questionText: 'Q1', correctAnswer: 'x' }],
    };
    const fixture = setup(content);
    let emitted: ReadingGroupQuestion | undefined;
    fixture.componentInstance.contentChange.subscribe(v => (emitted = v as ReadingGroupQuestion));

    fixture.componentInstance.addSubQuestion();

    expect(emitted!.questions.length).toBe(2);
  });

  it('removeSubQuestion refuses to go below 1 sub-question', () => {
    const content: ReadingGroupQuestion = {
      type: 'reading_group', id: 'g1', passage: 'Text',
      questions: [{ type: 'gap_fill', id: 'q1', questionText: 'Q1', correctAnswer: 'x' }],
    };
    const fixture = setup(content);
    const spy = jasmine.createSpy();
    fixture.componentInstance.contentChange.subscribe(spy);

    fixture.componentInstance.removeSubQuestion(0);

    expect(spy).not.toHaveBeenCalled();
  });

  it('renders a nested app-question-editor for each reading_group sub-question', () => {
    const content: ReadingGroupQuestion = {
      type: 'reading_group', id: 'g1', passage: 'Text',
      questions: [
        { type: 'gap_fill', id: 'q1', questionText: 'Q1', correctAnswer: 'x' },
        { type: 'gap_fill', id: 'q2', questionText: 'Q2', correctAnswer: 'y' },
      ],
    };
    const fixture = setup(content);
    const nested = fixture.nativeElement.querySelectorAll('app-question-editor');
    expect(nested.length).toBe(2);
  });
});
