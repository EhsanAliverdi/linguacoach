import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminSecuritySettings,
  AdminAuthEventItem,
  AdminAuthEventListQuery as AdminAuthEventListParams,
} from '../models/admin.models';

export type { AdminSecuritySettings, AdminAuthEventItem, AdminAuthEventListParams };

/** Security-specific paged response — uses `total` (not `totalCount`) to match the auth-events endpoint. */
export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AdminSecurityService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getSettings(): Observable<AdminSecuritySettings> {
    return this.http.get<AdminSecuritySettings>(`${this.api}/admin/security/settings`);
  }

  getAuthEvents(params: AdminAuthEventListParams = {}): Observable<PagedResponse<AdminAuthEventItem>> {
    let p = new HttpParams();
    if (params.page)      p = p.set('page', params.page);
    if (params.pageSize)  p = p.set('pageSize', params.pageSize);
    if (params.userId)    p = p.set('userId', params.userId);
    if (params.email)     p = p.set('email', params.email);
    if (params.eventType) p = p.set('eventType', params.eventType);
    if (params.outcome)   p = p.set('outcome', params.outcome);
    if (params.from)      p = p.set('from', params.from);
    if (params.to)        p = p.set('to', params.to);
    return this.http.get<PagedResponse<AdminAuthEventItem>>(`${this.api}/admin/security/auth-events`, { params: p });
  }
}
