import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface ReadingMultipleChoiceOption {
  id: string;
  text: string;
}

export interface ReadingMultipleChoiceContent {
  learningGoal?: string | null;
  instructions?: string | null;
  passage?: string | null;
  incompleteText?: string | null;
  question: string;
  options: ReadingMultipleChoiceOption[];
  correctOptionId?: string | null;
  explanation?: string | null;
  distractorExplanations?: Record<string, string> | null;
  audioScript?: string | null;
  audioUrl?: string | null;
  scenario?: string | null;
}

export interface ReadingMultipleChoiceAnswer {
  selectedOptionId: string;
}

@Component({
  selector: 'app-reading-multiple-choice',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent],
  templateUrl: './reading-multiple-choice.component.html',
})
export class ReadingMultipleChoiceComponent {
  @Input() content!: ReadingMultipleChoiceContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ReadingMultipleChoiceAnswer>();

  selectedOptionId: string | null = null;

  selectOption(optionId: string): void {
    if (this.disabled) return;
    this.selectedOptionId = optionId;
  }

  get incompleteTextDisplay(): string {
    return (this.content.incompleteText ?? '').replace(/\{\{missing\}\}/g, '_____');
  }

  get canSubmit(): boolean {
    return !!this.selectedOptionId;
  }

  submit(): void {
    if (this.canSubmit) {
      this.submitted.emit({ selectedOptionId: this.selectedOptionId! });
    }
  }
}
