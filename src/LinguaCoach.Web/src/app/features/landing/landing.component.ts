import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="sp-page">
      <section class="sp-shell">
        <nav class="flex items-center justify-between py-2">
          <a routerLink="/" class="sp-brand" aria-label="SpeakPath home">
            <span class="sp-brand-mark">S</span>
            <span>SpeakPath</span>
          </a>
          <a routerLink="/login" class="sp-button-secondary">Pilot login</a>
        </nav>

        <div class="grid gap-8 py-10 md:grid-cols-[1.1fr_0.9fr] md:items-center md:py-14">
          <div>
            <p class="sp-eyebrow mb-3">Workplace communication practice</p>
            <h1 class="max-w-3xl text-4xl font-bold leading-tight text-slate-950 sm:text-5xl">
              Practise the workplace message before you send it.
            </h1>
            <p class="mt-5 max-w-2xl text-base leading-7 text-slate-600 sm:text-lg">
              SpeakPath helps immigrant professionals sound clear, polite, and professional in real work situations. The first pilot path supports Persian-speaking Document Controllers building workplace English confidence in Australia.
            </p>
            <div class="mt-7 flex flex-col gap-3 sm:flex-row">
              <a routerLink="/login" class="sp-button-primary">Sign in to SpeakPath</a>
              <a href="#scenario" class="sp-button-secondary">Preview a practice task</a>
            </div>
            <p class="mt-4 text-sm text-slate-500">
              Access is currently admin-created for pilot users.
            </p>
          </div>

          <div id="scenario" class="sp-card">
            <p class="sp-eyebrow mb-3">First practice scenario</p>
            <h2 class="text-xl font-bold text-slate-950">Follow up a pending document approval</h2>
            <p class="mt-3 text-sm leading-6 text-slate-600">
              You need to ask a project manager to review the latest revision without sounding impatient or too direct.
            </p>
            <div class="mt-5 rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p class="text-xs font-semibold uppercase text-slate-500">Phrases to practise</p>
              <div class="mt-3 flex flex-wrap gap-2">
                <span class="rounded-full bg-white px-3 py-1 text-xs font-medium text-slate-700 ring-1 ring-slate-200">pending approval</span>
                <span class="rounded-full bg-white px-3 py-1 text-xs font-medium text-slate-700 ring-1 ring-slate-200">could you please review</span>
                <span class="rounded-full bg-white px-3 py-1 text-xs font-medium text-slate-700 ring-1 ring-slate-200">when you have a chance</span>
              </div>
            </div>
            <div class="mt-5 border-l-4 border-indigo-500 pl-4">
              <p class="text-sm font-semibold text-slate-900">Not generic English lessons.</p>
              <p class="mt-1 text-sm leading-6 text-slate-600">
                Role-specific writing practice for emails, follow-ups, clarification, and professional tone.
              </p>
            </div>
          </div>
        </div>

        <div class="grid gap-4 pb-10 sm:grid-cols-3">
          <div class="sp-card">
            <p class="text-sm font-bold text-slate-950">1. Choose the work context</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">Start with the language path, career role, and skill focus that match the student's work.</p>
          </div>
          <div class="sp-card">
            <p class="text-sm font-bold text-slate-950">2. Practise a realistic message</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">Write the email or follow-up in a safe place before using it at work.</p>
          </div>
          <div class="sp-card">
            <p class="text-sm font-bold text-slate-950">3. Review structured feedback</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">See clearer phrasing, tone guidance, and phrases to practise again.</p>
          </div>
        </div>
      </section>
    </main>
  `,
})
export class LandingComponent {}
