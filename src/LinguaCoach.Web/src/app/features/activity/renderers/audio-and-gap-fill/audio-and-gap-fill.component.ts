import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface AudioGapItem {
  id: string;
  before: string;
  after: string;
}

export interface AudioAndGapFillContent {
  audioUrl?: string | null;
  audioUnavailableMessage?: string | null;
  scenario?: string | null;
  learningGoal?: string | null;
  instructions?: string | null;
  gaps: AudioGapItem[];
  wordBank?: string[];
}

export interface AudioAndGapFillAnswer {
  answers: Record<string, string>;
}

@Component({
  selector: 'app-audio-and-gap-fill',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent],
  templateUrl: './audio-and-gap-fill.component.html',
})
export class AudioAndGapFillComponent {
  @Input() content!: AudioAndGapFillContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<AudioAndGapFillAnswer>();

  answers: Record<string, string> = {};

  get allFilled(): boolean {
    return this.content?.gaps?.length > 0 &&
      this.content.gaps.every(g => (this.answers[g.id] ?? '').trim().length > 0);
  }

  fillFromBank(word: string): void {
    const target = this.content.gaps.find(g => !(this.answers[g.id] ?? '').trim())?.id;
    if (target) {
      this.answers = { ...this.answers, [target]: word };
    }
  }

  submit(): void {
    if (this.allFilled) {
      this.submitted.emit({ answers: { ...this.answers } });
    }
  }
}
