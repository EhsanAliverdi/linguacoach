import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WritingEvaluationDto } from '../../../../core/models/activity.models';
import { FeedbackAiDisclaimerComponent } from './feedback-ai-disclaimer.component';
import { FeedbackPendingStateComponent } from './feedback-pending-state.component';

@Component({
  selector: 'app-feedback-writing-eval',
  standalone: true,
  imports: [CommonModule, FeedbackAiDisclaimerComponent, FeedbackPendingStateComponent],
  templateUrl: './feedback-writing-eval.component.html',
})
export class FeedbackWritingEvalComponent {
  @Input() eval: WritingEvaluationDto | null = null;
  @Input() loading = false;

  get isCompleted(): boolean {
    return this.eval?.status === 'Completed';
  }

  get isPending(): boolean {
    return !this.eval || this.eval.status === 'Pending' || this.eval.status === 'Evaluating';
  }

  get isFailed(): boolean {
    return this.eval?.status === 'Failed';
  }

  get isNotSupported(): boolean {
    return this.eval?.status === 'NotSupported' || this.eval?.status === 'Skipped';
  }

  scorePercent(value: number | null | undefined): string {
    if (value == null) return '--';
    return `${Math.round(value * 100)}%`;
  }
}
