import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
} from '../../../core/services/admin-integrations.service';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminNotificationConfigStatusV2,
  AdminUpdateEmailConfigRequest,
  AdminUpdateSmsConfigRequest,
  AdminUpdateConfigResult,
  AdminTestEmailResult,
} from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminCheckboxComponent,
  SpAdminConfigCardComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminToggleComponent,
} from '../../../design-system/admin';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';

@Component({
  selector: 'app-admin-integrations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminButtonGroupComponent,
    SpAdminCheckboxComponent,
    SpAdminConfigCardComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminToggleComponent,
    SpAdminVisualPlaceholderComponent,
  ],
  templateUrl: './admin-integrations.component.html',
})
export class AdminIntegrationsComponent implements OnInit {
  // ── Storage ────────────────────────────────────────────────────────────────
  storage = signal<StorageSettings | null>(null);
  storageTest = signal<StorageTestResult | null>(null);
  storageError = signal('');
  testing = signal(false);
  loading = signal(true);

  // Storage slide-over
  storageOpen = signal(false);

  // ── Email (SMTP) — moved from Notifications ────────────────────────────────
  emailConfigLoading = signal(false);
  emailConfigError = signal('');
  notifConfig = signal<AdminNotificationConfigStatusV2 | null>(null);

  emailOpen = signal(false);
  emailSaving = signal(false);
  emailSaveError = signal('');
  emailSaveSuccess = signal('');
  emailForm = {
    isEnabled: false,
    provider: 'Smtp',
    host: '',
    port: 587,
    useSsl: true,
    fromAddress: '',
    fromDisplayName: 'SpeakPath',
    username: '',
    newSecret: '',
    clearSecret: false,
  };

  readonly emailProviderOptions = [
    { value: 'Smtp',     label: 'SMTP' },
    { value: 'Resend',   label: 'Resend (resend.com)' },
    { value: 'SendGrid', label: 'SendGrid' },
  ];

  testEmailAddress = '';
  testEmailLoading = signal(false);
  testEmailResult = signal<AdminTestEmailResult | null>(null);

  // ── SMS — moved from Notifications ────────────────────────────────────────
  smsOpen = signal(false);
  smsSaving = signal(false);
  smsSaveError = signal('');
  smsSaveSuccess = signal('');
  smsForm = {
    isEnabled: false,
    provider: '',
    senderId: '',
    newSecret: '',
    clearSecret: false,
  };

  // ── Footer actions ─────────────────────────────────────────────────────────
  storageFooterActions = computed(() => [
    { id: 'close', label: 'Close', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  emailFooterActions = computed(() => [
    { id: 'save', label: this.emailSaving() ? 'Saving…' : 'Save email config', variant: 'primary' as const, appearance: 'solid' as const, disabled: this.emailSaving(), loading: this.emailSaving() },
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  smsFooterActions = computed(() => [
    { id: 'save', label: this.smsSaving() ? 'Saving…' : 'Save SMS config', variant: 'primary' as const, appearance: 'solid' as const, disabled: this.smsSaving(), loading: this.smsSaving() },
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  readonly emailStatusTone = computed(() => {
    const cfg = this.notifConfig();
    if (!cfg) return 'neutral' as const;
    const l = cfg.email.statusLabel.toLowerCase();
    if (l === 'configured' || l === 'enabled') return 'success' as const;
    if (l === 'disabled') return 'neutral' as const;
    return 'warning' as const;
  });

  constructor(
    private svc: AdminIntegrationsService,
    private adminApi: AdminApiService,
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.storageError.set('');
    this.svc.getStorage().subscribe({
      next: s => { this.storage.set(s); this.loading.set(false); },
      error: err => { this.storageError.set(err.error?.error ?? 'Could not load storage settings.'); this.loading.set(false); },
    });
    this.loadEmailConfig();
  }

  loadEmailConfig(): void {
    this.emailConfigLoading.set(true);
    this.emailConfigError.set('');
    this.adminApi.getNotificationConfig().subscribe({
      next: cfg => {
        this.notifConfig.set(cfg);
        this.emailConfigLoading.set(false);
        this.emailForm.isEnabled = cfg.email.enabled;
        this.emailForm.provider = cfg.email.provider ?? 'Smtp';
        this.emailForm.host = cfg.email.host ?? '';
        this.emailForm.port = cfg.email.port;
        this.emailForm.useSsl = cfg.email.useSsl;
        this.emailForm.fromAddress = cfg.email.fromAddress ?? '';
        this.emailForm.fromDisplayName = cfg.email.fromDisplayName ?? 'SpeakPath';
        this.emailForm.username = '';
        this.emailForm.newSecret = '';
        this.emailForm.clearSecret = false;
        this.smsForm.isEnabled = cfg.sms.enabled;
        this.smsForm.provider = cfg.sms.provider ?? '';
        this.smsForm.senderId = cfg.sms.senderId ?? '';
        this.smsForm.newSecret = '';
        this.smsForm.clearSecret = false;
      },
      error: () => { this.emailConfigError.set('Could not load email/SMS configuration.'); this.emailConfigLoading.set(false); },
    });
  }

  testConnection(): void {
    this.testing.set(true);
    this.storageTest.set(null);
    this.svc.testStorage().subscribe({
      next: r => { this.storageTest.set(r); this.testing.set(false); },
      error: err => {
        this.testing.set(false);
        this.storageTest.set({ ok: false, lastCheckedUtc: new Date().toISOString(), error: err.error?.error ?? 'Test failed.' });
      },
    });
  }

  openStorageSlideOver(): void {
    this.storageTest.set(null);
    this.storageOpen.set(true);
  }

  openEmailSlideOver(): void {
    this.emailSaveError.set('');
    this.emailSaveSuccess.set('');
    this.testEmailAddress = '';
    this.testEmailResult.set(null);
    this.emailOpen.set(true);
  }

  openSmsSlideOver(): void {
    this.smsSaveError.set('');
    this.smsSaveSuccess.set('');
    this.smsOpen.set(true);
  }

  onEmailFooterAction(id: string): void {
    if (id === 'save') this.saveEmailConfig();
    else this.emailOpen.set(false);
  }

  onSmsFooterAction(id: string): void {
    if (id === 'save') this.saveSmsConfig();
    else this.smsOpen.set(false);
  }

  saveEmailConfig(): void {
    this.emailSaving.set(true);
    this.emailSaveError.set('');
    this.emailSaveSuccess.set('');
    const req: AdminUpdateEmailConfigRequest = {
      isEnabled: this.emailForm.isEnabled,
      provider: this.emailForm.provider || 'Smtp',
      host: this.emailForm.host || null,
      port: this.emailForm.port || null,
      useSsl: this.emailForm.useSsl,
      fromAddress: this.emailForm.fromAddress || null,
      fromDisplayName: this.emailForm.fromDisplayName || null,
      username: this.emailForm.username || null,
      newSecret: this.emailForm.newSecret || null,
      clearSecret: this.emailForm.clearSecret,
    };
    this.adminApi.updateEmailConfig(req).subscribe({
      next: (result: AdminUpdateConfigResult) => {
        this.emailSaving.set(false);
        this.emailSaveSuccess.set(result.message);
        this.emailForm.newSecret = '';
        this.emailForm.clearSecret = false;
        this.loadEmailConfig();
      },
      error: (err) => {
        this.emailSaving.set(false);
        this.emailSaveError.set(err?.error?.error ?? 'Could not save email configuration.');
      },
    });
  }

  saveSmsConfig(): void {
    this.smsSaving.set(true);
    this.smsSaveError.set('');
    this.smsSaveSuccess.set('');
    const req: AdminUpdateSmsConfigRequest = {
      isEnabled: this.smsForm.isEnabled,
      provider: this.smsForm.provider || null,
      senderId: this.smsForm.senderId || null,
      newSecret: this.smsForm.newSecret || null,
      clearSecret: this.smsForm.clearSecret,
    };
    this.adminApi.updateSmsConfig(req).subscribe({
      next: (result: AdminUpdateConfigResult) => {
        this.smsSaving.set(false);
        this.smsSaveSuccess.set(result.message);
        this.smsForm.newSecret = '';
        this.smsForm.clearSecret = false;
        this.loadEmailConfig();
      },
      error: (err) => {
        this.smsSaving.set(false);
        this.smsSaveError.set(err?.error?.error ?? 'Could not save SMS configuration.');
      },
    });
  }

  sendTestEmail(): void {
    const addr = this.testEmailAddress.trim();
    if (!addr) return;
    this.testEmailLoading.set(true);
    this.testEmailResult.set(null);
    this.adminApi.testEmail(addr).subscribe({
      next: (result) => { this.testEmailResult.set(result); this.testEmailLoading.set(false); },
      error: () => {
        this.testEmailResult.set({ succeeded: false, wasSkipped: false, message: 'Request failed.' });
        this.testEmailLoading.set(false);
      },
    });
  }

  storageConnected = computed(() => {
    const s = this.storage();
    return !!(s?.accessKey && s?.secretKey);
  });

  storageBadgeTone = computed(() => this.storageConnected() ? 'success' as const : 'warning' as const);
  storageBadgeLabel = computed(() => this.storageConnected() ? 'Connected' : 'Credentials missing');
}
