import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NotificationService, NotificationListResponse } from './notification.service';

const mockItem = () => ({
  id: 'n1',
  title: 'Test',
  body: 'Body',
  category: 'System',
  severity: 'Info',
  channel: 'InApp',
  status: 'Queued',
  createdAtUtc: new Date().toISOString(),
  readAtUtc: null,
  expiresAtUtc: null,
  deepLinkUrl: null,
  metadataJson: null,
});

const mockPage = (): NotificationListResponse => ({
  items: [mockItem()],
  totalCount: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
});

describe('NotificationService', () => {
  let svc: NotificationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [NotificationService],
    });
    svc = TestBed.inject(NotificationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list calls GET /api/notifications with default params', () => {
    svc.list().subscribe(r => expect(r.items.length).toBe(1));
    const req = http.expectOne(r => r.url === '/api/notifications');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('unreadOnly')).toBe('false');
    req.flush(mockPage());
  });

  it('list passes unreadOnly=true when requested', () => {
    svc.list(1, 20, true).subscribe();
    const req = http.expectOne(r => r.url === '/api/notifications');
    expect(req.request.params.get('unreadOnly')).toBe('true');
    req.flush(mockPage());
  });

  it('getUnreadCount calls GET /api/notifications/unread-count', () => {
    svc.getUnreadCount().subscribe(r => expect(r.unreadCount).toBe(3));
    const req = http.expectOne('/api/notifications/unread-count');
    expect(req.request.method).toBe('GET');
    req.flush({ unreadCount: 3 });
  });

  it('markRead calls POST /api/notifications/:id/read', () => {
    svc.markRead('n1').subscribe();
    const req = http.expectOne('/api/notifications/n1/read');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(null);
  });

  it('markAllRead calls POST /api/notifications/read-all', () => {
    svc.markAllRead().subscribe();
    const req = http.expectOne('/api/notifications/read-all');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('archive calls POST /api/notifications/:id/archive', () => {
    svc.archive('n2').subscribe();
    const req = http.expectOne('/api/notifications/n2/archive');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(null);
  });
});
