import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { OnboardingService } from '../../../../core/services/onboarding.service';

interface DurationOption { label: string; minutes: number; hint: string; }

@Component({
  selector: 'app-step2-track',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './step2-track.component.html',
})
export class Step2TrackComponent {
  readonly durations: DurationOption[] = [
    { label: '10 minutes', minutes: 10, hint: 'Quick daily check-in' },
    { label: '15 minutes', minutes: 15, hint: 'Focused short practice' },
    { label: '20 minutes', minutes: 20, hint: 'Balanced daily session' },
    { label: '30 minutes', minutes: 30, hint: 'Deep practice session' },
  ];

  selected = signal<number | null>(null);
  submitting = signal(false);
  error = signal('');

  constructor(private onboarding: OnboardingService, private router: Router) {}

  select(minutes: number): void { this.selected.set(minutes); }

  next(): void {
    if (this.selected() === null) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'preference', preferredDurationMinutes: this.selected()! }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-3']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}

