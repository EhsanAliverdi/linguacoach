import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivityService } from '../../../core/services/activity.service';
import { ActivityDto, ActivityFeedbackDto, FeedbackChangeDto, ListeningAnswer, VocabAnswer } from '../../../core/models/activity.models';

type PageState = 'loading' | 'learning' | 'writing' | 'submitting' | 'feedback' | 'error';

@Component({
  selector: 'app-activity-lesson',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './activity-lesson.component.html',
})
export class ActivityLessonComponent implements OnInit {
  state = signal<PageState>('loading');
  activity = signal<ActivityDto | null>(null);
  feedback = signal<ActivityFeedbackDto | null>(null);
  draftText = '';
  errorMessage = signal('');

  // Retry/improve tracking
  attemptCount = signal(0);
  previousScore = signal<number | null>(null);

  // Native-language explanation visibility (hidden by default)
  showNativeExplanation = signal(false);

  // VocabularyPractice state
  vocabAnswers: Record<string, string> = {};
  showHints: Record<string, boolean> = {};
  listeningAnswers: Record<string, string> = {};
  listeningResponseText = '';

  readonly stepDots = [
    { n: 1, key: 'learning', label: 'Lesson' },
    { n: 2, key: 'writing',  label: 'Practice' },
    { n: 3, key: 'feedback', label: 'Feedback' },
  ];

  constructor(
    private activityService: ActivityService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.activityService.getNext().subscribe({
      next: a => { this.activity.set(a); this.state.set('learning'); },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(this.extractError(err, 'Could not load activity. Please try again.'));
        this.state.set('error');
      },
    });
  }

  private extractError(err: HttpErrorResponse, fallback: string): string {
    const msg = err.error?.error ?? err.error?.message ?? fallback;
    const cid = err.error?.correlationId ?? err.headers?.get('x-correlation-id');
    return cid ? `${msg}\nReference: ${cid}` : msg;
  }

  stepState(key: string): 'done' | 'active' | 'future' {
    const order = ['learning', 'writing', 'feedback'];
    const current = this.state();
    const activeKey = current === 'submitting' ? 'writing' : current;
    const ki = order.indexOf(key);
    const ai = order.indexOf(activeKey);
    if (ki < ai) return 'done';
    if (ki === ai) return 'active';
    return 'future';
  }

  scoreRingColour(score: number | null): string {
    if (score === null) return 'var(--sp-faint)';
    if (score >= 85) return 'var(--sp-success)';
    if (score >= 70) return 'var(--sp-vocabulary)';
    return 'var(--sp-speaking)';
  }

  scoreBandLabel(score: number | null): string {
    if (score === null) return '';
    if (score >= 85) return 'Great work';
    if (score >= 70) return 'Good effort';
    return 'Keep going';
  }

  scoreImprovementMessage(): string {
    const current = this.feedback()?.score ?? null;
    const prev = this.previousScore();
    if (prev === null || current === null) return '';
    const diff = Math.round(current - prev);
    if (diff > 0) return `+${diff} — great improvement!`;
    if (diff < 0) return `${diff} — don't worry, keep practising.`;
    return 'Same score — try the suggestions above.';
  }

  categoryColour(category: string | null): string {
    switch (category) {
      case 'grammar': return 'var(--sp-writing)';
      case 'vocabulary': return 'var(--sp-vocabulary)';
      case 'tone': return 'var(--sp-listening)';
      case 'clarity': return 'var(--sp-pronunciation)';
      case 'structure': return 'var(--sp-speaking)';
      case 'punctuation': return 'var(--sp-muted)';
      default: return 'var(--sp-muted)';
    }
  }

  categoryLabel(category: string | null): string {
    if (!category) return '';
    return category.charAt(0).toUpperCase() + category.slice(1);
  }

  severityOrder(c: FeedbackChangeDto): number {
    switch (c.severity) {
      case 'high': return 0;
      case 'medium': return 1;
      default: return 2;
    }
  }

  isVocabPractice(): boolean {
    return this.activity()?.activityType === 'vocabularyPractice';
  }

  isListeningComprehension(): boolean {
    return this.activity()?.activityType === 'listeningComprehension';
  }

  vocabItemsFilled(): boolean {
    const items = this.activity()?.vocabItems ?? [];
    return items.length > 0 && items.every(i => (this.vocabAnswers[i.vocabularyItemId] ?? '').trim().length > 0);
  }

  toggleHint(itemId: string): void {
    this.showHints[itemId] = !this.showHints[itemId];
  }

  listeningItemsFilled(): boolean {
    const questions = this.activity()?.listeningQuestions ?? [];
    return questions.length > 0 && questions.every(q => (this.listeningAnswers[q.id] ?? '').trim().length > 0);
  }

  startPractice(): void {
    this.state.set('writing');
  }

  startWriting(): void {
    this.state.set('writing');
  }

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

  // Pre-fill textarea with previous submission and start a new attempt
  improveAnswer(): void {
    // draftText already contains the previous draft — just go back to writing
    this.state.set('writing');
  }

  tryAgain(): void {
    this.draftText = '';
    this.vocabAnswers = {};
    this.showHints = {};
    this.listeningAnswers = {};
    this.listeningResponseText = '';
    this.state.set('writing');
  }

  nextActivity(): void {
    this.state.set('loading');
    this.feedback.set(null);
    this.activity.set(null);
    this.draftText = '';
    this.vocabAnswers = {};
    this.showHints = {};
    this.listeningAnswers = {};
    this.listeningResponseText = '';
    this.attemptCount.set(0);
    this.previousScore.set(null);
    this.activityService.getNext().subscribe({
      next: a => { this.activity.set(a); this.state.set('learning'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not load next activity.');
        this.state.set('error');
      },
    });
  }

  backToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  isAiGenerated(): boolean {
    return this.activity()?.source === 'aiGenerated';
  }

  get wordCount(): number {
    return this.draftText.trim().split(/\s+/).filter(Boolean).length;
  }
}
