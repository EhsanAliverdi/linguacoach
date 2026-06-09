import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PlacementService } from '../../core/services/placement.service';
import {
  PlacementSection, PlacementResult, PlacementAnswerInput,
} from '../../core/models/placement.models';

type PageState = 'loading' | 'intro' | 'section' | 'evaluating' | 'result' | 'error';
type RecordState = 'idle' | 'requesting' | 'denied' | 'unsupported' | 'recording' | 'recorded';

@Component({
  selector: 'app-placement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './placement.component.html',
})
export class PlacementComponent implements OnInit, OnDestroy {
  state = signal<PageState>('loading');
  error = signal('');
  submitting = signal(false);

  section = signal<PlacementSection | null>(null);
  currentOrder = signal(1);
  totalSections = signal(6);
  result = signal<PlacementResult | null>(null);

  // Working answers for the current section, keyed by questionKey.
  answers = signal<Record<string, string>>({});

  readonly ratingScale = [1, 2, 3, 4, 5];

  // Server-side audio for the listening section
  listeningAudioUrl = signal<string | null>(null);
  listeningAudioAvailable = signal(false);

  // Fallback SpeechSynthesis state (used only when server audio is unavailable)
  isSpeaking = signal(false);

  // Recording state (speaking section)
  recordState = signal<RecordState>('idle');
  recordingSeconds = signal(0);
  liveTranscript = signal('');
  private mediaRecorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];
  private recordingTimer: ReturnType<typeof setInterval> | null = null;
  private recognition: any = null;

  progressPercent = computed(() => {
    const total = this.totalSections();
    if (total === 0) return 0;
    return Math.round(((this.currentOrder() - 1) / total) * 100);
  });

  isLastSection = computed(() => this.currentOrder() >= this.totalSections());

  constructor(private placement: PlacementService, private router: Router) {}

  ngOnDestroy(): void {
    this.cleanupRecording();
    if ('speechSynthesis' in window) window.speechSynthesis.cancel();
  }

  ngOnInit(): void {
    this.placement.getStatus().subscribe({
      next: status => {
        this.totalSections.set(status.totalSections);
        this.currentOrder.set(status.currentSectionOrder);
        if (status.isCompleted) {
          this.loadResult();
        } else if (status.status === 'NotStarted') {
          this.state.set('intro');
        } else {
          this.loadCurrentSection();
        }
      },
      error: () => { this.error.set('Could not load your placement.'); this.state.set('error'); },
    });
  }

  begin(): void {
    this.submitting.set(true);
    this.placement.start().subscribe({
      next: status => {
        this.currentOrder.set(status.currentSectionOrder);
        this.totalSections.set(status.totalSections);
        this.submitting.set(false);
        this.loadCurrentSection();
      },
      error: () => { this.submitting.set(false); this.error.set('Could not start placement.'); this.state.set('error'); },
    });
  }

  private loadCurrentSection(): void {
    this.cleanupRecording();
    this.isSpeaking.set(false);
    this.state.set('loading');
    this.placement.getCurrent().subscribe({
      next: cur => {
        if (cur.isCompleted) { this.loadResult(); return; }
        this.section.set(cur.section);
        this.currentOrder.set(cur.currentSectionOrder);
        this.totalSections.set(cur.totalSections);
        this.answers.set({});
        // Populate server-side audio fields for the listening section
        this.listeningAudioUrl.set(cur.audioUrl ?? null);
        this.listeningAudioAvailable.set(cur.audioAvailable ?? false);
        this.state.set('section');
      },
      error: () => { this.error.set('Could not load the current section.'); this.state.set('error'); },
    });
  }

  // ── Speaking section recording ────────────────────────────────────────────

  private cleanupRecording(): void {
    if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
      this.mediaRecorder.stop();
    }
    this.mediaRecorder = null;
    this.recordedChunks = [];
    if (this.recordingTimer) { clearInterval(this.recordingTimer); this.recordingTimer = null; }
    if (this.recognition) { try { this.recognition.stop(); } catch { /* ignore */ } this.recognition = null; }
    this.recordState.set('idle');
    this.recordingSeconds.set(0);
    this.liveTranscript.set('');
  }

  startRecording(questionKey: string): void {
    if (!navigator.mediaDevices || !window.MediaRecorder) {
      this.recordState.set('unsupported');
      return;
    }
    this.recordState.set('requesting');
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(stream => {
        this.recordedChunks = [];
        const recorder = new MediaRecorder(stream);
        recorder.ondataavailable = e => { if (e.data.size > 0) this.recordedChunks.push(e.data); };
        recorder.onstop = () => {
          stream.getTracks().forEach(t => t.stop());
          // Finalise transcript into the answer field
          const transcript = this.liveTranscript();
          if (transcript.trim()) this.setAnswer(questionKey, transcript.trim());
          this.recordState.set('recorded');
          if (this.recordingTimer) { clearInterval(this.recordingTimer); this.recordingTimer = null; }
        };
        recorder.start();
        this.mediaRecorder = recorder;
        this.recordingSeconds.set(0);
        this.recordState.set('recording');
        this.recordingTimer = setInterval(() => this.recordingSeconds.update(s => s + 1), 1000);
        this.startSpeechRecognition(questionKey);
      })
      .catch(() => this.recordState.set('denied'));
  }

  stopRecording(): void {
    if (this.mediaRecorder && this.mediaRecorder.state === 'recording') {
      this.mediaRecorder.stop();
    }
    if (this.recognition) { try { this.recognition.stop(); } catch { /* ignore */ } }
  }

  reRecord(questionKey: string): void {
    this.liveTranscript.set('');
    this.setAnswer(questionKey, '');
    this.recordState.set('idle');
  }

  private startSpeechRecognition(questionKey: string): void {
    const SpeechRecognition = (window as any).SpeechRecognition ?? (window as any).webkitSpeechRecognition;
    if (!SpeechRecognition) return; // transcript stays empty; user can type manually
    const rec = new SpeechRecognition();
    rec.lang = 'en-US';
    rec.interimResults = true;
    rec.continuous = true;
    let finalText = '';
    rec.onresult = (event: any) => {
      let interim = '';
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const t = event.results[i][0].transcript;
        if (event.results[i].isFinal) { finalText += t + ' '; } else { interim += t; }
      }
      const combined = (finalText + interim).trim();
      this.liveTranscript.set(combined);
      this.setAnswer(questionKey, combined);
    };
    rec.onerror = () => { /* ignore; recording continues, no transcript */ };
    rec.start();
    this.recognition = rec;
  }

  setAnswer(questionKey: string, value: string): void {
    this.answers.update(a => ({ ...a, [questionKey]: value }));
  }

  answerValue(questionKey: string): string {
    return this.answers()[questionKey] ?? '';
  }

  canContinue(): boolean {
    const sec = this.section();
    if (!sec) return false;
    for (const q of sec.questions) {
      const optional = sec.sectionType === 'self_check' && (q.type === 'text' || q.key === 'self_level');
      if (optional) continue;
      // Speaking section: allow continue once recording is done (transcript may be empty on some browsers)
      if (sec.sectionType === 'speaking' && q.type === 'text') {
        if (this.recordState() !== 'recorded' && !this.answerValue(q.key)) return false;
        continue;
      }
      if (!this.answerValue(q.key)) return false;
    }
    return true;
  }

  saveAndContinue(): void {
    const sec = this.section();
    if (!sec || !this.canContinue()) return;

    const payloadAnswers: PlacementAnswerInput[] = sec.questions.map(q => {
      const value = this.answerValue(q.key);
      if (q.type === 'choice' || q.type === 'rating') {
        return { questionKey: q.key, selectedOption: value || null, responseText: null };
      }
      return { questionKey: q.key, responseText: value || null, selectedOption: null };
    });

    this.submitting.set(true);
    this.placement.saveAnswers({ sectionKey: sec.key, answers: payloadAnswers }).subscribe({
      next: status => {
        this.submitting.set(false);
        this.currentOrder.set(status.currentSectionOrder);
        if (sec.order >= status.totalSections) {
          this.evaluate();
        } else {
          this.loadCurrentSection();
        }
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.error ?? 'Could not save your answers.');
      },
    });
  }

  private evaluate(): void {
    this.state.set('evaluating');
    this.placement.complete().subscribe({
      next: res => { this.result.set(res); this.state.set('result'); },
      error: () => { this.error.set('Could not evaluate your placement.'); this.state.set('error'); },
    });
  }

  private loadResult(): void {
    this.placement.getResult().subscribe({
      next: res => { this.result.set(res); this.state.set('result'); },
      error: () => { this.error.set('Could not load your result.'); this.state.set('error'); },
    });
  }

  continueToCourse(): void {
    this.router.navigate(['/dashboard']);
  }

  speakAudio(script: string): void {
    if (!('speechSynthesis' in window)) return;
    window.speechSynthesis.cancel();
    const utt = new SpeechSynthesisUtterance(script);
    utt.lang = 'en-GB';
    utt.rate = 0.95;
    utt.onstart = () => this.isSpeaking.set(true);
    utt.onend = () => this.isSpeaking.set(false);
    utt.onerror = () => this.isSpeaking.set(false);
    window.speechSynthesis.speak(utt);
  }

  stopAudio(): void {
    if ('speechSynthesis' in window) window.speechSynthesis.cancel();
    this.isSpeaking.set(false);
  }

  retry(): void {
    this.error.set('');
    this.ngOnInit();
  }
}
