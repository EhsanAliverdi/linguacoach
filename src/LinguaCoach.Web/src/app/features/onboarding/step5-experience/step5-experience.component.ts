import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';

interface ExperienceOption { label: string; description: string; value: number; }

@Component({
  selector: 'app-step5-experience',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step5-experience.component.html',
})
export class Step5ExperienceComponent {
  experienceLevels: ExperienceOption[] = [
    { label: 'No professional experience yet', description: 'Still studying or just starting out.', value: 0 },
    { label: 'Entry level / graduate', description: 'Recently joined the workforce.', value: 1 },
    { label: 'Junior (0–2 years)', description: 'Early in my career, learning the role.', value: 2 },
    { label: 'Mid-level (2–5 years)', description: 'Working independently in my role.', value: 3 },
    { label: 'Senior (5–10 years)', description: 'Experienced, often mentoring others.', value: 4 },
    { label: 'Lead / manager (10+ years)', description: 'Leading teams or managing others.', value: 5 },
  ];

  familiarityLevels: ExperienceOption[] = [
    { label: 'New to this type of role', description: 'Just starting in this field.', value: 0 },
    { label: 'Understand the basics', description: 'Getting familiar with the work.', value: 1 },
    { label: 'Currently working in this role', description: 'Doing this work day to day.', value: 2 },
    { label: 'Experienced in this role', description: 'Confident and skilled in my work.', value: 3 },
    { label: 'Manage or train others in this role', description: 'Leading others in this kind of work.', value: 4 },
  ];

  selectedExperience = signal<number>(2); // default: Junior
  selectedFamiliarity = signal<number>(2); // default: Currently working
  submitting = signal(false);
  error = signal('');

  constructor(private onboarding: OnboardingService, private router: Router) {}

  selectExperience(value: number): void { this.selectedExperience.set(value); }
  selectFamiliarity(value: number): void { this.selectedFamiliarity.set(value); }

  finish(): void {
    this.submitting.set(true);
    this.error.set('');
    this.onboarding.submitExperience(this.selectedExperience(), this.selectedFamiliarity()).subscribe({
      next: () => this.router.navigate(['/placement']),
      error: () => {
        // Experience step is not blocking — navigate to placement even on error.
        this.router.navigate(['/placement']);
      },
    });
  }

  skip(): void {
    this.router.navigate(['/placement']);
  }
}
