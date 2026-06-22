import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface MatchingPair {
  id: string;
  meaningId: string;
  phrase: string;
  meaning: string;
  context?: string | null;
}

export interface MatchingPairsContent {
  learningGoal?: string | null;
  teachingNote?: string | null;
  instructions?: string | null;
  pairs: MatchingPair[];
}

export interface MatchingPairsAnswer {
  selections: Record<string, string>;
}

@Component({
  selector: 'app-matching-pairs',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent],
  templateUrl: './matching-pairs.component.html',
})
export class MatchingPairsComponent {
  @Input() content!: MatchingPairsContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<MatchingPairsAnswer>();

  // phraseId → selected meaningId
  selections: Record<string, string> = {};
  selectedMeaningId: string | null = null;
  pendingPhraseId: string | null = null;

  get shuffledMeanings(): MatchingPair[] {
    if (!this.content?.pairs) return [];
    return [...this.content.pairs].sort(() => Math.random() - 0.5);
  }

  private _shuffled: MatchingPair[] | null = null;
  get displayMeanings(): MatchingPair[] {
    if (!this._shuffled) {
      this._shuffled = this.content?.pairs
        ? [...this.content.pairs].sort(() => Math.random() - 0.5)
        : [];
    }
    return this._shuffled;
  }

  selectPhrase(id: string): void {
    if (this.disabled) return;
    this.pendingPhraseId = id;
  }

  selectMeaning(meaningId: string): void {
    if (this.disabled || !this.pendingPhraseId) return;
    this.selections = { ...this.selections, [this.pendingPhraseId]: meaningId };
    this.pendingPhraseId = null;
  }

  isMatchedPhrase(id: string): boolean {
    return id in this.selections;
  }

  isMatchedMeaning(meaningId: string): boolean {
    return Object.values(this.selections).includes(meaningId);
  }

  get allMatched(): boolean {
    return this.content?.pairs?.length > 0 &&
      this.content.pairs.every(p => p.id in this.selections);
  }

  submit(): void {
    if (this.allMatched) {
      this.submitted.emit({ selections: { ...this.selections } });
    }
  }
}
