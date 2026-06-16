import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface AnswerShortQuestionItem {
  id: string;
  question?: string | null;
  audioScript?: string | null;
  audioUrl?: string | null;
}

export interface AnswerShortQuestionContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: AnswerShortQuestionItem[];
}

export interface AnswerShortQuestionAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-answer-short-question',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './answer-short-question.component.html',
})
export class AnswerShortQuestionComponent {
  @Input() content!: AnswerShortQuestionContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<AnswerShortQuestionAnswer>();

  responses: Record<string, string> = {};

  valueFor(itemId: string): string {
    return this.responses[itemId] ?? '';
  }

  onInput(itemId: string, value: string): void {
    this.responses[itemId] = value;
  }

  get canSubmit(): boolean {
    return (this.content?.items ?? []).some(i => (this.responses[i.id] ?? '').trim().length > 0);
  }

  submit(): void {
    if (!this.canSubmit) return;
    const items = (this.content?.items ?? []).map(i => ({
      itemId: i.id,
      answerText: (this.responses[i.id] ?? '').trim(),
    }));
    this.submitted.emit({ items });
  }
}
