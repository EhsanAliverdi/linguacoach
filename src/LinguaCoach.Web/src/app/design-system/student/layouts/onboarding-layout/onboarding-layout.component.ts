import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Minimal chromeless shell for onboarding and placement — no sidebar, no header nav, no
 * bottom nav. Students shouldn't be able to navigate away to the rest of the app (dashboard,
 * practice, etc.) while they're still being onboarded or placed.
 */
@Component({
  selector: 'app-onboarding-layout',
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
        </div>
        <router-outlet />
      </div>
    </div>
  `,
})
export class OnboardingLayoutComponent {}
