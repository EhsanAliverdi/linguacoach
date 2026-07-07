import { Formio } from '@formio/js';

/**
 * Custom Form.io component (registered via Formio.Components.addComponent in
 * register-custom-components.ts) — a presentational, non-input audio player. Carries no audio
 * source in its own schema (there is none to author: placement listening audio is generated
 * per-assessment server-side from a backend-only script, see AdaptivePlacementAudioService). The
 * host page (PlacementComponent, via FormioRendererComponent's `audioSrc` input) calls
 * `setAudioSrc(url)` once the real audio URL is resolved. In the builder canvas (authoring time,
 * no real assessment) it just shows its placeholder — the same way the existing "content"
 * component shows static authored text with no live data.
 */
const FormioComponentBase = (Formio as any).Components.components.base;

export class AudioPlayerComponent extends FormioComponentBase {
  static schema(...extend: any[]) {
    return FormioComponentBase.schema(
      {
        type: 'audioPlayer',
        label: 'Audio Player',
        key: 'listening_audio',
        input: false,
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: 'Audio Player',
      group: 'basic',
      icon: 'volume-up',
      weight: 90,
      schema: AudioPlayerComponent.schema(),
    };
  }

  static savedValueTypes() {
    return [];
  }

  get defaultSchema() {
    return AudioPlayerComponent.schema();
  }

  get emptyValue() {
    return '';
  }

  private pendingSrc: string | null = null;

  /** @formio/js's own base Component class is untyped (loaded dynamically off Formio.Components,
   *  not imported as a typed module) — this narrows `this` to `any` for calls into that API,
   *  since accessing them directly off `this` trips noPropertyAccessFromIndexSignature. */
  private get fio(): any {
    return this;
  }

  render(): string {
    return super.render(`
      <div class="sf-audio-player">
        <audio ref="audioEl" controls style="width:100%; display:none;"></audio>
        <div ref="audioPlaceholder" class="sf-audio-placeholder">Audio will play here when this question is shown.</div>
      </div>
    `);
  }

  attach(element: HTMLElement) {
    this.fio.loadRefs(element, { audioEl: 'single', audioPlaceholder: 'single' });
    this.applySrc(this.pendingSrc);
    return super.attach(element);
  }

  /** Called by the host (FormioRendererComponent) once the real audio URL is resolved. */
  setAudioSrc(url: string | null): void {
    this.pendingSrc = url;
    this.applySrc(url);
  }

  private applySrc(url: string | null): void {
    const audioEl: HTMLAudioElement | undefined = this.fio.refs?.audioEl;
    const placeholder: HTMLElement | undefined = this.fio.refs?.audioPlaceholder;
    if (!audioEl) return;

    if (url) {
      audioEl.src = url;
      audioEl.style.display = '';
      if (placeholder) placeholder.style.display = 'none';
    } else {
      audioEl.removeAttribute('src');
      audioEl.style.display = 'none';
      if (placeholder) placeholder.style.display = '';
    }
  }
}
