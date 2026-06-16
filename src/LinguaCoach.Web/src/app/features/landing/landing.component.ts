import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  template: `
<div style="min-height:100vh">

  <!-- ── Topnav ─────────────────────────────────────────────────────── -->
  <div class="sp-public-shell">
    <header class="sp-public-header">
      <a routerLink="/" class="sp-brand" style="text-decoration:none">
        <svg width="32" height="32" viewBox="0 0 48 48" fill="none" aria-hidden="true">
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
      </a>
      <a routerLink="/login" class="sp-button-ghost" style="min-height:0;padding:9px 18px;font-size:14px">Sign in</a>
    </header>
  </div>

  <!-- ── Hero ───────────────────────────────────────────────────────── -->
  <div class="sp-public-shell">
    <div class="sp-public-hero">

      <!-- Left: headline + CTA -->
      <div>
        <p class="sp-eyebrow" style="margin-bottom:12px">Real-life English communication practice</p>
        <h1 style="font-size:clamp(28px,4.5vw,46px);font-weight:800;color:var(--sp-ink);letter-spacing:-.03em;line-height:1.12;margin:0">
          Practise the conversation<br>before you need it.
        </h1>
        <p style="margin-top:18px;font-size:16px;font-weight:500;color:var(--sp-text);line-height:1.65;max-width:480px">
          AI coaching for real-life messages and conversations, personalised learning paths, and practical English — built for people settling into a new life.
        </p>
        <div style="margin-top:28px;display:flex;flex-wrap:wrap;gap:12px;align-items:center">
          <a routerLink="/login" class="sp-button-primary">Sign in to SpeakPath</a>
          <a href="#how-it-works" class="sp-button-ghost">How it works</a>
        </div>
        <p style="margin-top:14px;font-size:13px;color:var(--sp-muted);font-weight:500">
          Pilot access only — accounts are created by your admin.
        </p>
      </div>

      <!-- Right: preview card -->
      <div class="sp-card" style="padding:22px">
        <p class="sp-eyebrow" style="margin-bottom:10px">Example scenario</p>
        <h2 style="font-size:16px;font-weight:800;color:var(--sp-ink);margin:0 0 8px">Follow up a pending document approval</h2>
        <p style="font-size:13.5px;font-weight:500;color:var(--sp-text);line-height:1.6;margin:0 0 16px">
          Ask the project manager to review the latest revision — without sounding impatient or too direct.
        </p>
        <div style="background:var(--sp-canvas2);border-radius:var(--sp-r-sm);padding:14px">
          <p class="sp-eyebrow" style="margin-bottom:8px">Phrases to practise</p>
          <div style="display:flex;flex-wrap:wrap;gap:7px">
            <span class="sp-phrase-chip">pending approval</span>
            <span class="sp-phrase-chip">could you please review</span>
            <span class="sp-phrase-chip">when you have a chance</span>
          </div>
        </div>
        <div style="margin-top:14px;display:flex;align-items:flex-start;gap:10px;background:var(--sp-writing-soft);border-radius:var(--sp-r-sm);padding:12px">
          <span style="font-size:16px;flex-shrink:0">✏️</span>
          <div>
            <p style="font-size:13px;font-weight:800;color:var(--sp-ink);margin:0 0 2px">Role-specific writing practice</p>
            <p style="font-size:12.5px;color:var(--sp-text);line-height:1.5;margin:0">Emails, follow-ups, clarification requests — all for your career context.</p>
          </div>
        </div>
      </div>

    </div>
  </div>

  <!-- ── How it works ──────────────────────────────────────────────── -->
  <div id="how-it-works" style="background:var(--sp-surface);border-top:1px solid var(--sp-border);border-bottom:1px solid var(--sp-border);padding:52px 0">
    <div class="sp-public-shell">
      <p class="sp-eyebrow" style="text-align:center;margin-bottom:8px">How it works</p>
      <h2 style="font-size:22px;font-weight:800;color:var(--sp-ink);text-align:center;margin:0 0 32px;letter-spacing:-.02em">Three steps to confident workplace English</h2>
      <div class="sp-marketing-grid">
        <div class="sp-marketing-card">
          <div class="sp-marketing-icon" style="background:var(--sp-writing-soft)">🎯</div>
          <p style="font-size:14px;font-weight:800;color:var(--sp-ink);margin:0 0 6px">1. Choose your context</p>
          <p style="font-size:13.5px;color:var(--sp-text);line-height:1.6;margin:0">Select the career role and skill focus that match your work.</p>
        </div>
        <div class="sp-marketing-card">
          <div class="sp-marketing-icon" style="background:var(--sp-pronunciation-soft)">✏️</div>
          <p style="font-size:14px;font-weight:800;color:var(--sp-ink);margin:0 0 6px">2. Practise a real message</p>
          <p style="font-size:13.5px;color:var(--sp-text);line-height:1.6;margin:0">Write the email or follow-up in a safe space before sending it at work.</p>
        </div>
        <div class="sp-marketing-card">
          <div class="sp-marketing-icon" style="background:var(--sp-vocabulary-soft)">💬</div>
          <p style="font-size:14px;font-weight:800;color:var(--sp-ink);margin:0 0 6px">3. Review AI coaching feedback</p>
          <p style="font-size:13.5px;color:var(--sp-text);line-height:1.6;margin:0">See clearer phrasing, tone guidance, and key phrases to practise again.</p>
        </div>
      </div>
    </div>
  </div>

  <!-- ── Product focus ─────────────────────────────────────────────── -->
  <div style="padding:52px 0 64px">
    <div class="sp-public-shell">
      <p class="sp-eyebrow" style="text-align:center;margin-bottom:8px">Built for the workplace</p>
      <h2 style="font-size:22px;font-weight:800;color:var(--sp-ink);text-align:center;margin:0 0 32px;letter-spacing:-.02em">What SpeakPath helps with</h2>
      <div class="sp-features-grid">
        @for (f of features; track f.title) {
          <div class="sp-marketing-card">
            <div style="font-size:22px;margin-bottom:10px">{{ f.icon }}</div>
            <p style="font-size:13.5px;font-weight:800;color:var(--sp-ink);margin:0 0 5px">{{ f.title }}</p>
            <p style="font-size:13px;color:var(--sp-text);line-height:1.55;margin:0">{{ f.desc }}</p>
          </div>
        }
      </div>
    </div>
  </div>

  <!-- ── Footer ─────────────────────────────────────────────────────── -->
  <div style="border-top:1px solid var(--sp-border);padding:24px 0">
    <div class="sp-public-shell" style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px">
      <p style="font-size:13px;color:var(--sp-muted);margin:0">© 2026 SpeakPath · Pilot programme</p>
      <a routerLink="/login" class="sp-link" style="font-size:13px">Sign in →</a>
    </div>
  </div>

</div>
  `,
})
export class LandingComponent {
  readonly features = [
    { icon: '🗂️', title: 'Personalised learning path', desc: 'An AI-built module sequence matched to your career, CEFR level, and weekly goals.' },
    { icon: '✉️', title: 'Workplace writing practice', desc: 'Emails, follow-ups, meeting recaps — real scenarios from your professional context.' },
    { icon: '🔊', title: 'Listening and vocabulary practice', desc: 'Practise workplace audio messages, transcript review, and useful professional phrases.' },
    { icon: '🌍', title: 'Built for immigrant professionals', desc: 'Persian to English as the first language pair, with more pairs planned.' },
  ];
}
