import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';

// Redirect-only component. Checks onboarding status and routes to the correct step.
@Component({
  selector: 'app-onboarding-resume',
  standalone: true,
  template: `<div class="flex justify-center py-16"><div class="w-6 h-6 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"></div></div>`,
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
          Track: '/onboarding/step-3',
          Career: '/onboarding/step-4',
        };
        const route = stepMap[status.currentStep] ?? '/onboarding/step-1';
        this.router.navigate([route]);
      },
      error: () => this.router.navigate(['/onboarding/step-1']),
    });
  }
}
