import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface RepeatSentenceItem {
  id: string;
  sentence?: string | null;
  audioScript?: string | null;
  audioUrl?: string | null;
  displayTitle?: string | null;
  difficulty?: string | null;
  focusAreas?: string[] | null;
  explanation?: string | null;
}

export interface RepeatSentenceContent {
  learningGoal?: string | null;
  instructions?: string | null;
  scenario?: string | null;
  items: RepeatSentenceItem[];
}

export interface RepeatSentenceAnswer {
  items: { itemId: string; answerText: string }[];
}

@Component({
  selector: 'app-repeat-sentence',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './repeat-sentence.component.html',
})
export class RepeatSentenceComponent {
  @Input() content!: RepeatSentenceContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<RepeatSentenceAnswer>();

  responses: Record<string, string> = {};

  valueFor(itemId: string): string {
    return this.responses[itemId] ?? '';
  }

  onInput(itemId: string, value: string): void {
    this.responses[itemId] = value;
  }

  sentenceOrFallback(item: RepeatSentenceItem): string {
    return item.sentence ?? item.audioScript ?? '';
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
