import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { NotificationDropdownComponent } from './notification-dropdown.component';
import { NotificationService, NotificationItem, NotificationListResponse } from '../../../core/services/notification.service';

function makeItem(overrides: Partial<NotificationItem> = {}): NotificationItem {
  return {
    id: 'n1',
    title: 'Test notification',
    body: 'Body text',
    category: 'System',
    severity: 'Info',
    channel: 'InApp',
    status: 'Pending',
    createdAtUtc: new Date(Date.now() - 60_000).toISOString(),
    readAtUtc: null,
    expiresAtUtc: null,
    deepLinkUrl: null,
    metadataJson: null,
    ...overrides,
  };
}

function makeListResponse(items: NotificationItem[]): NotificationListResponse {
  return { items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1 };
}

describe('NotificationDropdownComponent', () => {
  let fixture: ComponentFixture<NotificationDropdownComponent>;
  let component: NotificationDropdownComponent;
  let svc: jasmine.SpyObj<NotificationService>;

  function setup() {
    svc = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'list', 'getUnreadCount', 'markRead', 'markAllRead', 'archive',
    ]);
    svc.getUnreadCount.and.returnValue(of({ unreadCount: 2 }));
    svc.list.and.returnValue(of(makeListResponse([])));

    TestBed.configureTestingModule({
      imports: [NotificationDropdownComponent],
      providers: [
        provideRouter([]),
        { provide: NotificationService, useValue: svc },
      ],
    });

    fixture = TestBed.createComponent(NotificationDropdownComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('loads unread count on init', () => {
    setup();
    expect(svc.getUnreadCount).toHaveBeenCalled();
    expect(component.unreadCount()).toBe(2);
    expect(component.hasUnread()).toBeTrue();
  });

  it('renders bell button', () => {
    setup();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Notifications"]');
    expect(btn).toBeTruthy();
  });

  it('does not show dropdown panel before toggle', () => {
    setup();
    expect(component.isOpen).toBeFalse();
    const panel = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(panel).toBeNull();
  });

  it('opens dropdown and loads notifications on toggle', () => {
    setup();
    const items = [makeItem()];
    svc.list.and.returnValue(of(makeListResponse(items)));

    component.toggleDropdown();
    fixture.detectChanges();

    expect(component.isOpen).toBeTrue();
    expect(svc.list).toHaveBeenCalled();
    expect(component.notifications()).toEqual(items);
  });

  it('shows loading state while fetching', () => {
    setup();
    const subject = new Subject<NotificationListResponse>();
    svc.list.and.returnValue(subject.asObservable());

    component.toggleDropdown();
    fixture.detectChanges();

    expect(component.loading()).toBeTrue();
    const status = fixture.nativeElement.querySelector('[role="status"]');
    expect(status).toBeTruthy();
  });

  it('shows error state and retry button on failure', () => {
    setup();
    svc.list.and.returnValue(throwError(() => new Error('fail')));

    component.toggleDropdown();
    fixture.detectChanges();

    expect(component.error()).toBe('Could not load notifications.');
    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert).toBeTruthy();
  });

  it('shows empty state when no notifications', () => {
    setup();
    svc.list.and.returnValue(of(makeListResponse([])));

    component.toggleDropdown();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No notifications');
  });

  it('shows notification list items', () => {
    setup();
    svc.list.and.returnValue(of(makeListResponse([makeItem({ title: 'Hello world' })])));

    component.toggleDropdown();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Hello world');
  });

  it('marks notification read and decrements unread count on click', () => {
    setup();
    const item = makeItem({ id: 'n1', readAtUtc: null });
    svc.list.and.returnValue(of(makeListResponse([item])));
    svc.markRead.and.returnValue(of(undefined));
    component.unreadCount.set(1);

    component.toggleDropdown();
    component.onNotificationClick(item);

    expect(svc.markRead).toHaveBeenCalledWith('n1');
    expect(component.unreadCount()).toBe(0);
  });

  it('does not call markRead for already-read notification', () => {
    setup();
    const item = makeItem({ readAtUtc: new Date().toISOString() });

    component.onNotificationClick(item);

    expect(svc.markRead).not.toHaveBeenCalled();
  });

  it('marks all read and clears unread count', () => {
    setup();
    svc.markAllRead.and.returnValue(of(undefined));
    const items = [makeItem({ id: 'n1' }), makeItem({ id: 'n2' })];
    component.notifications.set(items);
    component.unreadCount.set(2);

    component.onMarkAllRead();

    expect(svc.markAllRead).toHaveBeenCalled();
    expect(component.unreadCount()).toBe(0);
    expect(component.notifications().every(n => n.readAtUtc !== null)).toBeTrue();
  });

  it('archives notification and removes it from list', () => {
    setup();
    const item = makeItem({ id: 'n1', readAtUtc: null });
    component.notifications.set([item]);
    component.unreadCount.set(1);
    svc.archive.and.returnValue(of(undefined));

    component.onArchive(new MouseEvent('click'), 'n1');

    expect(svc.archive).toHaveBeenCalledWith('n1');
    expect(component.notifications().length).toBe(0);
    expect(component.unreadCount()).toBe(0);
  });

  it('retry calls loadNotifications again', () => {
    setup();
    svc.list.and.returnValue(of(makeListResponse([])));
    component.isOpen = true;

    component.retry();

    expect(svc.list).toHaveBeenCalledTimes(1);
  });

  it('closeDropdown sets isOpen false', () => {
    setup();
    component.isOpen = true;
    component.closeDropdown();
    expect(component.isOpen).toBeFalse();
  });

  it('severityIcon returns correct emoji', () => {
    setup();
    expect(component.severityIcon('Error')).toBe('🔴');
    expect(component.severityIcon('Warning')).toBe('🟡');
    expect(component.severityIcon('Success')).toBe('🟢');
    expect(component.severityIcon('Info')).toBe('🔵');
  });

  it('timeAgo returns seconds ago', () => {
    setup();
    const iso = new Date(Date.now() - 30_000).toISOString();
    expect(component.timeAgo(iso)).toContain('s ago');
  });

  it('no hard-coded demo data in component', () => {
    setup();
    // Verify notifications come from service, not static data
    expect(component.notifications()).toEqual([]);
    svc.list.and.returnValue(of(makeListResponse([])));
    component.loadNotifications();
    expect(component.notifications().length).toBe(0);
  });
});
