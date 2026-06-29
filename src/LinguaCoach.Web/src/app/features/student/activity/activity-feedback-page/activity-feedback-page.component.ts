import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityFeedbackDto, FeedbackChangeDto, SpeakingEvaluationDto } from '../../../../core/models/activity.models';
import { PatternEvaluationResultComponent } from '../pattern-evaluation-result/pattern-evaluation-result.component';
import { ActivityService } from '../../../../core/services/activity.service';
import { Subscription, interval } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';

/**
 * Page 3 of the Teach -> Practice -> Feedback flow.
 * Renders the pattern-evaluation result when present, otherwise the legacy
 * per-activity-type feedback sections. Shared by Today's Lesson and Practice Gym.
 */
@Component({
  selector: 'app-activity-feedback-page',
  standalone: true,
  imports: [CommonModule, PatternEvaluationResultComponent],
  templateUrl: './activity-feedback-page.component.html',
})
export class ActivityFeedbackPageComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) feedback!: ActivityFeedbackDto;
  @Input() attemptCount = 0;
  @Input() previousScore: number | null = null;
  @Input() activityId: string | null = null;
  @Input() attemptId: string | null = null;

  @Output() improveAnswer = new EventEmitter<void>();
  @Output() tryAgain = new EventEmitter<void>();
  @Output() nextActivity = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();

  showNativeExplanation = signal(false);
  evaluation = signal<SpeakingEvaluationDto | null>(null);
  evaluationLoading = signal(false);

  private _pollSub: Subscription | null = null;

  constructor(private _activityService: ActivityService) {}

  ngOnChanges(): void {
    if (!this.hasFeedbackContent && this.activityId && this.attemptId) {
      this._startEvaluationLoad();
    }
  }

  ngOnDestroy(): void {
    this._pollSub?.unsubscribe();
  }

  private _startEvaluationLoad(): void {
    this._pollSub?.unsubscribe();
    this.evaluationLoading.set(true);

    this._activityService.getAttemptEvaluation(this.activityId!, this.attemptId!).subscribe({
      next: dto => {
        this.evaluation.set(dto);
        this.evaluationLoading.set(false);
        if (dto.status === 'Pending' || dto.status === 'Evaluating') {
          this._startPolling();
        }
      },
      error: () => this.evaluationLoading.set(false),
    });
  }

  private _startPolling(): void {
    let polls = 0;
    this._pollSub = interval(10_000).pipe(
      switchMap(() => this._activityService.getAttemptEvaluation(this.activityId!, this.attemptId!)),
      takeWhile(dto => {
        polls++;
        const stillPending = dto.status === 'Pending' || dto.status === 'Evaluating';
        return stillPending && polls < 12;
      }, true),
    ).subscribe({
      next: dto => this.evaluation.set(dto),
    });
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
    const current = this.feedback.score ?? null;
    const prev = this.previousScore;
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

  trackChange(_index: number, change: FeedbackChangeDto): FeedbackChangeDto {
    return change;
  }

  get hasFeedbackContent(): boolean {
    const fb = this.feedback;
    return !!(
      fb.patternEvaluation ||
      fb.score !== null ||
      fb.coachSummary ||
      fb.changes.length ||
      fb.whatYouDidWell.length ||
      fb.questionFeedback?.length
    );
  }
}

