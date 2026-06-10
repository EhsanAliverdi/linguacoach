import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatternEvaluationDto, PatternEvaluationItemResult } from '../../../core/models/activity.models';

@Component({
  selector: 'app-pattern-evaluation-result',
  standalone: true,
  imports: [CommonModule],
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

  get isReadOnly(): boolean {
    return this.result.exercisePatternKey === 'lesson_reflection';
  }

  get showScoreCard(): boolean {
    return this.result.maxScore > 0 && !this.isReadOnly;
  }

  scoreRingColour(): string {
    const p = this.result.percentage;
    if (p >= 85) return 'var(--sp-success)';
    if (p >= 70) return 'var(--sp-vocabulary)';
    return 'var(--sp-speaking)';
  }

  scoreBandLabel(): string {
    const p = this.result.percentage;
    if (p >= 85) return 'Great work';
    if (p >= 70) return 'Good effort';
    return 'Keep going';
  }

  correctCount(): number {
    return this.result.itemResults.filter(i => i.isCorrect).length;
  }

  totalCount(): number {
    return this.result.itemResults.length;
  }

  // For MatchingPairs: resolve the phrase label from the item key (e.g. "phrase_0" → index 0)
  phraseIndexFromKey(key: string): number {
    const m = key.match(/(\d+)$/);
    return m ? parseInt(m[1], 10) : 0;
  }
}
