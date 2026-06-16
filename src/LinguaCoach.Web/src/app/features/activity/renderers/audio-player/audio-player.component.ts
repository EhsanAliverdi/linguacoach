import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

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

  get showPlayer(): boolean {
    return !!this.audioUrl;
  }

  get statusMessage(): string {
    if (this.audioStatus === 'pending') {
      return 'Audio is being prepared. You can still complete this exercise.';
    }
    return this.audioUnavailableMessage || 'Audio is temporarily unavailable. Complete this as a text-based exercise.';
  }
}
