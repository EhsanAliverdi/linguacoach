import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step } from '../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-preferred-name',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="sp-card" data-testid="step-preferred-name">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>
      <input
        class="sp-input w-full mb-2"
        type="text"
        placeholder="Your preferred name"
        [(ngModel)]="value"
        [maxlength]="maxLength"
        data-testid="preferred-name-input"
      />
      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <div class="flex gap-3 mt-4">
        <button class="sp-btn-primary flex-1" (click)="submit()">Continue</button>
        <button class="sp-btn-ghost flex-1" (click)="skip()">Skip for now</button>
      </div>
    </div>
  `,
})
export class OnboardingV2PreferredNameComponent {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  value = '';
  error: string | null = null;

  get maxLength(): number {
    return this.step.validationMetadata?.maxLength ?? 100;
  }

  submit(): void {
    if (this.value.length > this.maxLength) {
      this.error = `Name must not exceed ${this.maxLength} characters.`;
      return;
    }
    this.error = null;
    this.submitted.emit(JSON.stringify({ value: this.value.trim() }));
  }

  skip(): void {
    this.submitted.emit(JSON.stringify({ value: '' }));
  }
}
