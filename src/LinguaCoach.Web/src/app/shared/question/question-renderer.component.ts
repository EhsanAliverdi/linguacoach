import { Component, input, model } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChoiceOption, QuestionAnswerItem, QuestionContent } from './question-content.models';

/**
 * Shared, polymorphic student-facing question renderer — used by both placement and
 * onboarding. Switches on QuestionContent.type; group types (listening/reading) render
 * their stimulus (audio player / passage box) then recurse into this same component for
 * each sub-question, so "one question" and "N questions under one passage" need no
 * special-casing by callers.
 */
@Component({
  selector: 'app-question-renderer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './question-renderer.component.html',
})
export class QuestionRendererComponent {
  content = input.required<QuestionContent>();
  answers = model<QuestionAnswerItem[]>([]);
  /** Resolved audio blob URL for a listening group's stimulus — fetching it is the caller's
   * concern (requires assessment/item context this generic component shouldn't own). */
  audioUrl = input<string | null>(null);
  audioLoading = input<boolean>(false);
  disabled = input<boolean>(false);

  getValues(questionId: string): string[] {
    return this.answers().find(a => a.questionId === questionId)?.values ?? [];
  }

  getSingleValue(questionId: string): string {
    return this.getValues(questionId)[0] ?? '';
  }

  setSingleValue(questionId: string, value: string): void {
    const next = this.answers().filter(a => a.questionId !== questionId);
    next.push({ questionId, values: [value] });
    this.answers.set(next);
  }

  toggleMultiValue(questionId: string, choiceKey: string): void {
    const current = this.getValues(questionId);
    const values = current.includes(choiceKey)
      ? current.filter(v => v !== choiceKey)
      : [...current, choiceKey];
    const next = this.answers().filter(a => a.questionId !== questionId);
    next.push({ questionId, values });
    this.answers.set(next);
  }

  isSelected(questionId: string, choiceKey: string): boolean {
    return this.getValues(questionId).includes(choiceKey);
  }

  trackChoice(_: number, choice: ChoiceOption): string {
    return choice.key;
  }

  // ── Yes/No + dropdown UX for single_choice questions sourced from "languages" ──────────
  // (e.g. onboarding's "support in another language?" step) — an empty or "none" answer
  // means No; any other selected key means Yes with that language chosen.

  isLanguageSupportYes(questionId: string): boolean {
    const value = this.getSingleValue(questionId);
    return !!value && value !== 'none';
  }

  nonNoneChoices(choices: ChoiceOption[]): ChoiceOption[] {
    return choices.filter(c => c.key !== 'none');
  }

  selectNoLanguageSupport(questionId: string): void {
    this.setSingleValue(questionId, 'none');
  }

  selectYesLanguageSupport(questionId: string, choices: ChoiceOption[]): void {
    if (this.isLanguageSupportYes(questionId)) return;
    const first = this.nonNoneChoices(choices)[0];
    this.setSingleValue(questionId, first?.key ?? '');
  }
}
