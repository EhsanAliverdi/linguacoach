import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminNotificationsComponent } from './admin-notifications.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { of, throwError } from 'rxjs';
import {
  AdminNotificationItem, AdminOutboxItem, PagedResponse,
  AdminSendNotificationResult,
  AdminNotificationConfigStatusV2, AdminTestEmailResult,
  AdminUpdateConfigResult,
  AdminTemplateItem, AdminTemplatePreviewResult,
} from '../../../core/models/admin.models';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

const makeNotif = (overrides: Partial<AdminNotificationItem> = {}): AdminNotificationItem => ({
  id: '1', recipientUserId: 'u1', recipientEmail: 'a@b.com',
  title: 'Test', body: 'Body', channel: 'Email', category: 'System',
  severity: 'Info', status: 'Queued', deepLinkUrl: null,
  createdAtUtc: new Date().toISOString(), readAtUtc: null, expiresAtUtc: null,
  ...overrides,
});

const makeOutbox = (overrides: Partial<AdminOutboxItem> = {}): AdminOutboxItem => ({
  id: 'o1', notificationId: null, recipientUserId: 'u1', recipientEmail: 'a@b.com',
  channel: 'Email', status: 'Queued', attemptCount: 0,
  createdAtUtc: new Date().toISOString(), nextAttemptAtUtc: null,
  lastAttemptAtUtc: null, processedAtUtc: null, lastError: null,
  ...overrides,
});

const pagedOf = <T>(items: T[]): PagedResponse<T> => ({
  items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1,
});

const makeSendResult = (overrides: Partial<AdminSendNotificationResult> = {}): AdminSendNotificationResult => ({
  requestedRecipientCount: 1,
  queuedCount: 1,
  skippedCount: 0,
  channelsQueued: ['InApp'],
  errors: [],
  ...overrides,
});

describe('AdminNotificationsComponent', () => {
  let fixture: ComponentFixture<AdminNotificationsComponent>;
  let component: AdminNotificationsComponent;
  let apiSpy: jasmine.SpyObj<AdminApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('AdminApiService', [
      'listAdminNotifications', 'listAdminOutbox',
      'retryOutboxItem', 'cancelOutboxItem',
      'sendAdminNotification', 'listStudents',
      'getNotificationConfig', 'testEmail',
      'updateEmailConfig', 'updateSmsConfig', 'updateInAppConfig',
      'listNotificationTemplates', 'createNotificationTemplate',
      'updateNotificationTemplate', 'deactivateNotificationTemplate',
      'previewNotificationTemplate',
    ]);
    const mockConfig: AdminNotificationConfigStatusV2 = {
      source: 'AppSettings',
      inApp: { channel: 'InApp', enabled: true, statusLabel: 'Enabled' },
      email: { enabled: true, configured: true, statusLabel: 'Enabled', host: 'smtp.test.com', port: 587, fromAddress: 'no-reply@test.com', fromDisplayName: 'Test', useSsl: true, hasUsername: true, hasPassword: true },
      sms: { enabled: false, configured: false, statusLabel: 'Foundation only — provider not connected', provider: null, senderId: null, hasApiKey: false },
      dispatchJob: { enabled: true, intervalDescription: 'Every 5 min', batchSize: 50 },
    };
    apiSpy.getNotificationConfig.and.returnValue(of(mockConfig));
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([])));
    apiSpy.listAdminNotifications.and.returnValue(of(pagedOf([makeNotif()])));
    apiSpy.listAdminOutbox.and.returnValue(of(pagedOf([makeOutbox()])));
    apiSpy.listStudents.and.returnValue(of(pagedOf([])));

    await TestBed.configureTestingModule({
      imports: [AdminNotificationsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AdminApiService, useValue: apiSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminNotificationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── List behaviour ─────────────────────────────────────────────────────────

  it('loads notification rows on init', () => {
    expect(apiSpy.listAdminNotifications).toHaveBeenCalled();
    expect(component.notifications().length).toBe(1);
  });

  it('loads outbox rows on init', () => {
    expect(apiSpy.listAdminOutbox).toHaveBeenCalled();
    expect(component.outbox().length).toBe(1);
  });

  it('applyNotifFilters resets page to 1 and reloads', () => {
    component.notifPage.set(3);
    component.applyNotifFilters();
    expect(component.notifPage()).toBe(1);
    expect(apiSpy.listAdminNotifications).toHaveBeenCalledTimes(2);
  });

  it('applyOutboxFilters resets page to 1 and reloads', () => {
    component.outboxPage.set(2);
    component.applyOutboxFilters();
    expect(component.outboxPage()).toBe(1);
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('onNotifPage sets page and reloads', () => {
    component.onNotifPage(5);
    expect(component.notifPage()).toBe(5);
    expect(apiSpy.listAdminNotifications).toHaveBeenCalledTimes(2);
  });

  it('onOutboxPage sets page and reloads', () => {
    component.onOutboxPage(4);
    expect(component.outboxPage()).toBe(4);
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('retry calls retryOutboxItem and reloads outbox', () => {
    apiSpy.retryOutboxItem.and.returnValue(of(undefined as any));
    component.retry(makeOutbox({ id: 'o1', status: 'Failed' }));
    expect(apiSpy.retryOutboxItem).toHaveBeenCalledWith('o1');
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('cancel calls cancelOutboxItem and reloads outbox', () => {
    apiSpy.cancelOutboxItem.and.returnValue(of(undefined as any));
    component.cancel(makeOutbox({ id: 'o1', status: 'Queued' }));
    expect(apiSpy.cancelOutboxItem).toHaveBeenCalledWith('o1');
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('canRetry returns true for Failed and Queued only', () => {
    expect(component.canRetry(makeOutbox({ status: 'Failed' }))).toBeTrue();
    expect(component.canRetry(makeOutbox({ status: 'Queued' }))).toBeTrue();
    expect(component.canRetry(makeOutbox({ status: 'Delivered' }))).toBeFalse();
    expect(component.canRetry(makeOutbox({ status: 'Archived' }))).toBeFalse();
  });

  it('canCancel returns false for Delivered and Archived', () => {
    expect(component.canCancel(makeOutbox({ status: 'Queued' }))).toBeTrue();
    expect(component.canCancel(makeOutbox({ status: 'Failed' }))).toBeTrue();
    expect(component.canCancel(makeOutbox({ status: 'Delivered' }))).toBeFalse();
    expect(component.canCancel(makeOutbox({ status: 'Archived' }))).toBeFalse();
  });

  it('shows loading state while loading notifications', () => {
    component.notifLoading.set(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-loading-state')).toBeTruthy();
  });

  it('shows empty state when notifications list is empty', () => {
    apiSpy.listAdminNotifications.and.returnValue(of(pagedOf([])));
    component.loadNotifications();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  it('shows error state when notifications load fails', () => {
    apiSpy.listAdminNotifications.and.returnValue(throwError(() => new Error('fail')));
    component.loadNotifications();
    fixture.detectChanges();
    expect(component.notifError()).toBe('Could not load notifications.');
  });

  // ── Send form ──────────────────────────────────────────────────────────────

  it('send notification slide-over is closed initially', () => {
    expect(component.sendOpen()).toBeFalse();
  });

  it('openSendForm opens slide-over and resets form state', () => {
    component.sendTitle = 'Old title';
    component.sendBody = 'Old body';
    component.openSendForm();
    expect(component.sendOpen()).toBeTrue();
    expect(component.sendTitle).toBe('');
    expect(component.sendBody).toBe('');
    expect(component.sendChannelInApp).toBeTrue();
    expect(component.sendChannelEmail).toBeFalse();
    expect(component.sendCategory).toBe('Admin');
    expect(component.sendSeverity).toBe('Info');
  });

  it('closeSendForm closes slide-over', () => {
    component.sendOpen.set(true);
    component.closeSendForm();
    expect(component.sendOpen()).toBeFalse();
  });

  it('submitSend sets error if no recipient resolved', () => {
    component.openSendForm();
    component.sendResolvedUserId = '';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.submitSend();
    expect(component.sendError()).toContain('recipient');
  });

  it('submitSend sets error if title is blank', () => {
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = '';
    component.sendBody = 'B';
    component.submitSend();
    expect(component.sendError()).toContain('Title');
  });

  it('submitSend sets error if body is blank', () => {
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = '';
    component.submitSend();
    expect(component.sendError()).toContain('Body');
  });

  it('submitSend sets error if no channel selected', () => {
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.sendChannelInApp = false;
    component.sendChannelEmail = false;
    component.submitSend();
    expect(component.sendError()).toContain('channel');
  });

  it('submitSend calls sendAdminNotification with InApp channel', () => {
    apiSpy.sendAdminNotification.and.returnValue(of(makeSendResult({ channelsQueued: ['InApp'] })));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'Hello';
    component.sendBody = 'World';
    component.sendChannelInApp = true;
    component.sendChannelEmail = false;
    component.submitSend();
    expect(apiSpy.sendAdminNotification).toHaveBeenCalledWith(jasmine.objectContaining({
      channels: ['InApp'],
      title: 'Hello',
      body: 'World',
      recipientUserIds: ['u1'],
    }));
  });

  it('submitSend calls sendAdminNotification with Email channel', () => {
    apiSpy.sendAdminNotification.and.returnValue(of(makeSendResult({ channelsQueued: ['Email'] })));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.sendChannelInApp = false;
    component.sendChannelEmail = true;
    component.submitSend();
    expect(apiSpy.sendAdminNotification).toHaveBeenCalledWith(jasmine.objectContaining({
      channels: ['Email'],
    }));
  });

  it('submitSend sends both channels when both selected', () => {
    apiSpy.sendAdminNotification.and.returnValue(of(makeSendResult({ channelsQueued: ['InApp', 'Email'], queuedCount: 2 })));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.sendChannelInApp = true;
    component.sendChannelEmail = true;
    component.submitSend();
    const call = apiSpy.sendAdminNotification.calls.mostRecent().args[0];
    expect(call.channels).toContain('InApp');
    expect(call.channels).toContain('Email');
  });

  it('on success sets sendSuccess message and reloads lists', () => {
    apiSpy.sendAdminNotification.and.returnValue(of(makeSendResult()));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.submitSend();
    expect(component.sendSuccess()).toContain('Queued');
    expect(apiSpy.listAdminNotifications).toHaveBeenCalledTimes(2);
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('on API error sets sendError message', () => {
    apiSpy.sendAdminNotification.and.returnValue(throwError(() => new Error('fail')));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.submitSend();
    expect(component.sendError()).toContain('Could not send');
  });

  it('SMS channel is not included in form channel options sent to API', () => {
    // Component only builds channels array from sendChannelInApp / sendChannelEmail
    // No SMS toggle exists — verify SMS cannot appear in the request
    apiSpy.sendAdminNotification.and.returnValue(of(makeSendResult()));
    component.openSendForm();
    component.sendResolvedUserId = 'u1';
    component.sendTitle = 'T';
    component.sendBody = 'B';
    component.sendChannelInApp = true;
    component.sendChannelEmail = false;
    component.submitSend();
    const call = apiSpy.sendAdminNotification.calls.mostRecent().args[0];
    expect(call.channels).not.toContain('Sms');
    expect(call.channels).not.toContain('SMS');
  });

  // ── Config tab ─────────────────────────────────────────────────────────────

  const makeConfig = (overrides: Partial<AdminNotificationConfigStatusV2> = {}): AdminNotificationConfigStatusV2 => ({
    inApp: { channel: 'InApp', enabled: true, statusLabel: 'Enabled' },
    email: {
      enabled: false, configured: false, statusLabel: 'Disabled',
      host: null, port: 587, fromAddress: null, fromDisplayName: null,
      useSsl: false, hasUsername: false, hasPassword: false,
    },
    sms: { enabled: false, configured: false, statusLabel: 'Deferred', provider: null, senderId: null, hasApiKey: false },
    dispatchJob: { enabled: true, intervalDescription: 'Every 2 minutes', batchSize: 50 },
    source: 'AppSettings',
    ...overrides,
  });

  const makeUpdateResult = (overrides: Partial<AdminUpdateConfigResult> = {}): AdminUpdateConfigResult => ({
    succeeded: true, message: 'Saved.', source: 'Database', ...overrides,
  });

  it('loadConfig calls getNotificationConfig and populates config signal', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.loadConfig();
    expect(apiSpy.getNotificationConfig).toHaveBeenCalled();
    expect(component.config()).toBeTruthy();
    expect(component.config()!.inApp.enabled).toBeTrue();
  });

  it('loadConfig sets configError on failure', () => {
    component.config.set(null);
    apiSpy.getNotificationConfig.and.returnValue(throwError(() => new Error('fail')));
    component.loadConfig();
    expect(component.configError()).toBe('Could not load configuration.');
    expect(component.config()).toBeNull();
  });

  it('onConfigTabActivated calls loadConfig only when config is null', () => {
    // ngOnInit already called getNotificationConfig once (count=1)
    // Reset config to null to simulate a cold tab activate
    component.config.set(null);
    apiSpy.getNotificationConfig.calls.reset();
    component.onConfigTabActivated();
    expect(apiSpy.getNotificationConfig).toHaveBeenCalledTimes(1);
    // second call should not reload when config is already set
    component.onConfigTabActivated();
    expect(apiSpy.getNotificationConfig).toHaveBeenCalledTimes(1);
  });

  it('sendTestEmail calls testEmail with trimmed address', () => {
    const result: AdminTestEmailResult = { succeeded: true, wasSkipped: false, message: 'Sent.' };
    apiSpy.testEmail.and.returnValue(of(result));
    component.testEmailAddress = '  test@example.com  ';
    component.sendTestEmail();
    expect(apiSpy.testEmail).toHaveBeenCalledWith('test@example.com');
    expect(component.testEmailResult()!.succeeded).toBeTrue();
  });

  it('sendTestEmail does nothing when address is blank', () => {
    component.testEmailAddress = '   ';
    component.sendTestEmail();
    expect(apiSpy.testEmail).not.toHaveBeenCalled();
  });

  it('sendTestEmail sets failure result on API error', () => {
    apiSpy.testEmail.and.returnValue(throwError(() => new Error('fail')));
    component.testEmailAddress = 'test@example.com';
    component.sendTestEmail();
    expect(component.testEmailResult()!.succeeded).toBeFalse();
    expect(component.testEmailResult()!.message).toBe('Request failed.');
  });

  it('configTone returns success for Enabled and Configured', () => {
    expect(component.configTone('Enabled')).toBe('success');
    expect(component.configTone('Configured')).toBe('success');
  });

  it('configTone returns neutral for Disabled and Deferred', () => {
    expect(component.configTone('Disabled')).toBe('neutral');
    expect(component.configTone('Deferred')).toBe('neutral');
  });

  it('configTone returns warning for unknown labels', () => {
    expect(component.configTone('Misconfigured')).toBe('warning');
  });

  it('config signal is populated after ngOnInit', () => {
    expect(component.config()).not.toBeNull();
  });

  it('loadConfig syncs emailForm from loaded config', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig({
      email: { enabled: true, configured: true, statusLabel: 'Configured',
               host: 'smtp.test.com', port: 465, fromAddress: 'a@b.com',
               fromDisplayName: 'Test', useSsl: true, hasUsername: true, hasPassword: true },
    })));
    component.loadConfig();
    expect(component.emailForm.isEnabled).toBeTrue();
    expect(component.emailForm.host).toBe('smtp.test.com');
    expect(component.emailForm.port).toBe(465);
    expect(component.emailForm.fromAddress).toBe('a@b.com');
  });

  it('saveEmailConfig calls updateEmailConfig without exposing newSecret in signal', () => {
    apiSpy.updateEmailConfig.and.returnValue(of(makeUpdateResult()));
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.emailForm.isEnabled = false;
    component.emailForm.newSecret = 'my-secret';
    component.saveEmailConfig();
    expect(apiSpy.updateEmailConfig).toHaveBeenCalledWith(jasmine.objectContaining({ isEnabled: false }));
    // After save, newSecret is cleared from form
    expect(component.emailForm.newSecret).toBe('');
    expect(component.emailSaveSuccess()).toBe('Saved.');
  });

  it('saveEmailConfig shows error on failure', () => {
    apiSpy.updateEmailConfig.and.returnValue(throwError(() => ({ error: { error: 'Host required.' } })));
    component.emailForm.isEnabled = true;
    component.saveEmailConfig();
    expect(component.emailSaveError()).toBe('Host required.');
  });

  it('saveSmsConfig calls updateSmsConfig and clears newSecret', () => {
    apiSpy.updateSmsConfig.and.returnValue(of(makeUpdateResult({ message: 'SMS saved.' })));
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.smsForm.isEnabled = false;
    component.smsForm.newSecret = 'api-key';
    component.saveSmsConfig();
    expect(apiSpy.updateSmsConfig).toHaveBeenCalled();
    expect(component.smsForm.newSecret).toBe('');
    expect(component.smsSaveSuccess()).toBe('SMS saved.');
  });

  it('saveInAppConfig calls updateInAppConfig', () => {
    apiSpy.updateInAppConfig.and.returnValue(of(makeUpdateResult({ message: 'InApp saved.' })));
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.inAppForm.isEnabled = true;
    component.saveInAppConfig();
    expect(apiSpy.updateInAppConfig).toHaveBeenCalledWith({ isEnabled: true });
    expect(component.inAppSaveSuccess()).toBe('InApp saved.');
  });

  it('sourceTone returns success for Database, info for Mixed, neutral for AppSettings', () => {
    expect(component.sourceTone('Database')).toBe('success');
    expect(component.sourceTone('Mixed')).toBe('info');
    expect(component.sourceTone('AppSettings')).toBe('neutral');
  });

  it('config response with source field is accepted', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig({ source: 'Database' })));
    component.loadConfig();
    expect(component.config()!.source).toBe('Database');
  });

  // ── Templates tab ─────────────────────────────────────────────────────────

  const makeTemplate = (overrides: Partial<AdminTemplateItem> = {}): AdminTemplateItem => ({
    id: 't1', templateKey: 'test.key', channel: 'InApp', name: 'Test Template',
    subject: null, title: 'Hi', body: 'Hello {{Name}}',
    category: 'Admin', severity: 'Info', isActive: true, version: 1,
    supportedVariablesJson: null, description: null,
    createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
    ...overrides,
  });

  it('onTemplatesTabActivated loads templates when list is empty', () => {
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([makeTemplate()])));
    component.onTemplatesTabActivated();
    expect(apiSpy.listNotificationTemplates).toHaveBeenCalled();
    expect(component.templates().length).toBe(1);
  });

  it('onTemplatesTabActivated does not reload when templates already loaded', () => {
    component.templates.set([makeTemplate()]);
    component.onTemplatesTabActivated();
    expect(apiSpy.listNotificationTemplates).not.toHaveBeenCalled();
  });

  it('loadTemplates sets error on failure', () => {
    apiSpy.listNotificationTemplates.and.returnValue(throwError(() => new Error('fail')));
    component.loadTemplates();
    expect(component.templatesError()).toBe('Could not load templates.');
  });

  it('applyTemplateFilters resets page and reloads', () => {
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([])));
    component.templatesPage.set(3);
    component.applyTemplateFilters();
    expect(component.templatesPage()).toBe(1);
    expect(apiSpy.listNotificationTemplates).toHaveBeenCalled();
  });

  it('openCreateTemplate resets form fields and opens slide-over', () => {
    component.tplKey = 'old.key';
    component.tplName = 'Old name';
    component.openCreateTemplate();
    expect(component.templateFormOpen()).toBeTrue();
    expect(component.templateFormMode).toBe('create');
    expect(component.tplKey).toBe('');
    expect(component.tplName).toBe('');
    expect(component.tplChannel).toBe('InApp');
  });

  it('openEditTemplate populates fields from template', () => {
    const t = makeTemplate({ id: 'abc', templateKey: 'foo.bar', channel: 'Email', subject: 'Subj' });
    component.openEditTemplate(t);
    expect(component.templateFormOpen()).toBeTrue();
    expect(component.templateFormMode).toBe('edit');
    expect(component.editingTemplateId).toBe('abc');
    expect(component.tplKey).toBe('foo.bar');
    expect(component.tplSubject).toBe('Subj');
  });

  it('closeTemplateForm closes the slide-over', () => {
    component.templateFormOpen.set(true);
    component.closeTemplateForm();
    expect(component.templateFormOpen()).toBeFalse();
  });

  it('submitTemplateForm sets error if name is blank', () => {
    component.openCreateTemplate();
    component.tplKey = 'test.key';
    component.tplName = '';
    component.tplBody = 'B';
    component.submitTemplateForm();
    expect(component.templateFormError()).toContain('Name');
  });

  it('submitTemplateForm sets error if body is blank', () => {
    component.openCreateTemplate();
    component.tplKey = 'test.key';
    component.tplName = 'N';
    component.tplBody = '';
    component.submitTemplateForm();
    expect(component.templateFormError()).toContain('Body');
  });

  it('submitTemplateForm sets error if key is blank (create mode)', () => {
    component.openCreateTemplate();
    component.tplKey = '';
    component.tplName = 'N';
    component.tplBody = 'B';
    component.submitTemplateForm();
    expect(component.templateFormError()).toContain('key');
  });

  it('submitTemplateForm (create) calls createNotificationTemplate', () => {
    apiSpy.createNotificationTemplate.and.returnValue(of(makeTemplate()));
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([makeTemplate()])));
    component.openCreateTemplate();
    component.tplKey = 'test.new';
    component.tplName = 'New';
    component.tplBody = 'Body';
    component.tplTitle = 'Title';
    component.submitTemplateForm();
    expect(apiSpy.createNotificationTemplate).toHaveBeenCalledWith(jasmine.objectContaining({
      templateKey: 'test.new', name: 'New', body: 'Body',
    }));
    expect(component.templateFormSuccess()).toContain('created');
  });

  it('submitTemplateForm (edit) calls updateNotificationTemplate', () => {
    const t = makeTemplate({ id: 'edit-id' });
    apiSpy.updateNotificationTemplate.and.returnValue(of(t));
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([t])));
    component.openEditTemplate(t);
    component.tplName = 'Updated name';
    component.tplBody = 'New body';
    component.submitTemplateForm();
    expect(apiSpy.updateNotificationTemplate).toHaveBeenCalledWith('edit-id', jasmine.objectContaining({
      name: 'Updated name', body: 'New body',
    }));
    expect(component.templateFormSuccess()).toContain('updated');
  });

  it('deactivateTemplate calls deactivateNotificationTemplate and reloads', () => {
    apiSpy.deactivateNotificationTemplate.and.returnValue(of(undefined as any));
    apiSpy.listNotificationTemplates.and.returnValue(of(pagedOf([])));
    component.deactivateTemplate(makeTemplate());
    expect(apiSpy.deactivateNotificationTemplate).toHaveBeenCalledWith('t1');
    expect(apiSpy.listNotificationTemplates).toHaveBeenCalled();
  });

  it('previewTemplate does not call sendAdminNotification', () => {
    const result: AdminTemplatePreviewResult = {
      succeeded: true, renderedSubject: null, renderedTitle: 'Hi Alice',
      renderedBody: 'Hello Alice', missingVariables: [],
    };
    apiSpy.previewNotificationTemplate.and.returnValue(of(result));
    const t = makeTemplate({ id: 'prev-id' });
    component.openEditTemplate(t);
    component.previewVariablesJson = '{"Name":"Alice"}';
    component.previewTemplate();
    expect(apiSpy.previewNotificationTemplate).toHaveBeenCalledWith('prev-id', { Name: 'Alice' });
    expect(apiSpy.sendAdminNotification).not.toHaveBeenCalled();
    expect(component.previewResult()!.renderedBody).toBe('Hello Alice');
  });

  it('previewTemplate sets error on invalid JSON', () => {
    const t = makeTemplate({ id: 'pj-id' });
    component.openEditTemplate(t);
    component.previewVariablesJson = 'not-json';
    component.previewTemplate();
    expect(apiSpy.previewNotificationTemplate).not.toHaveBeenCalled();
    expect(component.previewError()).toContain('JSON');
  });

  it('previewTemplate does nothing when no editingTemplateId', () => {
    component.templateFormMode = 'create';
    component.editingTemplateId = '';
    component.previewTemplate();
    expect(apiSpy.previewNotificationTemplate).not.toHaveBeenCalled();
  });

  it('templates tab does not add SMS send UI', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.innerHTML).not.toContain('sms-send');
    expect(el.innerHTML).not.toContain('sms-form');
  });

  it('does not expose send-notification template UI', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.innerHTML).not.toMatch(/template.*picker/i);
    expect(el.innerHTML).not.toContain('template-picker');
  });

  it('lookupRecipient calls listStudents with search param', () => {
    component.sendRecipientEmail = 'test@example.com';
    component.lookupRecipient();
    expect(apiSpy.listStudents).toHaveBeenCalledWith(jasmine.objectContaining({ search: 'test@example.com' }));
  });

  it('lookupRecipient resolves user when email matches', () => {
    apiSpy.listStudents.and.returnValue(of(pagedOf([
      { userId: 'found-id', email: 'test@example.com' } as any,
    ])));
    component.sendRecipientEmail = 'test@example.com';
    component.lookupRecipient();
    expect(component.sendResolvedUserId).toBe('found-id');
    expect(component.sendResolvedEmail).toBe('test@example.com');
  });

  it('lookupRecipient sets error when no match found', () => {
    apiSpy.listStudents.and.returnValue(of(pagedOf([])));
    component.sendRecipientEmail = 'nobody@example.com';
    component.lookupRecipient();
    expect(component.studentSearchError()).toContain('No student found');
    expect(component.sendResolvedUserId).toBe('');
  });

  // ── SMS foundation-only label ─────────────────────────────────────────────

  it('SMS channel status card shows Foundation only badge', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.activeTab = 'config';
    component.loadConfig();
    fixture.detectChanges();
    const badges = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-badge'));
    expect(badges.some(b => b.textContent?.trim() === 'Foundation only')).toBeTrue();
  });

  it('Foundation only badge does not appear for In-App or Email channel cards', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.activeTab = 'config';
    component.loadConfig();
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-card'));
    const inAppCard = cards.find(c => c.textContent?.includes('In-App') && !c.textContent?.includes('SMS'));
    expect(inAppCard?.textContent).not.toContain('Foundation only');
    const emailCard = cards.find(c => c.textContent?.includes('Email') && !c.textContent?.includes('SMS'));
    expect(emailCard?.textContent).not.toContain('Foundation only');
  });

  it('SMS config card shows warning alert about provider not connected', () => {
    apiSpy.getNotificationConfig.and.returnValue(of(makeConfig()));
    component.activeTab = 'config';
    component.loadConfig();
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('SMS delivery is not production-ready');
  });

  it('renders sp-admin-page-header with title Notifications', () => {
    fixture.detectChanges();
    const header = fixture.nativeElement.querySelector('sp-admin-page-header') as HTMLElement | null;
    expect(header).toBeTruthy();
    expect(header!.getAttribute('title')).toBe('Notifications');
  });

  it('page header is not nested inside sp-admin-page-body', () => {
    fixture.detectChanges();
    const body = fixture.nativeElement.querySelector('sp-admin-page-body') as HTMLElement | null;
    expect(body).toBeTruthy();
    const headerInsideBody = body!.querySelector('sp-admin-page-header');
    expect(headerInsideBody).toBeNull();
  });
});
