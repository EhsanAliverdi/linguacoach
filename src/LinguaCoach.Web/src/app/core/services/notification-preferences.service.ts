import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NotificationPreferenceItem {
  category: string;
  channel: string;
  isEnabled: boolean;
  isRequired: boolean;
}

export interface UpdateNotificationPreferenceRequest {
  category: string;
  channel: string;
  isEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  getPreferences(): Observable<NotificationPreferenceItem[]> {
    return this.http.get<NotificationPreferenceItem[]>(`${this.api}/notifications/preferences`);
  }

  updatePreferences(prefs: UpdateNotificationPreferenceRequest[]): Observable<void> {
    return this.http.put<void>(`${this.api}/notifications/preferences`, prefs);
  }
}
