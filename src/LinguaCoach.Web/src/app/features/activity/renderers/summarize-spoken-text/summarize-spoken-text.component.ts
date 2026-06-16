import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface SummarizeSpokenTextContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  audioScript?: string | null;
  audioUrl?: string | null;
  prompt?: string | null;
  summaryRequirements?: string[];
}

export interface SummarizeSpokenTextAnswer {
  summaryText: string;
}

@Component({
  selector: 'app-summarize-spoken-text',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './summarize-spoken-text.component.html',
})
export class SummarizeSpokenTextComponent {
  @Input() content!: SummarizeSpokenTextContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<SummarizeSpokenTextAnswer>();

  summaryText = '';

  get canSubmit(): boolean {
    return this.summaryText.trim().length > 0;
  }

  submit(): void {
    if (!this.canSubmit) return;
    this.submitted.emit({ summaryText: this.summaryText.trim() });
  }
}
