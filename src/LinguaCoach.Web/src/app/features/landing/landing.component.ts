import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="sp-page">
      <div class="sp-shell">

        <!-- Nav -->
        <nav class="flex items-center justify-between py-3">
          <div class="sp-brand">
            <svg width="34" height="34" viewBox="0 0 48 48" fill="none" aria-hidden="true">
              <defs>
                <linearGradient id="lg-land" x1="6" y1="4" x2="42" y2="44" gradientUnits="userSpaceOnUse">
                  <stop stop-color="#FF7A59"/><stop offset=".52" stop-color="#B45CF0"/><stop offset="1" stop-color="#5B4BE8"/>
                </linearGradient>
              </defs>
              <rect x="2" y="2" width="44" height="44" rx="13" fill="url(#lg-land)"/>
              <circle cx="14" cy="33" r="3.4" fill="#fff"/>
              <circle cx="24" cy="26" r="3.4" fill="#fff" opacity=".92"/>
              <path d="M30 12.5h6.5A3.5 3.5 0 0 1 40 16v3.5a3.5 3.5 0 0 1-3.5 3.5H34l-3.2 2.3.2-2.3h-1A3.5 3.5 0 0 1 26.5 19.5V16A3.5 3.5 0 0 1 30 12.5Z" fill="#fff"/>
              <path d="M15.4 30.6 22 27.2M25.7 23.4l3.2-2.6" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-opacity=".75"/>
            </svg>
            <span><span class="sp-wordmark-speak">Speak</span><span class="sp-wordmark-path">Path</span></span>
          </div>
          <a routerLink="/login" class="sp-button-ghost min-h-0 rounded-[14px] px-4 py-2 text-sm">Pilot login</a>
        </nav>

        <!-- Hero -->
        <div class="grid gap-8 py-10 md:grid-cols-[1.1fr_0.9fr] md:items-center md:py-16">
          <div>
            <p class="sp-eyebrow mb-3">Workplace communication practice</p>
            <h1 class="text-4xl font-extrabold leading-tight tracking-tight sm:text-5xl" style="color:var(--sp-ink);letter-spacing:-.03em">
              Practise the workplace message<br class="hidden sm:inline"> before you send it.
            </h1>
            <p class="mt-5 max-w-2xl text-base font-medium leading-7 sm:text-lg" style="color:var(--sp-text)">
              SpeakPath helps immigrant professionals sound clear, polite, and confident in real work situations. AI-generated writing practice, built for your career.
            </p>
            <div class="mt-7 flex flex-col gap-3 sm:flex-row">
              <a routerLink="/login" class="sp-button-primary">Sign in to SpeakPath</a>
              <a href="#how-it-works" class="sp-button-ghost">See how it works</a>
            </div>
            <p class="mt-4 text-sm font-medium" style="color:var(--sp-muted)">
              Access is currently admin-created for pilot users.
            </p>
          </div>

          <!-- Preview card -->
          <div class="sp-card" style="border-color:var(--sp-writing-soft)">
            <p class="sp-eyebrow mb-3">First practice scenario</p>
            <h2 class="text-lg font-extrabold" style="color:var(--sp-ink)">Follow up a pending document approval</h2>
            <p class="mt-2 text-sm font-medium leading-6" style="color:var(--sp-text)">
              You need to ask a project manager to review the latest revision without sounding impatient or too direct.
            </p>
            <div class="mt-4 rounded-[14px] p-4" style="background:var(--sp-canvas2)">
              <p class="sp-eyebrow mb-3">Phrases to practise</p>
              <div class="flex flex-wrap gap-2">
                <span class="sp-phrase-chip">pending approval</span>
                <span class="sp-phrase-chip">could you please review</span>
                <span class="sp-phrase-chip">when you have a chance</span>
              </div>
            </div>
            <div class="mt-4 flex items-start gap-3 rounded-[14px] border p-3" style="background:var(--sp-writing-soft);border-color:var(--sp-writing-soft)">
              <span class="mt-0.5 text-base">✏️</span>
              <div>
                <p class="text-sm font-extrabold" style="color:var(--sp-ink)">Role-specific writing practice.</p>
                <p class="mt-0.5 text-sm font-medium leading-6" style="color:var(--sp-text)">
                  Emails, follow-ups, clarification requests — all for your career context.
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- How it works -->
        <div id="how-it-works" class="grid gap-4 pb-12 sm:grid-cols-3">
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-[14px]" style="background:var(--sp-writing-soft)">
              <span style="font-size:18px">🎯</span>
            </div>
            <p class="font-extrabold" style="color:var(--sp-ink)">1. Choose your context</p>
            <p class="mt-2 text-sm font-medium leading-6" style="color:var(--sp-text)">Select the language path, career role, and skill focus that match your work.</p>
          </div>
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-[14px]" style="background:var(--sp-pronunciation-soft)">
              <span style="font-size:18px">✏️</span>
            </div>
            <p class="font-extrabold" style="color:var(--sp-ink)">2. Practise a real message</p>
            <p class="mt-2 text-sm font-medium leading-6" style="color:var(--sp-text)">Write the email or follow-up in a safe place before using it at work.</p>
          </div>
          <div class="sp-card">
            <div class="mb-3 flex h-10 w-10 items-center justify-center rounded-[14px]" style="background:var(--sp-vocabulary-soft)">
              <span style="font-size:18px">💬</span>
            </div>
            <p class="font-extrabold" style="color:var(--sp-ink)">3. Review coaching feedback</p>
            <p class="mt-2 text-sm font-medium leading-6" style="color:var(--sp-text)">See clearer phrasing, tone guidance, and key phrases to practise again.</p>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class LandingComponent {}
