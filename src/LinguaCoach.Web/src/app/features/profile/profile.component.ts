import { Component, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Profile card -->
    <div style="background:var(--sp-grad-brand-soft);border:1px solid #EADBFF;border-radius:var(--sp-r-xl);padding:24px;display:flex;align-items:center;gap:16px;margin-bottom:20px;min-width:0">
      <div style="width:56px;height:56px;border-radius:50%;background:var(--sp-grad-brand);display:flex;align-items:center;justify-content:center;font-size:22px;font-weight:800;color:#fff;flex-shrink:0">
        {{ avatarLetter() }}
      </div>
      <div style="min-width:0;flex:1">
        <div style="font-size:16px;font-weight:800;color:var(--sp-ink);overflow-wrap:anywhere;word-break:break-all">{{ displayEmail() }}</div>
        <div style="font-size:13px;color:var(--sp-muted);margin-top:3px">Student · Persian to English</div>
      </div>
    </div>

    <!-- Learning rows -->
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
