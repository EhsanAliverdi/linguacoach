import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ModuleDefinitionDto,
  ModuleDefinitionListResult,
  GenerateModuleFromItemsRequestBody,
  GenerateModuleFromResourceRequestBody,
  GenerateModuleFromLearnItemRequestBody,
  GenerateModuleFromActivityRequestBody,
  GenerateModuleDefinitionResult,
} from '../models/admin-module-definition.models';

@Injectable({ providedIn: 'root' })
export class AdminModuleDefinitionService {
  private readonly base = `${environment.apiUrl}/admin/modules`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, cefrLevel?: string, skill?: string, subskill?: string,
    contextTag?: string, focusTag?: string, difficultyBand?: number, learnItemId?: string,
    activityDefinitionId?: string, search?: string,
  ): Observable<ModuleDefinitionListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status && status !== 'all') params = params.set('status', status);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (skill) params = params.set('skill', skill);
    if (subskill) params = params.set('subskill', subskill);
    if (contextTag) params = params.set('contextTag', contextTag);
    if (focusTag) params = params.set('focusTag', focusTag);
    if (difficultyBand !== undefined && difficultyBand !== null) params = params.set('difficultyBand', difficultyBand);
    if (learnItemId) params = params.set('learnItemId', learnItemId);
    if (activityDefinitionId) params = params.set('activityDefinitionId', activityDefinitionId);
    if (search) params = params.set('search', search);
    return this.http.get<ModuleDefinitionListResult>(this.base, { params });
  }

  get(id: string): Observable<ModuleDefinitionDto> {
    return this.http.get<ModuleDefinitionDto>(`${this.base}/${id}`);
  }

  generateFromItems(body: GenerateModuleFromItemsRequestBody): Observable<GenerateModuleDefinitionResult> {
    return this.http.post<GenerateModuleDefinitionResult>(`${this.base}/generate-from-items`, body);
  }

  generateFromResource(body: GenerateModuleFromResourceRequestBody): Observable<GenerateModuleDefinitionResult> {
    return this.http.post<GenerateModuleDefinitionResult>(`${this.base}/generate-from-resource`, body);
  }

  generateFromLearnItem(body: GenerateModuleFromLearnItemRequestBody): Observable<GenerateModuleDefinitionResult> {
    return this.http.post<GenerateModuleDefinitionResult>(`${this.base}/generate-from-learn-item`, body);
  }

  generateFromActivity(body: GenerateModuleFromActivityRequestBody): Observable<GenerateModuleDefinitionResult> {
    return this.http.post<GenerateModuleDefinitionResult>(`${this.base}/generate-from-activity`, body);
  }

  approve(id: string, notes?: string | null): Observable<ModuleDefinitionDto> {
    return this.http.post<ModuleDefinitionDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<ModuleDefinitionDto> {
    return this.http.post<ModuleDefinitionDto>(`${this.base}/${id}/reject`, { reason });
  }
}
