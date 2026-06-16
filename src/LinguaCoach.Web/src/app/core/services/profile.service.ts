import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface StudentProfileResponse {
  profileId: string;
  userId: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  preferredName: string | null;
  email: string | null;
  cefrLevel: string | null;
  learningGoals: string[];
  customLearningGoal: string | null;
  focusAreas: string[];
  customFocusArea: string | null;
  supportLanguageCode: string | null;
  supportLanguageName: string | null;
  translationHelpPreference: 'Never' | 'WhenDifficult' | 'AlwaysAvailable' | null;
  preferredSessionDurationMinutes: number | null;
  difficultyPreference: 'Gentle' | 'Balanced' | 'Challenging' | null;
  learningPreferencesUpdatedAt: string | null;
}

export interface UpdateLearningPreferencesRequest {
  preferredName?: string | null;
  supportLanguageCode?: string | null;
  supportLanguageName?: string | null;
  translationHelpPreference?: number | null;
  learningGoals?: string[] | null;
  customLearningGoal?: string | null;
  focusAreas?: string[] | null;
  customFocusArea?: string | null;
  difficultyPreference?: number | null;
  preferredSessionDurationMinutes?: number | null;
}

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getProfile(): Observable<StudentProfileResponse> {
    return this.http.get<StudentProfileResponse>(`${this.api}/profile`);
  }

  updatePreferences(request: UpdateLearningPreferencesRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/profile/preferences`, request);
  }
}
