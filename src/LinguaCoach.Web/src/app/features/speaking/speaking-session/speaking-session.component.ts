import { Component, OnInit, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { SpeakingService } from '../../../core/services/speaking.service';
import { SpeakingSessionDto, SpeakingTurnResultDto } from '../../../core/models/speaking.models';

// Fixed scenario ID for MVP (Document Controller — seeded in T11 migration).
// In T12, the student will pick from a list.
const DOCUMENT_CONTROLLER_SCENARIO_ID = '70000000-0000-0000-0000-000000000001';

type PageState = 'loading' | 'ready' | 'listening' | 'processing' | 'feedback' | 'complete' | 'error';

@Component({
  selector: 'app-speaking-session',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './speaking-session.component.html',
})
export class SpeakingSessionComponent implements OnInit, OnDestroy {
  state = signal<PageState>('loading');
  session = signal<SpeakingSessionDto | null>(null);
  lastResult = signal<SpeakingTurnResultDto | null>(null);
  currentQuestion = signal('');
  transcript = signal('');
  turnNumber = signal(0);
  errorMessage = signal('');

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private recognition: any = null;
  private sessionId = '';

  constructor(private speakingService: SpeakingService, private router: Router) {}

  ngOnInit(): void {
    this.speakingService.createSession(DOCUMENT_CONTROLLER_SCENARIO_ID).subscribe({
      next: s => {
        this.session.set(s);
        this.sessionId = s.sessionId;
        this.currentQuestion.set(s.firstAiQuestion);
        this.turnNumber.set(1);
        this.state.set('ready');
      },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not start speaking session.');
        this.state.set('error');
      },
    });
  }

  ngOnDestroy(): void {
    this.stopRecognition();
  }

  startListening(): void {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const SpeechRecognitionAPI: any =
      (window as any).SpeechRecognition ?? (window as any).webkitSpeechRecognition;

    if (!SpeechRecognitionAPI) {
      // Fallback: allow typed transcript
      this.state.set('listening');
      return;
    }

    this.recognition = new SpeechRecognitionAPI() as any;
    (this.recognition as any).lang = 'en-US';
    (this.recognition as any).interimResults = false;
    (this.recognition as any).maxAlternatives = 1;

    (this.recognition as any).onresult = (event: any) => {
      const text = event.results[0][0].transcript;
      this.transcript.set(text);
      this.state.set('feedback');
      this.submitTranscript(text);
    };

    (this.recognition as any).onerror = () => {
      this.state.set('ready');
    };

    (this.recognition as any).onend = () => {
      if (this.state() === 'listening') this.state.set('ready');
    };

    this.recognition.start();
    this.state.set('listening');
  }

  stopListening(): void {
    this.stopRecognition();
    if (this.state() === 'listening') this.state.set('ready');
  }

  submitTranscriptManually(text: string): void {
    if (!text.trim()) return;
    this.transcript.set(text);
    this.state.set('processing');
    this.submitTranscript(text);
  }

  private submitTranscript(text: string): void {
    this.state.set('processing');
    this.speakingService.submitTurn(this.sessionId, text).subscribe({
      next: result => {
        this.lastResult.set(result);
        if (result.sessionComplete) {
          this.state.set('complete');
        } else {
          this.currentQuestion.set(result.aiReply);
          this.turnNumber.update(n => n + 1);
          this.transcript.set('');
          this.state.set('feedback');
        }
      },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Failed to process your response.');
        this.state.set('ready');
      },
    });
  }

  continueTurn(): void {
    this.lastResult.set(null);
    this.state.set('ready');
  }

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  private stopRecognition(): void {
    if (this.recognition) {
      this.recognition.abort();
      this.recognition = null;
    }
  }

  scoreColour(score: number | null): string {
    if (score === null) return 'text-slate-400';
    if (score >= 75) return 'text-green-600';
    if (score >= 50) return 'text-amber-600';
    return 'text-red-600';
  }

  isSpeechSupported(): boolean {
    return !!(window as any).SpeechRecognition || !!(window as any).webkitSpeechRecognition;
  }
}
