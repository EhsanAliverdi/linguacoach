import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OnboardingV2Service } from '../../../../core/services/onboarding-v2.service';
import { OnboardingV2Status, OnboardingV2Step } from '../../../../core/models/onboarding-v2.models';
import { OnboardingV2WelcomeComponent } from './steps/onboarding-v2-welcome.component';
import { OnboardingV2PreferredNameComponent } from './steps/onboarding-v2-preferred-name.component';
import { OnboardingV2SupportLanguageComponent } from './steps/onboarding-v2-support-language.component';
import { OnboardingV2LearningGoalsComponent } from './steps/onboarding-v2-learning-goals.component';
import { OnboardingV2FocusAreasComponent } from './steps/onboarding-v2-focus-areas.component';
import { OnboardingV2DifficultyComponent } from './steps/onboarding-v2-difficulty.component';
import { OnboardingV2SingleChoiceComponent } from './steps/onboarding-v2-single-choice.component';
import { OnboardingV2MultipleChoiceComponent } from './steps/onboarding-v2-multiple-choice.component';
import { OnboardingV2FreeTextComponent } from './steps/onboarding-v2-free-text.component';
import { OnboardingV2AssessmentComponent } from './steps/onboarding-v2-assessment.component';
import { OnboardingV2SummaryComponent } from './steps/onboarding-v2-summary.component';
import { OnboardingV2SessionDurationComponent } from './steps/onboarding-v2-session-duration.component';
import { OnboardingV2WorkExperienceComponent } from './steps/onboarding-v2-work-experience.component';

@Component({
  selector: 'app-onboarding-v2',
  standalone: true,
  imports: [
    CommonModule,
    OnboardingV2WelcomeComponent,
    OnboardingV2PreferredNameComponent,
    OnboardingV2SupportLanguageComponent,
    OnboardingV2LearningGoalsComponent,
    OnboardingV2FocusAreasComponent,
    OnboardingV2DifficultyComponent,
    OnboardingV2SingleChoiceComponent,
    OnboardingV2MultipleChoiceComponent,
    OnboardingV2FreeTextComponent,
    OnboardingV2AssessmentComponent,
    OnboardingV2SummaryComponent,
    OnboardingV2SessionDurationComponent,
    OnboardingV2WorkExperienceComponent,
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
              <span>Progress</span>
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
            <app-onboarding-v2-preferred-name
              *ngIf="currentStep.stepType === 'PreferredName'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-support-language
              *ngIf="currentStep.stepType === 'SupportLanguage'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-learning-goals
              *ngIf="currentStep.stepType === 'LearningGoals'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-focus-areas
              *ngIf="currentStep.stepType === 'FocusAreas'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-difficulty
              *ngIf="currentStep.stepType === 'DifficultyPreference'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-single-choice
              *ngIf="currentStep.stepType === 'SingleChoice'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-multiple-choice
              *ngIf="currentStep.stepType === 'MultipleChoice'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-free-text
              *ngIf="currentStep.stepType === 'FreeText'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-assessment
              *ngIf="currentStep.stepType === 'AssessmentQuestion'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-summary
              *ngIf="currentStep.stepType === 'Summary'"
              [step]="currentStep"
              [status]="status"
              (completed)="onCompleted()"
            />
            <app-onboarding-v2-session-duration
              *ngIf="currentStep.stepType === 'SessionDuration'"
              [step]="currentStep"
              (submitted)="onStepSubmitted($event)"
            />
            <app-onboarding-v2-work-experience
              *ngIf="currentStep.stepType === 'WorkExperience'"
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
    this.triggerComplete();
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

