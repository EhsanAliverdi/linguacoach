import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PlacementStatus, PlacementCurrentSection, SavePlacementAnswers, PlacementResult,
  AdaptivePlacementSummary, AdaptivePlacementNextItem, AdaptivePlacementRespondRequest,
  AdaptivePlacementSubmitResult, PlacementConfig,
} from '../models/placement.models';
import { map } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class PlacementService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<PlacementStatus> {
    return this.http.get<PlacementStatus>(`${this.api}/placement/status`);
  }

  start(): Observable<PlacementStatus> {
    return this.http.post<PlacementStatus>(`${this.api}/placement/start`, {});
  }

  getCurrent(): Observable<PlacementCurrentSection> {
    return this.http.get<PlacementCurrentSection>(`${this.api}/placement/current`);
  }

  saveAnswers(payload: SavePlacementAnswers): Observable<PlacementStatus> {
    return this.http.post<PlacementStatus>(`${this.api}/placement/answers`, payload);
  }

  complete(): Observable<PlacementResult> {
    return this.http.post<PlacementResult>(`${this.api}/placement/complete`, {});
  }

  getResult(): Observable<PlacementResult> {
    return this.http.get<PlacementResult>(`${this.api}/placement/result`);
  }

  /** Fetches listening audio as a blob URL. HttpClient sends the JWT automatically.
   *  audioApiPath is the relative path returned by the backend, e.g. /api/placement/audio/{id}/listening
   */
  getListeningAudioBlobUrl(audioApiPath: string): Observable<string> {
    const url = `${this.api.replace(/\/api$/, '')}${audioApiPath}`;
    return this.http.get(url, { responseType: 'blob' }).pipe(
      map(blob => URL.createObjectURL(blob))
    );
  }

  // ── Phase 14A — Adaptive placement ──────────────────────────────────────────

  getPlacementConfig(): Observable<PlacementConfig> {
    return this.http.get<PlacementConfig>(`${this.api}/student/placement/config`);
  }

  getAdaptiveCurrent(): Observable<AdaptivePlacementSummary | null> {
    return this.http.get<AdaptivePlacementSummary | null>(`${this.api}/student/placement/current`);
  }

  getAdaptiveNextItem(assessmentId: string): Observable<AdaptivePlacementNextItem | null> {
    return this.http.get<AdaptivePlacementNextItem | null>(
      `${this.api}/student/placement/next`, { params: { assessmentId } });
  }

  startAdaptive(): Observable<AdaptivePlacementSummary> {
    return this.http.post<AdaptivePlacementSummary>(`${this.api}/student/placement/start`, {});
  }

  resumeAdaptive(): Observable<AdaptivePlacementSummary> {
    return this.http.post<AdaptivePlacementSummary>(`${this.api}/student/placement/resume`, {});
  }

  respondToItem(req: AdaptivePlacementRespondRequest): Observable<AdaptivePlacementSubmitResult> {
    return this.http.post<AdaptivePlacementSubmitResult>(`${this.api}/student/placement/respond`, req);
  }

  completeAdaptive(assessmentId: string): Observable<AdaptivePlacementSummary> {
    return this.http.post<AdaptivePlacementSummary>(
      `${this.api}/student/placement/complete`, { assessmentId });
  }

  /** Fetches (generating on first request) an adaptive listening item's audio as a blob URL. */
  getAdaptiveItemAudioBlobUrl(assessmentId: string, itemId: string): Observable<string> {
    const url = `${this.api}/student/placement/audio/${assessmentId}/items/${itemId}/listening`;
    return this.http.get(url, { responseType: 'blob' }).pipe(
      map(blob => URL.createObjectURL(blob))
    );
  }
}
