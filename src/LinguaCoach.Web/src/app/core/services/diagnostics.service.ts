import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DiagnosticsStatus {
  environment: string;
  version: string;
  serverTimeUtc: string;
  uptimeSeconds: number;
  logLevel: string;
  diagnosticEventsEnabled: boolean;
  diagnosticEventCount: number;
  database: { reachable: boolean };
  ai: { providerConfigured: boolean; activeProvider: string | null; activeModel: string | null };
}

export interface DiagnosticEventItem {
  timestampUtc: string;
  level: string;
  category: string;
  message: string;
  correlationId: string | null;
  userId: string | null;
  path: string | null;
  statusCode: number | null;
  elapsedMs: number | null;
}

export interface DiagnosticsEventsResponse {
  enabled: boolean;
  total: number;
  items: DiagnosticEventItem[];
}

export interface EventQuery {
  level?: string;
  category?: string;
  correlationId?: string;
  q?: string;
  limit?: number;
}

@Injectable({ providedIn: 'root' })
export class DiagnosticsService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<DiagnosticsStatus> {
    return this.http.get<DiagnosticsStatus>(`${this.api}/admin/diagnostics/status`);
  }

  getEvents(query: EventQuery = {}): Observable<DiagnosticsEventsResponse> {
    let params = new HttpParams();
    if (query.level) params = params.set('level', query.level);
    if (query.category) params = params.set('category', query.category);
    if (query.correlationId) params = params.set('correlationId', query.correlationId);
    if (query.q) params = params.set('q', query.q);
    if (query.limit) params = params.set('limit', query.limit.toString());
    return this.http.get<DiagnosticsEventsResponse>(`${this.api}/admin/diagnostics/events`, { params });
  }
}
