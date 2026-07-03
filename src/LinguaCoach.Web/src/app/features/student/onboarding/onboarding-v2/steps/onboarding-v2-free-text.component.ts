import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-free-text',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="sp-card" data-testid="step-free-text">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>
      <textarea
        class="sp-input w-full h-32 resize-none"
        [placeholder]="'Enter your answer'"
        [(ngModel)]="value"
        [maxlength]="maxLength"
        data-testid="free-text-input"
      ></textarea>
      <p class="text-xs text-slate-400 text-right mt-1">{{ value.length }}/{{ maxLength }}</p>
      <p *ngIf="error" class="text-red-600 text-sm mt-2">{{ error }}</p>
      <div class="flex gap-3 mt-4">
        <button class="sp-btn-primary flex-1" (click)="submit()">Continue</button>
        <button class="sp-btn-ghost flex-1" (click)="skip()">Skip</button>
      </div>
    </div>
  `,
})
export class OnboardingV2FreeTextComponent implements OnChanges {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  value = '';
  error: string | null = null;

  // Two consecutive steps of the same stepType (e.g. career_context then
  // learning_goal_description, both FreeText) reuse this same component instance —
  // Angular's *ngIf only re-renders on a truthiness change, not a step-key change — so
  // component state must be reset explicitly or the next step inherits the previous answer.
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['step'] && !changes['step'].firstChange) {
      this.value = '';
      this.error = null;
    }
  }

  get maxLength(): number {
    return this.step.validationMetadata?.maxLength ?? 500;
  }

  submit(): void {
    if (this.value.length > this.maxLength) {
      this.error = `Answer must not exceed ${this.maxLength} characters.`;
      return;
    }
    this.error = null;
    this.submitted.emit(JSON.stringify({ value: this.value.trim() }));
  }

  skip(): void {
    this.submitted.emit(JSON.stringify({ value: '' }));
  }
}



