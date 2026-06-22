import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface HighlightIncorrectWordsToken {
  id: string;
  text: string;
  position: number;
}

export interface HighlightIncorrectWordsContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  audioScript?: string | null;
  audioUrl?: string | null;
  displayTranscript?: string | null;
  tokens: HighlightIncorrectWordsToken[];
  question?: string | null;
  // The fields below are only present after submission feedback is merged in.
  incorrectTokenIds?: string[] | null;
  corrections?: Record<string, string> | null;
  tokenExplanations?: Record<string, string> | null;
  explanation?: string | null;
}

export interface HighlightIncorrectWordsAnswer {
  selectedTokenIds: string[];
}

@Component({
  selector: 'app-highlight-incorrect-words',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './highlight-incorrect-words.component.html',
})
export class HighlightIncorrectWordsComponent {
  @Input() content!: HighlightIncorrectWordsContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<HighlightIncorrectWordsAnswer>();

  selectedTokenIds = new Set<string>();

  isSelected(tokenId: string): boolean {
    return this.selectedTokenIds.has(tokenId);
  }

  toggle(tokenId: string): void {
    if (this.disabled) return;
    if (this.selectedTokenIds.has(tokenId)) {
      this.selectedTokenIds.delete(tokenId);
    } else {
      this.selectedTokenIds.add(tokenId);
    }
  }

  get canSubmit(): boolean {
    return this.selectedTokenIds.size > 0;
  }

  submit(): void {
    if (this.canSubmit) {
      this.submitted.emit({ selectedTokenIds: Array.from(this.selectedTokenIds) });
    }
  }
}
