import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminReviewQueueResult } from '../models/admin-review-queue.models';

@Injectable({ providedIn: 'root' })
export class AdminReviewQueueService {
  private readonly base = `${environment.apiUrl}/admin/review-queue`;

  constructor(private http: HttpClient) {}

  list(page: number, pageSize: number, entityType?: string, reviewStatus = 'PendingReview'): Observable<AdminReviewQueueResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (entityType && entityType !== 'all') params = params.set('entityType', entityType);
    if (reviewStatus) params = params.set('reviewStatus', reviewStatus);
    return this.http.get<AdminReviewQueueResult>(this.base, { params });
  }
}
