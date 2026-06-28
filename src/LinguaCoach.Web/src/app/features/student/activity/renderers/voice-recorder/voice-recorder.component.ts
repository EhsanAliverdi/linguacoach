import { Component, Input, Output, EventEmitter, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export type RecorderState =
  | 'idle'
  | 'requesting-permission'
  | 'permission-denied'
  | 'unsupported'
  | 'recording'
  | 'recorded';

export interface RecordedAudio {
  blob: Blob;
  mimeType: string;
  durationSeconds: number;
  previewUrl: string;
}

@Component({
  selector: 'app-voice-recorder',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './voice-recorder.component.html',
})
export class VoiceRecorderComponent implements OnDestroy {
  @Input() prompt: string | null = null;
  @Input() situation: string | null = null;
  @Input() disabled = false;
  @Output() recorded = new EventEmitter<RecordedAudio>();

  readonly recorderState = signal<RecorderState>(this.initialState());

  previewUrl: string | null = null;

  private _mediaRecorder: MediaRecorder | null = null;
  private _stream: MediaStream | null = null;
  private _chunks: BlobPart[] = [];
  private _startTime = 0;
  private _previewObjectUrl: string | null = null;

  private initialState(): RecorderState {
    if (typeof navigator === 'undefined') return 'unsupported';
    if (!navigator.mediaDevices?.getUserMedia) return 'unsupported';
    return 'idle';
  }

  async startRecording(): Promise<void> {
    if (this.disabled) return;
    this.recorderState.set('requesting-permission');

    try {
      this._stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.recorderState.set('permission-denied');
      return;
    }

    const mimeType = this.preferredMimeType();
    this._chunks = [];
    this._mediaRecorder = mimeType
      ? new MediaRecorder(this._stream, { mimeType })
      : new MediaRecorder(this._stream);

    this._mediaRecorder.ondataavailable = (e) => {
      if (e.data.size > 0) this._chunks.push(e.data);
    };

    this._mediaRecorder.onstop = () => {
      const actualMime = this._mediaRecorder?.mimeType ?? 'audio/webm';
      const blob = new Blob(this._chunks, { type: actualMime });
      if (this._previewObjectUrl) {
        URL.revokeObjectURL(this._previewObjectUrl);
      }
      this._previewObjectUrl = URL.createObjectURL(blob);
      this.previewUrl = this._previewObjectUrl;
      const durationSeconds = (Date.now() - this._startTime) / 1000;
      this.recorderState.set('recorded');
      this.recorded.emit({ blob, mimeType: actualMime, durationSeconds, previewUrl: this._previewObjectUrl });
    };

    this._startTime = Date.now();
    this._mediaRecorder.start();
    this.recorderState.set('recording');
  }

  stopRecording(): void {
    if (this._mediaRecorder?.state === 'recording') {
      this._mediaRecorder.stop();
    }
    this.releaseStream();
  }

  reRecord(): void {
    if (this._previewObjectUrl) {
      URL.revokeObjectURL(this._previewObjectUrl);
      this._previewObjectUrl = null;
    }
    this.previewUrl = null;
    this.recorderState.set('idle');
  }

  private releaseStream(): void {
    if (this._stream) {
      this._stream.getTracks().forEach(t => t.stop());
      this._stream = null;
    }
  }

  private preferredMimeType(): string | null {
    if (typeof MediaRecorder === 'undefined') return null;
    const types = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/mp4'];
    return types.find(t => MediaRecorder.isTypeSupported(t)) ?? null;
  }

  ngOnDestroy(): void {
    if (this._mediaRecorder?.state === 'recording') {
      this._mediaRecorder.stop();
    }
    this.releaseStream();
    if (this._previewObjectUrl) {
      URL.revokeObjectURL(this._previewObjectUrl);
    }
  }
}
