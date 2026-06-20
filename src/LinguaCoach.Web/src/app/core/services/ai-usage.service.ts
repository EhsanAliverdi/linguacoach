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
  byProvider: { provider: string; calls: number; successful: number; fallback: number; costUsd: number }[];
  byFeature: { feature: string; calls: number; successful: number; costUsd: number }[];
}

export interface AiUsageRecentResponse {
  total: number;
  items: AiUsageRecentItem[];
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

@Injectable({ providedIn: 'root' })
export class AiUsageService {
  constructor(private http: HttpClient) {}

  getSummary(range?: AiUsageDateRange): Observable<AiUsageSummary> {
    let params = new HttpParams();
    if (range?.from) params = params.set('from', range.from);
    if (range?.to)   params = params.set('to',   range.to);
    return this.http.get<AiUsageSummary>('/api/admin/ai-usage/summary', { params });
  }

  getRecent(limit = 100, range?: AiUsageDateRange): Observable<AiUsageRecentResponse> {
    let params = new HttpParams().set('limit', limit.toString());
    if (range?.from) params = params.set('from', range.from);
    if (range?.to)   params = params.set('to',   range.to);
    return this.http.get<AiUsageRecentResponse>('/api/admin/ai-usage/recent', { params });
  }
}
