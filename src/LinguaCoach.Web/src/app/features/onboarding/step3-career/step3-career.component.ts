import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReferenceService } from '../../../core/services/reference.service';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { CareerProfileDto } from '../../../core/models/reference.models';

@Component({
  selector: 'app-step3-career',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step3-career.component.html',
})
export class Step3CareerComponent implements OnInit {
  careers = signal<CareerProfileDto[]>([]);
  selected = signal<string | null>(null);
  loading = signal(true);
  submitting = signal(false);
  error = signal('');

  constructor(private ref: ReferenceService, private onboarding: OnboardingService, private router: Router) {}

  ngOnInit(): void {
    this.ref.getLanguagePairs().subscribe({
      next: pairs => {
        if (pairs.length === 0) { this.error.set('No language pairs found.'); this.loading.set(false); return; }
        this.ref.getCareerProfiles(pairs[0].id).subscribe({
          next: careers => { this.careers.set(careers); this.loading.set(false); },
          error: () => { this.error.set('Could not load career profiles.'); this.loading.set(false); },
        });
      },
      error: () => { this.error.set('Could not load language pairs.'); this.loading.set(false); },
    });
  }

  select(id: string): void { this.selected.set(id); }

  next(): void {
    if (!this.selected()) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'career', careerProfileId: this.selected()! }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-4']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}
