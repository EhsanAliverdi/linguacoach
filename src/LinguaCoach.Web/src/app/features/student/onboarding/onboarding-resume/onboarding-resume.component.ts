import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { OnboardingService } from '../../../../core/services/onboarding.service';

// Redirect-only component. Every student (new or resuming) goes through onboarding V2 —
// its own status check (OnboardingV2Component.load) redirects to /dashboard if already
// complete, so this only needs to route into v2 and let it take over from there. The legacy
// v1 status check here is kept only as a fast-path: if a legacy-complete student's V1
// OnboardingStatus is already Complete, skip straight to the dashboard without waiting on
// a v2 progress round-trip.
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
        this.router.navigate([status.isComplete ? '/dashboard' : '/onboarding/v2']);
      },
      error: () => this.router.navigate(['/onboarding/v2']),
    });
  }
}

