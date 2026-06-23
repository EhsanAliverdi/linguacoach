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
  SpAdminBadgeComponent, SpAdminBadgeTone,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminInputComponent,
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
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-security.component.html',
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
