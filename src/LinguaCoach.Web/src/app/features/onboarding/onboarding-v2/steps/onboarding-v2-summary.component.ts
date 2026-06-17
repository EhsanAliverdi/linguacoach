import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step, OnboardingV2Status } from '../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-card text-center" data-testid="step-summary">
      <div class="text-4xl mb-4">✓</div>
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>
      <p class="text-xs text-slate-400 mb-6" *ngIf="status.preliminaryCefrLevel">
        Estimated starting level: <strong>{{ status.preliminaryCefrLevel }}</strong>
        — this is a rough guide and will be refined after your placement check.
      </p>
      <button
        class="sp-btn-primary w-full"
        (click)="completed.emit()"
        data-testid="onboarding-complete-btn"
      >
        Start learning
      </button>
    </div>
  `,
})
export class OnboardingV2SummaryComponent {
  @Input() step!: OnboardingV2Step;
  @Input() status!: OnboardingV2Status;
  @Output() completed = new EventEmitter<void>();
}
