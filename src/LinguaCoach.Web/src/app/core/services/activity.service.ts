import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ActivityDto, ActivityFeedbackDto, ActivityType, ListeningAnswer, VocabAnswer } from '../models/activity.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly base = `${environment.apiUrl}/activity`;

  constructor(private http: HttpClient) {}

  getNext(type?: ActivityType): Observable<ActivityDto> {
    const params = type ? { type: this.toApiActivityType(type) } : undefined;
    return this.http.get<ActivityDto>(`${this.base}/next`, { params });
  }

  private toApiActivityType(type: ActivityType): string {
    switch (type) {
      case 'writingScenario': return 'WritingScenario';
      case 'listeningComprehension': return 'ListeningComprehension';
      case 'vocabularyPractice': return 'VocabularyPractice';
      case 'speakingRolePlay': return 'SpeakingRolePlay';
      case 'pronunciationPractice': return 'PronunciationPractice';
      case 'readingTask': return 'ReadingTask';
      default: return String(type);
    }
  }

  submitAttempt(activityId: string, submittedContent: string, audioUrl?: string): Observable<ActivityFeedbackDto> {
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/attempt`, {
      submittedContent,
      audioUrl: audioUrl ?? null,
    });
  }

  submitVocabAttempt(activityId: string, answers: VocabAnswer[]): Observable<ActivityFeedbackDto> {
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/attempt`, {
      submittedContent: '',
      answers,
    });
  }

  submitListeningAttempt(activityId: string, answers: ListeningAnswer[], responseText: string): Observable<ActivityFeedbackDto> {
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/attempt`, {
      submittedContent: responseText,
      responseText,
      answers,
    });
  }
}
