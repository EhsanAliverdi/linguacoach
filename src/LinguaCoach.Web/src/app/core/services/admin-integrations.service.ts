import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface StorageSettings {
  provider: string;
  endpoint: string | null;
  bucketName: string | null;
  accessKey: string | null;   // "configured" when a secret is set, otherwise null
  secretKey: string | null;   // "configured" when a secret is set, otherwise null
  useSsl: boolean;
  signedUrlExpiryMinutes: number;
}

export interface StorageTestResult {
  ok: boolean;
  lastCheckedUtc: string;
  error: string | null;
}

export interface GenerationSettings {
  readyLessonBufferSize: number;
  refillThreshold: number;
  refillBatchSize: number;
  maxGenerationAttempts: number;
  generationTimeoutSeconds: number;
  ttsTimeoutSeconds: number;
  maxConcurrentGenerationJobs: number;
  maxConcurrentTtsJobs: number;
  enableBackgroundGeneration: boolean;
  enableTtsGeneration: boolean;
  practiceGymReadyExercisesPerType: number;
  practiceGymRefillThresholdPerType: number;
  practiceGymRefillCountPerType: number;
}

export interface GenerationBatch {
  id: string;
  studentProfileId: string;
  triggerReason: string;
  status: string;
  requestedSessionCount: number;
  completedSessionCount: number;
  providerName: string | null;
  modelName: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  failureReason: string | null;
  createdAt: string;
}

export interface BatchesResponse {
  summary: {
    queued: number;
    running: number;
    failed: number;
    lastSuccessfulGenerationUtc: string | null;
  };
  readyBufferPerStudent: { studentProfileId: string; readyCount: number }[];
  batches: GenerationBatch[];
}

@Injectable({ providedIn: 'root' })
export class AdminIntegrationsService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStorage(): Observable<StorageSettings> {
    return this.http.get<StorageSettings>(`${this.api}/admin/integrations/storage`);
  }

  updateStorage(settings: Partial<StorageSettings>): Observable<unknown> {
    return this.http.patch(`${this.api}/admin/integrations/storage`, settings);
  }

  testStorage(): Observable<StorageTestResult> {
    return this.http.post<StorageTestResult>(`${this.api}/admin/integrations/storage/test`, {});
  }

  getGenerationSettings(): Observable<GenerationSettings> {
    return this.http.get<GenerationSettings>(`${this.api}/admin/generation/settings`);
  }

  updateGenerationSettings(settings: GenerationSettings): Observable<GenerationSettings> {
    return this.http.patch<GenerationSettings>(`${this.api}/admin/generation/settings`, settings);
  }

  getBatches(): Observable<BatchesResponse> {
    return this.http.get<BatchesResponse>(`${this.api}/admin/generation/batches`);
  }

  retryBatch(id: string): Observable<unknown> {
    return this.http.post(`${this.api}/admin/generation/batches/${id}/retry`, {});
  }

  cancelBatch(id: string): Observable<unknown> {
    return this.http.post(`${this.api}/admin/generation/batches/${id}/cancel`, {});
  }

  generateLessons(studentProfileId: string, count?: number): Observable<{ queued: boolean; requestedCount: number }> {
    const query = count ? `?count=${count}` : '';
    return this.http.post<{ queued: boolean; requestedCount: number }>(
      `${this.api}/admin/students/${studentProfileId}/generate-lessons${query}`, {});
  }
}
