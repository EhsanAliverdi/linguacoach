import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface DescribeImageItem {
  id: string;
  imagePrompt: string;
  imageDescription?: string | null;
  imageUrl?: string | null;
  displayTitle?: string | null;
  contextLabel?: string | null;
  focusAreas?: string[] | null;
  explanation?: string | null;
}

export interface DescribeImageContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: DescribeImageItem[];
}

export interface DescribeImageAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-describe-image',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './describe-image.component.html',
})
export class DescribeImageComponent {
  @Input() content!: DescribeImageContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<DescribeImageAnswer>();

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
