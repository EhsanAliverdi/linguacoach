import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface ReadingMultipleChoiceMultiOption {
  id: string;
  text: string;
}

export interface ReadingMultipleChoiceMultiContent {
  learningGoal?: string | null;
  instructions?: string | null;
  passage: string;
  question: string;
  options: ReadingMultipleChoiceMultiOption[];
  correctOptionIds?: string[] | null;
  explanation?: string | null;
  optionExplanations?: Record<string, string> | null;
}

export interface ReadingMultipleChoiceMultiAnswer {
  selectedOptionIds: string[];
}

@Component({
  selector: 'app-reading-multiple-choice-multi',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent],
  templateUrl: './reading-multiple-choice-multi.component.html',
})
export class ReadingMultipleChoiceMultiComponent {
  @Input() content!: ReadingMultipleChoiceMultiContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ReadingMultipleChoiceMultiAnswer>();

  selectedOptionIds = new Set<string>();

  toggleOption(optionId: string): void {
    if (this.disabled) return;
    if (this.selectedOptionIds.has(optionId)) {
      this.selectedOptionIds.delete(optionId);
    } else {
      this.selectedOptionIds.add(optionId);
    }
    // trigger change detection
    this.selectedOptionIds = new Set(this.selectedOptionIds);
  }

  isSelected(optionId: string): boolean {
    return this.selectedOptionIds.has(optionId);
  }

  get canSubmit(): boolean {
    return this.selectedOptionIds.size > 0;
  }

  submit(): void {
    if (this.canSubmit) {
      this.submitted.emit({ selectedOptionIds: [...this.selectedOptionIds] });
    }
  }
}
