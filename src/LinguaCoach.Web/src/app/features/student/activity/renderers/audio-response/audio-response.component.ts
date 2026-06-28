import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VoiceRecorderComponent, RecordedAudio } from '../voice-recorder/voice-recorder.component';

export interface AudioResponseContent {
  prompt: string | null;
  situation: string | null;
}

@Component({
  selector: 'app-audio-response',
  standalone: true,
  imports: [CommonModule, VoiceRecorderComponent],
  templateUrl: './audio-response.component.html',
})
export class AudioResponseComponent {
  @Input({ required: true }) content!: AudioResponseContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<{ blob: Blob; mimeType: string; durationSeconds: number }>();

  readonly recordedAudio = signal<RecordedAudio | null>(null);

  onRecorded(audio: RecordedAudio): void {
    this.recordedAudio.set(audio);
  }

  submit(): void {
    const audio = this.recordedAudio();
    if (!audio || this.disabled) return;
    this.submitted.emit({
      blob: audio.blob,
      mimeType: audio.mimeType,
      durationSeconds: audio.durationSeconds,
    });
  }
}
