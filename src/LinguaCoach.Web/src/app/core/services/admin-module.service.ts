import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ModuleDto,
  ModuleListResult,
  GenerateModuleFromItemsRequestBody,
  GenerateModuleFromResourceRequestBody,
  GenerateModuleFromLessonRequestBody,
  GenerateModuleFromExerciseRequestBody,
  GenerateModuleResult,
  ModulePreviewResult,
  ModulePreviewSubmitRequestBody,
  ModulePreviewSubmitResult,
} from '../models/admin-module.models';

@Injectable({ providedIn: 'root' })
export class AdminModuleService {
  private readonly base = `${environment.apiUrl}/admin/modules`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, cefrLevel?: string, skill?: string, subskill?: string,
    contextTag?: string, focusTag?: string, difficultyBand?: number, lessonId?: string,
    exerciseId?: string, search?: string,
  ): Observable<ModuleListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status && status !== 'all') params = params.set('status', status);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (skill) params = params.set('skill', skill);
    if (subskill) params = params.set('subskill', subskill);
    if (contextTag) params = params.set('contextTag', contextTag);
    if (focusTag) params = params.set('focusTag', focusTag);
    if (difficultyBand !== undefined && difficultyBand !== null) params = params.set('difficultyBand', difficultyBand);
    if (lessonId) params = params.set('lessonId', lessonId);
    if (exerciseId) params = params.set('exerciseId', exerciseId);
    if (search) params = params.set('search', search);
    return this.http.get<ModuleListResult>(this.base, { params });
  }

  get(id: string): Observable<ModuleDto> {
    return this.http.get<ModuleDto>(`${this.base}/${id}`);
  }

  preview(id: string): Observable<ModulePreviewResult> {
    return this.http.get<ModulePreviewResult>(`${this.base}/${id}/preview`);
  }

  previewSubmit(id: string, body: ModulePreviewSubmitRequestBody): Observable<ModulePreviewSubmitResult> {
    return this.http.post<ModulePreviewSubmitResult>(`${this.base}/${id}/preview/submit`, body);
  }

  generateFromItems(body: GenerateModuleFromItemsRequestBody): Observable<GenerateModuleResult> {
    return this.http.post<GenerateModuleResult>(`${this.base}/generate-from-items`, body);
  }

  generateFromResource(body: GenerateModuleFromResourceRequestBody): Observable<GenerateModuleResult> {
    return this.http.post<GenerateModuleResult>(`${this.base}/generate-from-resource`, body);
  }

  generateFromResourceWithAi(body: GenerateModuleFromResourceRequestBody): Observable<GenerateModuleResult> {
    return this.http.post<GenerateModuleResult>(`${this.base}/generate-from-resource/ai`, body);
  }

  generateFromLesson(body: GenerateModuleFromLessonRequestBody): Observable<GenerateModuleResult> {
    return this.http.post<GenerateModuleResult>(`${this.base}/generate-from-lesson`, body);
  }

  generateFromExercise(body: GenerateModuleFromExerciseRequestBody): Observable<GenerateModuleResult> {
    return this.http.post<GenerateModuleResult>(`${this.base}/generate-from-exercise`, body);
  }

  approve(id: string, notes?: string | null): Observable<ModuleDto> {
    return this.http.post<ModuleDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<ModuleDto> {
    return this.http.post<ModuleDto>(`${this.base}/${id}/reject`, { reason });
  }
}
