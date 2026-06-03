import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WritingScenarioDto, WritingExerciseDto, WritingFeedbackDto } from '../models/writing.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class WritingService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getScenarios(): Observable<WritingScenarioDto[]> {
    return this.http.get<WritingScenarioDto[]>(`${this.api}/writing/scenarios`);
  }

  getExercise(scenarioId: string): Observable<WritingExerciseDto> {
    return this.http.get<WritingExerciseDto>(`${this.api}/writing/exercise/${scenarioId}`);
  }

  submitDraft(draftText: string, scenarioId?: string): Observable<WritingFeedbackDto> {
    return this.http.post<WritingFeedbackDto>(`${this.api}/writing/exercise/submit`, { draftText, scenarioId });
  }
}
