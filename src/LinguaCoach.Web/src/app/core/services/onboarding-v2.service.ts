import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StudentOnboardingActiveDto, SubmitOnboardingResult } from '../models/onboarding-v2.models';

@Injectable({ providedIn: 'root' })
export class OnboardingV2Service {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getActive(): Observable<StudentOnboardingActiveDto> {
    return this.http.get<StudentOnboardingActiveDto>(`${this.api}/onboarding/active`);
  }

  saveDraft(submissionJson: string): Observable<void> {
    return this.http.post<void>(`${this.api}/onboarding/save-draft`, { submissionJson });
  }

  submit(submissionJson: string): Observable<SubmitOnboardingResult> {
    return this.http.post<SubmitOnboardingResult>(`${this.api}/onboarding/submit`, { submissionJson });
  }
}
