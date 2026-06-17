import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  OnboardingV2Status,
  SubmitStepResult,
  CompleteOnboardingResult,
} from '../models/onboarding-v2.models';

@Injectable({ providedIn: 'root' })
export class OnboardingV2Service {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<OnboardingV2Status> {
    return this.http.get<OnboardingV2Status>(`${this.api}/onboarding`);
  }

  submitStep(stepKey: string, answerJson: string): Observable<SubmitStepResult> {
    return this.http.post<SubmitStepResult>(
      `${this.api}/onboarding/steps/${encodeURIComponent(stepKey)}`,
      { answerJson }
    );
  }

  complete(): Observable<CompleteOnboardingResult> {
    return this.http.post<CompleteOnboardingResult>(`${this.api}/onboarding/complete`, {});
  }
}
