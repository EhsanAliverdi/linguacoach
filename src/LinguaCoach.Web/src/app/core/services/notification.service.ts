import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface NotificationItem {
  id: string;
  title: string;
  body: string;
  category: string;
  severity: string;
  channel: string;
  status: string;
  createdAtUtc: string;
  readAtUtc: string | null;
  expiresAtUtc: string | null;
  deepLinkUrl: string | null;
  metadataJson: string | null;
}

export interface NotificationListResponse {
  items: NotificationItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly base = '/api/notifications';

  constructor(private http: HttpClient) {}

  list(page = 1, pageSize = 20, unreadOnly = false): Observable<NotificationListResponse> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('unreadOnly', unreadOnly);
    return this.http.get<NotificationListResponse>(this.base, { params });
  }

  getUnreadCount(): Observable<UnreadCountResponse> {
    return this.http.get<UnreadCountResponse>(`${this.base}/unread-count`);
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/read`, null);
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.base}/read-all`, null);
  }

  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/archive`, null);
  }
}
