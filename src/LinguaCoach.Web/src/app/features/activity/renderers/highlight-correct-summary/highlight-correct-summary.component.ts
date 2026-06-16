import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface HighlightCorrectSummaryOption {
  id: string;
  text: string;
}

export interface HighlightCorrectSummaryContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  audioScript?: string | null;
  audioUrl?: string | null;
  question: string;
  options: HighlightCorrectSummaryOption[];
  correctOptionId?: string | null;
  explanation?: string | null;
  distractorExplanations?: Record<string, string> | null;
}

export interface HighlightCorrectSummaryAnswer {
  selectedOptionId: string;
}

@Component({
  selector: 'app-highlight-correct-summary',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './highlight-correct-summary.component.html',
})
export class HighlightCorrectSummaryComponent {
  @Input() content!: HighlightCorrectSummaryContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<HighlightCorrectSummaryAnswer>();

  selectedOptionId: string | null = null;

  select(optionId: string): void {
    if (this.disabled) return;
    this.selectedOptionId = optionId;
  }

  get canSubmit(): boolean {
    return !!this.selectedOptionId;
  }

  submit(): void {
    if (this.canSubmit && this.selectedOptionId) {
      this.submitted.emit({ selectedOptionId: this.selectedOptionId });
    }
  }
}
