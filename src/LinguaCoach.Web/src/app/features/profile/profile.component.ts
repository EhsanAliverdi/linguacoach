import { Component, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
<div class="sp-app">

  <!-- Desktop sidebar -->
  <aside class="sp-side">
    <a routerLink="/dashboard" class="sp-sidebrand">
      <svg width="34" height="34" viewBox="0 0 48 48" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id="lg-prof-side" x1="6" y1="4" x2="42" y2="44" gradientUnits="userSpaceOnUse">
            <stop stop-color="#FF7A59"/><stop offset=".52" stop-color="#B45CF0"/><stop offset="1" stop-color="#5B4BE8"/>
          </linearGradient>
        </defs>
        <rect x="2" y="2" width="44" height="44" rx="13" fill="url(#lg-prof-side)"/>
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
      <a routerLink="/progress" class="sp-sidelink">
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        Progress
      </a>
      <a routerLink="/profile" class="sp-sidelink is-active">
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
    <header class="sp-topbar">
      <div>
        <div class="sp-greet-sm">Your account</div>
        <div class="sp-greet-lg">Profile</div>
      </div>
    </header>

    <main class="sp-content">

      <!-- Profile card -->
      <div style="background:var(--sp-grad-brand-soft);border:1px solid #EADBFF;border-radius:var(--sp-r-xl);padding:24px;display:flex;align-items:center;gap:20px;margin-bottom:20px">
        <div style="width:64px;height:64px;border-radius:50%;background:var(--sp-grad-brand);display:flex;align-items:center;justify-content:center;font-size:26px;font-weight:800;color:#fff;flex-shrink:0">
          {{ avatarLetter() }}
        </div>
        <div>
          <div style="font-size:18px;font-weight:800;color:var(--sp-ink)">{{ displayEmail() }}</div>
          <div style="font-size:13px;color:var(--sp-muted);margin-top:3px">Student · Persian to English</div>
        </div>
      </div>

      <!-- Settings rows -->
      <div class="sp-section-h">
        <h3>Learning</h3>
      </div>
      <div class="sp-card" style="padding:0;overflow:hidden;margin-bottom:16px">
        <div style="display:flex;flex-direction:column">
          <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid var(--sp-border)">
            <div>
              <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">Learning goal</div>
              <div style="font-size:12px;color:var(--sp-muted);margin-top:2px">Workplace English</div>
            </div>
            <svg width="16" height="16" fill="none" stroke="var(--sp-faint)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
          </div>
          <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid var(--sp-border)">
            <div>
              <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">Current level</div>
              <div style="font-size:12px;color:var(--sp-muted);margin-top:2px">Not assessed yet</div>
            </div>
            <svg width="16" height="16" fill="none" stroke="var(--sp-faint)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
          </div>
          <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid var(--sp-border)">
            <div>
              <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">Practising</div>
              <div style="font-size:12px;color:var(--sp-muted);margin-top:2px">Writing · Speaking coming soon</div>
            </div>
            <svg width="16" height="16" fill="none" stroke="var(--sp-faint)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
          </div>
          <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px">
            <div>
              <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">Daily reminder</div>
              <div style="font-size:12px;color:var(--sp-muted);margin-top:2px">Coming soon</div>
            </div>
            <svg width="16" height="16" fill="none" stroke="var(--sp-faint)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
          </div>
        </div>
      </div>

      <!-- Settings section -->
      <div class="sp-section-h">
        <h3>Settings</h3>
      </div>
      <div class="sp-card" style="padding:0;overflow:hidden;margin-bottom:20px">
        <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px">
          <div>
            <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">Notifications</div>
            <div style="font-size:12px;color:var(--sp-muted);margin-top:2px">Coming soon</div>
          </div>
          <div style="width:40px;height:24px;border-radius:12px;background:var(--sp-canvas2);position:relative">
            <div style="width:20px;height:20px;border-radius:50%;background:var(--sp-faint);position:absolute;top:2px;left:2px"></div>
          </div>
        </div>
      </div>

      <!-- Sign out -->
      <button (click)="auth.logout()"
        style="width:100%;display:flex;align-items:center;justify-content:center;gap:8px;padding:14px;border-radius:var(--sp-r-lg);background:var(--sp-speaking-soft);border:1px solid #F5BDB3;color:var(--sp-speaking-ink);font-size:14px;font-weight:700;cursor:pointer;transition:opacity .15s">
        <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
        Sign out
      </button>

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
    <a routerLink="/progress" class="sp-navbtn">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
      Progress
    </a>
    <a routerLink="/profile" class="sp-navbtn is-active">
      <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
      Profile
    </a>
  </nav>

</div>
  `,
})
export class ProfileComponent {
  avatarLetter = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'U';
  });

  displayEmail = computed(() => {
    return this.auth.currentUser()?.email ?? '';
  });

  constructor(public auth: AuthService) {}
}
