import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { OnboardingService } from '../../../../core/services/onboarding.service';

// Redirect-only component. Checks onboarding status and routes to the correct step.
@Component({
  selector: 'app-onboarding-resume',
  standalone: true,
  template: `
    <div class="rounded-2xl border border-slate-200 bg-white p-6 text-center shadow-sm">
      <div class="mx-auto mb-4 h-7 w-7 rounded-full border-2 border-indigo-500 border-t-transparent animate-spin"></div>
      <h2 class="text-base font-semibold text-slate-900">Finding your next setup step</h2>
      <p class="mt-1 text-sm text-slate-500">We will take you back to the right place if setup was interrupted.</p>
    </div>
  `,
})
export class OnboardingResumeComponent implements OnInit {
  constructor(private onboarding: OnboardingService, private router: Router) {}

  ngOnInit(): void {
    this.onboarding.getStatus().subscribe({
      next: status => {
        if (status.isComplete) { this.router.navigate(['/dashboard']); return; }
        const stepMap: Record<string, string> = {
          None: '/onboarding/step-1',
          Language: '/onboarding/step-2',
          Preference: '/onboarding/step-3',
          Career: '/onboarding/step-4',
        };
        const route = stepMap[status.currentStep] ?? '/onboarding/step-1';
        this.router.navigate([route]);
      },
      error: () => this.router.navigate(['/onboarding/step-1']),
    });
  }
}

