import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface RetellLectureItem {
  id: string;
  lectureTitle: string;
  lectureTopic?: string | null;
  audioScript: string;
  audioUrl?: string | null;
  contextLabel?: string | null;
  difficulty?: string | null;
  focusAreas?: string[] | null;
}

export interface RetellLectureContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: RetellLectureItem[];
}

export interface RetellLectureAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-retell-lecture',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './retell-lecture.component.html',
})
export class RetellLectureComponent {
  @Input() content!: RetellLectureContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<RetellLectureAnswer>();

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
