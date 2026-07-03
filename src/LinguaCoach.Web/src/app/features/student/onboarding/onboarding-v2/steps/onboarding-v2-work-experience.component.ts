import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step, OnboardingV2Option } from '../../../../../core/models/onboarding-v2.models';

interface FamiliarityOption { key: string; label: string; }

// roleFamiliarity is a fixed universal enum (not admin-customizable via OptionsJson,
// which only carries one option list — used here for experienceLevel).
const ROLE_FAMILIARITY_OPTIONS: FamiliarityOption[] = [
  { key: 'NewToRole', label: 'New to this type of role' },
  { key: 'UnderstandsBasics', label: 'Understand the basics' },
  { key: 'CurrentlyWorkingInRole', label: 'Currently working in this role' },
  { key: 'ExperiencedInRole', label: 'Experienced in this role' },
  { key: 'ManagesOrTrainsOthers', label: 'Manage or train others in this role' },
];

@Component({
  selector: 'app-onboarding-v2-work-experience',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-card" data-testid="step-work-experience">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>

      <p class="text-sm font-semibold text-slate-700 mb-2">How many years of professional experience do you have?</p>
      <div class="space-y-2 mb-6">
        <button
          *ngFor="let opt of step.options"
          class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
          [class.border-blue-500]="selectedExperience === opt.key"
          [class.bg-blue-50]="selectedExperience === opt.key"
          [class.border-slate-200]="selectedExperience !== opt.key"
          (click)="selectedExperience = opt.key; error = null"
          [attr.data-testid]="'work-experience-level-' + opt.key"
        >
          {{ opt.label }}
        </button>
      </div>

      <p class="text-sm font-semibold text-slate-700 mb-2">How familiar are you with your current type of role?</p>
      <div class="space-y-2 mb-6">
        <button
          *ngFor="let opt of familiarityOptions"
          class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
          [class.border-blue-500]="selectedFamiliarity === opt.key"
          [class.bg-blue-50]="selectedFamiliarity === opt.key"
          [class.border-slate-200]="selectedFamiliarity !== opt.key"
          (click)="selectedFamiliarity = opt.key; error = null"
          [attr.data-testid]="'work-experience-familiarity-' + opt.key"
        >
          {{ opt.label }}
        </button>
      </div>

      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <button class="sp-btn-primary w-full" [disabled]="!selectedExperience || !selectedFamiliarity" (click)="submit()">Continue</button>
      <button class="mt-3 w-full text-center text-sm text-slate-500 underline hover:text-slate-700" (click)="skip()">
        Skip for now
      </button>
    </div>
  `,
})
export class OnboardingV2WorkExperienceComponent {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  familiarityOptions = ROLE_FAMILIARITY_OPTIONS;
  selectedExperience: string | null = null;
  selectedFamiliarity: string | null = null;
  error: string | null = null;

  submit(): void {
    if (!this.selectedExperience || !this.selectedFamiliarity) {
      this.error = 'Please answer both questions, or skip.';
      return;
    }
    this.submitted.emit(JSON.stringify({
      experienceLevel: this.selectedExperience,
      roleFamiliarity: this.selectedFamiliarity,
    }));
  }

  skip(): void {
    this.submitted.emit(JSON.stringify({}));
  }
}
