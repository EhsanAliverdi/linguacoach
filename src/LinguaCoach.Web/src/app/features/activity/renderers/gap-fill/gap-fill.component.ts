import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface GapFillItem {
  id: string;
  before: string;
  after: string;
  acceptedAnswers?: string[];
  hint?: string | null;
}

export interface GapFillContent {
  learningGoal?: string | null;
  teachingNote?: string | null;
  instructions?: string | null;
  items: GapFillItem[];
  wordBank?: string[];
}

export interface GapFillAnswer {
  answers: Record<string, string>;
}

@Component({
  selector: 'app-gap-fill',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './gap-fill.component.html',
})
export class GapFillComponent {
  @Input() content!: GapFillContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<GapFillAnswer>();

  answers: Record<string, string> = {};
  showHints: Record<string, boolean> = {};

  toggleHint(itemId: string): void {
    this.showHints[itemId] = !this.showHints[itemId];
  }

  get allFilled(): boolean {
    return this.content?.items?.length > 0 &&
      this.content.items.every(item => (this.answers[item.id] ?? '').trim().length > 0);
  }

  fillFromBank(word: string, itemId?: string): void {
    // Find first unfilled gap, or fill specified one
    const target = itemId ?? this.content.items.find(i => !(this.answers[i.id] ?? '').trim())?.id;
    if (target) {
      this.answers = { ...this.answers, [target]: word };
    }
  }

  clearAnswer(itemId: string): void {
    const copy = { ...this.answers };
    delete copy[itemId];
    this.answers = copy;
  }

  submit(): void {
    if (this.allFilled) {
      this.submitted.emit({ answers: { ...this.answers } });
    }
  }
}
