import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';
import { AudioPlayerComponent } from '../audio-player/audio-player.component';

export interface AudioQuestion {
  id: string;
  question: string;
}

export interface AudioAndFreeTextContent {
  audioUrl?: string | null;
  audioDurationSeconds?: number | null;
  audioUnavailableMessage?: string | null;
  scenario?: string | null;
  learningGoal?: string | null;
  instructions?: string | null;
  questions: AudioQuestion[];
  responseTask?: string | null;
}

export interface AudioAndFreeTextAnswer {
  answers: Record<string, string>;
  responseText?: string;
}

@Component({
  selector: 'app-audio-and-free-text',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseLessonIntroComponent, AudioPlayerComponent],
  templateUrl: './audio-and-free-text.component.html',
})
export class AudioAndFreeTextComponent {
  @Input() content!: AudioAndFreeTextContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<AudioAndFreeTextAnswer>();

  answers: Record<string, string> = {};
  responseText = '';

  get allAnswered(): boolean {
    return this.content?.questions?.length > 0 &&
      this.content.questions.every(q => (this.answers[q.id] ?? '').trim().length > 0);
  }

  submit(): void {
    if (this.allAnswered) {
      this.submitted.emit({ answers: { ...this.answers }, responseText: this.responseText });
    }
  }
}
