import { Formio } from '@formio/js';
import { MicRecorder, MicRecorderState } from '../mic-recorder';
import { PlacementFormioContext } from '../placement-context.model';
import { escapeHtml } from '../escape-html';

/**
 * Custom Form.io input component (registered via Formio.Components.addComponent in
 * register-custom-components.ts) — records a speaking response via getUserMedia/MediaRecorder
 * (through the framework-agnostic MicRecorder helper), uploads it immediately on stop through the
 * host-supplied `options.placementContext.uploadSpeakingAudio`, and stores the resulting
 * `{ storageKey, mimeType, durationSeconds }` as its own Form.io value — so it flows through the
 * existing, unchanged submitForm()/(submit) -> /api/student/placement/respond path exactly like
 * any other component's answer.
 */
const FormioComponentBase = (Formio as any).Components.components.base;

interface SpeakingResponseValue {
  storageKey: string;
  mimeType: string;
  durationSeconds: number | null;
}

export class SpeakingResponseComponent extends FormioComponentBase {
  static schema(...extend: any[]) {
    return FormioComponentBase.schema(
      {
        type: 'speakingResponse',
        label: 'Speaking Response',
        key: 'answer',
        input: true,
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: 'Speaking Response',
      group: 'basic',
      icon: 'microphone',
      weight: 91,
      schema: SpeakingResponseComponent.schema(),
    };
  }

  static savedValueTypes() {
    return [];
  }

  get defaultSchema() {
    return SpeakingResponseComponent.schema();
  }

  get emptyValue() {
    return null;
  }

  get defaultValue() {
    return null;
  }

  /** @formio/js's own base Component class is untyped (loaded dynamically off Formio.Components,
   *  not imported as a typed module) — this narrows `this` to `any` for calls into that API,
   *  since accessing them directly off `this` trips noPropertyAccessFromIndexSignature. */
  private get fio(): any {
    return this;
  }

  isEmpty(value?: SpeakingResponseValue | null): boolean {
    const v = value === undefined ? (this.fio.dataValue as SpeakingResponseValue | null) : value;
    return !v?.storageKey;
  }

  private recorder: MicRecorder | null = null;
  private uploadState: 'idle' | 'uploading' | 'error' = 'idle';
  private uploadError: string | null = null;
  private lastPreviewUrl: string | null = null;

  private get placementContext(): PlacementFormioContext | null {
    return this.fio.options?.placementContext ?? null;
  }

  render(): string {
    // Component.render()'s generic wrapper doesn't auto-render a label for a bare `Component`
    // subclass (only Field-derived stock types do that via their own template) — this component
    // *is* the question (its `label` is the speaking prompt), so it must show it explicitly.
    const label = escapeHtml(this.fio.component?.label ?? '');
    return super.render(`
      <div class="sf-speaking-response">
        ${label ? `<div class="sf-speaking-label">${label}</div>` : ''}
        <div ref="speakingStatus" class="sf-speaking-status"></div>
        <div class="sf-speaking-actions">
          <button type="button" ref="recordBtn" class="sf-speaking-btn sf-speaking-btn-record">Record</button>
          <button type="button" ref="stopBtn" class="sf-speaking-btn sf-speaking-btn-stop" style="display:none;">Stop</button>
          <button type="button" ref="rerecordBtn" class="sf-speaking-btn sf-speaking-btn-rerecord" style="display:none;">Re-record</button>
        </div>
        <audio ref="previewAudio" controls style="width:100%; display:none; margin-top:8px;"></audio>
      </div>
    `);
  }

  attach(element: HTMLElement) {
    this.fio.loadRefs(element, {
      speakingStatus: 'single',
      recordBtn: 'single',
      stopBtn: 'single',
      rerecordBtn: 'single',
      previewAudio: 'single',
    });

    this.recorder = new MicRecorder(
      (state) => this.onRecorderStateChange(state),
      (audio) => this.onRecorded(audio.blob, audio.mimeType, audio.durationSeconds, audio.previewUrl),
    );

    this.fio.addEventListener(this.fio.refs.recordBtn, 'click', () => this.recorder?.start());
    this.fio.addEventListener(this.fio.refs.stopBtn, 'click', () => this.recorder?.stop());
    this.fio.addEventListener(this.fio.refs.rerecordBtn, 'click', () => {
      this.uploadState = 'idle';
      this.uploadError = null;
      this.fio.setValue(null);
      this.recorder?.reset();
    });

    this.renderStatus(this.recorder.state);
    return super.attach(element);
  }

  detach(): void {
    this.recorder?.destroy();
    this.recorder = null;
    super.detach();
  }

  private onRecorderStateChange(state: MicRecorderState): void {
    this.renderStatus(state);
  }

  private async onRecorded(blob: Blob, mimeType: string, durationSeconds: number, previewUrl: string): Promise<void> {
    this.lastPreviewUrl = previewUrl;
    const previewAudio: HTMLAudioElement | undefined = this.fio.refs?.previewAudio;
    if (previewAudio) {
      previewAudio.src = previewUrl;
      previewAudio.style.display = '';
    }

    const context = this.placementContext;
    if (!context) {
      this.uploadState = 'error';
      this.uploadError = 'Recording could not be uploaded (no upload context).';
      this.renderStatus('recorded');
      return;
    }

    this.uploadState = 'uploading';
    this.renderStatus('recorded');

    try {
      const result = await context.uploadSpeakingAudio(blob, mimeType, durationSeconds);
      this.uploadState = 'idle';
      this.fio.setValue({
        storageKey: result.storageKey,
        mimeType: result.mimeType,
        durationSeconds: result.durationSeconds,
      });
    } catch {
      this.uploadState = 'error';
      this.uploadError = 'Upload failed — please re-record and try again.';
    }
    this.renderStatus('recorded');
  }

  private renderStatus(state: MicRecorderState): void {
    const statusEl: HTMLElement | undefined = this.fio.refs?.speakingStatus;
    const recordBtn: HTMLButtonElement | undefined = this.fio.refs?.recordBtn;
    const stopBtn: HTMLButtonElement | undefined = this.fio.refs?.stopBtn;
    const rerecordBtn: HTMLButtonElement | undefined = this.fio.refs?.rerecordBtn;
    if (!statusEl || !recordBtn || !stopBtn || !rerecordBtn) return;

    recordBtn.style.display = 'none';
    stopBtn.style.display = 'none';
    rerecordBtn.style.display = 'none';

    switch (state) {
      case 'unsupported':
        statusEl.textContent = 'Recording is not supported in this browser.';
        break;
      case 'idle':
        statusEl.textContent = 'Tap Record and speak your answer.';
        recordBtn.style.display = '';
        break;
      case 'requesting-permission':
        statusEl.textContent = 'Requesting microphone access...';
        break;
      case 'permission-denied':
        statusEl.textContent = 'Microphone access was denied. Please allow it and try again.';
        recordBtn.style.display = '';
        break;
      case 'recording':
        statusEl.textContent = 'Recording...';
        stopBtn.style.display = '';
        break;
      case 'recorded':
        if (this.uploadState === 'uploading') {
          statusEl.textContent = 'Uploading your recording...';
        } else if (this.uploadState === 'error') {
          statusEl.textContent = this.uploadError ?? 'Upload failed.';
        } else {
          statusEl.textContent = 'Recorded.';
        }
        rerecordBtn.style.display = '';
        break;
    }
  }
}
