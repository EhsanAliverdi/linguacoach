import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminOnboardingCategoryDto,
  AdminOnboardingFlowDto,
  AdminOnboardingFlowSummary,
  AdminOnboardingStepDto,
  CategoryRequest,
  CreateFlowRequest,
  StepRequest,
} from '../models/admin-onboarding.models';

@Injectable({ providedIn: 'root' })
export class AdminOnboardingService {
  private readonly base = `${environment.apiUrl}/admin/onboarding`;

  constructor(private http: HttpClient) {}

  listFlows(): Observable<AdminOnboardingFlowSummary[]> {
    return this.http.get<AdminOnboardingFlowSummary[]>(`${this.base}/flows`);
  }

  getActiveFlow(): Observable<AdminOnboardingFlowDto> {
    return this.http.get<AdminOnboardingFlowDto>(`${this.base}/flow`);
  }

  createFlow(request: CreateFlowRequest): Observable<AdminOnboardingFlowDto> {
    return this.http.post<AdminOnboardingFlowDto>(`${this.base}/flows`, request);
  }

  activateFlow(flowId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/flows/${flowId}/activate`, null);
  }

  addStep(flowId: string, request: StepRequest): Observable<AdminOnboardingStepDto> {
    return this.http.post<AdminOnboardingStepDto>(`${this.base}/flows/${flowId}/steps`, request);
  }

  updateStep(flowId: string, stepKey: string, request: StepRequest): Observable<AdminOnboardingStepDto> {
    return this.http.put<AdminOnboardingStepDto>(`${this.base}/flows/${flowId}/steps/${stepKey}`, request);
  }

  removeStep(flowId: string, stepKey: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/flows/${flowId}/steps/${stepKey}`);
  }

  reorderSteps(flowId: string, stepKeyOrder: string[]): Observable<void> {
    return this.http.put<void>(`${this.base}/flows/${flowId}/steps/reorder`, { stepKeyOrder });
  }

  addCategory(flowId: string, request: CategoryRequest): Observable<AdminOnboardingCategoryDto> {
    return this.http.post<AdminOnboardingCategoryDto>(`${this.base}/flows/${flowId}/categories`, request);
  }

  updateCategory(flowId: string, categoryId: string, request: CategoryRequest): Observable<AdminOnboardingCategoryDto> {
    return this.http.put<AdminOnboardingCategoryDto>(`${this.base}/flows/${flowId}/categories/${categoryId}`, request);
  }

  removeCategory(flowId: string, categoryId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/flows/${flowId}/categories/${categoryId}`);
  }
}
