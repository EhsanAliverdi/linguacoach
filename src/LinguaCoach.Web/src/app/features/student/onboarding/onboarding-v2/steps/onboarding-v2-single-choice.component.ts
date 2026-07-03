import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step, OnboardingV2Option } from '../../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-single-choice',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-card" data-testid="step-single-choice">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>
      <div class="space-y-2 mb-6">
        <button
          *ngFor="let opt of step.options"
          class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
          [class.border-blue-500]="selectedKey === opt.key"
          [class.bg-blue-50]="selectedKey === opt.key"
          [class.border-slate-200]="selectedKey !== opt.key"
          (click)="select(opt)"
          [attr.data-testid]="'choice-option-' + opt.key"
        >
          {{ opt.label }}
        </button>
      </div>
      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <button class="sp-btn-primary w-full" [disabled]="!selectedKey" (click)="submit()">Continue</button>
    </div>
  `,
})
export class OnboardingV2SingleChoiceComponent implements OnChanges {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  selectedKey: string | null = null;
  error: string | null = null;

  // If a future flow uses two SingleChoice steps back to back, this component instance is
  // reused — reset state on every step change or the second step inherits the first's answer.
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['step'] && !changes['step'].firstChange) {
      this.selectedKey = null;
      this.error = null;
    }
  }

  select(opt: OnboardingV2Option): void {
    this.selectedKey = opt.key;
    this.error = null;
  }

  submit(): void {
    if (!this.selectedKey) {
      this.error = 'Please select an option.';
      return;
    }
    this.submitted.emit(JSON.stringify({ key: this.selectedKey }));
  }
}



