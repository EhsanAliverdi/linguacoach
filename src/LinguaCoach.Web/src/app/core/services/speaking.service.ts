import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SpeakingSessionDto, SpeakingTurnResultDto } from '../models/speaking.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SpeakingService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createSession(scenarioId: string): Observable<SpeakingSessionDto> {
    return this.http.post<SpeakingSessionDto>(`${this.api}/speaking/sessions`, { scenarioId });
  }

  submitTurn(sessionId: string, userTranscript: string): Observable<SpeakingTurnResultDto> {
    return this.http.post<SpeakingTurnResultDto>(
      `${this.api}/speaking/sessions/${sessionId}/turns`,
      { userTranscript });
  }
}
