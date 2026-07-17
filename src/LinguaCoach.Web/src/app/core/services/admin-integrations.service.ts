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
}
