import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdaptivePlacementSummary, AdaptivePlacementNextItem, AdaptivePlacementRespondRequest,
  AdaptivePlacementSubmitResult, PlacementConfig, PlacementSkillStatus,
} from '../models/placement.models';
import { map } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class PlacementService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ── Phase 14A — Adaptive placement ──────────────────────────────────────────

  getPlacementConfig(): Observable<PlacementConfig> {
    return this.http.get<PlacementConfig>(`${this.api}/student/placement/config`);
  }

  getAdaptiveCurrent(): Observable<AdaptivePlacementSummary | null> {
    return this.http.get<AdaptivePlacementSummary | null>(`${this.api}/student/placement/current`);
  }

  getAdaptiveNextItem(assessmentId: string, skill?: string): Observable<AdaptivePlacementNextItem | null> {
    const params: Record<string, string> = { assessmentId };
    if (skill) params['skill'] = skill;
    return this.http.get<AdaptivePlacementNextItem | null>(
      `${this.api}/student/placement/next`, { params });
  }

  getSkillStatus(): Observable<PlacementSkillStatus[]> {
    return this.http.get<PlacementSkillStatus[]>(`${this.api}/student/placement/skills`);
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

  /** Uploads a recorded speaking response for an adaptive placement item (used by the
   *  "speakingResponse" Form.io component's placementContext.uploadSpeakingAudio). */
  uploadAdaptiveSpeakingAudio(
    assessmentId: string, itemId: string, blob: Blob, mimeType: string, durationSeconds: number,
  ): Observable<{ storageKey: string; mimeType: string; durationSeconds: number | null }> {
    const url = `${this.api}/student/placement/audio/${assessmentId}/items/${itemId}/speaking`;
    const form = new FormData();
    form.append('audioFile', blob, `recording.${mimeType.includes('webm') ? 'webm' : 'audio'}`);
    form.append('durationSeconds', String(durationSeconds));
    return this.http.post<{ storageKey: string; mimeType: string; durationSeconds: number | null }>(url, form);
  }
}
