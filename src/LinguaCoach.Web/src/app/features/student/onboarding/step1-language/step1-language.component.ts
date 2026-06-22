import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReferenceService } from '../../../../core/services/reference.service';
import { OnboardingService } from '../../../../core/services/onboarding.service';
import { LanguagePairDto } from '../../../../core/models/reference.models';

@Component({
  selector: 'app-step1-language',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step1-language.component.html',
})
export class Step1LanguageComponent implements OnInit {
  pairs = signal<LanguagePairDto[]>([]);
  selected = signal<string | null>(null);
  loading = signal(true);
  submitting = signal(false);
  error = signal('');

  constructor(private ref: ReferenceService, private onboarding: OnboardingService, private router: Router) {}

  ngOnInit(): void {
    this.ref.getLanguagePairs().subscribe({
      next: pairs => { this.pairs.set(pairs); this.loading.set(false); },
      error: () => { this.error.set('Could not load language options.'); this.loading.set(false); },
    });

    this.onboarding.getStatus().subscribe({
      next: status => {
        if (status.languagePairId) this.selected.set(status.languagePairId);
      },
      error: () => {},
    });
  }

  select(id: string): void { this.selected.set(id); }

  next(): void {
    if (!this.selected()) return;
    this.submitting.set(true);
    this.onboarding.submitStep({ step: 'language', languagePairId: this.selected()! }).subscribe({
      next: () => this.router.navigate(['/onboarding/step-2']),
      error: err => { this.submitting.set(false); this.error.set(err.error?.error ?? 'Failed to save.'); },
    });
  }
}

