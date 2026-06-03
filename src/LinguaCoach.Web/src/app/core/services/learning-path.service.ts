import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LearningPathDetail } from '../models/learning-path.models';

@Injectable({ providedIn: 'root' })
export class LearningPathService {
  private readonly base = '/api/learning-path';

  constructor(private http: HttpClient) {}

  getActivePath(): Observable<LearningPathDetail> {
    return this.http.get<LearningPathDetail>(this.base);
  }
}
