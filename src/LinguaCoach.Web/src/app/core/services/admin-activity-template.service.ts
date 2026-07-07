import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminActivityTemplateDto,
  AdminActivityTemplateListResult,
  ActivityTemplateCreateRequest,
  ActivityTemplateUpdateRequest,
  ActivityTemplateReviewRequest,
  ActivityTemplatePublishRequest,
  ActivityTemplateGeneratePreviewRequest,
  ActivityTemplateInstanceResult,
} from '../models/admin-activity-template.models';

@Injectable({ providedIn: 'root' })
export class AdminActivityTemplateService {
  private readonly base = `${environment.apiUrl}/admin/activity-templates`;

  constructor(private http: HttpClient) {}

  list(
    page: number,
    pageSize: number,
    skill?: string,
    cefrLevel?: string,
    reviewStatus?: string,
    search?: string,
  ): Observable<AdminActivityTemplateListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (skill && skill !== 'all') params = params.set('skill', skill);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (reviewStatus && reviewStatus !== 'all') params = params.set('reviewStatus', reviewStatus);
    if (search) params = params.set('search', search);
    return this.http.get<AdminActivityTemplateListResult>(this.base, { params });
  }

  get(templateId: string): Observable<AdminActivityTemplateDto> {
    return this.http.get<AdminActivityTemplateDto>(`${this.base}/${templateId}`);
  }

  add(request: ActivityTemplateCreateRequest): Observable<AdminActivityTemplateDto> {
    return this.http.post<AdminActivityTemplateDto>(this.base, request);
  }

  update(templateId: string, request: ActivityTemplateUpdateRequest): Observable<AdminActivityTemplateDto> {
    return this.http.put<AdminActivityTemplateDto>(`${this.base}/${templateId}`, request);
  }

  remove(templateId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${templateId}`);
  }

  setReviewStatus(templateId: string, request: ActivityTemplateReviewRequest): Observable<AdminActivityTemplateDto> {
    return this.http.post<AdminActivityTemplateDto>(`${this.base}/${templateId}/review`, request);
  }

  setPublished(templateId: string, request: ActivityTemplatePublishRequest): Observable<AdminActivityTemplateDto> {
    return this.http.post<AdminActivityTemplateDto>(`${this.base}/${templateId}/publish`, request);
  }

  generatePreview(templateId: string, request: ActivityTemplateGeneratePreviewRequest): Observable<ActivityTemplateInstanceResult> {
    return this.http.post<ActivityTemplateInstanceResult>(`${this.base}/${templateId}/generate-preview`, request);
  }
}
