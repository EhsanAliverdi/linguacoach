import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface SummarizeGroupDiscussionSpeaker {
  name: string;
  role?: string | null;
  viewpoint?: string | null;
}

export interface SummarizeGroupDiscussionItem {
  id: string;
  discussionTitle: string;
  discussionTopic?: string | null;
  audioScript: string;
  audioUrl?: string | null;
  contextLabel?: string | null;
  speakers?: SummarizeGroupDiscussionSpeaker[] | null;
  focusAreas?: string[] | null;
}

export interface SummarizeGroupDiscussionContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: SummarizeGroupDiscussionItem[];
}

export interface SummarizeGroupDiscussionAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-summarize-group-discussion',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './summarize-group-discussion.component.html',
})
export class SummarizeGroupDiscussionComponent {
  @Input() content!: SummarizeGroupDiscussionContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<SummarizeGroupDiscussionAnswer>();

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
