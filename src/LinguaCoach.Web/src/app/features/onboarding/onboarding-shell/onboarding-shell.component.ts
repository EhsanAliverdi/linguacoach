import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-onboarding-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="min-h-screen bg-slate-50">
      <div class="max-w-xl mx-auto px-4 py-10">
        <div class="text-center mb-8">
          <h1 class="text-2xl font-bold text-slate-900">SpeakPath</h1>
          <p class="text-sm text-slate-500 mt-1">Let's set up your learning profile</p>
        </div>
        <router-outlet />
      </div>
    </div>
  `,
})
export class OnboardingShellComponent {}
