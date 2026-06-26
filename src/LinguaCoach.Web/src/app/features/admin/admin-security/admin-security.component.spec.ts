import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminSecurityComponent } from './admin-security.component';
import { AdminSecurityService } from '../../../core/services/admin-security.service';
import type { AdminSecuritySettings, AdminAuthEventItem } from '../../../core/services/admin-security.service';

const SETTINGS: AdminSecuritySettings = {
  passwordPolicy: {
    requiredLength: 12,
    requireUppercase: true,
    requireDigit: true,
    requireSpecial: true,
  } as any,
  lockout: { maxFailedAttempts: 5, lockoutDurationMinutes: 15 } as any,
  rateLimitPolicies: [],
  jwt: { accessTokenExpiryMinutes: 60, issuer: 'test', audience: 'test' } as any,
  refreshToken: { expiryDays: 30, rotateOnUse: true } as any,
  securityHeaders: { hstsEnabled: true } as any,
  externalLogin: { google: { enabled: false, clientIdConfigured: false, clientSecretConfigured: false, allowAutoLinkByEmail: false, allowStudentAutoProvisioning: false, allowedDomains: [] } },
};

const EVENT: AdminAuthEventItem = {
  id: 'evt-1',
  userId: 'u1',
  emailOrUserName: 'alice@example.com',
  eventType: 'Login',
  outcome: 'Success',
  ipAddress: '1.2.3.4',
  failureReasonCode: null,
  correlationId: null,
  occurredAtUtc: '2026-06-01T10:00:00Z',
};

const EVENTS_RESPONSE = { items: [EVENT], total: 1, page: 1, pageSize: 20 };

function makeSvc(settingsOk = true, eventsOk = true) {
  return {
    getSettings: jasmine.createSpy('getSettings').and.returnValue(
      settingsOk ? of(SETTINGS) : throwError(() => ({ error: { error: 'Settings unavailable' } }))
    ),
    getAuthEvents: jasmine.createSpy('getAuthEvents').and.returnValue(
      eventsOk ? of(EVENTS_RESPONSE) : throwError(() => ({ error: { error: 'Events unavailable' } }))
    ),
  };
}

describe('AdminSecurityComponent', () => {
  let fixture: ComponentFixture<AdminSecurityComponent>;
  let component: AdminSecurityComponent;
  let svc: ReturnType<typeof makeSvc>;

  async function setup(settingsOk = true, eventsOk = true) {
    svc = makeSvc(settingsOk, eventsOk);
    await TestBed.configureTestingModule({
      imports: [AdminSecurityComponent],
      providers: [
        provideRouter([]),
        { provide: AdminSecurityService, useValue: svc },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminSecurityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  // ── Init ─────────────────────────────────────────────────────────────────────

  it('creates the component', async () => {
    await setup();
    expect(component).toBeTruthy();
  });

  it('calls getSettings on init', async () => {
    await setup();
    expect(svc.getSettings).toHaveBeenCalledTimes(1);
  });

  it('calls getAuthEvents on init', async () => {
    await setup();
    expect(svc.getAuthEvents).toHaveBeenCalledTimes(1);
  });

  // ── Page structure ────────────────────────────────────────────────────────────

  it('renders page header', async () => {
    await setup();
    expect(fixture.nativeElement.querySelector('sp-admin-page-header')).toBeTruthy();
  });

  it('page header has title Security', async () => {
    await setup();
    const header = fixture.nativeElement.querySelector('sp-admin-page-header');
    expect(header?.getAttribute('title')).toBe('Security');
  });

  it('renders page body', async () => {
    await setup();
    expect(fixture.nativeElement.querySelector('sp-admin-page-body')).toBeTruthy();
  });

  // ── Settings signal ───────────────────────────────────────────────────────────

  it('settings signal is populated after load', async () => {
    await setup();
    expect(component.settings()).toEqual(SETTINGS);
  });

  it('loadingSettings is false after successful load', async () => {
    await setup();
    expect(component.loadingSettings()).toBeFalse();
  });

  it('settingsError is empty after successful load', async () => {
    await setup();
    expect(component.settingsError()).toBe('');
  });

  // ── Auth events signal ────────────────────────────────────────────────────────

  it('events signal is populated after load', async () => {
    await setup();
    expect(component.events().length).toBe(1);
    expect(component.events()[0].emailOrUserName).toBe('alice@example.com');
  });

  it('eventsTotal reflects response', async () => {
    await setup();
    expect(component.eventsTotal()).toBe(1);
  });

  it('eventsPage defaults to 1', async () => {
    await setup();
    expect(component.eventsPage()).toBe(1);
  });

  it('eventsTotalPages is 1 when total <= pageSize', async () => {
    await setup();
    expect(component.eventsTotalPages()).toBe(1);
  });

  it('loadingEvents is false after successful load', async () => {
    await setup();
    expect(component.loadingEvents()).toBeFalse();
  });

  // ── Error states ──────────────────────────────────────────────────────────────

  it('sets settingsError when getSettings fails', async () => {
    await setup(false, true);
    expect(component.settingsError()).toContain('Settings unavailable');
  });

  it('sets eventsError when getAuthEvents fails', async () => {
    await setup(true, false);
    expect(component.eventsError()).toContain('Events unavailable');
  });

  it('settings signal remains null when load fails', async () => {
    await setup(false, true);
    expect(component.settings()).toBeNull();
  });

  // ── Pagination ────────────────────────────────────────────────────────────────

  it('onPageChange updates eventsPage and reloads events', async () => {
    await setup();
    svc.getAuthEvents.calls.reset();
    component.onPageChange(2);
    expect(component.eventsPage()).toBe(2);
    expect(svc.getAuthEvents).toHaveBeenCalledTimes(1);
  });

  // ── Filter ────────────────────────────────────────────────────────────────────

  it('onFilterChange resets page to 1 and reloads events', async () => {
    await setup();
    component.eventsPage.set(3);
    svc.getAuthEvents.calls.reset();
    component.onFilterChange();
    expect(component.eventsPage()).toBe(1);
    expect(svc.getAuthEvents).toHaveBeenCalledTimes(1);
  });

  // ── Helper methods ────────────────────────────────────────────────────────────

  it('outcomeTone returns success for Success', async () => {
    await setup();
    expect(component.outcomeTone('Success')).toBe('success');
  });

  it('outcomeTone returns danger for Failure', async () => {
    await setup();
    expect(component.outcomeTone('Failure')).toBe('danger');
  });

  it('outcomeTone returns neutral for unknown', async () => {
    await setup();
    expect(component.outcomeTone('Unknown')).toBe('neutral');
  });

  it('boolTone returns success for true', async () => {
    await setup();
    expect(component.boolTone(true)).toBe('success');
  });

  it('boolTone returns neutral for false', async () => {
    await setup();
    expect(component.boolTone(false)).toBe('neutral');
  });

  it('boolLabel returns yes label for true', async () => {
    await setup();
    expect(component.boolLabel(true, 'Enabled', 'Disabled')).toBe('Enabled');
  });

  it('boolLabel returns no label for false', async () => {
    await setup();
    expect(component.boolLabel(false, 'Enabled', 'Disabled')).toBe('Disabled');
  });

  it('formatDateTime returns a formatted string for valid ISO', async () => {
    await setup();
    const result = component.formatDateTime('2026-06-01T10:00:00Z');
    expect(result).toBeTruthy();
    expect(result).not.toBe('2026-06-01T10:00:00Z'); // should be transformed
  });

  it('formatDateTime returns original string for invalid input', async () => {
    await setup();
    expect(component.formatDateTime('not-a-date')).toBeTruthy();
  });

  // ── Secret handling ───────────────────────────────────────────────────────────

  it('does not expose any secret key in rendered content', async () => {
    await setup();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).not.toContain('JWT_KEY');
    expect(text).not.toContain('OPENAI_API_KEY');
    expect(text).not.toContain('ANTHROPIC_API_KEY');
    expect(text).not.toContain('sk-');
  });

  // ── Filter option arrays ──────────────────────────────────────────────────────

  it('eventTypeOptions includes All types and Login', async () => {
    await setup();
    const labels = component.eventTypeOptions.map(o => o.label);
    expect(labels).toContain('All types');
    expect(labels).toContain('Login');
  });

  it('outcomeOptions includes All outcomes and Success', async () => {
    await setup();
    const labels = component.outcomeOptions.map(o => o.label);
    expect(labels).toContain('All outcomes');
    expect(labels).toContain('Success');
  });
});
