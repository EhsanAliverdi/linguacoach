import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CefrAssessmentResult } from '../models/assessment.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AssessmentService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  assessCefr(studentSample: string): Observable<CefrAssessmentResult> {
    return this.http.post<CefrAssessmentResult>(`${this.api}/assessment/cefr`, { studentSample });
  }
}
