import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminNotificationsComponent } from './admin-notifications.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { of, throwError } from 'rxjs';
import {
  AdminNotificationItem, AdminOutboxItem, PagedResponse,
  AdminSendNotificationResult,
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
    ]);
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
});
