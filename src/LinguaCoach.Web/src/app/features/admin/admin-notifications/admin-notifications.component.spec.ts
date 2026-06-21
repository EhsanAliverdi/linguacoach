import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminNotificationsComponent } from './admin-notifications.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { of, throwError } from 'rxjs';
import { AdminNotificationItem, AdminOutboxItem, PagedResponse } from '../../../core/models/admin.models';
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

describe('AdminNotificationsComponent', () => {
  let fixture: ComponentFixture<AdminNotificationsComponent>;
  let component: AdminNotificationsComponent;
  let apiSpy: jasmine.SpyObj<AdminApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('AdminApiService', [
      'listAdminNotifications', 'listAdminOutbox', 'retryOutboxItem', 'cancelOutboxItem',
    ]);
    apiSpy.listAdminNotifications.and.returnValue(of(pagedOf([makeNotif()])));
    apiSpy.listAdminOutbox.and.returnValue(of(pagedOf([makeOutbox()])));

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

  it('loads notification rows on init', () => {
    expect(apiSpy.listAdminNotifications).toHaveBeenCalled();
    expect(component.notifications().length).toBe(1);
    expect(component.notifications()[0].title).toBe('Test');
  });

  it('loads outbox rows on init', () => {
    expect(apiSpy.listAdminOutbox).toHaveBeenCalled();
    expect(component.outbox().length).toBe(1);
    expect(component.outbox()[0].channel).toBe('Email');
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
    const item = makeOutbox({ id: 'o1', status: 'Failed' });
    component.retry(item);
    expect(apiSpy.retryOutboxItem).toHaveBeenCalledWith('o1');
    expect(apiSpy.listAdminOutbox).toHaveBeenCalledTimes(2);
  });

  it('cancel calls cancelOutboxItem and reloads outbox', () => {
    apiSpy.cancelOutboxItem.and.returnValue(of(undefined as any));
    const item = makeOutbox({ id: 'o1', status: 'Queued' });
    component.cancel(item);
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
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('sp-admin-loading-state')).toBeTruthy();
  });

  it('shows empty state when notifications list is empty', () => {
    apiSpy.listAdminNotifications.and.returnValue(of(pagedOf([])));
    component.loadNotifications();
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  it('shows error state when notifications load fails', () => {
    apiSpy.listAdminNotifications.and.returnValue(throwError(() => new Error('fail')));
    component.loadNotifications();
    fixture.detectChanges();
    expect(component.notifError()).toBe('Could not load notifications.');
  });

  it('does not expose send-notification UI', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.innerHTML).not.toContain('send-notification');
    expect(el.innerHTML).not.toContain('Send Notification');
  });
});
