import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminNotificationItem, AdminOutboxItem,
  AdminNotificationListQuery, AdminOutboxListQuery,
  PagedResponse,
} from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent, SpAdminBadgeTone,
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
} from '../../../admin';

@Component({
  selector: 'app-admin-notifications',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
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
  templateUrl: './admin-notifications.component.html',
})
export class AdminNotificationsComponent implements OnInit {
  // ── Notification tab ───────────────────────────────────────────────────────
  notifLoading = signal(false);
  notifError = signal('');
  notifications = signal<AdminNotificationItem[]>([]);
  notifTotal = signal(0);
  notifPage = signal(1);
  readonly notifPageSize = 20;
  notifTotalPages = computed(() => Math.max(1, Math.ceil(this.notifTotal() / this.notifPageSize)));

  notifChannelFilter = '';
  notifStatusFilter = '';
  notifCategoryFilter = '';
  notifSeverityFilter = '';
  notifSearch = '';

  // ── Outbox tab ─────────────────────────────────────────────────────────────
  outboxLoading = signal(false);
  outboxError = signal('');
  outbox = signal<AdminOutboxItem[]>([]);
  outboxTotal = signal(0);
  outboxPage = signal(1);
  readonly outboxPageSize = 20;
  outboxTotalPages = computed(() => Math.max(1, Math.ceil(this.outboxTotal() / this.outboxPageSize)));

  outboxChannelFilter = '';
  outboxStatusFilter = '';
  outboxFailedOnly = false;

  activeTab: 'notifications' | 'outbox' = 'notifications';

  retryingId = signal<string | null>(null);
  cancellingId = signal<string | null>(null);

  readonly channelOptions: SpAdminSelectOption[] = [
    { value: 'InApp', label: 'InApp' },
    { value: 'Email', label: 'Email' },
    { value: 'Sms', label: 'SMS' },
  ];
  readonly notifStatusOptions: SpAdminSelectOption[] = [
    { value: 'Queued', label: 'Queued' },
    { value: 'Delivered', label: 'Delivered' },
    { value: 'Read', label: 'Read' },
    { value: 'Failed', label: 'Failed' },
    { value: 'Archived', label: 'Archived' },
  ];
  readonly outboxStatusOptions: SpAdminSelectOption[] = [
    { value: 'Queued', label: 'Queued' },
    { value: 'Delivered', label: 'Delivered' },
    { value: 'Failed', label: 'Failed' },
    { value: 'Archived', label: 'Archived' },
  ];
  readonly categoryOptions: SpAdminSelectOption[] = [
    { value: 'System', label: 'System' },
    { value: 'Account', label: 'Account' },
    { value: 'Learning', label: 'Learning' },
    { value: 'BillingUsage', label: 'Billing/Usage' },
    { value: 'Admin', label: 'Admin' },
    { value: 'BackgroundJob', label: 'Background Job' },
  ];
  readonly severityOptions: SpAdminSelectOption[] = [
    { value: 'Info', label: 'Info' },
    { value: 'Success', label: 'Success' },
    { value: 'Warning', label: 'Warning' },
    { value: 'Error', label: 'Error' },
  ];

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.loadNotifications();
    this.loadOutbox();
  }

  loadNotifications(): void {
    this.notifLoading.set(true);
    this.notifError.set('');
    const q: AdminNotificationListQuery = {
      page: this.notifPage(),
      pageSize: this.notifPageSize,
      channel: this.notifChannelFilter || undefined,
      status: this.notifStatusFilter || undefined,
      category: this.notifCategoryFilter || undefined,
      severity: this.notifSeverityFilter || undefined,
      search: this.notifSearch || undefined,
    };
    this.adminApi.listAdminNotifications(q).subscribe({
      next: (res) => {
        this.notifications.set(res.items);
        this.notifTotal.set(res.totalCount);
        this.notifLoading.set(false);
      },
      error: () => {
        this.notifError.set('Could not load notifications.');
        this.notifLoading.set(false);
      },
    });
  }

  loadOutbox(): void {
    this.outboxLoading.set(true);
    this.outboxError.set('');
    const q: AdminOutboxListQuery = {
      page: this.outboxPage(),
      pageSize: this.outboxPageSize,
      channel: this.outboxChannelFilter || undefined,
      status: this.outboxStatusFilter || undefined,
      failedOnly: this.outboxFailedOnly || undefined,
    };
    this.adminApi.listAdminOutbox(q).subscribe({
      next: (res) => {
        this.outbox.set(res.items);
        this.outboxTotal.set(res.totalCount);
        this.outboxLoading.set(false);
      },
      error: () => {
        this.outboxError.set('Could not load outbox.');
        this.outboxLoading.set(false);
      },
    });
  }

  applyNotifFilters(): void {
    this.notifPage.set(1);
    this.loadNotifications();
  }

  applyOutboxFilters(): void {
    this.outboxPage.set(1);
    this.loadOutbox();
  }

  onNotifPage(page: number): void {
    this.notifPage.set(page);
    this.loadNotifications();
  }

  onOutboxPage(page: number): void {
    this.outboxPage.set(page);
    this.loadOutbox();
  }

  retry(item: AdminOutboxItem): void {
    this.retryingId.set(item.id);
    this.adminApi.retryOutboxItem(item.id).subscribe({
      next: () => {
        this.retryingId.set(null);
        this.loadOutbox();
      },
      error: () => this.retryingId.set(null),
    });
  }

  cancel(item: AdminOutboxItem): void {
    this.cancellingId.set(item.id);
    this.adminApi.cancelOutboxItem(item.id).subscribe({
      next: () => {
        this.cancellingId.set(null);
        this.loadOutbox();
      },
      error: () => this.cancellingId.set(null),
    });
  }

  canRetry(item: AdminOutboxItem): boolean {
    return item.status === 'Failed' || item.status === 'Queued';
  }

  canCancel(item: AdminOutboxItem): boolean {
    return item.status !== 'Delivered' && item.status !== 'Archived';
  }

  statusTone(status: string): SpAdminBadgeTone {
    switch (status) {
      case 'Delivered': return 'success';
      case 'Failed': return 'danger';
      case 'Queued': return 'warning';
      case 'Archived': return 'neutral';
      default: return 'info';
    }
  }

  severityTone(severity: string): SpAdminBadgeTone {
    switch (severity) {
      case 'Error': return 'danger';
      case 'Warning': return 'warning';
      case 'Success': return 'success';
      default: return 'info';
    }
  }

  timeAgo(iso: string): string {
    const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
    if (diff < 60) return `${diff}s ago`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
  }
}
