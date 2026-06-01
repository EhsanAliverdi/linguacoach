import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WritingExerciseDto, WritingFeedbackDto } from '../models/writing.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class WritingService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getExercise(): Observable<WritingExerciseDto> {
    return this.http.get<WritingExerciseDto>(`${this.api}/writing/exercise`);
  }

  submitDraft(draftText: string): Observable<WritingFeedbackDto> {
    return this.http.post<WritingFeedbackDto>(`${this.api}/writing/exercise/submit`, { draftText });
  }
}
