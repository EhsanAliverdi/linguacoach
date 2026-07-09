import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ActivityDefinitionDto,
  ActivityDefinitionListResult,
  GenerateActivityFromResourcesRequestBody,
  GenerateActivityFromLearnItemRequestBody,
  GenerateActivityDefinitionResult,
} from '../models/admin-activity-definition.models';

@Injectable({ providedIn: 'root' })
export class AdminActivityDefinitionService {
  private readonly base = `${environment.apiUrl}/admin/activities`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, activityType?: string, rendererType?: string,
    cefrLevel?: string, skill?: string, subskill?: string, contextTag?: string, focusTag?: string,
    difficultyBand?: number, learnItemId?: string, search?: string,
  ): Observable<ActivityDefinitionListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status && status !== 'all') params = params.set('status', status);
    if (activityType && activityType !== 'all') params = params.set('activityType', activityType);
    if (rendererType && rendererType !== 'all') params = params.set('rendererType', rendererType);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (skill) params = params.set('skill', skill);
    if (subskill) params = params.set('subskill', subskill);
    if (contextTag) params = params.set('contextTag', contextTag);
    if (focusTag) params = params.set('focusTag', focusTag);
    if (difficultyBand !== undefined && difficultyBand !== null) params = params.set('difficultyBand', difficultyBand);
    if (learnItemId) params = params.set('learnItemId', learnItemId);
    if (search) params = params.set('search', search);
    return this.http.get<ActivityDefinitionListResult>(this.base, { params });
  }

  get(id: string): Observable<ActivityDefinitionDto> {
    return this.http.get<ActivityDefinitionDto>(`${this.base}/${id}`);
  }

  generateFromResources(body: GenerateActivityFromResourcesRequestBody): Observable<GenerateActivityDefinitionResult> {
    return this.http.post<GenerateActivityDefinitionResult>(`${this.base}/generate-from-resources`, body);
  }

  generateFromLearnItem(body: GenerateActivityFromLearnItemRequestBody): Observable<GenerateActivityDefinitionResult> {
    return this.http.post<GenerateActivityDefinitionResult>(`${this.base}/generate-from-learn-item`, body);
  }

  approve(id: string, notes?: string | null): Observable<ActivityDefinitionDto> {
    return this.http.post<ActivityDefinitionDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<ActivityDefinitionDto> {
    return this.http.post<ActivityDefinitionDto>(`${this.base}/${id}/reject`, { reason });
  }
}
