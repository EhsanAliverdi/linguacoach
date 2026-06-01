import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReferenceService } from '../../../core/services/reference.service';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { LearningTrackDto } from '../../../core/models/reference.models';

@Component({
  selector: 'app-step2-track',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step2-track.component.html',
})
export class Step2TrackComponent implements OnInit {
  tracks = signal<LearningTrackDto[]>([]);
  selected = signal<string | null>(null);
  loading = signal(true);
  submitting = signal(false);
  error = signal('');

  constructor(private ref: ReferenceService, private onboarding: OnboardingService, private router: Router) {}

  ngOnInit(): void {
    // Fetch onboarding status to get the language pair stored in step 1
    this.onboarding.getStatus().subscribe({
      next: () => {
        // Language pair ID isn't returned by status; use the first available pair as proxy.
        // The backend validates the pair anyway.
        this.ref.getLanguagePairs().subscribe({
          next: pairs => {
            if (pairs.length === 0) { this.error.set('No language pairs found.'); this.loading.set(false); return; }
            this.ref.getTracks(pairs[0].id).subscribe({
              next: tracks => { this.tracks.set(tracks); this.loading.set(false); },
              error: () => { this.error.set('Could not load tracks.'); this.loading.set(false); },
            });
          },
          error: () => { this.error.set('Could not load language pairs.'); this.loading.set(false); },
        });
      },
      error: () => { this.error.set('Could not load your profile.'); this.loading.set(false); },
    });
  }

  select(id: string): void { this.selected.set(id); }

  next(): void {
    if (!this.selected()) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'track', learningTrackId: this.selected()! }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-3']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}
