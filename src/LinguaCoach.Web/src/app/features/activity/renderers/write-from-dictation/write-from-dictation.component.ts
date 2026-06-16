import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface WriteFromDictationItem {
  id: string;
  audioScript?: string | null;
  audioUrl?: string | null;
}

export interface WriteFromDictationContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: WriteFromDictationItem[];
}

export interface WriteFromDictationAnswer {
  items: { itemId: string; submittedText: string }[];
}

@Component({
  selector: 'app-write-from-dictation',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './write-from-dictation.component.html',
})
export class WriteFromDictationComponent {
  @Input() content!: WriteFromDictationContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<WriteFromDictationAnswer>();

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
      submittedText: (this.responses[i.id] ?? '').trim(),
    }));
    this.submitted.emit({ items });
  }
}
