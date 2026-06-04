import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-progress',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
<div class="sp-app">

  <!-- Desktop sidebar -->
  <aside class="sp-side">
    <a routerLink="/dashboard" class="sp-sidebrand">
      <svg width="34" height="34" viewBox="0 0 48 48" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id="lg-prog-side" x1="6" y1="4" x2="42" y2="44" gradientUnits="userSpaceOnUse">
            <stop stop-color="#FF7A59"/><stop offset=".52" stop-color="#B45CF0"/><stop offset="1" stop-color="#5B4BE8"/>
          </linearGradient>
        </defs>
        <rect x="2" y="2" width="44" height="44" rx="13" fill="url(#lg-prog-side)"/>
        <circle cx="14" cy="33" r="3.4" fill="#fff"/>
        <circle cx="24" cy="26" r="3.4" fill="#fff" opacity=".92"/>
        <path d="M30 12.5h6.5A3.5 3.5 0 0 1 40 16v3.5a3.5 3.5 0 0 1-3.5 3.5H34l-3.2 2.3.2-2.3h-1A3.5 3.5 0 0 1 26.5 19.5V16A3.5 3.5 0 0 1 30 12.5Z" fill="#fff"/>
        <path d="M15.4 30.6 22 27.2M25.7 23.4l3.2-2.6" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-opacity=".75"/>
      </svg>
      <span style="font-size:17px;font-weight:800;color:var(--sp-ink)"><span>Speak</span><span style="color:var(--sp-writing)">Path</span></span>
    </a>
    <nav style="display:flex;flex-direction:column;gap:4px;flex:1">
      <a routerLink="/dashboard" class="sp-sidelink">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
        Dashboard
      </a>
      <a routerLink="/my-path" class="sp-sidelink">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
        My Path
      </a>
      <a routerLink="/activity" class="sp-sidelink is-practice">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M12 20h9M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>
        Practice
      </a>
      <a routerLink="/progress" class="sp-sidelink is-active">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        Progress
      </a>
      <a routerLink="/profile" class="sp-sidelink">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
        Profile
      </a>
    </nav>
    <div style="background:var(--sp-grad-brand-soft);border:1px solid #EADBFF;border-radius:var(--sp-r-lg);padding:14px;margin-top:16px">
      <div style="font-size:22px;margin-bottom:6px">🔥</div>
      <p style="font-size:13px;font-weight:800;color:var(--sp-ink);margin-bottom:4px">Build your streak</p>
      <p style="font-size:12px;font-weight:500;color:var(--sp-text)">Practice every day!</p>
    </div>
  </aside>

  <!-- Main -->
  <div style="flex:1;min-width:0;display:flex;flex-direction:column">
    <!-- Cool gradient header -->
    <div style="background:var(--sp-grad-cool);padding:28px 24px 24px">
      <div class="sp-greet-sm" style="color:rgba(255,255,255,.8)">Your achievements</div>
      <h1 class="sp-h1" style="color:#fff;margin-top:4px">Progress</h1>
      <p style="font-size:14px;color:rgba(255,255,255,.85);font-weight:500;margin-top:6px">Keep up the great work</p>
    </div>

    <main class="sp-content">

      <!-- Stat tiles -->
      <div class="sp-statgrid" style="margin-bottom:24px">
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;margin-bottom:4px">🔥</div>
          <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">day streak</div>
          <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;margin-bottom:4px">✅</div>
          <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">activities</div>
          <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;margin-bottom:4px">⭐</div>
          <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">avg score</div>
          <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
        </div>
      </div>

      <!-- Skill levels -->
      <div class="sp-section-h">
        <h3>Skill levels</h3>
      </div>
      <div class="sp-card" style="padding:18px;margin-bottom:20px">
        <div style="display:flex;flex-direction:column;gap:14px">
          <!-- Writing — has real data placeholder -->
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-writing)"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-ink)">Writing</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-writing)">Level 3 · Building</span>
            </div>
            <div class="sp-progress-track" style="height:8px">
              <div class="sp-progress-fill" style="width:64%;background:var(--sp-writing)"></div>
            </div>
          </div>
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-speaking)"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-muted)">Speaking</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-faint)">Coming soon</span>
            </div>
            <div class="sp-progress-track" style="height:8px"><div style="width:0"></div></div>
          </div>
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-listening)"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-muted)">Listening</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-faint)">Coming soon</span>
            </div>
            <div class="sp-progress-track" style="height:8px"><div style="width:0"></div></div>
          </div>
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-vocabulary)"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-muted)">Vocabulary</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-faint)">Coming soon</span>
            </div>
            <div class="sp-progress-track" style="height:8px"><div style="width:0"></div></div>
          </div>
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-pronunciation)"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-muted)">Pronunciation</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-faint)">Coming soon</span>
            </div>
            <div class="sp-progress-track" style="height:8px"><div style="width:0"></div></div>
          </div>
        </div>
      </div>

      <!-- Recent results -->
      <div class="sp-section-h">
        <h3>Recent results</h3>
      </div>
      <div class="sp-empty-state">
        <div style="font-size:32px;margin-bottom:12px">📊</div>
        <h3 style="font-size:16px;font-weight:800;color:var(--sp-ink);margin-bottom:6px">No results yet</h3>
        <p style="font-size:13px;color:var(--sp-muted);line-height:1.6;max-width:280px;text-align:center">Complete your first activity to see your progress here.</p>
        <a routerLink="/activity" class="sp-button-primary" style="margin-top:16px;display:inline-flex">Start practising →</a>
      </div>

      <p style="text-align:center;font-size:12px;color:var(--sp-faint);margin-top:24px;font-weight:500">
        Your progress history will appear here as you practise.
      </p>

    </main>
  </div>

  <!-- Mobile bottom nav -->
  <nav class="sp-bottomnav">
    <a routerLink="/dashboard" class="sp-navbtn">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
      Home
    </a>
    <a routerLink="/my-path" class="sp-navbtn">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
      My Path
    </a>
    <a routerLink="/activity" class="sp-navbtn" style="position:relative">
      <div style="width:52px;height:52px;border-radius:50%;background:var(--sp-grad-brand);display:flex;align-items:center;justify-content:center;box-shadow:var(--sp-sh-glow);position:absolute;top:-16px">
        <svg width="22" height="22" fill="none" stroke="#fff" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M12 20h9M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>
      </div>
      <span style="margin-top:22px">Practice</span>
    </a>
    <a routerLink="/progress" class="sp-navbtn is-active">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
      Progress
    </a>
    <a routerLink="/profile" class="sp-navbtn">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
      Profile
    </a>
  </nav>

</div>
  `,
})
export class ProgressComponent {
  constructor(public auth: AuthService) {}
}
