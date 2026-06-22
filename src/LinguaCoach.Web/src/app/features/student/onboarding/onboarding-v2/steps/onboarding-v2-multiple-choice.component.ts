import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-multiple-choice',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-card" data-testid="step-multiple-choice">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-4" *ngIf="step.description">{{ step.description }}</p>
      <p class="text-xs text-slate-500 mb-4" *ngIf="maxSelections < 100">Select up to {{ maxSelections }}</p>
      <div class="space-y-2 mb-6">
        <button
          *ngFor="let opt of step.options"
          class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
          [class.border-blue-500]="selectedKeys.has(opt.key)"
          [class.bg-blue-50]="selectedKeys.has(opt.key)"
          [class.border-slate-200]="!selectedKeys.has(opt.key)"
          (click)="toggle(opt.key)"
          [attr.data-testid]="'multi-option-' + opt.key"
        >
          {{ opt.label }}
        </button>
      </div>
      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <button class="sp-btn-primary w-full" (click)="submit()">Continue</button>
    </div>
  `,
})
export class OnboardingV2MultipleChoiceComponent {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  selectedKeys = new Set<string>();
  error: string | null = null;

  get maxSelections(): number {
    return this.step.validationMetadata?.maxSelections ?? 100;
  }

  toggle(key: string): void {
    if (this.selectedKeys.has(key)) {
      this.selectedKeys.delete(key);
    } else if (this.selectedKeys.size < this.maxSelections) {
      this.selectedKeys.add(key);
    }
    this.error = null;
  }

  submit(): void {
    if (this.selectedKeys.size === 0) {
      this.error = 'Please select at least one option.';
      return;
    }
    this.error = null;
    this.submitted.emit(JSON.stringify({ keys: Array.from(this.selectedKeys) }));
  }
}



