import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-onboarding-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="sp-page">
      <div class="sp-narrow-shell">
        <div class="mb-8 text-center">
          <div class="sp-brand justify-center">
            <span class="sp-brand-mark">S</span>
            <span>SpeakPath</span>
          </div>
          <p class="mt-3 text-sm leading-6 text-slate-600">Set up a personalised practice path that matches your goals.</p>
        </div>
        <router-outlet />
      </div>
    </div>
  `,
})
export class OnboardingShellComponent {}
