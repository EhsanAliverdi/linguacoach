/**
 * Framework-agnostic getUserMedia/MediaRecorder wrapper used by the vanilla Form.io
 * "speakingResponse" component (shared/formio/components/speaking-response.component.ts). A
 * Form.io component has no Angular DI/signals, so this mirrors — but does not share code with —
 * the Activity feature's VoiceRecorderComponent (features/student/activity/renderers/voice-recorder),
 * which is left untouched. Same state machine and mime-type fallback list, callback-based instead
 * of signal-based.
 */
export type MicRecorderState =
  | 'idle'
  | 'requesting-permission'
  | 'permission-denied'
  | 'unsupported'
  | 'recording'
  | 'recorded';

export interface MicRecordedAudio {
  blob: Blob;
  mimeType: string;
  durationSeconds: number;
  previewUrl: string;
}

export class MicRecorder {
  state: MicRecorderState;

  private mediaRecorder: MediaRecorder | null = null;
  private stream: MediaStream | null = null;
  private chunks: BlobPart[] = [];
  private startTime = 0;
  private previewObjectUrl: string | null = null;

  constructor(
    private readonly onStateChange: (state: MicRecorderState) => void,
    private readonly onRecorded: (audio: MicRecordedAudio) => void,
  ) {
    this.state = typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia
      ? 'unsupported'
      : 'idle';
  }

  async start(): Promise<void> {
    this.setState('requesting-permission');

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.setState('permission-denied');
      return;
    }

    const mimeType = this.preferredMimeType();
    this.chunks = [];
    this.mediaRecorder = mimeType
      ? new MediaRecorder(this.stream, { mimeType })
      : new MediaRecorder(this.stream);

    this.mediaRecorder.ondataavailable = (e) => {
      if (e.data.size > 0) this.chunks.push(e.data);
    };

    this.mediaRecorder.onstop = () => {
      const actualMime = this.mediaRecorder?.mimeType ?? 'audio/webm';
      const blob = new Blob(this.chunks, { type: actualMime });
      if (this.previewObjectUrl) URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = URL.createObjectURL(blob);
      const durationSeconds = (Date.now() - this.startTime) / 1000;
      this.setState('recorded');
      this.onRecorded({ blob, mimeType: actualMime, durationSeconds, previewUrl: this.previewObjectUrl });
    };

    this.startTime = Date.now();
    this.mediaRecorder.start();
    this.setState('recording');
  }

  stop(): void {
    if (this.mediaRecorder?.state === 'recording') this.mediaRecorder.stop();
    this.releaseStream();
  }

  /** Discards the current recording and returns to idle so the student can record again. */
  reset(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
    this.setState('idle');
  }

  destroy(): void {
    if (this.mediaRecorder?.state === 'recording') this.mediaRecorder.stop();
    this.releaseStream();
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
  }

  private setState(state: MicRecorderState): void {
    this.state = state;
    this.onStateChange(state);
  }

  private releaseStream(): void {
    if (this.stream) {
      this.stream.getTracks().forEach(t => t.stop());
      this.stream = null;
    }
  }

  private preferredMimeType(): string | null {
    if (typeof MediaRecorder === 'undefined') return null;
    const types = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/mp4'];
    return types.find(t => MediaRecorder.isTypeSupported(t)) ?? null;
  }
}
