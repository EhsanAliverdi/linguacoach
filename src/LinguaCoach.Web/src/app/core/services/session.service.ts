import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TodaysSessionResponse,
  StartSessionResponse,
  CompleteSessionResponse,
  CompleteExerciseResponse,
  SessionHistoryResponse,
} from '../models/session.models';

/**
 * Phase I2B — Today is module-only now. `getById`/`prepareExercise` were removed along with the
 * legacy lesson-runner page (their only caller) and the backend actions/handlers behind them
 * (GET /api/sessions/{id}, POST .../prepare). Start/complete/completeExercise/history remain —
 * they still operate on legitimate historical LearningSession data.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly base = `${environment.apiUrl}/sessions`;

  constructor(private http: HttpClient) {}

  getToday(): Observable<TodaysSessionResponse> {
    return this.http.get<TodaysSessionResponse>(`${this.base}/today`);
  }

  start(sessionId: string): Observable<StartSessionResponse> {
    return this.http.post<StartSessionResponse>(`${this.base}/${sessionId}/start`, null);
  }

  complete(sessionId: string): Observable<CompleteSessionResponse> {
    return this.http.post<CompleteSessionResponse>(`${this.base}/${sessionId}/complete`, null);
  }

  completeExercise(sessionId: string, exerciseId: string): Observable<CompleteExerciseResponse> {
    return this.http.post<CompleteExerciseResponse>(
      `${this.base}/${sessionId}/exercises/${exerciseId}/complete`, null);
  }

  getHistory(page = 1, pageSize = 20): Observable<SessionHistoryResponse> {
    return this.http.get<SessionHistoryResponse>(`${this.base}/history?page=${page}&pageSize=${pageSize}`);
  }
}
