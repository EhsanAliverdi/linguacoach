import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ActivityDto, ActivityFeedbackDto, ActivityType, ListeningAnswer, SpeakingEvaluationDto, VocabAnswer, WritingEvaluationDto } from '../models/activity.models';
import { environment } from '../../../environments/environment';
import { ExerciseTypeDefinition } from '../models/admin.models';


export interface ExerciseTypeSelection {
  key: string;
  displayName: string;
  primarySkill: string;
  secondarySkills: string[];
  rendererKey: string;
  evaluatorKey: string;
  legacyActivityType?: string | null;
  exercisePatternKey?: string | null;
  isAvailableForGeneration: boolean;
}

export interface ExerciseTypeSelectionResponse {
  hasSelection: boolean;
  selectedExerciseType?: ExerciseTypeSelection | null;
  reason?: string | null;
}

export interface PracticeGymNextResponse {
  hasActivity: boolean;
  activityId?: string | null;
  exerciseType?: string | null;
  primarySkill?: string | null;
  source?: 'pool' | 'onDemandFallback' | null;
  poolItemId?: string | null;
  reason?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly base = `${environment.apiUrl}/activity`;

  constructor(private http: HttpClient) {}

  getExerciseTypes(): Observable<ExerciseTypeDefinition[]> {
    return this.http.get<ExerciseTypeDefinition[]>(`${this.base}/exercise-types`);
  }

  selectPracticeGymExerciseType(skill: string): Observable<ExerciseTypeSelectionResponse> {
    return this.http.get<ExerciseTypeSelectionResponse>(`${this.base}/exercise-types/select`, {
      params: { skill, context: 'practiceGym' },
    });
  }

  getPracticeGymNext(options: { skill?: string; exerciseType?: string }): Observable<PracticeGymNextResponse> {
    const params: Record<string, string> = {};
    if (options.exerciseType) params['exerciseType'] = options.exerciseType;
    if (options.skill) params['skill'] = options.skill;
    return this.http.get<PracticeGymNextResponse>(`${this.base}/practice-gym/next`, { params });
  }

  getNext(type?: ActivityType, patternKey?: string): Observable<ActivityDto> {
    if (patternKey) {
      return this.http.get<ActivityDto>(`${this.base}/next`, { params: { pattern: patternKey } });
    }
    const params = type ? { type: this.toApiActivityType(type) } : undefined;
    return this.http.get<ActivityDto>(`${this.base}/next`, { params });
  }

  getById(activityId: string): Observable<ActivityDto> {
    return this.http.get<ActivityDto>(`${this.base}/${activityId}`);
  }

  /** Fetches protected activity audio as a blob URL. HttpClient attaches the JWT. */
  getAudioBlobUrl(audioApiPath: string): Observable<string> {
    return this.http.get(this.toAbsoluteApiUrl(audioApiPath), { responseType: 'blob' }).pipe(
      map(blob => URL.createObjectURL(blob))
    );
  }

  private toAbsoluteApiUrl(apiPath: string): string {
    if (apiPath.startsWith('blob:') || apiPath.startsWith('http://') || apiPath.startsWith('https://')) {
      return apiPath;
    }

    return `${environment.apiUrl.replace(/\/api$/, '')}${apiPath}`;
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

  submitSpeakingAttempt(activityId: string, audioBlob: Blob, mimeType: string, durationSeconds?: number): Observable<ActivityFeedbackDto> {
    const form = new FormData();
    form.append('audioFile', audioBlob, `recording${this.mimeTypeToExtension(mimeType)}`);
    if (durationSeconds != null) {
      form.append('durationSeconds', String(durationSeconds));
    }
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/speaking-attempt`, form);
  }

  submitAudioAttempt(activityId: string, audioBlob: Blob, mimeType: string, durationSeconds?: number): Observable<ActivityFeedbackDto> {
    const form = new FormData();
    form.append('audioFile', audioBlob, `recording${this.mimeTypeToExtension(mimeType)}`);
    if (durationSeconds != null) {
      form.append('durationSeconds', String(durationSeconds));
    }
    return this.http.post<ActivityFeedbackDto>(`${this.base}/${activityId}/audio-attempt`, form);
  }

  /** Returns the speaking evaluation status and result for a submitted audio attempt. */
  getAttemptEvaluation(activityId: string, attemptId: string): Observable<SpeakingEvaluationDto> {
    return this.http.get<SpeakingEvaluationDto>(`${this.base}/${activityId}/attempts/${attemptId}/evaluation`);
  }

  /** Returns the writing evaluation status and scores for a submitted written attempt. Returns 404 when no record exists. */
  getWritingEvaluation(activityId: string, attemptId: string): Observable<WritingEvaluationDto> {
    return this.http.get<WritingEvaluationDto>(`${this.base}/${activityId}/attempts/${attemptId}/writing-evaluation`);
  }

  private mimeTypeToExtension(mimeType: string): string {
    const base = mimeType.split(';')[0].trim();
    switch (base) {
      case 'audio/webm': return '.webm';
      case 'audio/wav': return '.wav';
      case 'audio/mpeg': return '.mp3';
      case 'audio/mp4': return '.mp4';
      case 'audio/x-m4a': return '.m4a';
      case 'audio/ogg': return '.ogg';
      default: return '.audio';
    }
  }
}
