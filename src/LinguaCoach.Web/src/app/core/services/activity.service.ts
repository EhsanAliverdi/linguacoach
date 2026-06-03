import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ActivityDto, ActivityFeedbackDto, ActivityType } from '../models/activity.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly base = `${environment.apiUrl}/activity`;

  constructor(private http: HttpClient) {}

  getNext(type?: ActivityType): Observable<ActivityDto> {
    const params = type ? { type } : {};
    return this.http.get<ActivityDto>(`${this.base}/next`, { params });
  }

  submitAttempt(activityId: string, submittedContent: string, audioUrl?: string): Observable<ActivityFeedbackDto> {
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/attempt`, {
      submittedContent,
      audioUrl: audioUrl ?? null,
    });
  }
}
