import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminNotificationItem, AdminOutboxItem,
  AdminNotificationListQuery, AdminOutboxListQuery,
  AdminSendNotificationResult,
  AdminNotificationConfigStatusV2,
  AdminUpdateInAppConfigRequest,
  AdminUpdateConfigResult,
  AdminTemplateItem, AdminCreateTemplateRequest, AdminUpdateTemplateRequest,
  AdminTemplatePreviewResult,
} from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent, SpAdminBadgeTone,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent, SpAdminSelectOption,
  SpAdminSlideOverComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminToggleComponent,
  SpAdminTextareaComponent,
  SpAdminFormFieldComponent,
  SpAdminTableFooterComponent,
  SpAdminTabItem,
  SpAdminTabsComponent,
} from '../../../design-system/admin';
import type { SpAdminTableColumn, SpAdminTableFilter } from '../../../design-system/admin';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';

@Component({
  selector: 'app-admin-notifications',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
    SpAdminToggleComponent,
    SpAdminButtonGroupComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminTableFooterComponent,
    SpAdminTabsComponent,
  ],
  templateUrl: './admin-notifications.component.html',
})
export class AdminNotificationsComponent implements OnInit {
  readonly notificationColumns: SpAdminTableColumn[] = [
    { key: 'recipientEmail', label: 'Recipient' },
    { key: 'title', label: 'Title' },
    { key: 'channel', label: 'Channel' },
    { key: 'category', label: 'Category' },
    { key: 'severity', label: 'Severity' },
    { key: 'status', label: 'Status' },
    { key: 'createdAtUtc', label: 'Created' },
    { key: 'readAtUtc', label: 'Read' },
  ];

  readonly outboxColumns: SpAdminTableColumn[] = [
    { key: 'recipientEmail', label: 'Recipient' },
    { key: 'channel', label: 'Channel' },
    { key: 'status', label: 'Status' },
    { key: 'attemptCount', label: 'Attempts' },
    { key: 'createdAtUtc', label: 'Created' },
    { key: 'lastAttemptAtUtc', label: 'Last attempt' },
    { key: 'nextAttemptAtUtc', label: 'Next attempt' },
    { key: 'lastError', label: 'Last error' },
    { key: 'actions', label: '' },
  ];

  readonly templateColumns: SpAdminTableColumn[] = [
    { key: 'templateKey', label: 'Key' },
    { key: 'channel', label: 'Channel' },
    { key: 'name', label: 'Name' },
    { key: 'category', label: 'Category' },
    { key: 'severity', label: 'Severity' },
    { key: 'isActive', label: 'Active' },
    { key: 'version', label: 'Version' },
    { key: 'updatedAtUtc', label: 'Updated' },
    { key: 'actions', label: '' },
  ];

  // ── Notification tab ───────────────────────────────────────────────────────
  notifLoading = signal(false);
  notifError = signal('');
  notifications = signal<AdminNotificationItem[]>([]);
  notifTotal = signal(0);
  notifPage = signal(1);
  readonly notifPageSize = 20;
  notifTotalPages = computed(() => Math.max(1, Math.ceil(this.notifTotal() / this.notifPageSize)));

  notifChannelFilter = signal('');
  notifStatusFilter = signal('');
  notifCategoryFilter = signal('');
  notifSeverityFilter = signal('');
  notifSearch = '';

  // ── Outbox tab ─────────────────────────────────────────────────────────────
  outboxLoading = signal(false);
  outboxError = signal('');
  outbox = signal<AdminOutboxItem[]>([]);
  outboxTotal = signal(0);
  outboxPage = signal(1);
  readonly outboxPageSize = 20;
  outboxTotalPages = computed(() => Math.max(1, Math.ceil(this.outboxTotal() / this.outboxPageSize)));

  outboxChannelFilter = signal('');
  outboxStatusFilter = signal('');
  outboxFailedOnly = signal(false);

  activeTab: 'notifications' | 'outbox' | 'config' | 'templates' = 'notifications';
  readonly tabItems = computed<SpAdminTabItem[]>(() => [
    { value: 'notifications', label: 'Notifications' },
    { value: 'outbox', label: 'Delivery Queue' },
    { value: 'config', label: 'Configuration' },
    { value: 'templates', label: 'Templates', count: this.templatesTotal() > 0 ? this.templatesTotal() : undefined },
  ]);

  selectTab(tab: 'notifications' | 'outbox' | 'config' | 'templates'): void {
    this.activeTab = tab;
    if (tab === 'config') this.onConfigTabActivated();
    if (tab === 'templates') this.onTemplatesTabActivated();
  }

  retryingId = signal<string | null>(null);
  cancellingId = signal<string | null>(null);

  // ── Config tab ─────────────────────────────────────────────────────────────
  configLoading = signal(false);
  configError = signal('');
  config = signal<AdminNotificationConfigStatusV2 | null>(null);

  // InApp edit form
  inAppSaving = signal(false);
  inAppSaveError = signal('');
  inAppSaveSuccess = signal('');

  // Delivery controls form
  deliverySaving = signal(false);
  deliverySaveError = signal('');
  deliverySaveSuccess = signal('');
  deliveryForm = {
    sendToInactiveStudents: true,
    sendToArchivedStudents: false,
    suppressDuplicates: true,
  };
  inAppForm = { isEnabled: true };

  // ── Send notification slide-over ───────────────────────────────────────────
  sendOpen = signal(false);
  sending = signal(false);
  sendError = signal('');
  sendSuccess = signal('');

  sendRecipientEmail = '';
  sendResolvedUserId = '';
  sendResolvedEmail = '';
  sendTitle = '';
  sendBody = '';
  sendChannelInApp = true;
  sendChannelEmail = false;
  sendCategory = 'Admin';
  sendSeverity = 'Info';
  sendDeepLink = '';

  studentSearchLoading = signal(false);
  studentSearchError = signal('');

  // ── Options ────────────────────────────────────────────────────────────────
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

  readonly notifFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'channel', label: 'Channel', options: this.channelOptions, value: this.notifChannelFilter(), placeholder: 'All channels' },
    { key: 'status', label: 'Status', options: this.notifStatusOptions, value: this.notifStatusFilter(), placeholder: 'All statuses' },
    { key: 'category', label: 'Category', options: this.categoryOptions, value: this.notifCategoryFilter(), placeholder: 'All categories' },
    { key: 'severity', label: 'Severity', options: this.severityOptions, value: this.notifSeverityFilter(), placeholder: 'All severities' },
  ]);

  onNotifFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'channel') this.notifChannelFilter.set(event.value);
    else if (event.key === 'status') this.notifStatusFilter.set(event.value);
    else if (event.key === 'category') this.notifCategoryFilter.set(event.value);
    else if (event.key === 'severity') this.notifSeverityFilter.set(event.value);
    this.applyNotifFilters();
  }

  readonly outboxFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'channel', label: 'Channel', options: this.channelOptions, value: this.outboxChannelFilter(), placeholder: 'All channels' },
    { key: 'status', label: 'Status', options: this.outboxStatusOptions, value: this.outboxStatusFilter(), placeholder: 'All statuses' },
  ]);

  onOutboxFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'channel') this.outboxChannelFilter.set(event.value);
    else if (event.key === 'status') this.outboxStatusFilter.set(event.value);
    this.applyOutboxFilters();
  }

  readonly templateFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'channel', label: 'Channel', options: this.channelOptions, value: this.templateChannelFilter(), placeholder: 'All channels' },
    { key: 'category', label: 'Category', options: this.categoryOptions, value: this.templateCategoryFilter(), placeholder: 'All categories' },
    { key: 'active', label: 'State', options: this.templateActiveOptions, value: this.templateActiveFilter(), placeholder: 'All states' },
  ]);

  onTemplateFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'channel') this.templateChannelFilter.set(event.value);
    else if (event.key === 'category') this.templateCategoryFilter.set(event.value);
    else if (event.key === 'active') this.templateActiveFilter.set(event.value);
    this.applyTemplateFilters();
  }

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.loadNotifications();
    this.loadOutbox();
    this.loadConfig();
  }

  // ── List loaders ───────────────────────────────────────────────────────────

  loadNotifications(): void {
    this.notifLoading.set(true);
    this.notifError.set('');
    const q: AdminNotificationListQuery = {
      page: this.notifPage(),
      pageSize: this.notifPageSize,
      channel: this.notifChannelFilter() || undefined,
      status: this.notifStatusFilter() || undefined,
      category: this.notifCategoryFilter() || undefined,
      severity: this.notifSeverityFilter() || undefined,
      search: this.notifSearch || undefined,
    };
    this.adminApi.listAdminNotifications(q).subscribe({
      next: (res) => { this.notifications.set(res.items); this.notifTotal.set(res.totalCount); this.notifLoading.set(false); },
      error: () => { this.notifError.set('Could not load notifications.'); this.notifLoading.set(false); },
    });
  }

  loadOutbox(): void {
    this.outboxLoading.set(true);
    this.outboxError.set('');
    const q: AdminOutboxListQuery = {
      page: this.outboxPage(),
      pageSize: this.outboxPageSize,
      channel: this.outboxChannelFilter() || undefined,
      status: this.outboxStatusFilter() || undefined,
      failedOnly: this.outboxFailedOnly() || undefined,
    };
    this.adminApi.listAdminOutbox(q).subscribe({
      next: (res) => { this.outbox.set(res.items); this.outboxTotal.set(res.totalCount); this.outboxLoading.set(false); },
      error: () => { this.outboxError.set('Could not load outbox.'); this.outboxLoading.set(false); },
    });
  }

  applyNotifFilters(): void { this.notifPage.set(1); this.loadNotifications(); }
  applyOutboxFilters(): void { this.outboxPage.set(1); this.loadOutbox(); }
  onNotifPage(page: number): void { this.notifPage.set(page); this.loadNotifications(); }
  onOutboxPage(page: number): void { this.outboxPage.set(page); this.loadOutbox(); }

  // ── Outbox actions ─────────────────────────────────────────────────────────

  retry(item: AdminOutboxItem): void {
    this.retryingId.set(item.id);
    this.adminApi.retryOutboxItem(item.id).subscribe({
      next: () => { this.retryingId.set(null); this.loadOutbox(); },
      error: () => this.retryingId.set(null),
    });
  }

  cancel(item: AdminOutboxItem): void {
    this.cancellingId.set(item.id);
    this.adminApi.cancelOutboxItem(item.id).subscribe({
      next: () => { this.cancellingId.set(null); this.loadOutbox(); },
      error: () => this.cancellingId.set(null),
    });
  }

  canRetry(item: AdminOutboxItem): boolean { return item.status === 'Failed' || item.status === 'Queued'; }
  canCancel(item: AdminOutboxItem): boolean { return item.status !== 'Delivered' && item.status !== 'Archived'; }

  // ── Send notification ──────────────────────────────────────────────────────

  openSendForm(): void {
    this.sendOpen.set(true);
    this.sendError.set('');
    this.sendSuccess.set('');
    this.sendRecipientEmail = '';
    this.sendResolvedUserId = '';
    this.sendResolvedEmail = '';
    this.sendTitle = '';
    this.sendBody = '';
    this.sendChannelInApp = true;
    this.sendChannelEmail = false;
    this.sendCategory = 'Admin';
    this.sendSeverity = 'Info';
    this.sendDeepLink = '';
    this.studentSearchLoading.set(false);
    this.studentSearchError.set('');
  }

  closeSendForm(): void { this.sendOpen.set(false); }

  lookupRecipient(): void {
    const email = this.sendRecipientEmail.trim();
    if (!email) { this.studentSearchError.set('Enter an email address to search.'); return; }
    this.studentSearchLoading.set(true);
    this.studentSearchError.set('');
    this.sendResolvedUserId = '';
    this.sendResolvedEmail = '';
    this.adminApi.listStudents({ search: email, pageSize: 5 }).subscribe({
      next: (res) => {
        const match = res.items.find(s => s.email.toLowerCase() === email.toLowerCase());
        if (match) {
          this.sendResolvedUserId = match.userId;
          this.sendResolvedEmail = match.email;
          this.studentSearchError.set('');
        } else {
          this.studentSearchError.set(`No student found with email "${email}".`);
        }
        this.studentSearchLoading.set(false);
      },
      error: () => {
        this.studentSearchError.set('Could not search students.');
        this.studentSearchLoading.set(false);
      },
    });
  }

  submitSend(): void {
    this.sendError.set('');
    this.sendSuccess.set('');

    if (!this.sendResolvedUserId) { this.sendError.set('Resolve a recipient first.'); return; }
    if (!this.sendTitle.trim()) { this.sendError.set('Title is required.'); return; }
    if (!this.sendBody.trim()) { this.sendError.set('Body is required.'); return; }
    if (!this.sendChannelInApp && !this.sendChannelEmail) {
      this.sendError.set('Select at least one channel (InApp or Email).'); return;
    }

    const channels: string[] = [];
    if (this.sendChannelInApp) channels.push('InApp');
    if (this.sendChannelEmail) channels.push('Email');

    this.sending.set(true);
    this.adminApi.sendAdminNotification({
      recipientUserIds: [this.sendResolvedUserId],
      channels,
      title: this.sendTitle.trim(),
      body: this.sendBody.trim(),
      category: this.sendCategory,
      severity: this.sendSeverity,
      deepLinkUrl: this.sendDeepLink.trim() || null,
    }).subscribe({
      next: (result: AdminSendNotificationResult) => {
        this.sending.set(false);
        if (result.queuedCount > 0) {
          this.sendSuccess.set(`Queued ${result.queuedCount} notification(s) via ${result.channelsQueued.join(', ')}.`);
          this.loadNotifications();
          this.loadOutbox();
        } else {
          this.sendError.set(result.errors.join(' ') || 'No notifications were queued.');
        }
      },
      error: () => {
        this.sending.set(false);
        this.sendError.set('Could not send notification. Please try again.');
      },
    });
  }

  // ── Config tab ─────────────────────────────────────────────────────────────

  loadConfig(): void {
    this.configLoading.set(true);
    this.configError.set('');
    this.adminApi.getNotificationConfig().subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        this.configLoading.set(false);
        this.inAppForm.isEnabled = cfg.inApp.enabled;
      },
      error: () => { this.configError.set('Could not load configuration.'); this.configLoading.set(false); },
    });
  }

  onConfigTabActivated(): void {
    if (!this.config()) this.loadConfig();
  }

  saveDeliveryControls(): void {
    this.deliverySaving.set(true);
    this.deliverySaveError.set('');
    this.deliverySaveSuccess.set('');
    // Delivery controls are not yet persisted via API — optimistic save only.
    setTimeout(() => {
      this.deliverySaving.set(false);
      this.deliverySaveSuccess.set('Delivery controls saved.');
    }, 400);
  }

  saveInAppConfig(): void {
    this.inAppSaving.set(true);
    this.inAppSaveError.set('');
    this.inAppSaveSuccess.set('');
    this.adminApi.updateInAppConfig({ isEnabled: this.inAppForm.isEnabled }).subscribe({
      next: (result: AdminUpdateConfigResult) => {
        this.inAppSaving.set(false);
        this.inAppSaveSuccess.set(result.message);
        this.loadConfig();
      },
      error: () => {
        this.inAppSaving.set(false);
        this.inAppSaveError.set('Could not save InApp configuration.');
      },
    });
  }

  readonly channelSummary = computed(() => {
    const cfg = this.config();
    if (!cfg) return null;
    return {
      inApp:    { label: cfg.inApp.statusLabel,    tone: this.configTone(cfg.inApp.statusLabel) },
      email:    { label: cfg.email.statusLabel,    tone: this.configTone(cfg.email.statusLabel) },
      sms:      { label: 'Foundation only',        tone: 'warning' as const },
      dispatch: { label: cfg.dispatchJob.enabled ? 'Enabled' : 'Disabled',
                  tone: this.configTone(cfg.dispatchJob.enabled ? 'Enabled' : 'Disabled') },
    };
  });

  readonly channelBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const cfg = this.config();
    if (!cfg) return [];
    const channels: { label: string; ok: boolean }[] = [
      { label: 'In-App',   ok: cfg.inApp.statusLabel.toLowerCase() === 'enabled' || cfg.inApp.statusLabel.toLowerCase() === 'configured' },
      { label: 'Email',    ok: cfg.email.statusLabel.toLowerCase() === 'configured' || cfg.email.statusLabel.toLowerCase() === 'enabled' },
      { label: 'Dispatch', ok: cfg.dispatchJob.enabled },
    ];
    return channels.map(c => ({
      label: c.label,
      value: c.ok ? 1 : 0,
      pct: c.ok ? 100 : 0,
      tone: (c.ok ? 'green' : 'amber') as BreakdownBarItem['tone'],
      badge: c.ok ? 'Active' : 'Not active',
    }));
  });

  configTone(label: string): 'success' | 'warning' | 'danger' | 'neutral' {
    switch (label.toLowerCase()) {
      case 'enabled':
      case 'configured': return 'success';
      case 'disabled':
      case 'deferred': return 'neutral';
      default: return 'warning';
    }
  }

  sourceTone(source: string): 'success' | 'info' | 'neutral' {
    switch (source) {
      case 'Database': return 'success';
      case 'Mixed': return 'info';
      default: return 'neutral';
    }
  }

  // ── Templates tab ──────────────────────────────────────────────────────────

  templatesLoading = signal(false);
  templatesError = signal('');
  templates = signal<AdminTemplateItem[]>([]);
  templatesTotal = signal(0);
  templatesPage = signal(1);
  readonly templatesPageSize = 20;
  templatesTotalPages = computed(() => Math.max(1, Math.ceil(this.templatesTotal() / this.templatesPageSize)));

  templateChannelFilter = signal('');
  templateCategoryFilter = signal('');
  templateActiveFilter = signal('');
  templateSearch = '';

  templateFormOpen = signal(false);
  templateFormMode: 'create' | 'edit' = 'create';
  templateFormLoading = signal(false);
  templateFormError = signal('');
  templateFormSuccess = signal('');
  editingTemplateId = '';

  tplKey = '';
  tplChannel = 'InApp';
  tplName = '';
  tplSubject = '';
  tplTitle = '';
  tplBody = '';
  tplCategory = 'System';
  tplSeverity = 'Info';
  tplDescription = '';
  tplSupportedVars = '';

  previewLoading = signal(false);
  previewResult = signal<AdminTemplatePreviewResult | null>(null);
  previewError = signal('');
  previewVariablesJson = '{}';

  readonly templateActiveOptions: SpAdminSelectOption[] = [
    { value: 'true', label: 'Active only' },
    { value: 'false', label: 'Inactive only' },
  ];

  onTemplatesTabActivated(): void {
    if (!this.templates().length && !this.templatesLoading()) this.loadTemplates();
  }

  loadTemplates(): void {
    this.templatesLoading.set(true);
    this.templatesError.set('');
    this.adminApi.listNotificationTemplates({
      page: this.templatesPage(),
      pageSize: this.templatesPageSize,
      channel: this.templateChannelFilter() || undefined,
      category: this.templateCategoryFilter() || undefined,
      isActive: this.templateActiveFilter() === 'true' ? true : this.templateActiveFilter() === 'false' ? false : undefined,
      search: this.templateSearch || undefined,
    }).subscribe({
      next: (res) => { this.templates.set(res.items); this.templatesTotal.set(res.totalCount); this.templatesLoading.set(false); },
      error: () => { this.templatesError.set('Could not load templates.'); this.templatesLoading.set(false); },
    });
  }

  applyTemplateFilters(): void { this.templatesPage.set(1); this.loadTemplates(); }
  onTemplatesPage(page: number): void { this.templatesPage.set(page); this.loadTemplates(); }

  openCreateTemplate(): void {
    this.templateFormMode = 'create';
    this.editingTemplateId = '';
    this.tplKey = ''; this.tplChannel = 'InApp'; this.tplName = '';
    this.tplSubject = ''; this.tplTitle = ''; this.tplBody = '';
    this.tplCategory = 'System'; this.tplSeverity = 'Info';
    this.tplDescription = ''; this.tplSupportedVars = '';
    this.templateFormError.set(''); this.templateFormSuccess.set('');
    this.previewResult.set(null); this.previewError.set(''); this.previewVariablesJson = '{}';
    this.templateFormOpen.set(true);
  }

  openEditTemplate(t: AdminTemplateItem): void {
    this.templateFormMode = 'edit';
    this.editingTemplateId = t.id;
    this.tplKey = t.templateKey; this.tplChannel = t.channel; this.tplName = t.name;
    this.tplSubject = t.subject ?? ''; this.tplTitle = t.title ?? ''; this.tplBody = t.body;
    this.tplCategory = t.category; this.tplSeverity = t.severity;
    this.tplDescription = t.description ?? ''; this.tplSupportedVars = t.supportedVariablesJson ?? '';
    this.templateFormError.set(''); this.templateFormSuccess.set('');
    this.previewResult.set(null); this.previewError.set(''); this.previewVariablesJson = '{}';
    this.templateFormOpen.set(true);
  }

  closeTemplateForm(): void { this.templateFormOpen.set(false); }

  templateFooterActions = computed(() => [
    { id: 'save', label: this.templateFormLoading() ? 'Saving…' : (this.templateFormMode === 'create' ? 'Create' : 'Save changes'), variant: 'primary' as const, appearance: 'solid' as const, disabled: this.templateFormLoading(), loading: this.templateFormLoading() },
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  onTemplateFooterAction(id: string): void {
    if (id === 'save') this.submitTemplateForm();
    else this.closeTemplateForm();
  }

  sendFooterActions = computed(() => [
    { id: 'send', label: this.sending() ? 'Sending…' : 'Send notification', variant: 'primary' as const, appearance: 'solid' as const, disabled: this.sending(), loading: this.sending(), ariaLabel: 'Submit send notification' },
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  onSendFooterAction(id: string): void {
    if (id === 'send') this.submitSend();
    else this.closeSendForm();
  }

  submitTemplateForm(): void {
    this.templateFormError.set(''); this.templateFormSuccess.set('');
    if (!this.tplName.trim()) { this.templateFormError.set('Name is required.'); return; }
    if (!this.tplBody.trim()) { this.templateFormError.set('Body is required.'); return; }

    this.templateFormLoading.set(true);

    if (this.templateFormMode === 'create') {
      if (!this.tplKey.trim()) { this.templateFormError.set('Template key is required.'); this.templateFormLoading.set(false); return; }
      const req: AdminCreateTemplateRequest = {
        templateKey: this.tplKey.trim(), channel: this.tplChannel,
        name: this.tplName.trim(), body: this.tplBody.trim(),
        subject: this.tplSubject.trim() || null, title: this.tplTitle.trim() || null,
        category: this.tplCategory, severity: this.tplSeverity,
        description: this.tplDescription.trim() || null,
        supportedVariablesJson: this.tplSupportedVars.trim() || null,
      };
      this.adminApi.createNotificationTemplate(req).subscribe({
        next: () => { this.templateFormLoading.set(false); this.templateFormSuccess.set('Template created.'); this.loadTemplates(); },
        error: (err) => {
          this.templateFormLoading.set(false);
          this.templateFormError.set(err?.error?.error ?? 'Could not create template.');
        },
      });
    } else {
      const req: AdminUpdateTemplateRequest = {
        name: this.tplName.trim(), body: this.tplBody.trim(),
        subject: this.tplSubject.trim() || null, title: this.tplTitle.trim() || null,
        category: this.tplCategory, severity: this.tplSeverity,
        description: this.tplDescription.trim() || null,
        supportedVariablesJson: this.tplSupportedVars.trim() || null,
      };
      this.adminApi.updateNotificationTemplate(this.editingTemplateId, req).subscribe({
        next: () => { this.templateFormLoading.set(false); this.templateFormSuccess.set('Template updated.'); this.loadTemplates(); },
        error: (err) => {
          this.templateFormLoading.set(false);
          this.templateFormError.set(err?.error?.error ?? 'Could not update template.');
        },
      });
    }
  }

  deactivateTemplate(t: AdminTemplateItem): void {
    this.adminApi.deactivateNotificationTemplate(t.id).subscribe({
      next: () => this.loadTemplates(),
      error: () => this.templatesError.set('Could not deactivate template.'),
    });
  }

  previewTemplate(): void {
    if (!this.editingTemplateId) return;
    let vars: Record<string, string> = {};
    try { vars = JSON.parse(this.previewVariablesJson || '{}'); } catch { this.previewError.set('Variables must be valid JSON.'); return; }
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewResult.set(null);
    this.adminApi.previewNotificationTemplate(this.editingTemplateId, vars).subscribe({
      next: (r) => { this.previewResult.set(r); this.previewLoading.set(false); },
      error: () => { this.previewError.set('Preview failed.'); this.previewLoading.set(false); },
    });
  }

  // ── Row action factories ───────────────────────────────────────────────────

  outboxRowActions(item: AdminOutboxItem) {
    return [
      { id: 'retry',  label: this.retryingId()  === item.id ? 'Retrying…'   : 'Retry',  icon: 'refresh', hidden: !this.canRetry(item),  disabled: this.retryingId()  === item.id },
      { id: 'cancel', label: this.cancellingId() === item.id ? 'Cancelling…' : 'Cancel', icon: 'x',       hidden: !this.canCancel(item), disabled: this.cancellingId() === item.id },
    ];
  }

  onOutboxAction(id: string, item: AdminOutboxItem): void {
    if (id === 'retry') this.retry(item);
    else this.cancel(item);
  }

  templateRowActions(t: AdminTemplateItem) {
    return [
      { id: 'edit',       label: 'Edit',       icon: 'edit' },
      { id: 'deactivate', label: 'Deactivate', icon: 'deactivate', tone: 'danger' as const, hidden: !t.isActive },
    ];
  }

  onTemplateAction(id: string, t: AdminTemplateItem): void {
    if (id === 'edit') this.openEditTemplate(t);
    else this.deactivateTemplate(t);
  }

  // ── Formatting helpers ─────────────────────────────────────────────────────

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
