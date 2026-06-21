import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AiUsageSummary {
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  fallbackCalls: number;
  totalCostUsd: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalTokens: number;
  successRate: number;
  zeroCostCallCount: number;
  zeroCostTotalTokens: number;
  byProvider: { provider: string; calls: number; successful: number; fallback: number; costUsd: number }[];
  byFeature: { feature: string; calls: number; successful: number; costUsd: number }[];
}

export interface AiUsageRecentResponse {
  items: AiUsageRecentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AiUsageRecentItem {
  id: string;
  createdAt: string;
  studentProfileId: string | null;
  featureKey: string;
  provider: string;
  model: string;
  isFallback: boolean;
  wasSuccessful: boolean;
  failureReason: string | null;
  inputTokens: number;
  outputTokens: number;
  costUsd: number;
  durationMs: number;
  correlationId: string | null;
}

export interface AiUsageDateRange {
  from?: string; // ISO-8601 UTC
  to?: string;   // ISO-8601 UTC
}

export interface AiUsageTrendBucket {
  date: string;          // 'yyyy-MM-dd'
  callCount: number;
  successCount: number;
  failureCount: number;
  fallbackCount: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  costUsd: number;
}

export interface AiUsageRecentCallFilter {
  provider?: string;
  model?: string;
  featureKey?: string;
  status?: string; // 'success' | 'failed' | 'fallback'
  studentId?: string; // UUID string
}

@Injectable({ providedIn: 'root' })
export class AiUsageService {
  constructor(private http: HttpClient) {}

  getSummary(range?: AiUsageDateRange, filters?: AiUsageRecentCallFilter): Observable<AiUsageSummary> {
    let params = new HttpParams();
    if (range?.from)          params = params.set('from',       range.from);
    if (range?.to)            params = params.set('to',         range.to);
    if (filters?.provider)    params = params.set('provider',   filters.provider);
    if (filters?.model)       params = params.set('model',      filters.model);
    if (filters?.featureKey)  params = params.set('featureKey', filters.featureKey);
    if (filters?.status)      params = params.set('status',     filters.status);
    if (filters?.studentId)   params = params.set('studentId',  filters.studentId);
    return this.http.get<AiUsageSummary>('/api/admin/ai-usage/summary', { params });
  }

  getTrends(range?: AiUsageDateRange, filters?: AiUsageRecentCallFilter): Observable<AiUsageTrendBucket[]> {
    let params = new HttpParams();
    if (range?.from)          params = params.set('from',       range.from);
    if (range?.to)            params = params.set('to',         range.to);
    if (filters?.provider)    params = params.set('provider',   filters.provider);
    if (filters?.model)       params = params.set('model',      filters.model);
    if (filters?.featureKey)  params = params.set('featureKey', filters.featureKey);
    if (filters?.status)      params = params.set('status',     filters.status);
    if (filters?.studentId)   params = params.set('studentId',  filters.studentId);
    return this.http.get<AiUsageTrendBucket[]>('/api/admin/ai-usage/trends', { params });
  }

  exportUsageCsv(range?: AiUsageDateRange, filters?: AiUsageRecentCallFilter): Observable<Blob> {
    let params = new HttpParams();
    if (range?.from)          params = params.set('from',       range.from);
    if (range?.to)            params = params.set('to',         range.to);
    if (filters?.provider)    params = params.set('provider',   filters.provider);
    if (filters?.model)       params = params.set('model',      filters.model);
    if (filters?.featureKey)  params = params.set('featureKey', filters.featureKey);
    if (filters?.status)      params = params.set('status',     filters.status);
    if (filters?.studentId)   params = params.set('studentId',  filters.studentId);
    return this.http.get('/api/admin/ai-usage/export.csv', { params, responseType: 'blob' });
  }

  getRecent(page = 1, pageSize = 25, range?: AiUsageDateRange, filters?: AiUsageRecentCallFilter): Observable<AiUsageRecentResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (range?.from)        params = params.set('from',       range.from);
    if (range?.to)          params = params.set('to',         range.to);
    if (filters?.provider)   params = params.set('provider',   filters.provider);
    if (filters?.model)      params = params.set('model',      filters.model);
    if (filters?.featureKey) params = params.set('featureKey', filters.featureKey);
    if (filters?.status)     params = params.set('status',     filters.status);
    if (filters?.studentId)  params = params.set('studentId',  filters.studentId);
    return this.http.get<AiUsageRecentResponse>('/api/admin/ai-usage/recent', { params });
  }
}
