import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AiUsageSummary {
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  fallbackCalls: number;
  totalCostUsd: number;
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

@Injectable({ providedIn: 'root' })
export class AiUsageService {
  constructor(private http: HttpClient) {}

  getSummary(): Observable<AiUsageSummary> {
    return this.http.get<AiUsageSummary>('/api/admin/ai-usage/summary');
  }

  getRecent(limit = 100): Observable<AiUsageRecentResponse> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<AiUsageRecentResponse>('/api/admin/ai-usage/recent', { params });
  }
}
