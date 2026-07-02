import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatternEvaluationDto, PatternEvaluationItemResult } from '../../../../core/models/activity.models';
import { FeedbackAiDisclaimerComponent } from '../feedback/feedback-ai-disclaimer.component';

@Component({
  selector: 'app-pattern-evaluation-result',
  standalone: true,
  imports: [CommonModule, FeedbackAiDisclaimerComponent],
  templateUrl: './pattern-evaluation-result.component.html',
})
export class PatternEvaluationResultComponent {
  @Input({ required: true }) result!: PatternEvaluationDto;

  get isMatchingPairs(): boolean {
    return this.result.exercisePatternKey === 'phrase_match';
  }

  get isGapFill(): boolean {
    return this.result.exercisePatternKey === 'gap_fill_workplace_phrase'
      || this.result.exercisePatternKey === 'listen_and_gap_fill';
  }

  get isListenAndAnswer(): boolean {
    return this.result.exercisePatternKey === 'listen_and_answer';
  }

  get isChatOrEmail(): boolean {
    return this.result.exercisePatternKey === 'email_reply'
      || this.result.exercisePatternKey === 'teams_chat_simulation';
  }

  get isSpokenResponse(): boolean {
    return this.result.exercisePatternKey === 'spoken_response_from_prompt';
  }

  get isAnswerShortQuestion(): boolean {
    return this.result.exercisePatternKey === 'answer_short_question';
  }

  // True for any multi-item per-item-keyed result that isn't covered by a more
  // specific block â€” used as a generic fallback for future speaking formats that
  // share the same { itemKey, studentAnswer, correctAnswer, feedback } shape.
  get isGenericItemResult(): boolean {
    return !this.isMatchingPairs
      && !this.isGapFill
      && !this.isChatOrEmail
      && !this.isListenAndAnswer
      && !this.isSpokenResponse
      && !this.isAnswerShortQuestion
      && !this.isReadOnly
      && this.result.itemResults.length > 0;
  }

  get isReadOnly(): boolean {
    return this.result.exercisePatternKey === 'lesson_reflection';
  }

  get showScoreCard(): boolean {
    return this.result.maxScore > 0 && !this.isReadOnly;
  }

  scoreRingColour(): string {
    const p = this.result.percentage;
    if (p >= 90) return 'var(--sp-success)';
    if (p >= 70) return 'var(--sp-vocabulary)';
    if (p >= 40) return 'var(--sp-warn)';
    return 'var(--sp-speaking)';
  }

  scoreBandLabel(): string {
    const p = this.result.percentage;
    if (p >= 90) return 'Excellent';
    if (p >= 70) return 'Good work';
    if (p >= 40) return 'Keep going';
    return 'Needs review';
  }

  // Score-aware instruction line shown below the band label.
  // 100% must not say "Improve your answer" or "Review corrections".
  scoreBandInstruction(): string {
    const p = this.result.percentage;
    if (p >= 90) return 'Ready for the next challenge.';
    if (p >= 70) return 'Small improvements suggested below.';
    if (p >= 40) return 'Review the corrections below and try again.';
    return 'Retry recommended — check the corrections below.';
  }

  // True when score is below excellent threshold â€” drives the instruction line colour.
  get showImprovementPrompt(): boolean {
    return this.result.percentage < 90;
  }

  correctCount(): number {
    return this.result.itemResults.filter(i => i.isCorrect).length;
  }

  totalCount(): number {
    return this.result.itemResults.length;
  }

  // For MatchingPairs: resolve the phrase label from the item key (e.g. "phrase_0" â†’ index 0)
  phraseIndexFromKey(key: string): number {
    const m = key.match(/(\d+)$/);
    return m ? parseInt(m[1], 10) : 0;
  }
}

