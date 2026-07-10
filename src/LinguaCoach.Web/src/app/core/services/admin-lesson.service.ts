import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LessonDto,
  LessonListResult,
  CreateLessonRequestBody,
  UpdateLessonRequestBody,
  GenerateLessonFromResourcesRequestBody,
  GenerateLessonFromResourcesResult,
} from '../models/admin-lesson.models';

@Injectable({ providedIn: 'root' })
export class AdminLessonService {
  private readonly base = `${environment.apiUrl}/admin/lessons`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, cefrLevel?: string, skill?: string, subskill?: string,
    contextTag?: string, focusTag?: string, difficultyBand?: number, search?: string,
    resourceType?: string, resourceId?: string,
  ): Observable<LessonListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status && status !== 'all') params = params.set('status', status);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (skill && skill !== 'all') params = params.set('skill', skill);
    if (subskill) params = params.set('subskill', subskill);
    if (contextTag) params = params.set('contextTag', contextTag);
    if (focusTag) params = params.set('focusTag', focusTag);
    if (difficultyBand !== undefined && difficultyBand !== null) params = params.set('difficultyBand', difficultyBand);
    if (search) params = params.set('search', search);
    if (resourceType) params = params.set('resourceType', resourceType);
    if (resourceId) params = params.set('resourceId', resourceId);
    return this.http.get<LessonListResult>(this.base, { params });
  }

  get(id: string): Observable<LessonDto> {
    return this.http.get<LessonDto>(`${this.base}/${id}`);
  }

  create(body: CreateLessonRequestBody): Observable<LessonDto> {
    return this.http.post<LessonDto>(this.base, body);
  }

  generateFromResources(body: GenerateLessonFromResourcesRequestBody): Observable<GenerateLessonFromResourcesResult> {
    return this.http.post<GenerateLessonFromResourcesResult>(`${this.base}/generate-from-resources`, body);
  }

  generateFromResourcesWithAi(body: GenerateLessonFromResourcesRequestBody): Observable<GenerateLessonFromResourcesResult> {
    return this.http.post<GenerateLessonFromResourcesResult>(`${this.base}/generate-from-resources/ai`, body);
  }

  update(id: string, body: UpdateLessonRequestBody): Observable<LessonDto> {
    return this.http.put<LessonDto>(`${this.base}/${id}`, body);
  }

  approve(id: string, notes?: string | null): Observable<LessonDto> {
    return this.http.post<LessonDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<LessonDto> {
    return this.http.post<LessonDto>(`${this.base}/${id}/reject`, { reason });
  }
}
