import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface RespondToSituationItem {
  id: string;
  situation: string;
  contextLabel?: string | null;
  role?: string | null;
  audience?: string | null;
  prompt?: string | null;
  audioUrl?: string | null;
  audioScript?: string | null;
  focusAreas?: string[] | null;
  explanation?: string | null;
}

export interface RespondToSituationContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: RespondToSituationItem[];
}

export interface RespondToSituationAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-respond-to-situation',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './respond-to-situation.component.html',
})
export class RespondToSituationComponent {
  @Input() content!: RespondToSituationContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<RespondToSituationAnswer>();

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
