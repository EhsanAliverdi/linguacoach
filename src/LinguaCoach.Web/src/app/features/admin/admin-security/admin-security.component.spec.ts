import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';

import { AdminSecurityComponent } from './admin-security.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminSecuritySettings, AdminAuthEventItem, PagedResponse } from '../../../core/models/admin.models';

const mockSettings: AdminSecuritySettings = {
  passwordPolicy: {
    requiredLength: 12,
    requireUppercase: true,
    requireLowercase: true,
    requireDigit: true,
    requireNonAlphanumeric: true,
  },
  lockout: { maxFailedAccessAttempts: 5, lockoutDurationMinutes: 15 },
  rateLimitPolicies: [
    { policyName: 'AuthLogin', permitLimit: 10, windowMinutes: 5, keyedBy: 'IP' },
  ],
  jwt: { accessTokenExpiryHours: 24, issuerConfigured: true, audienceConfigured: true },
  refreshToken: {
    expiryDays: 14,
    rotationEnabled: true,
    revokeOnPasswordChange: true,
    revokeOnPasswordReset: true,
  },
  securityHeaders: {
    xContentTypeOptionsEnabled: true,
    xFrameOptionsEnabled: true,
    referrerPolicyEnabled: true,
    permissionsPolicyEnabled: true,
    cspStatus: 'Deferred',
    hstsStatus: 'Deferred',
  },
  externalLogin: {
    google: {
      enabled: false,
      clientIdConfigured: false,
      clientSecretConfigured: false,
      allowAutoLinkByEmail: true,
      allowStudentAutoProvisioning: false,
      allowedDomains: [],
    },
  },
};

const mockEvent: AdminAuthEventItem = {
  id: 'evt-1',
  eventType: 'LoginSucceeded',
  outcome: 'Success',
  userId: 'u1',
  emailOrUserName: 'test@example.com',
  failureReasonCode: null,
  ipAddress: '127.0.0.1',
  correlationId: null,
  occurredAtUtc: new Date().toISOString(),
};

const mockEventsPage: PagedResponse<AdminAuthEventItem> = {
  items: [mockEvent],
  totalCount: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

describe('AdminSecurityComponent', () => {
  let fixture: ComponentFixture<AdminSecurityComponent>;
  let component: AdminSecurityComponent;
  let adminApi: jasmine.SpyObj<AdminApiService>;

  beforeEach(async () => {
    const spy = jasmine.createSpyObj<AdminApiService>('AdminApiService', [
      'getSecuritySettings',
      'listSecurityAuthEvents',
    ]);
    spy.getSecuritySettings.and.returnValue(of(mockSettings));
    spy.listSecurityAuthEvents.and.returnValue(of(mockEventsPage));

    await TestBed.configureTestingModule({
      imports: [AdminSecurityComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AdminApiService, useValue: spy },
      ],
    }).compileComponents();

    adminApi = TestBed.inject(AdminApiService) as jasmine.SpyObj<AdminApiService>;
    fixture = TestBed.createComponent(AdminSecurityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('loads settings on init', () => {
    expect(adminApi.getSecuritySettings).toHaveBeenCalled();
    expect(component.settings()).toEqual(mockSettings);
    expect(component.settingsLoading()).toBeFalse();
    expect(component.settingsError()).toBe('');
  });

  it('loads auth events on init', () => {
    expect(adminApi.listSecurityAuthEvents).toHaveBeenCalled();
    expect(component.events().length).toBe(1);
    expect(component.eventsTotal()).toBe(1);
    expect(component.eventsLoading()).toBeFalse();
  });

  it('shows settings error when API fails', async () => {
    adminApi.getSecuritySettings.and.returnValue(throwError(() => new Error('fail')));
    component.loadSettings();
    expect(component.settingsError()).toBe('Could not load security settings.');
  });

  it('shows events error when API fails', async () => {
    adminApi.listSecurityAuthEvents.and.returnValue(throwError(() => new Error('fail')));
    component.loadEvents();
    expect(component.eventsError()).toBe('Could not load auth events.');
  });

  it('defaults to overview tab', () => {
    expect(component.activeTab).toBe('overview');
  });

  it('switches to events tab', () => {
    component.onTabChange('events');
    expect(component.activeTab).toBe('events');
  });

  it('resets page on filter apply', () => {
    component.eventsPage.set(3);
    component.applyEventFilters();
    expect(component.eventsPage()).toBe(1);
  });

  it('updates page on pagination', () => {
    component.onEventsPage(2);
    expect(component.eventsPage()).toBe(2);
    expect(adminApi.listSecurityAuthEvents).toHaveBeenCalledTimes(2);
  });

  describe('tone helpers', () => {
    it('boolTone returns success for true', () => {
      expect(component.boolTone(true)).toBe('success');
    });
    it('boolTone returns neutral for false', () => {
      expect(component.boolTone(false)).toBe('neutral');
    });
    it('configuredTone returns success for configured', () => {
      expect(component.configuredTone(true)).toBe('success');
    });
    it('configuredTone returns warning when not configured', () => {
      expect(component.configuredTone(false)).toBe('warning');
    });
    it('outcomeTone returns success for Success', () => {
      expect(component.outcomeTone('Success')).toBe('success');
    });
    it('outcomeTone returns danger for Failure', () => {
      expect(component.outcomeTone('Failure')).toBe('danger');
    });
    it('outcomeTone returns warning for Blocked', () => {
      expect(component.outcomeTone('Blocked')).toBe('warning');
    });
  });

  describe('boolLabel', () => {
    it('returns trueLabel when true', () => {
      expect(component.boolLabel(true, 'Required', 'Not required')).toBe('Required');
    });
    it('returns falseLabel when false', () => {
      expect(component.boolLabel(false, 'Required', 'Not required')).toBe('Not required');
    });
  });

  describe('computedTotalPages', () => {
    it('computes total pages', () => {
      component.eventsTotal.set(45);
      expect(component.eventsTotalPages()).toBe(3);
    });
    it('returns at least 1 for empty result', () => {
      component.eventsTotal.set(0);
      expect(component.eventsTotalPages()).toBe(1);
    });
  });

  describe('formatEventType', () => {
    it('inserts spaces before capitals', () => {
      expect(component.formatEventType('LoginSucceeded')).toBe('Login Succeeded');
    });
  });

  // ── Deferred security capabilities card ──────────────────────────────────

  describe('deferred security capabilities card', () => {
    it('renders the deferred capabilities card', () => {
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('Deferred security capabilities');
    });

    it('lists MFA', () => {
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('Multi-factor authentication');
    });

    it('lists enterprise SSO', () => {
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('Enterprise SSO');
    });

    it('lists distributed rate limiting', () => {
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('Distributed');
    });

    it('lists CSP or deployment hardening', () => {
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text.includes('CSP') || text.includes('deployment')).toBeTrue();
    });

    it('does not display any secret key or credential value', () => {
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).not.toContain('ANTHROPIC_API_KEY');
      expect(text).not.toContain('OPENAI_API_KEY');
      expect(text).not.toContain('JWT_KEY');
    });
  });
});
