import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type AudioLoadState = 'idle' | 'loading' | 'ready' | 'failed';

@Component({
  selector: 'app-audio-player',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './audio-player.component.html',
})
export class AudioPlayerComponent {
  @Input() audioUrl?: string | null;
  @Input() audioStatus?: string | null;
  @Input() audioUnavailableMessage?: string | null;
  @Input() audioScript?: string | null;
  @Input() label = 'Audio';
  @Input() helpText?: string | null;

  audioState: AudioLoadState = 'idle';
  retryKey = 0;

  get showPlayer(): boolean {
    return !!this.audioUrl;
  }

  get statusMessage(): string {
    if (this.audioStatus === 'pending') {
      return 'Audio is being prepared. You can still complete this exercise.';
    }
    return this.audioUnavailableMessage || 'Audio is temporarily unavailable. Complete this as a text-based exercise.';
  }

  onLoadStart(): void {
    this.audioState = 'loading';
  }

  onCanPlay(): void {
    this.audioState = 'ready';
  }

  onError(): void {
    this.audioState = 'failed';
  }

  retry(): void {
    this.audioState = 'idle';
    this.retryKey++;
  }
}
