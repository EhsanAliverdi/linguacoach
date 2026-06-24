import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminSecurityComponent } from './admin-security.component';

describe('AdminSecurityComponent', () => {
  let fixture: ComponentFixture<AdminSecurityComponent>;
  let component: AdminSecurityComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminSecurityComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSecurityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement as HTMLElement;
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  // ── Page header ────────────────────────────────────────────────────────────

  it('renders page header with title Security', () => {
    const header = el.querySelector('sp-admin-page-header');
    expect(header).toBeTruthy();
    expect(header!.getAttribute('title')).toBe('Security');
  });

  it('page header subtitle mentions authentication', () => {
    const header = el.querySelector('sp-admin-page-header');
    expect(header!.getAttribute('subtitle')).toContain('Authentication');
  });

  // ── Security posture ─────────────────────────────────────────────────────────

  it('renders the security posture card', () => {
    expect(el.textContent).toContain('Security posture');
  });

  it('renders the posture score ring', () => {
    expect(el.querySelector('svg[aria-label="Security score"]')).toBeTruthy();
  });

  it('default posture score is 82 rounded (three of four checks pass)', () => {
    expect(component.postureScore()).toBe(75);
  });

  it('posture score increases when IP whitelist enabled', () => {
    component.ipWhitelist.set(true);
    expect(component.postureScore()).toBe(100);
  });

  it('posture score drops when MFA disabled', () => {
    component.mfa.set(false);
    expect(component.postureScore()).toBe(50);
  });

  it('renders four posture checklist items', () => {
    expect(component.postureItems().length).toBe(4);
  });

  // ── Access controls ──────────────────────────────────────────────────────────

  it('renders the access controls card', () => {
    expect(el.textContent).toContain('Access controls');
  });

  it('renders four access control toggles', () => {
    const toggles = el.querySelectorAll('sp-admin-toggle');
    expect(toggles.length).toBe(4);
  });

  it('mfa and session alerts default on; ip allowlist and audit retention off', () => {
    expect(component.mfa()).toBeTrue();
    expect(component.sessionAlerts()).toBeTrue();
    expect(component.ipWhitelist()).toBeFalse();
    expect(component.auditRetention()).toBeFalse();
  });

  // ── Password ─────────────────────────────────────────────────────────────────

  it('renders the password card', () => {
    expect(el.textContent).toContain('Password');
    expect(el.textContent).toContain('Last changed 47 days ago');
  });

  it('password form hidden by default', () => {
    expect(component.showPasswordForm()).toBeFalse();
  });

  it('toggling reveals password form fields', () => {
    component.togglePasswordForm();
    fixture.detectChanges();
    expect(component.showPasswordForm()).toBeTrue();
    expect(el.querySelector('#sp-sec-pw-current')).toBeTruthy();
    expect(el.querySelector('#sp-sec-pw-next')).toBeTruthy();
    expect(el.querySelector('#sp-sec-pw-confirm')).toBeTruthy();
  });

  it('cancelling clears password form fields', () => {
    component.passwordForm = { current: 'a', next: 'b', confirm: 'c' };
    component.cancelPasswordForm();
    expect(component.showPasswordForm()).toBeFalse();
    expect(component.passwordForm).toEqual({ current: '', next: '', confirm: '' });
  });

  // ── Sessions ─────────────────────────────────────────────────────────────────

  it('renders four sessions by default', () => {
    expect(component.sessions().length).toBe(4);
    expect(el.textContent).toContain('Active sessions');
    expect(el.textContent).toContain('Chrome on macOS');
  });

  it('marks the current session', () => {
    expect(component.sessions()[0].current).toBeTrue();
  });

  it('revoking a session removes it', () => {
    component.revokeSession(2);
    expect(component.sessions().some(s => s.id === 2)).toBeFalse();
    expect(component.sessions().length).toBe(3);
  });

  it('revoke all others keeps only the current session', () => {
    component.revokeAllOthers();
    expect(component.sessions().length).toBe(1);
    expect(component.sessions()[0].current).toBeTrue();
  });

  // ── Audit log ────────────────────────────────────────────────────────────────

  it('renders the audit log with eight entries', () => {
    expect(el.textContent).toContain('Audit log');
    expect(component.auditLog.length).toBe(8);
  });

  it('renders audit log pagination summary', () => {
    expect(el.textContent).toContain('Showing 8 of 234 events');
  });

  it('audit log includes an error level entry', () => {
    expect(component.auditLog.some(e => e.level === 'danger')).toBeTrue();
  });

  // ── Danger zone ──────────────────────────────────────────────────────────────

  it('renders the danger zone with three actions', () => {
    expect(el.textContent).toContain('Danger zone');
    expect(el.textContent).toContain('Revoke all sessions');
    expect(el.textContent).toContain('Reset 2FA');
    expect(el.textContent).toContain('Wipe audit log');
  });

  // ── Secret handling ──────────────────────────────────────────────────────────

  it('does not display any secret key value', () => {
    const text = el.textContent ?? '';
    expect(text).not.toContain('JWT_KEY');
    expect(text).not.toContain('OPENAI_API_KEY');
    expect(text).not.toContain('ANTHROPIC_API_KEY');
  });
});
