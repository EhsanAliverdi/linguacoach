import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivityService } from '../../../core/services/activity.service';
import { ActivityDto, ActivityFeedbackDto, ListeningAnswer, VocabAnswer } from '../../../core/models/activity.models';
import { ExerciseAnswerPayload } from '../exercise-renderer/exercise-renderer.component';
import { ActivityTeachPageComponent } from '../activity-teach-page/activity-teach-page.component';
import { ActivityPracticePageComponent } from '../activity-practice-page/activity-practice-page.component';
import { ActivityFeedbackPageComponent } from '../activity-feedback-page/activity-feedback-page.component';

type PageState =
  | 'loading' | 'learning' | 'writing' | 'submitting' | 'feedback' | 'error'
  | 'mic-unsupported' | 'mic-permission' | 'mic-denied'
  | 'ready' | 'recording' | 'recorded' | 'submitting-audio';

@Component({
  selector: 'app-activity-lesson',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    ActivityTeachPageComponent, ActivityPracticePageComponent, ActivityFeedbackPageComponent,
  ],
  templateUrl: './activity-lesson.component.html',
})
export class ActivityLessonComponent implements OnInit, OnDestroy {
  state = signal<PageState>('loading');
  activity = signal<ActivityDto | null>(null);
  feedback = signal<ActivityFeedbackDto | null>(null);
  draftText = '';
  errorMessage = signal('');

  // Retry/improve tracking
  attemptCount = signal(0);
  previousScore = signal<number | null>(null);

  // VocabularyPractice state
  vocabAnswers: Record<string, string> = {};
  showHints: Record<string, boolean> = {};
  listeningAnswers: Record<string, string> = {};
  listeningResponseText = '';

  // SpeakingRolePlay state
  private mediaRecorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];
  recordingMimeType = '';
  audioBlob: Blob | null = null;
  audioBlobUrl: string | null = null;
  recordingStartTime: number | null = null;
  recordingDurationSeconds: number | null = null;
  private activityAudioBlobUrl: string | null = null;

  readonly stepDots = [
    { n: 1, key: 'learning', label: 'Lesson' },
    { n: 2, key: 'writing',  label: 'Practice' },
    { n: 3, key: 'feedback', label: 'Feedback' },
  ];

  constructor(
    private activityService: ActivityService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(() => {
      this.resetState();
      this.loadActivity();
    });
  }

  ngOnDestroy(): void {
    this.cleanupRecording();
    this.cleanupActivityAudio();
    if (this.audioBlobUrl) {
      URL.revokeObjectURL(this.audioBlobUrl);
    }
  }

  private resetState(): void {
    this.draftText = '';
    this.vocabAnswers = {};
    this.showHints = {};
    this.listeningAnswers = {};
    this.listeningResponseText = '';
    this.cleanupRecording();
    this.cleanupActivityAudio();
    this.activity.set(null);
    this.feedback.set(null);
    this.attemptCount.set(0);
    this.previousScore.set(null);
    this.errorMessage.set('');
  }

  private cleanupRecording(): void {
    if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
      this.mediaRecorder.stop();
    }
    this.mediaRecorder = null;
    this.recordedChunks = [];
    if (this.audioBlobUrl) {
      URL.revokeObjectURL(this.audioBlobUrl);
      this.audioBlobUrl = null;
    }
    this.audioBlob = null;
    this.recordingMimeType = '';
    this.recordingStartTime = null;
    this.recordingDurationSeconds = null;
  }

  private cleanupActivityAudio(): void {
    if (this.activityAudioBlobUrl) {
      URL.revokeObjectURL(this.activityAudioBlobUrl);
      this.activityAudioBlobUrl = null;
    }
  }

  loadActivity(): void {
    this.state.set('loading');
    const specificId = this.route.snapshot.queryParamMap.get('activityId');
    const patternKey = this.route.snapshot.queryParamMap.get('pattern') ?? undefined;
    const obs = specificId
      ? this.activityService.getById(specificId)
      : this.activityService.getNext(patternKey ? undefined : this.preferredActivityType(), patternKey);
    obs.subscribe({
      next: a => {
        this.setActivityWithAudio(a);
      },
      error: (err: HttpErrorResponse) => {
        const msg = err.status === 503
          ? 'The AI service is not available. Please try again shortly.'
          : this.extractError(err, 'Could not load activity. Please try again.');
        this.errorMessage.set(msg);
        this.state.set('error');
      },
    });
  }

  private setActivityWithAudio(activity: ActivityDto): void {
    if (!activity.audioAvailable || !activity.audioUrl) {
      this.setReadyActivity(activity);
      return;
    }

    this.activityService.getAudioBlobUrl(activity.audioUrl).subscribe({
      next: blobUrl => {
        this.cleanupActivityAudio();
        this.activityAudioBlobUrl = blobUrl;
        this.setReadyActivity({ ...activity, audioUrl: blobUrl });
      },
      error: () => {
        this.setReadyActivity({
          ...activity,
          audioAvailable: false,
          audioUrl: null,
          audioUnavailableMessage: activity.audioUnavailableMessage
            || 'Audio is temporarily unavailable. Complete this as a transcript-based listening practice.',
        });
      },
    });
  }

  private setReadyActivity(activity: ActivityDto): void {
    this.activity.set(activity);
    if (activity.activityType === 'speakingRolePlay') {
      this.initSpeakingState();
    } else {
      this.state.set('learning');
    }
  }

  private initSpeakingState(): void {
    if (!navigator.mediaDevices || !window.MediaRecorder) {
      this.state.set('mic-unsupported');
      return;
    }
    this.state.set('learning');
  }

  private preferredActivityType(): ActivityDto['activityType'] | undefined {
    const raw = this.route.snapshot.queryParamMap.get('type');
    switch (raw) {
      case 'WritingScenario':
      case 'writingScenario': return 'writingScenario';
      case 'VocabularyPractice':
      case 'vocabularyPractice': return 'vocabularyPractice';
      case 'ListeningComprehension':
      case 'listeningComprehension': return 'listeningComprehension';
      case 'SpeakingRolePlay':
      case 'speakingRolePlay': return 'speakingRolePlay';
      default: return undefined;
    }
  }

  private extractError(err: HttpErrorResponse, fallback: string): string {
    const msg = err.error?.error ?? err.error?.message ?? fallback;
    const cid = err.error?.correlationId ?? err.headers?.get('x-correlation-id');
    return cid ? `${msg}\nReference: ${cid}` : msg;
  }

  stepState(key: string): 'done' | 'active' | 'future' {
    const order = ['learning', 'writing', 'feedback'];
    const current = this.state();
    const activeKey = ['submitting', 'mic-permission', 'ready', 'recording', 'recorded', 'submitting-audio'].includes(current)
      ? 'writing' : current;
    const ki = order.indexOf(key);
    const ai = order.indexOf(activeKey);
    if (ki < ai) return 'done';
    if (ki === ai) return 'active';
    return 'future';
  }

  isVocabPractice(): boolean {
    return this.activity()?.activityType === 'vocabularyPractice';
  }

  isListeningComprehension(): boolean {
    return this.activity()?.activityType === 'listeningComprehension';
  }

  isSpeakingRolePlay(): boolean {
    return this.activity()?.activityType === 'speakingRolePlay';
  }

  usesExerciseRenderer(): boolean {
    const activity = this.activity();
    if (activity?.activityType === 'speakingRolePlay') return false;
    return activity?.interactionMode != null
      || (activity?.activityType === 'writingScenario' && !!activity.contentJson);
  }

  rendererSkillLabel(): string {
    switch (this.activity()?.interactionMode) {
      case 'matchingPairs':
      case 'gapFill':
        return 'Vocabulary';
      case 'audioAndFreeText':
      case 'audioAndGapFill':
        return 'Listening';
      case 'readOnly':
        return 'Reflection';
      case 'chatReply':
      case 'freeTextEntry':
      default:
        return this.activity()?.activityType === 'speakingRolePlay' ? 'Speaking' : 'Writing';
    }
  }

  toggleHint(itemId: string): void {
    this.showHints[itemId] = !this.showHints[itemId];
  }

  startPractice(): void {
    if (this.isSpeakingRolePlay()) {
      this.requestMicPermission();
    } else {
      this.state.set('writing');
    }
  }

  startWriting(): void {
    this.state.set('writing');
  }

  setDraftText(value: string): void {
    this.draftText = value;
  }

  setListeningResponseText(value: string): void {
    this.listeningResponseText = value;
  }

  // ── Speaking: microphone + recording ──────────────────────────────────────

  requestMicPermission(): void {
    this.state.set('mic-permission');
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(stream => {
        stream.getTracks().forEach(t => t.stop()); // release immediately; re-request on record
        this.state.set('ready');
      })
      .catch(() => {
        this.state.set('mic-denied');
      });
  }

  startRecording(): void {
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(stream => {
        this.recordedChunks = [];
        const recorder = new MediaRecorder(stream);
        this.recordingMimeType = recorder.mimeType || 'audio/webm';
        this.recordingStartTime = Date.now();
        recorder.ondataavailable = e => { if (e.data.size > 0) this.recordedChunks.push(e.data); };
        recorder.onstop = () => {
          stream.getTracks().forEach(t => t.stop());
          this.audioBlob = new Blob(this.recordedChunks, { type: this.recordingMimeType });
          if (this.audioBlobUrl) URL.revokeObjectURL(this.audioBlobUrl);
          this.audioBlobUrl = URL.createObjectURL(this.audioBlob);
          if (this.recordingStartTime) {
            this.recordingDurationSeconds = (Date.now() - this.recordingStartTime) / 1000;
          }
          this.state.set('recorded');
        };
        recorder.start();
        this.mediaRecorder = recorder;
        this.state.set('recording');
      })
      .catch(() => {
        this.state.set('mic-denied');
      });
  }

  stopRecording(): void {
    if (this.mediaRecorder && this.mediaRecorder.state === 'recording') {
      this.mediaRecorder.stop();
    }
  }

  reRecord(): void {
    if (this.audioBlobUrl) {
      URL.revokeObjectURL(this.audioBlobUrl);
      this.audioBlobUrl = null;
    }
    this.audioBlob = null;
    this.recordedChunks = [];
    this.state.set('ready');
  }

  submitRecording(): void {
    const a = this.activity();
    if (!a || !this.audioBlob) return;
    this.state.set('submitting-audio');
    this.activityService.submitSpeakingAttempt(
      a.activityId,
      this.audioBlob,
      this.recordingMimeType,
      this.recordingDurationSeconds ?? undefined,
    ).subscribe({
      next: fb => {
        this.previousScore.set(this.feedback()?.score ?? null);
        this.feedback.set(fb);
        this.attemptCount.update(n => n + 1);
        this.state.set('feedback');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(this.extractError(err, 'Could not process your recording. Please try again.'));
        this.state.set('recorded');
      },
    });
  }

  // ── General submission ─────────────────────────────────────────────────────

  onSubmitVocab(): void {
    const a = this.activity();
    if (!a?.vocabItems?.length) return;
    const answers: VocabAnswer[] = a.vocabItems.map(item => ({
      vocabularyItemId: item.vocabularyItemId,
      answer: this.vocabAnswers[item.vocabularyItemId] ?? '',
    }));
    this.state.set('submitting');
    this.activityService.submitVocabAttempt(a.activityId, answers).subscribe({
      next: fb => {
        this.previousScore.set(this.feedback()?.score ?? null);
        this.feedback.set(fb);
        this.attemptCount.update(n => n + 1);
        this.state.set('feedback');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(this.extractError(err, 'Failed to submit answers. Please try again.'));
        this.state.set('writing');
      },
    });
  }

  onSubmitListening(): void {
    const a = this.activity();
    if (!a?.listeningQuestions?.length) return;
    const answers: ListeningAnswer[] = a.listeningQuestions.map(q => ({
      questionId: q.id,
      answer: this.listeningAnswers[q.id] ?? '',
    }));
    this.state.set('submitting');
    this.activityService.submitListeningAttempt(a.activityId, answers, this.listeningResponseText).subscribe({
      next: fb => {
        this.previousScore.set(this.feedback()?.score ?? null);
        this.feedback.set(fb);
        this.attemptCount.update(n => n + 1);
        this.state.set('feedback');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(this.extractError(err, 'Failed to submit listening answers. Please try again.'));
        this.state.set('writing');
      },
    });
  }

  onSubmit(): void {
    const a = this.activity();
    if (!this.draftText.trim() || !a) return;
    this.state.set('submitting');
    this.activityService.submitAttempt(a.activityId, this.draftText).subscribe({
      next: fb => {
        this.previousScore.set(this.feedback()?.score ?? null);
        this.feedback.set(fb);
        this.attemptCount.update(n => n + 1);
        this.state.set('feedback');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(this.extractError(err, 'Failed to get feedback. Please try again.'));
        this.state.set('writing');
      },
    });
  }

  onRendererSubmit(payload: ExerciseAnswerPayload): void {
    const a = this.activity();
    if (!a) return;

    this.state.set('submitting');
    const handleError = (err: HttpErrorResponse) => {
      this.errorMessage.set(this.extractError(err, 'Failed to submit answers. Please try again.'));
      this.state.set('writing');
    };
    const handleFeedback = (fb: ActivityFeedbackDto) => {
      this.previousScore.set(this.feedback()?.score ?? null);
      this.feedback.set(fb);
      this.attemptCount.update(n => n + 1);
      this.state.set('feedback');
    };

    if (payload.kind === 'audioFreeText') {
      this.activityService.submitListeningAttempt(a.activityId, payload.answers, payload.responseText).subscribe({
        next: handleFeedback,
        error: handleError,
      });
      return;
    }

    if (payload.kind === 'audioGapFill') {
      this.activityService.submitListeningAttempt(a.activityId, payload.answers, '').subscribe({
        next: handleFeedback,
        error: handleError,
      });
      return;
    }

    let submittedContent: string;
    if (payload.kind === 'freeText') {
      submittedContent = payload.text;
    } else if (payload.kind === 'chatReply') {
      submittedContent = payload.replyText;
    } else if (payload.kind === 'matchingPairs') {
      // Convert to backend's expected { pairs: { phraseKey: meaningKey } } format
      const pairs: Record<string, string> = {};
      for (const p of payload.submittedPairs) {
        pairs[p.leftId] = p.rightId;
      }
      submittedContent = JSON.stringify({ pairs });
    } else if (payload.kind === 'gapFill') {
      // Convert to backend's expected { answers: { gapId: value } } format
      const answers: Record<string, string> = {};
      for (const a of payload.answers) {
        answers[a.gapId] = a.value;
      }
      submittedContent = JSON.stringify({ answers });
    } else if (payload.kind === 'emailReply') {
      submittedContent = JSON.stringify({ subject: payload.subject, body: payload.body });
    } else {
      submittedContent = JSON.stringify(payload);
    }

    this.activityService.submitAttempt(a.activityId, submittedContent).subscribe({
      next: handleFeedback,
      error: handleError,
    });
  }

  onReadOnlyDone(): void {
    this.nextActivity();
  }

  improveAnswer(): void {
    this.state.set('writing');
  }

  tryAgain(): void {
    this.draftText = '';
    this.vocabAnswers = {};
    this.showHints = {};
    this.listeningAnswers = {};
    this.listeningResponseText = '';
    if (this.isSpeakingRolePlay()) {
      this.cleanupRecording();
      this.state.set('ready');
    } else {
      this.state.set('writing');
    }
  }

  nextActivity(): void {
    const returnTo = this.route.snapshot.queryParamMap.get('returnTo');
    if (returnTo) {
      // In lesson context — go back to the lesson page, not the next activity.
      this.router.navigateByUrl(returnTo);
    } else {
      this.resetState();
      this.loadActivity();
    }
  }

  backToDashboard(): void {
    const returnTo = this.route.snapshot.queryParamMap.get('returnTo');
    this.router.navigateByUrl(returnTo ?? '/dashboard');
  }

  isAiGenerated(): boolean {
    return this.activity()?.source === 'aiGenerated';
  }
}
