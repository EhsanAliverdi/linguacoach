import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LearnItemDto,
  LearnItemListResult,
  CreateLearnItemRequestBody,
  UpdateLearnItemRequestBody,
  GenerateLearnItemFromResourcesRequestBody,
  GenerateLearnItemFromResourcesResult,
} from '../models/admin-learn-item.models';

@Injectable({ providedIn: 'root' })
export class AdminLearnItemService {
  private readonly base = `${environment.apiUrl}/admin/learn-items`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, cefrLevel?: string, skill?: string, subskill?: string,
    contextTag?: string, focusTag?: string, difficultyBand?: number, search?: string,
    resourceType?: string, resourceId?: string,
  ): Observable<LearnItemListResult> {
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
    return this.http.get<LearnItemListResult>(this.base, { params });
  }

  get(id: string): Observable<LearnItemDto> {
    return this.http.get<LearnItemDto>(`${this.base}/${id}`);
  }

  create(body: CreateLearnItemRequestBody): Observable<LearnItemDto> {
    return this.http.post<LearnItemDto>(this.base, body);
  }

  generateFromResources(body: GenerateLearnItemFromResourcesRequestBody): Observable<GenerateLearnItemFromResourcesResult> {
    return this.http.post<GenerateLearnItemFromResourcesResult>(`${this.base}/generate-from-resources`, body);
  }

  update(id: string, body: UpdateLearnItemRequestBody): Observable<LearnItemDto> {
    return this.http.put<LearnItemDto>(`${this.base}/${id}`, body);
  }

  approve(id: string, notes?: string | null): Observable<LearnItemDto> {
    return this.http.post<LearnItemDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<LearnItemDto> {
    return this.http.post<LearnItemDto>(`${this.base}/${id}/reject`, { reason });
  }
}
