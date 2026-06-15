import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface ReadingWritingFillInBlanksGap {
  id: string;
  options: string[];
}

export interface ReadingWritingFillInBlanksContent {
  learningGoal?: string | null;
  instructions?: string | null;
  passageWithBlanks: string;
  gaps: ReadingWritingFillInBlanksGap[];
}

export interface ReadingWritingFillInBlanksAnswer {
  answers: Record<string, string>;
}

@Component({
  selector: 'app-reading-writing-fill-in-blanks',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './reading-writing-fill-in-blanks.component.html',
})
export class ReadingWritingFillInBlanksComponent {
  @Input() content!: ReadingWritingFillInBlanksContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ReadingWritingFillInBlanksAnswer>();

  selections: Record<string, string> = {};

  get passageParts(): { text: string; gapId: string | null }[] {
    const parts: { text: string; gapId: string | null }[] = [];
    const passage = this.content?.passageWithBlanks ?? '';
    const token = /\{\{(gap\d+)\}\}/g;
    let lastIndex = 0;
    let match: RegExpExecArray | null;
    while ((match = token.exec(passage)) !== null) {
      if (match.index > lastIndex) {
        parts.push({ text: passage.slice(lastIndex, match.index), gapId: null });
      }
      parts.push({ text: '', gapId: match[1] });
      lastIndex = match.index + match[0].length;
    }
    if (lastIndex < passage.length) {
      parts.push({ text: passage.slice(lastIndex), gapId: null });
    }
    return parts;
  }

  gapOptions(gapId: string): string[] {
    return this.content?.gaps?.find(g => g.id === gapId)?.options ?? [];
  }

  get canSubmit(): boolean {
    return (this.content?.gaps ?? []).every(g => !!this.selections[g.id]);
  }

  submit(): void {
    if (this.canSubmit) {
      this.submitted.emit({ answers: { ...this.selections } });
    }
  }
}
