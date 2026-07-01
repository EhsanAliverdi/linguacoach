import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EvaluationStatus } from '../../../../core/models/activity.models';

@Component({
  selector: 'app-feedback-pending-state',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feedback-pending-state.component.html',
})
export class FeedbackPendingStateComponent {
  @Input({ required: true }) status!: EvaluationStatus;
  @Input() label: 'speaking' | 'writing' = 'speaking';

  get isEvaluating(): boolean {
    return this.status === 'Pending' || this.status === 'Evaluating';
  }

  get isFailed(): boolean {
    return this.status === 'Failed';
  }
}
