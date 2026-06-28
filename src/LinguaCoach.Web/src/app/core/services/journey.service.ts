import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StudentJourney } from '../models/journey.models';

@Injectable({ providedIn: 'root' })
export class JourneyService {
  constructor(private http: HttpClient) {}

  getJourney(): Observable<StudentJourney> {
    return this.http.get<StudentJourney>('/api/student/learning-plan/journey');
  }
}
