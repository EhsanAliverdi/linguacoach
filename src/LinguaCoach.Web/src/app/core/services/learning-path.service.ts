import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LearningPathDetail, StudentLearningMemory } from '../models/learning-path.models';

@Injectable({ providedIn: 'root' })
export class LearningPathService {
  private readonly base = '/api/learning-path';

  constructor(private http: HttpClient) {}

  getActivePath(): Observable<LearningPathDetail> {
    return this.http.get<LearningPathDetail>(this.base);
  }

  completeModule(moduleId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/modules/${moduleId}/complete`, null);
  }

  getLearningMemory(): Observable<StudentLearningMemory> {
    return this.http.get<StudentLearningMemory>(`${this.base}/memory`);
  }

  generateNextModules(pathId?: string): Observable<LearningPathDetail> {
    return this.http.post<LearningPathDetail>(`${this.base}/generate-next`, pathId ? { pathId } : {});
  }
}
