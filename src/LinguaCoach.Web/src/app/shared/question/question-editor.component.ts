import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ChoiceOption,
  FreeTextQuestion,
  GapFillQuestion,
  LeafQuestionContent,
  ListeningGroupQuestion,
  MultipleChoiceQuestion,
  QuestionContent,
  ReadingGroupQuestion,
  SingleChoiceQuestion,
} from './question-content.models';
import {
  SpAdminButtonComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../../design-system/admin';

export type EditableQuestionType =
  | 'single_choice' | 'multiple_choice' | 'gap_fill' | 'free_text' | 'reading_group' | 'listening_group';

const LEAF_TYPES: EditableQuestionType[] = ['single_choice', 'multiple_choice', 'gap_fill', 'free_text'];

const TYPE_LABELS: Record<EditableQuestionType, string> = {
  single_choice: 'Single choice',
  multiple_choice: 'Multiple choice',
  gap_fill: 'Gap fill',
  free_text: 'Free text',
  reading_group: 'Reading passage',
  listening_group: 'Listening audio',
};

/**
 * Shared, polymorphic question editor (Unified Question-Schema Phase 4/6) — one slide-over form
 * that conditionally renders fields by the selected type, with a repeatable sub-question editor
 * (recursively reusing itself) for the two group types. Used by both the placement admin page
 * and onboarding's admin page — callers restrict which types are offered via `availableTypes`
 * (placement: single_choice/gap_fill/reading_group/listening_group; onboarding: single_choice/
 * multiple_choice/free_text, since onboarding steps are profile capture, not passage/audio groups).
 *
 * Pure input/output, no local state: every edit constructs a new QuestionContent and emits it —
 * the caller owns the source of truth (its form model) and passes the updated value back down.
 * Correct-answer fields are optional throughout — a question with no correct answer (e.g. an
 * onboarding profile-capture question) is a valid, unscored state.
 */
@Component({
  selector: 'app-question-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminButtonComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
    QuestionEditorComponent,
  ],
  templateUrl: './question-editor.component.html',
})
export class QuestionEditorComponent {
  content = input.required<QuestionContent>();
  contentChange = output<QuestionContent>();

  /** Which types the type selector offers. Group sub-question editors are restricted to leaf
   * types (via the recursive nested instance) to prevent group-in-group nesting. */
  availableTypes = input<EditableQuestionType[]>(
    ['single_choice', 'multiple_choice', 'gap_fill', 'free_text', 'reading_group', 'listening_group']);
  showRemove = input<boolean>(false);
  removed = output<void>();

  readonly leafTypes: EditableQuestionType[] = LEAF_TYPES;

  typeSelectOptions(): { value: string; label: string }[] {
    return this.availableTypes().map(t => ({ value: t, label: TYPE_LABELS[t] }));
  }

  onTypeChange(newType: string): void {
    const c = this.content();
    const questionText = 'questionText' in c ? c.questionText : '';

    switch (newType as EditableQuestionType) {
      case 'single_choice':
        this.contentChange.emit({
          type: 'single_choice', id: c.id, questionText,
          choices: [{ key: 'A', label: '' }, { key: 'B', label: '' }],
          correctAnswerKey: 'A',
        } satisfies SingleChoiceQuestion);
        break;
      case 'multiple_choice':
        this.contentChange.emit({
          type: 'multiple_choice', id: c.id, questionText,
          choices: [{ key: 'A', label: '' }, { key: 'B', label: '' }],
        } satisfies MultipleChoiceQuestion);
        break;
      case 'gap_fill':
        this.contentChange.emit({ type: 'gap_fill', id: c.id, questionText } satisfies GapFillQuestion);
        break;
      case 'free_text':
        this.contentChange.emit({ type: 'free_text', id: c.id, questionText } satisfies FreeTextQuestion);
        break;
      case 'reading_group':
        this.contentChange.emit({
          type: 'reading_group', id: c.id, passage: '',
          questions: [{ type: 'single_choice', id: 'q1', questionText: '', choices: [{ key: 'A', label: '' }], correctAnswerKey: 'A' }],
        } satisfies ReadingGroupQuestion);
        break;
      case 'listening_group':
        this.contentChange.emit({
          type: 'listening_group', id: c.id, audioScript: '',
          questions: [{ type: 'single_choice', id: 'q1', questionText: '', choices: [{ key: 'A', label: '' }], correctAnswerKey: 'A' }],
        } satisfies ListeningGroupQuestion);
        break;
    }
  }

  updateQuestionText(value: string): void {
    const c = this.content();
    if ('questionText' in c) this.contentChange.emit({ ...c, questionText: value });
  }

  updateGapFillAnswer(value: string): void {
    const c = this.content() as GapFillQuestion;
    this.contentChange.emit({ ...c, correctAnswer: value || null });
  }

  updateFreeTextMaxLength(value: number | null): void {
    const c = this.content() as FreeTextQuestion;
    this.contentChange.emit({ ...c, maxLength: value });
  }

  updateChoiceLabel(index: number, label: string): void {
    const c = this.content() as SingleChoiceQuestion | MultipleChoiceQuestion;
    const choices = c.choices.map((ch, i) => (i === index ? { ...ch, label } : ch));
    this.contentChange.emit({ ...c, choices } as QuestionContent);
  }

  updateCorrectAnswerKey(key: string): void {
    const c = this.content() as SingleChoiceQuestion;
    this.contentChange.emit({ ...c, correctAnswerKey: key || null });
  }

  isMultiCorrectAnswer(key: string): boolean {
    const c = this.content() as MultipleChoiceQuestion;
    return (c.correctAnswerKeys ?? []).includes(key);
  }

  toggleMultiCorrectAnswer(key: string): void {
    const c = this.content() as MultipleChoiceQuestion;
    const current = c.correctAnswerKeys ?? [];
    const correctAnswerKeys = current.includes(key) ? current.filter(k => k !== key) : [...current, key];
    this.contentChange.emit({ ...c, correctAnswerKeys: correctAnswerKeys.length ? correctAnswerKeys : null });
  }

  addChoice(): void {
    const c = this.content() as SingleChoiceQuestion | MultipleChoiceQuestion;
    const nextKey = String.fromCharCode(65 + c.choices.length); // A, B, C, ...
    this.contentChange.emit({ ...c, choices: [...c.choices, { key: nextKey, label: '' }] } as QuestionContent);
  }

  removeChoice(index: number): void {
    const c = this.content() as SingleChoiceQuestion;
    if (c.choices.length <= 2) return; // keep at least 2 choices
    const choices = c.choices.filter((_, i) => i !== index);
    const correctAnswerKey = choices.some(ch => ch.key === c.correctAnswerKey) ? c.correctAnswerKey : choices[0]?.key;
    this.contentChange.emit({ ...c, choices, correctAnswerKey } as QuestionContent);
  }

  updatePassage(value: string): void {
    const c = this.content() as ReadingGroupQuestion;
    this.contentChange.emit({ ...c, passage: value });
  }

  updateAudioScript(value: string): void {
    const c = this.content() as ListeningGroupQuestion;
    this.contentChange.emit({ ...c, audioScript: value });
  }

  updateSubQuestion(index: number, updated: QuestionContent): void {
    const c = this.content() as ReadingGroupQuestion | ListeningGroupQuestion;
    const questions = c.questions.map((q, i) => (i === index ? (updated as LeafQuestionContent) : q));
    this.contentChange.emit({ ...c, questions } as QuestionContent);
  }

  addSubQuestion(): void {
    const c = this.content() as ReadingGroupQuestion | ListeningGroupQuestion;
    const nextId = `q${c.questions.length + 1}`;
    const newLeaf: LeafQuestionContent = {
      type: 'single_choice', id: nextId, questionText: '', choices: [{ key: 'A', label: '' }], correctAnswerKey: 'A',
    };
    this.contentChange.emit({ ...c, questions: [...c.questions, newLeaf] } as QuestionContent);
  }

  removeSubQuestion(index: number): void {
    const c = this.content() as ReadingGroupQuestion | ListeningGroupQuestion;
    if (c.questions.length <= 1) return; // keep at least 1 sub-question
    this.contentChange.emit({ ...c, questions: c.questions.filter((_, i) => i !== index) } as QuestionContent);
  }

  trackChoice(index: number, choice: ChoiceOption): string {
    return `${index}-${choice.key}`;
  }

  choiceOptions(choices: ChoiceOption[]): { value: string; label: string }[] {
    return [{ value: '', label: '(no correct answer)' }, ...choices.map(c => ({ value: c.key, label: `${c.key} — ${c.label || '(empty)'}` }))];
  }
}
