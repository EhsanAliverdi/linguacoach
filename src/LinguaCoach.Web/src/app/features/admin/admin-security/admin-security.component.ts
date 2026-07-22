import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminSecurityService,
  AdminSecuritySettings,
  AdminAuthEventItem,
  AdminAuthEventListParams,
} from '../../../core/services/admin-security.service';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminStatusCardComponent,
  SpAdminStatusGridComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
  SpAdminTruncatedTextComponent,
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
    SpAdminCodePillComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminStatusCardComponent,
    SpAdminStatusGridComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
  ],
  templateUrl: './admin-security.component.html',
})
export class AdminSecurityComponent implements OnInit {
  readonly ratePolicyColumns: SpAdminTableColumn[] = [
    { key: 'policyName', label: 'Policy' },
    { key: 'permitLimit', label: 'Permit limit' },
    { key: 'windowMinutes', label: 'Window' },
    { key: 'keyedBy', label: 'Keyed by' },
  ];

  readonly eventColumns: SpAdminTableColumn[] = [
    { key: 'occurredAtUtc', label: 'Time' },
    { key: 'eventType', label: 'Event type' },
    { key: 'outcome', label: 'Outcome' },
    { key: 'email', label: 'Email' },
    { key: 'ipAddress', label: 'IP' },
    { key: 'correlationId', label: 'Correlation' },
  ];

  settings = signal<AdminSecuritySettings | null>(null);
  loadingSettings = signal(true);
  settingsError = signal('');

  events = signal<AdminAuthEventItem[]>([]);
  eventsTotal = signal(0);
  loadingEvents = signal(false);
  eventsError = signal('');
  eventsPage = signal(1);
  readonly eventsPageSize = 20;

  filterEmail = '';
  filterEventType = signal('');
  filterOutcome = signal('');

  readonly eventsTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.eventsTotal() / this.eventsPageSize))
  );

  readonly eventTypeOptions = [
    { value: '', label: 'All types' },
    { value: 'Login', label: 'Login' },
    { value: 'Logout', label: 'Logout' },
    { value: 'PasswordReset', label: 'Password reset' },
    { value: 'PasswordChange', label: 'Password change' },
    { value: 'ExternalLogin', label: 'External login' },
    { value: 'TokenRefresh', label: 'Token refresh' },
    { value: 'AccountLocked', label: 'Account locked' },
  ];

  readonly outcomeOptions = [
    { value: '', label: 'All outcomes' },
    { value: 'Success', label: 'Success' },
    { value: 'Failure', label: 'Failure' },
  ];

  constructor(private svc: AdminSecurityService) {}

  ngOnInit(): void {
    this.loadSettings();
    this.loadEvents();
  }

  loadSettings(): void {
    this.loadingSettings.set(true);
    this.settingsError.set('');
    this.svc.getSettings().subscribe({
      next: s => { this.settings.set(s); this.loadingSettings.set(false); },
      error: err => { this.loadingSettings.set(false); this.settingsError.set(err.error?.error ?? 'Could not load security settings.'); },
    });
  }

  loadEvents(): void {
    this.loadingEvents.set(true);
    this.eventsError.set('');
    const params: AdminAuthEventListParams = {
      page: this.eventsPage(),
      pageSize: this.eventsPageSize,
      email: this.filterEmail || undefined,
      eventType: this.filterEventType() || undefined,
      outcome: this.filterOutcome() || undefined,
    };
    this.svc.getAuthEvents(params).subscribe({
      next: r => { this.events.set(r.items); this.eventsTotal.set(r.total); this.loadingEvents.set(false); },
      error: err => { this.loadingEvents.set(false); this.eventsError.set(err.error?.error ?? 'Could not load auth events.'); },
    });
  }

  onPageChange(page: number): void {
    this.eventsPage.set(page);
    this.loadEvents();
  }

  onFilterChange(): void {
    this.eventsPage.set(1);
    this.loadEvents();
  }

  private emailSearchDebounce?: ReturnType<typeof setTimeout>;
  onEmailSearchChange(value: string): void {
    this.filterEmail = value;
    clearTimeout(this.emailSearchDebounce);
    this.emailSearchDebounce = setTimeout(() => this.onFilterChange(), 300);
  }

  eventsFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'eventType', label: 'Event type', options: this.eventTypeOptions, value: this.filterEventType() },
    { key: 'outcome', label: 'Outcome', options: this.outcomeOptions, value: this.filterOutcome() },
  ]);

  onEventsFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'eventType') this.filterEventType.set(event.value);
    else if (event.key === 'outcome') this.filterOutcome.set(event.value);
    this.onFilterChange();
  }

  outcomeTone(outcome: string): 'success' | 'danger' | 'neutral' {
    if (outcome?.toLowerCase() === 'success') return 'success';
    if (outcome?.toLowerCase() === 'failure') return 'danger';
    return 'neutral';
  }

  formatDateTime(iso: string): string {
    try {
      const d = new Date(iso);
      const date = d.toLocaleDateString('en-AU', { day: '2-digit', month: 'short' });
      const time = d.toLocaleTimeString('en-AU', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' });
      return `${date} ${time}`;
    } catch { return iso; }
  }

  boolTone(v: boolean): 'success' | 'neutral' {
    return v ? 'success' : 'neutral';
  }

  boolLabel(v: boolean, yes = 'Yes', no = 'No'): string {
    return v ? yes : no;
  }
}
