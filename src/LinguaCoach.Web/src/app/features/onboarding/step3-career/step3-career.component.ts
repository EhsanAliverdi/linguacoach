import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';

const SUGGESTIONS = [
  'Junior software engineer',
  'Project planner',
  'Nurse',
  'Customer support officer',
  'Document controller',
];

@Component({
  selector: 'app-step3-career',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './step3-career.component.html',
})
export class Step3CareerComponent {
  careerText = signal('');
  submitting = signal(false);
  error = signal('');

  readonly suggestions = SUGGESTIONS;

  constructor(private onboarding: OnboardingService, private router: Router) {}

  applySuggestion(text: string): void {
    this.careerText.set(text);
  }

  next(): void {
    const text = this.careerText().trim();
    if (!text) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'career', careerContext: text }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-4']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}
