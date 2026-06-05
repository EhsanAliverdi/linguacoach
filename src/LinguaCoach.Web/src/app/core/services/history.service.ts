import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ModuleActivityHistory, ActivityAttemptHistory } from '../models/history.models';

@Injectable({ providedIn: 'root' })
export class HistoryService {
  constructor(private http: HttpClient) {}

  getModuleActivities(moduleId: string): Observable<ModuleActivityHistory> {
    return this.http.get<ModuleActivityHistory>(`/api/learning-path/modules/${moduleId}/activities`);
  }

  getActivityAttempts(activityId: string): Observable<ActivityAttemptHistory> {
    return this.http.get<ActivityAttemptHistory>(`/api/activity/${activityId}/attempts`);
  }
}
