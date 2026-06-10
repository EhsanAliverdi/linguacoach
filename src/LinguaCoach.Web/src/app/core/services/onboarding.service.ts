import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OnboardingStatusResponse, OnboardingStepResult, OnboardingStepRequest } from '../models/onboarding.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<OnboardingStatusResponse> {
    return this.http.get<OnboardingStatusResponse>(`${this.api}/onboarding/status`);
  }

  submitStep(request: OnboardingStepRequest): Observable<OnboardingStepResult> {
    return this.http.patch<OnboardingStepResult>(`${this.api}/onboarding`, request);
  }

  submitExperience(professionalExperienceLevel: number, roleFamiliarity: number): Observable<{ success: boolean }> {
    return this.http.patch<{ success: boolean }>(`${this.api}/onboarding/experience`, {
      professionalExperienceLevel,
      roleFamiliarity,
    });
  }
}
