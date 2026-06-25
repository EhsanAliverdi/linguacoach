import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminToggleComponent,
} from '../../../design-system/admin';

export interface SecuritySessionVm {
  id: number;
  device: string;
  location: string;
  ip: string;
  last: string;
  current: boolean;
}

export type AuditLevel = 'info' | 'warn' | 'danger';

export interface AuditEntryVm {
  id: number;
  actor: string;
  action: string;
  time: string;
  level: AuditLevel;
}

export interface PostureItemVm {
  label: string;
  done: boolean;
}

/**
 * Admin Security page — UI-only security posture surface.
 *
 * IMPORTANT: All state here (toggles, sessions, audit log, password form,
 * danger zone) is local UI placeholder state. No backend calls are made.
 * Never display real secrets here.
 */
@Component({
  selector: 'app-admin-security',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminToggleComponent,
  ],
  templateUrl: './admin-security.component.html',
  styles: [`
    .sp-sec-row { display: grid; gap: 20px; margin-bottom: 20px; }
    .sp-sec-g2 { grid-template-columns: 1fr 1fr; }
    .sp-sec-g2-skew { grid-template-columns: 1fr 1.6fr; }
    @media (max-width: 800px) { .sp-sec-g2, .sp-sec-g2-skew { grid-template-columns: 1fr; } }

    .sp-sec-card-sub { font-size: 12.5px; color: var(--sp-admin-text-muted); margin: 2px 0 0; line-height: 1.5; }

    .sp-sec-posture { display: flex; align-items: center; gap: 20px; }
    .sp-sec-ring { flex-shrink: 0; }
    .sp-sec-check-list { flex: 1; display: flex; flex-direction: column; gap: 10px; }
    .sp-sec-check-row { display: flex; align-items: center; gap: 8px; }
    .sp-sec-check-box {
      width: 18px; height: 18px; border-radius: 5px; flex-shrink: 0;
      display: grid; place-items: center; font-size: 11px; font-weight: 900;
    }
    .sp-sec-check-box.on { background: var(--sp-admin-green-bg); color: var(--sp-admin-green); }
    .sp-sec-check-box.off { background: var(--sp-admin-bg); color: var(--sp-admin-text-dim); }
    .sp-sec-check-label { font-size: 13px; font-weight: 600; color: var(--sp-admin-text); }
    .sp-sec-check-label.off { color: var(--sp-admin-text-muted); }

    .sp-sec-toggle-row {
      display: flex; align-items: center; justify-content: space-between; gap: 12px;
      padding: 12px 0; border-top: 1px solid var(--sp-admin-border);
    }
    .sp-sec-toggle-row:first-child { border-top: none; padding-top: 0; }
    .sp-sec-toggle-label { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text); }
    .sp-sec-toggle-sub { font-size: 12px; color: var(--sp-admin-text-muted); margin-top: 1px; }

    .sp-sec-pw-summary {
      display: flex; align-items: center; gap: 12px; padding: 14px 16px;
      background: var(--sp-admin-bg); border-radius: 10px;
    }
    .sp-sec-pw-icon {
      width: 36px; height: 36px; border-radius: 10px; flex-shrink: 0;
      background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary);
      display: grid; place-items: center; font-size: 17px;
    }
    .sp-sec-pw-dots { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text); letter-spacing: 2px; }
    .sp-sec-pw-meta { font-size: 12px; color: var(--sp-admin-text-muted); }
    .sp-sec-pw-form { display: flex; flex-direction: column; gap: 12px; }
    .sp-sec-field { display: flex; flex-direction: column; gap: 4px; }
    .sp-sec-field-label { font-size: 12px; font-weight: 600; color: var(--sp-admin-text-muted); }
    .sp-sec-field-input {
      height: 36px; border-radius: 8px; border: 1.5px solid var(--sp-admin-border-2);
      padding: 0 12px; font-size: 13.5px; font-family: inherit; color: var(--sp-admin-text);
      background: var(--sp-admin-surface);
    }
    .sp-sec-field-input:focus { outline: none; border-color: var(--sp-admin-primary); box-shadow: var(--sp-admin-focus-ring); }
    .sp-sec-pw-actions { display: flex; gap: 8px; margin-top: 4px; }

    .sp-sec-session-row {
      display: flex; align-items: center; gap: 12px; padding: 12px 20px;
      border-bottom: 1px solid var(--sp-admin-border);
    }
    .sp-sec-session-row:last-child { border-bottom: none; }
    .sp-sec-session-icon {
      width: 36px; height: 36px; border-radius: 10px; flex-shrink: 0;
      display: grid; place-items: center; font-size: 16px;
      background: var(--sp-admin-bg); color: var(--sp-admin-text-muted);
    }
    .sp-sec-session-icon.current { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); }
    .sp-sec-session-main { flex: 1; min-width: 0; }
    .sp-sec-session-title { display: flex; align-items: center; gap: 7px; }
    .sp-sec-session-device { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text); }
    .sp-sec-session-meta { font-size: 11.5px; color: var(--sp-admin-text-muted); margin-top: 1px; }

    .sp-sec-log-row {
      display: flex; align-items: center; gap: 14px; padding: 11px 20px;
      border-bottom: 1px solid var(--sp-admin-border); font-size: 12.5px;
    }
    .sp-sec-log-row:last-of-type { border-bottom: none; }
    .sp-sec-log-row.danger { background: #FFF8F8; }
    .sp-sec-log-time { color: var(--sp-admin-text-muted); min-width: 72px; flex-shrink: 0; }
    .sp-sec-log-level { font-weight: 800; min-width: 44px; flex-shrink: 0; letter-spacing: .04em; }
    .sp-sec-log-actor { font-weight: 700; color: var(--sp-admin-text-muted); min-width: 56px; flex-shrink: 0; }
    .sp-sec-log-msg { color: var(--sp-admin-text); }

    .sp-sec-pagination {
      display: flex; align-items: center; justify-content: space-between; gap: 12px;
      padding: 14px 20px;
    }
    .sp-sec-pag-info { font-size: 12.5px; color: var(--sp-admin-text-muted); }
    .sp-sec-pag-btns { display: flex; gap: 4px; }
    .sp-sec-pag-btn {
      min-width: 30px; height: 30px; padding: 0 8px; border-radius: 7px;
      border: 1.5px solid var(--sp-admin-border-2); background: var(--sp-admin-surface);
      font-size: 12.5px; font-weight: 600; color: var(--sp-admin-text-secondary);
      cursor: pointer; font-family: inherit;
    }
    .sp-sec-pag-btn.cur { background: var(--sp-admin-primary); border-color: var(--sp-admin-primary); color: #fff; }
    .sp-sec-pag-btn:disabled { opacity: .45; cursor: not-allowed; }

    .sp-sec-danger {
      margin-top: 28px; border: 1.5px solid var(--sp-admin-danger-bg);
      border-radius: 14px; padding: 20px; background: #FFFBFB;
    }
    .sp-sec-danger-title { font-size: 14px; font-weight: 800; color: var(--sp-admin-danger-ink); }
    .sp-sec-danger-sub { font-size: 12.5px; color: var(--sp-admin-text-muted); margin: 2px 0 12px; }
    .sp-sec-danger-row {
      display: flex; align-items: center; justify-content: space-between; gap: 12px;
      padding: 12px 0; border-bottom: 1px solid var(--sp-admin-danger-bg);
    }
    .sp-sec-danger-row:last-child { border-bottom: none; }
    .sp-sec-danger-label { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text); }
    .sp-sec-danger-item-sub { font-size: 12px; color: var(--sp-admin-text-muted); margin-top: 1px; }
  `],
})
export class AdminSecurityComponent {
  // ── Access control toggles (UI-only) ──────────────────────────────────────
  readonly mfa = signal(true);
  readonly sessionAlerts = signal(true);
  readonly ipWhitelist = signal(false);
  readonly auditRetention = signal(false);

  // ── Password form (UI-only) ───────────────────────────────────────────────
  readonly showPasswordForm = signal(false);
  passwordForm = { current: '', next: '', confirm: '' };

  // ── Sessions (UI-only placeholder) ────────────────────────────────────────
  readonly sessions = signal<SecuritySessionVm[]>([
    { id: 1, device: 'Chrome on macOS',   location: 'London, UK',   ip: '82.44.120.5',  last: 'Now',        current: true },
    { id: 2, device: 'Safari on iPhone',  location: 'London, UK',   ip: '82.44.120.6',  last: '2h ago',     current: false },
    { id: 3, device: 'Chrome on Windows', location: 'Dubai, UAE',   ip: '94.200.11.21', last: 'Yesterday',  current: false },
    { id: 4, device: 'Firefox on Linux',  location: 'Frankfurt, DE', ip: '37.49.225.4', last: '3 days ago', current: false },
  ]);

  // ── Audit log (UI-only placeholder) ───────────────────────────────────────
  readonly auditLog: AuditEntryVm[] = [
    { id: 1, actor: 'Ehsan',  action: 'Updated AI Config — model changed to gpt-4o', time: '2 min ago',  level: 'info' },
    { id: 2, actor: 'Ehsan',  action: 'Exported student list (42 records)',          time: '18 min ago', level: 'warn' },
    { id: 3, actor: 'System', action: 'Automatic backup completed successfully',     time: '1 hr ago',   level: 'info' },
    { id: 4, actor: 'Ehsan',  action: 'Rotated Admin API key',                       time: '4 hr ago',   level: 'warn' },
    { id: 5, actor: 'Ehsan',  action: 'Signed in from Chrome on macOS',              time: 'Yesterday',  level: 'info' },
    { id: 6, actor: 'Ehsan',  action: 'Deleted student: Omar Khalid',                time: 'Yesterday',  level: 'danger' },
    { id: 7, actor: 'System', action: 'Failed login attempt (wrong password)',       time: '2 days ago', level: 'danger' },
    { id: 8, actor: 'Ehsan',  action: 'Changed notification settings',               time: '3 days ago', level: 'info' },
  ];

  // ── Posture ring ───────────────────────────────────────────────────────────
  readonly postureItems = computed<PostureItemVm[]>(() => [
    { label: 'MFA enabled',        done: this.mfa() },
    { label: 'Strong password',     done: true },
    { label: 'Session alerts on',   done: this.sessionAlerts() },
    { label: 'IP whitelist active', done: this.ipWhitelist() },
  ]);

  /** Score derived from which posture items pass (0-100). */
  readonly postureScore = computed(() => {
    const items = this.postureItems();
    const done = items.filter(i => i.done).length;
    return Math.round((done / items.length) * 100);
  });

  readonly postureTone = computed(() => {
    const s = this.postureScore();
    if (s >= 80) return 'success' as const;
    if (s >= 50) return 'warning' as const;
    return 'danger' as const;
  });

  readonly postureLabel = computed(() => {
    const s = this.postureScore();
    if (s >= 80) return 'Good';
    if (s >= 50) return 'Fair';
    return 'At risk';
  });

  // SVG ring geometry (r=34).
  readonly ringCircumference = 2 * Math.PI * 34;
  readonly ringDash = computed(() => {
    const frac = this.postureScore() / 100;
    const c = this.ringCircumference;
    return `${c * frac} ${c * (1 - frac)}`;
  });
  readonly ringOffset = this.ringCircumference * 0.25;
  readonly ringColor = computed(() => {
    const t = this.postureTone();
    if (t === 'success') return 'var(--sp-admin-green)';
    if (t === 'warning') return 'var(--sp-admin-warn)';
    return 'var(--sp-admin-danger)';
  });

  readonly levelColor: Record<AuditLevel, string> = {
    info: 'var(--sp-admin-green)',
    warn: 'var(--sp-admin-warn)',
    danger: 'var(--sp-admin-danger)',
  };
  readonly levelLabel: Record<AuditLevel, string> = {
    info: 'INFO',
    warn: 'WARN',
    danger: 'ERR',
  };

  // ── Actions (all UI-only) ──────────────────────────────────────────────────

  refresh(): void {
    // UI-only placeholder. No backend call.
  }

  togglePasswordForm(): void {
    this.showPasswordForm.update(v => !v);
  }

  cancelPasswordForm(): void {
    this.showPasswordForm.set(false);
    this.passwordForm = { current: '', next: '', confirm: '' };
  }

  updatePassword(): void {
    // UI-only placeholder. No backend call. Never persist secrets.
    this.cancelPasswordForm();
  }

  revokeSession(id: number): void {
    this.sessions.update(list => list.filter(s => s.id !== id));
  }

  revokeAllOthers(): void {
    this.sessions.update(list => list.filter(s => s.current));
  }

  // Danger zone — UI-only, no backend calls.
  dangerRevokeAll(): void { /* UI-only */ }
  dangerReset2fa(): void { /* UI-only */ }
  dangerWipeLog(): void { /* UI-only */ }
}
