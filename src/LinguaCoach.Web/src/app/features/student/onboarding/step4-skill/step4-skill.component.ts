import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OnboardingService } from '../../../../core/services/onboarding.service';

interface SkillOption { label: string; description: string; value: number; }

@Component({
  selector: 'app-step4-skill',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './step4-skill.component.html',
})
export class Step4SkillComponent {
  skills: SkillOption[] = [
    { label: 'Writing', description: 'Emails, reports, and professional documents.', value: 0 },
    { label: 'Speaking', description: 'Meetings, presentations, and conversations.', value: 1 },
    { label: 'Vocabulary', description: 'Role-specific terms and phrases.', value: 2 },
    { label: 'Listening', description: 'Comprehension, podcasts, and meeting audio.', value: 3 },
  ];

  selected = signal<number | null>(null);
  learningGoalText = signal('');
  submitting = signal(false);
  error = signal('');

  constructor(private onboarding: OnboardingService, private router: Router) {}

  select(value: number): void { this.selected.set(value); }

  finish(): void {
    this.submitting.set(true);
    const goal = this.learningGoalText().trim() || undefined;
    // Default to Writing (0) if no skill tag was explicitly chosen.
    const skillFocus = this.selected() ?? 0;
    this.onboarding.submitStep({
      step: 'skill',
      skillFocus,
      learningGoalDescription: goal,
    }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-5']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}

