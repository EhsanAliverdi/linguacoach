import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AdminPasswordPolicySettings {
  requiredLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireDigit: boolean;
  requireNonAlphanumeric: boolean;
}

export interface AdminLockoutSettings {
  maxFailedAccessAttempts: number;
  lockoutDurationMinutes: number;
}

export interface AdminRateLimitPolicyInfo {
  policyName: string;
  permitLimit: number;
  windowMinutes: number;
  keyedBy: string;
}

export interface AdminJwtSettings {
  accessTokenExpiryHours: number;
  issuerConfigured: boolean;
  audienceConfigured: boolean;
}

export interface AdminRefreshTokenSettings {
  expiryDays: number;
  rotationEnabled: boolean;
  revokeOnPasswordChange: boolean;
  revokeOnPasswordReset: boolean;
}

export interface AdminSecurityHeadersSettings {
  xContentTypeOptionsEnabled: boolean;
  xFrameOptionsEnabled: boolean;
  referrerPolicyEnabled: boolean;
  permissionsPolicyEnabled: boolean;
  cspStatus: string;
  hstsStatus: string;
}

export interface AdminGoogleExternalLoginSettings {
  enabled: boolean;
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  allowAutoLinkByEmail: boolean;
  allowStudentAutoProvisioning: boolean;
  allowedDomains: string[];
}

export interface AdminExternalLoginSettings {
  google: AdminGoogleExternalLoginSettings;
}

export interface AdminSecuritySettings {
  passwordPolicy: AdminPasswordPolicySettings;
  lockout: AdminLockoutSettings;
  rateLimitPolicies: AdminRateLimitPolicyInfo[];
  jwt: AdminJwtSettings;
  refreshToken: AdminRefreshTokenSettings;
  securityHeaders: AdminSecurityHeadersSettings;
  externalLogin: AdminExternalLoginSettings;
}

export interface AdminAuthEventItem {
  id: string;
  eventType: string;
  outcome: string;
  userId: string | null;
  emailOrUserName: string | null;
  failureReasonCode: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  occurredAtUtc: string;
}

export interface AdminAuthEventListParams {
  page?: number;
  pageSize?: number;
  userId?: string;
  email?: string;
  eventType?: string;
  outcome?: string;
  from?: string;
  to?: string;
}

export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AdminSecurityService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getSettings(): Observable<AdminSecuritySettings> {
    return this.http.get<AdminSecuritySettings>(`${this.api}/admin/security/settings`);
  }

  getAuthEvents(params: AdminAuthEventListParams = {}): Observable<PagedResponse<AdminAuthEventItem>> {
    let p = new HttpParams();
    if (params.page)      p = p.set('page', params.page);
    if (params.pageSize)  p = p.set('pageSize', params.pageSize);
    if (params.userId)    p = p.set('userId', params.userId);
    if (params.email)     p = p.set('email', params.email);
    if (params.eventType) p = p.set('eventType', params.eventType);
    if (params.outcome)   p = p.set('outcome', params.outcome);
    if (params.from)      p = p.set('from', params.from);
    if (params.to)        p = p.set('to', params.to);
    return this.http.get<PagedResponse<AdminAuthEventItem>>(`${this.api}/admin/security/auth-events`, { params: p });
  }
}
