import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityFeedbackDto, ActivityType, FeedbackChangeDto, SpeakingEvaluationDto, StageContentDto, WritingEvaluationDto } from '../../../../core/models/activity.models';
import { PatternEvaluationResultComponent } from '../pattern-evaluation-result/pattern-evaluation-result.component';
import { ActivityService } from '../../../../core/services/activity.service';
import { Subscription, interval } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';
import { FeedbackAiDisclaimerComponent } from '../feedback/feedback-ai-disclaimer.component';
import { FeedbackWritingEvalComponent } from '../feedback/feedback-writing-eval.component';
import { FeedbackNextStepsComponent } from '../feedback/feedback-next-steps.component';
import { FeedbackSkillContextComponent } from '../feedback/feedback-skill-context.component';
import { FeedbackSupportLangComponent } from '../feedback/feedback-support-lang.component';
import { ActivityFeedbackPromptComponent } from '../activity-feedback-prompt/activity-feedback-prompt.component';

@Component({
  selector: 'app-activity-feedback-page',
  standalone: true,
  imports: [
    CommonModule,
    PatternEvaluationResultComponent,
    FeedbackAiDisclaimerComponent,
    FeedbackWritingEvalComponent,
    FeedbackNextStepsComponent,
    FeedbackSkillContextComponent,
    FeedbackSupportLangComponent,
    ActivityFeedbackPromptComponent,
  ],
  templateUrl: './activity-feedback-page.component.html',
})
export class ActivityFeedbackPageComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) feedback!: ActivityFeedbackDto;
  @Input() attemptCount = 0;
  @Input() previousScore: number | null = null;
  @Input() activityId: string | null = null;
  @Input() attemptId: string | null = null;
  @Input() activityType: ActivityType | null = null;
  @Input() stageContent: StageContentDto | null = null;

  @Output() improveAnswer = new EventEmitter<void>();
  @Output() tryAgain = new EventEmitter<void>();
  @Output() nextActivity = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();

  showNativeExplanation = signal(false);
  evaluation = signal<SpeakingEvaluationDto | null>(null);
  evaluationLoading = signal(false);
  writingEvaluation = signal<WritingEvaluationDto | null>(null);
  writingEvaluationLoading = signal(false);

  private _pollSub: Subscription | null = null;
  private _writingPollSub: Subscription | null = null;

  constructor(private _activityService: ActivityService) {}

  ngOnChanges(): void {
    if (!this.hasFeedbackContent && this.activityId && this.attemptId) {
      this._startEvaluationLoad();
    }
    if (this.isWritingActivity && this.activityId && this.attemptId) {
      this._loadWritingEvaluation();
    }
  }

  ngOnDestroy(): void {
    this._pollSub?.unsubscribe();
    this._writingPollSub?.unsubscribe();
  }

  get isWritingActivity(): boolean {
    return this.activityType === 'writingScenario';
  }

  get isSpeakingActivity(): boolean {
    return this.activityType === 'speakingRolePlay' || this.activityType === 'pronunciationPractice';
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

  private _loadWritingEvaluation(): void {
    this._writingPollSub?.unsubscribe();
    this.writingEvaluationLoading.set(true);

    this._activityService.getWritingEvaluation(this.activityId!, this.attemptId!).subscribe({
      next: dto => {
        this.writingEvaluation.set(dto);
        this.writingEvaluationLoading.set(false);
        if (dto.status === 'Pending' || dto.status === 'Evaluating') {
          this._startWritingPolling();
        }
      },
      error: () => this.writingEvaluationLoading.set(false),
    });
  }

  private _startWritingPolling(): void {
    let polls = 0;
    this._writingPollSub = interval(8_000).pipe(
      switchMap(() => this._activityService.getWritingEvaluation(this.activityId!, this.attemptId!)),
      takeWhile(dto => {
        polls++;
        const stillPending = dto.status === 'Pending' || dto.status === 'Evaluating';
        return stillPending && polls < 15;
      }, true),
    ).subscribe({
      next: dto => this.writingEvaluation.set(dto),
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

  get showFeedbackPrompt(): boolean {
    return !!this.feedback.feedbackPolicy
      && this.feedback.feedbackPolicy.policy !== 'off'
      && !!this.activityId
      && !!this.attemptId;
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
