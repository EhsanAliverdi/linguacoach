import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityType } from '../../../../core/models/activity.models';

@Component({
  selector: 'app-feedback-next-steps',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feedback-next-steps.component.html',
})
export class FeedbackNextStepsComponent {
  @Input() activityType: ActivityType | null = null;
  @Input() hasPatternResult = false;

  @Output() improve = new EventEmitter<void>();
  @Output() tryAgain = new EventEmitter<void>();
  @Output() nextActivity = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();

  get showImprove(): boolean {
    return this.activityType === 'writingScenario';
  }

  get showTryAgain(): boolean {
    return this.activityType !== 'speakingRolePlay' && this.activityType !== 'pronunciationPractice';
  }
}
