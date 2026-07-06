import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateTemplateRequest,
  SaveDraftRequest,
  StudentFlowTemplateDetailDto,
  StudentFlowTemplateSummaryDto,
  StudentFlowTemplateVersionDto,
} from '../models/admin-onboarding.models';

@Injectable({ providedIn: 'root' })
export class AdminOnboardingService {
  private readonly base = `${environment.apiUrl}/admin/onboarding`;

  constructor(private http: HttpClient) {}

  listTemplates(): Observable<StudentFlowTemplateSummaryDto[]> {
    return this.http.get<StudentFlowTemplateSummaryDto[]>(`${this.base}/templates`);
  }

  getActiveTemplate(): Observable<StudentFlowTemplateDetailDto> {
    return this.http.get<StudentFlowTemplateDetailDto>(`${this.base}/templates/active`);
  }

  getTemplate(templateId: string): Observable<StudentFlowTemplateDetailDto> {
    return this.http.get<StudentFlowTemplateDetailDto>(`${this.base}/templates/${templateId}`);
  }

  createTemplate(request: CreateTemplateRequest): Observable<StudentFlowTemplateDetailDto> {
    return this.http.post<StudentFlowTemplateDetailDto>(`${this.base}/templates`, request);
  }

  saveDraft(templateId: string, request: SaveDraftRequest): Observable<StudentFlowTemplateVersionDto> {
    return this.http.put<StudentFlowTemplateVersionDto>(`${this.base}/templates/${templateId}/draft`, request);
  }

  publish(templateId: string): Observable<StudentFlowTemplateVersionDto> {
    return this.http.post<StudentFlowTemplateVersionDto>(`${this.base}/templates/${templateId}/publish`, {});
  }

  archive(templateId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/templates/${templateId}/archive`, {});
  }
}
