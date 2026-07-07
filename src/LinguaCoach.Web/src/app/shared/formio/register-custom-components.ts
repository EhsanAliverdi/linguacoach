import { Formio } from '@formio/js';
import { AudioPlayerComponent } from './components/audio-player.component';
import { SpeakingResponseComponent } from './components/speaking-response.component';

let registered = false;

/**
 * Registers LinguaCoach's custom Form.io components once, globally, before any
 * Formio.builder()/Formio.createForm() call runs — both the shared builder
 * (FormioBuilderComponent) and renderer (FormioRendererComponent) then pick these up
 * automatically for every consumer (onboarding + placement), the same way stock Form.io types do.
 * Must be imported at app bootstrap (see app.config.ts) rather than lazily from a feature module,
 * since a form authored/rendered before this runs would 400 on the backend allow-list check but
 * render as an "Unknown component" client-side rather than the real custom UI.
 */
export function registerCustomFormioComponents(): void {
  if (registered) return;
  registered = true;
  (Formio as any).Components.addComponent('audioPlayer', AudioPlayerComponent);
  (Formio as any).Components.addComponent('speakingResponse', SpeakingResponseComponent);
}
