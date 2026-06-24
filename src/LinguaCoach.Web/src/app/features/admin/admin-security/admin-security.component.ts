import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminSecuritySettings,
  AdminAuthEventItem,
  AdminAuthEventListQuery,
  PagedResponse,
} from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent, SpAdminBadgeTone,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminFormGridComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent, SpAdminSelectOption,
  SpAdminTableComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-admin-security',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-security.component.html',
  styles: [`
    .sp-sec-kpi-strip{display:grid;grid-template-columns:repeat(2,1fr);gap:14px;margin-bottom:20px;}
    @media(min-width:900px){.sp-sec-kpi-strip{grid-template-columns:repeat(4,1fr);}}
    .sp-sec-tab-bar{display:flex;gap:0;margin-bottom:20px;border-bottom:2px solid var(--sp-admin-border,#ECE9F5);}
    .sp-sec-tab{padding:8px 20px;font-size:14px;font-weight:600;background:none;border:none;border-bottom:2px solid transparent;margin-bottom:-2px;cursor:pointer;color:var(--sp-admin-muted,#8B85A0);transition:color 0.15s,border-color 0.15s;}
    .sp-sec-tab--active{color:var(--sp-admin-primary,#5B4BE8);border-bottom-color:var(--sp-admin-primary,#5B4BE8);}
    .sp-sec-cols{display:grid;grid-template-columns:1fr;gap:20px;align-items:start;margin-bottom:20px;}
    @media(min-width:1000px){.sp-sec-cols{grid-template-columns:1fr 1fr;}}
    .sp-sec-setting-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:16px;}
    @media(min-width:700px){.sp-sec-setting-grid{grid-template-columns:repeat(3,1fr);}}
    .sp-sec-field{display:flex;flex-direction:column;gap:4px;}
    .sp-sec-field-label{font-size:12px;font-weight:600;color:var(--sp-admin-muted,#8B85A0);}
    .sp-sec-field-value{font-size:14px;font-weight:700;color:var(--sp-admin-text,#211B36);}
    .sp-sec-config-note{font-size:12px;color:var(--sp-admin-muted,#8B85A0);background:var(--sp-admin-surface-alt,#F6F4FB);border-radius:8px;padding:10px 12px;margin-top:8px;}
    .sp-sec-deferred-grid{display:grid;gap:12px;}
    .sp-sec-deferred-row{display:grid;grid-template-columns:1fr auto;align-items:start;gap:6px 12px;padding:10px 0;border-top:1px solid var(--sp-admin-border-subtle,#F4F2FC);}
    .sp-sec-deferred-row:first-child{border-top:none;padding-top:0;}
    .sp-sec-deferred-label{font-size:13px;font-weight:700;color:var(--sp-admin-text,#211B36);}
    .sp-sec-deferred-note{grid-column:1/-1;font-size:12px;color:var(--sp-admin-text-muted,#8B85A0);}
  `],
})
export class AdminSecurityComponent implements OnInit {
  // ── Settings ──────────────────────────────────────────────────────────────
  settingsLoading = signal(false);
  settingsError = signal('');
  settings = signal<AdminSecuritySettings | null>(null);

  // ── Auth events tab ───────────────────────────────────────────────────────
  eventsLoading = signal(false);
  eventsError = signal('');
  events = signal<AdminAuthEventItem[]>([]);
  eventsTotal = signal(0);
  eventsPage = signal(1);
  readonly eventsPageSize = 20;
  eventsTotalPages = computed(() => Math.max(1, Math.ceil(this.eventsTotal() / this.eventsPageSize)));

  readonly kpiSummary = computed(() => {
    const s = this.settings();
    if (!s) return null;
    return {
      passwordMinLength: s.passwordPolicy.requiredLength,
      lockoutAttempts: s.lockout.maxFailedAccessAttempts,
      lockoutMinutes: s.lockout.lockoutDurationMinutes,
      ratePolicies: s.rateLimitPolicies.length,
      tokenRotation: s.refreshToken.rotationEnabled,
      googleEnabled: s.externalLogin.google.enabled,
      googleConfigured: s.externalLogin.google.clientIdConfigured && s.externalLogin.google.clientSecretConfigured,
    };
  });

  eventTypeFilter = '';
  outcomeFilter = '';
  emailSearch = '';

  activeTab: 'overview' | 'events' = 'overview';

  readonly eventTypeOptions: SpAdminSelectOption[] = [
    { value: 'LoginSucceeded', label: 'Login Succeeded' },
    { value: 'LoginFailed', label: 'Login Failed' },
    { value: 'LoginLockedOut', label: 'Login Locked Out' },
    { value: 'PasswordChanged', label: 'Password Changed' },
    { value: 'PasswordChangeFailed', label: 'Password Change Failed' },
    { value: 'PasswordResetRequested', label: 'Password Reset Requested' },
    { value: 'PasswordResetSucceeded', label: 'Password Reset Succeeded' },
    { value: 'PasswordResetFailed', label: 'Password Reset Failed' },
    { value: 'ExternalLoginSucceeded', label: 'External Login Succeeded' },
    { value: 'ExternalLoginFailed', label: 'External Login Failed' },
    { value: 'ExternalLoginLinked', label: 'External Login Linked' },
    { value: 'ExternalLoginRejected', label: 'External Login Rejected' },
    { value: 'RefreshTokenIssued', label: 'Refresh Token Issued' },
    { value: 'RefreshTokenRotated', label: 'Refresh Token Rotated' },
    { value: 'RefreshTokenReuseDetected', label: 'Refresh Token Reuse Detected' },
    { value: 'AllSessionsRevoked', label: 'All Sessions Revoked' },
  ];

  readonly outcomeOptions: SpAdminSelectOption[] = [
    { value: 'Success', label: 'Success' },
    { value: 'Failure', label: 'Failure' },
    { value: 'Blocked', label: 'Blocked' },
  ];

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.loadSettings();
    this.loadEvents();
  }

  // ── Settings ──────────────────────────────────────────────────────────────

  loadSettings(): void {
    this.settingsLoading.set(true);
    this.settingsError.set('');
    this.adminApi.getSecuritySettings().subscribe({
      next: (s) => { this.settings.set(s); this.settingsLoading.set(false); },
      error: () => { this.settingsError.set('Could not load security settings.'); this.settingsLoading.set(false); },
    });
  }

  // ── Auth events ───────────────────────────────────────────────────────────

  loadEvents(): void {
    this.eventsLoading.set(true);
    this.eventsError.set('');
    const q: AdminAuthEventListQuery = {
      page: this.eventsPage(),
      pageSize: this.eventsPageSize,
      eventType: this.eventTypeFilter || undefined,
      outcome: this.outcomeFilter || undefined,
      email: this.emailSearch || undefined,
    };
    this.adminApi.listSecurityAuthEvents(q).subscribe({
      next: (res: PagedResponse<AdminAuthEventItem>) => {
        this.events.set(res.items);
        this.eventsTotal.set(res.totalCount);
        this.eventsLoading.set(false);
      },
      error: () => { this.eventsError.set('Could not load auth events.'); this.eventsLoading.set(false); },
    });
  }

  applyEventFilters(): void { this.eventsPage.set(1); this.loadEvents(); }
  onEventsPage(page: number): void { this.eventsPage.set(page); this.loadEvents(); }

  onTabChange(tab: 'overview' | 'events'): void {
    this.activeTab = tab;
  }

  // ── Tone helpers ──────────────────────────────────────────────────────────

  outcomeTone(outcome: string): SpAdminBadgeTone {
    switch (outcome) {
      case 'Success': return 'success';
      case 'Failure': return 'danger';
      case 'Blocked': return 'warning';
      default: return 'neutral';
    }
  }

  boolTone(val: boolean): SpAdminBadgeTone {
    return val ? 'success' : 'neutral';
  }

  boolLabel(val: boolean, trueLabel = 'Yes', falseLabel = 'No'): string {
    return val ? trueLabel : falseLabel;
  }

  configuredTone(configured: boolean): SpAdminBadgeTone {
    return configured ? 'success' : 'warning';
  }

  timeAgo(iso: string): string {
    const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
    if (diff < 60) return `${diff}s ago`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
  }

  formatEventType(type: string): string {
    return type.replace(/([A-Z])/g, ' $1').trim();
  }
}
