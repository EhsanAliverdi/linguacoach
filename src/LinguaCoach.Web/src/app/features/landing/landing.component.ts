import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="sp-page">
      <div class="sp-shell">

        <!-- Nav -->
        <nav class="flex items-center justify-between py-2">
          <a routerLink="/" class="sp-brand" aria-label="SpeakPath home">
            <span class="sp-brand-mark">S</span>
            <span>SpeakPath</span>
          </a>
          <a routerLink="/login" class="sp-button-secondary text-xs sm:text-sm">Pilot login</a>
        </nav>

        <!-- Hero -->
        <div class="grid gap-8 py-10 md:grid-cols-[1.1fr_0.9fr] md:items-center md:py-16">
          <div>
            <p class="sp-eyebrow mb-3">Workplace communication practice</p>
            <h1 class="max-w-3xl text-4xl font-extrabold leading-tight tracking-tight text-slate-950 sm:text-5xl">
              Practise the workplace message<br class="hidden sm:inline"> before you send it.
            </h1>
            <p class="mt-5 max-w-2xl text-base leading-7 text-slate-600 sm:text-lg">
              SpeakPath helps immigrant professionals sound clear, polite, and confident in real work situations. AI-generated writing practice, built for your career.
            </p>
            <div class="mt-7 flex flex-col gap-3 sm:flex-row">
              <a routerLink="/login" class="sp-button-primary">Sign in to SpeakPath</a>
              <a href="#how-it-works" class="sp-button-secondary">See how it works</a>
            </div>
            <p class="mt-4 text-sm text-slate-400">
              Access is currently admin-created for pilot users.
            </p>
          </div>

          <!-- Preview card -->
          <div class="sp-card border-violet-100">
            <p class="sp-eyebrow mb-3">First practice scenario</p>
            <h2 class="text-lg font-bold text-slate-950">Follow up a pending document approval</h2>
            <p class="mt-2 text-sm leading-6 text-slate-600">
              You need to ask a project manager to review the latest revision without sounding impatient or too direct.
            </p>
            <div class="mt-4 rounded-xl border border-slate-100 bg-slate-50 p-4">
              <p class="text-xs font-bold uppercase tracking-wide text-slate-400">Phrases to practise</p>
              <div class="mt-3 flex flex-wrap gap-2">
                <span class="sp-phrase-chip">pending approval</span>
                <span class="sp-phrase-chip">could you please review</span>
                <span class="sp-phrase-chip">when you have a chance</span>
              </div>
            </div>
            <div class="mt-4 flex items-start gap-3 rounded-xl border border-violet-100 bg-violet-50/60 p-3">
              <span class="mt-0.5 text-lg">✏️</span>
              <div>
                <p class="text-sm font-semibold text-slate-900">Role-specific writing practice.</p>
                <p class="mt-0.5 text-sm leading-6 text-slate-600">
                  Emails, follow-ups, clarification requests, and professional tone — all for your career context.
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- How it works -->
        <div id="how-it-works" class="grid gap-4 pb-12 sm:grid-cols-3">
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-xl text-lg" style="background-color: #ede9fe;">🎯</div>
            <p class="font-bold text-slate-950">1. Choose your context</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">Select the language path, career role, and skill focus that match your work.</p>
          </div>
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-teal-100 text-lg">✏️</div>
            <p class="font-bold text-slate-950">2. Practise a real message</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">Write the email or follow-up in a safe place before using it at work.</p>
          </div>
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 text-lg">💬</div>
            <p class="font-bold text-slate-950">3. Review coaching feedback</p>
            <p class="mt-2 text-sm leading-6 text-slate-600">See clearer phrasing, tone guidance, and key phrases to practise again.</p>
          </div>
        </div>

      </div>
    </main>
  `,
})
export class LandingComponent {}
