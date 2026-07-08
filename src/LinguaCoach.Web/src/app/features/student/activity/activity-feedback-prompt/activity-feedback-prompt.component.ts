import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ActivityFeedbackClarityRating,
  ActivityFeedbackDifficultyRating,
  ActivityFeedbackPolicyDto,
  ActivityFeedbackRepeatPreference,
  ActivityFeedbackUsefulnessRating,
} from '../../../../core/models/activity.models';
import { ActivityService } from '../../../../core/services/activity.service';

/** Phase B2 — student self-reported feedback on a completed activity: difficulty, clarity,
 * usefulness, and repeat preference. Shown from ActivityFeedbackPageComponent when the
 * submit-attempt response carries a non-'off' FeedbackPolicy. Skip is only offered when the
 * policy is 'optional'. */
@Component({
  selector: 'app-activity-feedback-prompt',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './activity-feedback-prompt.component.html',
})
export class ActivityFeedbackPromptComponent {
  @Input({ required: true }) policy!: ActivityFeedbackPolicyDto;
  @Input({ required: true }) learningActivityId!: string;
  @Input({ required: true }) attemptId!: string;

  difficulty = signal<ActivityFeedbackDifficultyRating | null>(null);
  clarity = signal<ActivityFeedbackClarityRating | null>(null);
  usefulness = signal<ActivityFeedbackUsefulnessRating | null>(null);
  repeatPreference = signal<ActivityFeedbackRepeatPreference | null>(null);
  comment = signal('');

  submitting = signal(false);
  submitted = signal(false);
  skipped = signal(false);
  errorMessage = signal<string | null>(null);

  readonly difficultyOptions: { value: ActivityFeedbackDifficultyRating; label: string }[] = [
    { value: 'tooEasy', label: 'Too easy' },
    { value: 'rightLevel', label: 'Right level' },
    { value: 'tooHard', label: 'Too hard' },
  ];

  readonly clarityOptions: { value: ActivityFeedbackClarityRating; label: string }[] = [
    { value: 'clear', label: 'Clear' },
    { value: 'okay', label: 'Okay' },
    { value: 'confusing', label: 'Confusing' },
  ];

  readonly usefulnessOptions: { value: ActivityFeedbackUsefulnessRating; label: string }[] = [
    { value: 'useful', label: 'Useful' },
    { value: 'notUseful', label: 'Not useful' },
  ];

  readonly repeatOptions: { value: ActivityFeedbackRepeatPreference; label: string }[] = [
    { value: 'moreLikeThis', label: 'More like this' },
    { value: 'neutral', label: "No preference" },
    { value: 'needRepeat', label: 'I need to repeat this' },
    { value: 'doNotShowSimilarSoon', label: "Don't show similar soon" },
  ];

  constructor(private _activityService: ActivityService) {}

  get isRequired(): boolean {
    return this.policy.policy === 'required';
  }

  get canSkip(): boolean {
    return this.policy.policy === 'optional';
  }

  get canSubmit(): boolean {
    return !!this.difficulty() && !!this.clarity() && !!this.usefulness() && !!this.repeatPreference();
  }

  skip(): void {
    this.skipped.set(true);
  }

  submit(): void {
    if (!this.canSubmit || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);

    this._activityService.submitAttemptFeedback(this.attemptId, {
      learningActivityId: this.learningActivityId,
      difficultyRating: this.difficulty()!,
      clarityRating: this.clarity()!,
      usefulnessRating: this.usefulness()!,
      repeatPreference: this.repeatPreference()!,
      optionalComment: this.comment().trim() || null,
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitted.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Could not save your feedback. Please try again.');
      },
    });
  }
}
