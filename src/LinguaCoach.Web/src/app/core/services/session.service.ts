import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TodaysSessionResponse,
  SessionDetailResponse,
  StartSessionResponse,
  CompleteSessionResponse,
  CompleteExerciseResponse,
  PrepareExerciseResponse,
} from '../models/session.models';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly base = `${environment.apiUrl}/sessions`;

  constructor(private http: HttpClient) {}

  getToday(): Observable<TodaysSessionResponse> {
    return this.http.get<TodaysSessionResponse>(`${this.base}/today`);
  }

  getById(sessionId: string): Observable<SessionDetailResponse> {
    return this.http.get<SessionDetailResponse>(`${this.base}/${sessionId}`);
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

  prepareExercise(sessionId: string, exerciseId: string): Observable<PrepareExerciseResponse> {
    return this.http.post<PrepareExerciseResponse>(
      `${this.base}/${sessionId}/exercises/${exerciseId}/prepare`, null);
  }
}
