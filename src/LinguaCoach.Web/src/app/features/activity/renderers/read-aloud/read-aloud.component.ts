import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface ReadAloudItem {
  id: string;
  text?: string | null;
  displayTitle?: string | null;
  difficulty?: string | null;
  focusAreas?: string[] | null;
  explanation?: string | null;
}

export interface ReadAloudContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: ReadAloudItem[];
}

export interface ReadAloudAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-read-aloud',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './read-aloud.component.html',
})
export class ReadAloudComponent {
  @Input() content!: ReadAloudContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ReadAloudAnswer>();

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
