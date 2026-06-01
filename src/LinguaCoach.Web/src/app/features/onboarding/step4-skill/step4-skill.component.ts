import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';

interface SkillOption { label: string; description: string; value: number; }

@Component({
  selector: 'app-step4-skill',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step4-skill.component.html',
})
export class Step4SkillComponent {
  skills: SkillOption[] = [
    { label: 'Writing', description: 'Emails, reports, and professional documents.', value: 0 },
    { label: 'Speaking', description: 'Meetings, presentations, and conversations.', value: 1 },
    { label: 'Vocabulary', description: 'Role-specific terms and phrases.', value: 2 },
  ];

  selected = signal<number | null>(null);
  submitting = signal(false);
  error = signal('');

  constructor(private onboarding: OnboardingService, private router: Router) {}

  select(value: number): void { this.selected.set(value); }

  finish(): void {
    if (this.selected() === null) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'skill', skillFocus: this.selected()! }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}
