import { Component, Input, Output, EventEmitter } from '@angular/core';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-welcome',
  standalone: true,
  template: `
    <div class="sp-card text-center" data-testid="step-welcome">
      <h1 class="sp-heading-2 mb-3">{{ step.title }}</h1>
      <p class="text-slate-600 mb-8" *ngIf="step.description">{{ step.description }}</p>
      <button class="sp-btn-primary w-full" (click)="next()">Get started</button>
    </div>
  `,
  imports: [],
})
export class OnboardingV2WelcomeComponent {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  next(): void {
    this.submitted.emit(JSON.stringify({ value: 'acknowledged' }));
  }
}



