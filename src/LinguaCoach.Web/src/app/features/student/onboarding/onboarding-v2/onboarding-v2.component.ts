import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OnboardingV2Service } from '../../../../core/services/onboarding-v2.service';
import { OnboardingV2Status, OnboardingV2Step } from '../../../../core/models/onboarding-v2.models';
import { OnboardingV2WelcomeComponent } from './steps/onboarding-v2-welcome.component';
import { OnboardingV2SummaryComponent } from './steps/onboarding-v2-summary.component';
import { OnboardingV2QuestionStepComponent } from './steps/onboarding-v2-question.component';

/**
 * Unified Question-Schema Phase 6b — routes to just 3 branches (Welcome / Summary / generic
 * Question) instead of 13 per-step-type components. Every question-shaped step (SingleChoice/
 * MultipleChoice/FreeText) renders through the same OnboardingV2QuestionStepComponent, which
 * itself delegates to the shared QuestionRendererComponent.
 */
@Component({
  selector: 'app-onboarding-v2',
  standalone: true,
  imports: [
    CommonModule,
    OnboardingV2WelcomeComponent,
    OnboardingV2SummaryComponent,
    OnboardingV2QuestionStepComponent,
  ],
  template: `
    <div class="sp-page">
      <div class="sp-narrow-shell">
        <!-- Brand header -->
        <div class="mb-8 text-center">
          <div class="sp-brand justify-center">
            <span class="sp-brand-mark">S</span>
            <span>SpeakPath</span>
          </div>
        </div>

        <div *ngIf="loading" class="text-center py-12 text-slate-500">Loading...</div>
        <div *ngIf="error" class="sp-alert-error mb-4">{{ error }}</div>

        <ng-container *ngIf="!loading && status">
          <!-- Progress bar -->
          <div class="mb-6" *ngIf="!status.isComplete">
            <div class="flex justify-between text-xs text-slate-500 mb-1">
              <span>{{ currentStep?.categoryName ?? 'Progress' }}</span>
              <span>{{ status.percentageComplete }}%</span>
            </div>
            <div class="w-full bg-slate-200 rounded-full h-2">
              <div
                class="bg-blue-600 h-2 rounded-full transition-all duration-300"
                [style.width.%]="status.percentageComplete"
                data-testid="onboarding-progress-bar"
                [attr.aria-valuenow]="status.percentageComplete"
              ></div>
            </div>
          </div>

          <!-- Step renderer -->
          <ng-container *ngIf="currentStep">
            <app-onboarding-v2-welcome
              *ngIf="currentStep.stepType === 'Welcome'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-summary
              *ngIf="currentStep.stepType === 'Summary'"
              [step]="currentStep"
              [status]="status"
              (completed)="onCompleted()"
            />
            <app-onboarding-v2-question
              *ngIf="currentStep.stepType !== 'Welcome' && currentStep.stepType !== 'Summary'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
          </ng-container>
        </ng-container>

        <div *ngIf="submitError" class="sp-alert-error mt-4">{{ submitError }}</div>
      </div>
    </div>
  `,
})
export class OnboardingV2Component implements OnInit {
  status: OnboardingV2Status | null = null;
  loading = true;
  error: string | null = null;
  submitError: string | null = null;

  constructor(
    private svc: OnboardingV2Service,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get currentStep(): OnboardingV2Step | null {
    if (!this.status || this.status.isComplete) return null;
    return this.status.steps.find(s => s.stepKey === this.status!.currentStepKey) ?? null;
  }

  private load(): void {
    this.loading = true;
    this.error = null;
    this.svc.getStatus().subscribe({
      next: status => {
        this.status = status;
        this.loading = false;
        if (status.isComplete) {
          this.router.navigate(['/dashboard']);
        }
      },
      error: () => {
        this.error = 'Could not load onboarding. Please refresh and try again.';
        this.loading = false;
      },
    });
  }

  onStepSubmitted(answerJson: string): void {
    if (!this.status?.currentStepKey) return;
    this.submitError = null;
    const stepKey = this.status.currentStepKey;
    this.svc.submitStep(stepKey, answerJson).subscribe({
      next: result => {
        if (this.status) {
          this.status = {
            ...this.status,
            currentStepKey: result.currentStepKey,
            completedStepKeys: result.completedStepKeys,
            percentageComplete: result.percentageComplete,
            isComplete: result.isComplete,
          };
        }
        if (result.isComplete) {
          this.triggerComplete();
        }
      },
      error: (err) => {
        this.submitError = err?.error?.error ?? 'Could not save your answer. Please try again.';
      },
    });
  }

  onCompleted(): void {
    // The summary step's "Start learning" button never went through onStepSubmitted, so
    // "summary" (a SystemRequired step) was never added to CompletedStepKeys -- /complete
    // always rejected with "Required steps not completed: summary". Submit it explicitly
    // before completing.
    const stepKey = this.status?.currentStepKey;
    if (!stepKey) {
      this.triggerComplete();
      return;
    }
    this.submitError = null;
    this.svc.submitStep(stepKey, '{}').subscribe({
      next: () => this.triggerComplete(),
      error: (err) => {
        this.submitError = err?.error?.error ?? 'Could not save your answer. Please try again.';
      },
    });
  }

  private triggerComplete(): void {
    this.svc.complete().subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.submitError = err?.error?.error ?? 'Could not complete onboarding. Please try again.';
      },
    });
  }
}
