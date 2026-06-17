import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AdminCurriculumObjectiveDto {
  id: string;
  key: string;
  title: string;
  description: string;
  cefrLevel: string;
  primarySkill: string;
  secondarySkillsJson: string;
  contextTagsJson: string;
  focusTagsJson: string;
  prerequisiteKeysJson: string;
  recommendedOrder: number;
  difficultyBand: number;
  isActive: boolean;
  isReviewable: boolean;
  isExamInspired: boolean;
  teachingNotes: string | null;
  examplePrompts: string | null;
  createdAt: string;
  adminUpdatedAt: string | null;
}

export interface AdminCurriculumObjectiveUpsertRequest {
  key: string;
  title: string;
  description: string;
  cefrLevel: string;
  primarySkill: string;
  secondarySkills: string[];
  contextTags: string[];
  focusTags: string[];
  prerequisiteObjectiveKeys: string[];
  recommendedOrder: number;
  difficultyBand: number;
  isActive: boolean;
  isReviewable: boolean;
  isExamInspired: boolean;
  teachingNotes: string | null;
  examplePrompts: string | null;
}

export interface CurriculumTaxonomyDto {
  cefrLevels: string[];
  skills: string[];
  contextTags: string[];
}

export interface AdminRoutingPreviewRequest {
  studentId?: string | null;
  cefrLevelOverride?: string | null;
  learningGoals?: string[];
  focusAreas?: string[];
  primarySkill?: string | null;
  source?: string | null;
  difficultyPreference?: string | null;
  allowReviewOrScaffold: boolean;
}

export interface AdminRoutingPreviewResult {
  targetCefrLevel: string;
  curriculumObjectiveKey: string | null;
  curriculumObjectiveTitle: string | null;
  contextTags: string[];
  focusTags: string[];
  difficultyBand: number;
  routingReason: string;
  isLowerLevelContent: boolean;
  explanation: string | null;
  fallbackUsed: boolean;
  noExactObjectiveFound: boolean;
  warnings: string[];
}

@Injectable({ providedIn: 'root' })
export class CurriculumService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  listObjectives(cefrLevel?: string, skill?: string, isActive?: boolean): Observable<AdminCurriculumObjectiveDto[]> {
    const params: Record<string, string> = {};
    if (cefrLevel) params['cefrLevel'] = cefrLevel;
    if (skill) params['skill'] = skill;
    if (isActive !== undefined) params['isActive'] = String(isActive);
    return this.http.get<AdminCurriculumObjectiveDto[]>(`${this.api}/admin/curriculum/objectives`, { params });
  }

  getObjective(key: string): Observable<AdminCurriculumObjectiveDto> {
    return this.http.get<AdminCurriculumObjectiveDto>(`${this.api}/admin/curriculum/objectives/${key}`);
  }

  getTaxonomy(): Observable<CurriculumTaxonomyDto> {
    return this.http.get<CurriculumTaxonomyDto>(`${this.api}/admin/curriculum/taxonomy`);
  }

  createObjective(request: AdminCurriculumObjectiveUpsertRequest): Observable<AdminCurriculumObjectiveDto> {
    return this.http.post<AdminCurriculumObjectiveDto>(`${this.api}/admin/curriculum/objectives`, request);
  }

  updateObjective(key: string, request: AdminCurriculumObjectiveUpsertRequest): Observable<AdminCurriculumObjectiveDto> {
    return this.http.put<AdminCurriculumObjectiveDto>(`${this.api}/admin/curriculum/objectives/${key}`, request);
  }

  activateObjective(key: string): Observable<AdminCurriculumObjectiveDto> {
    return this.http.post<AdminCurriculumObjectiveDto>(`${this.api}/admin/curriculum/objectives/${key}/activate`, {});
  }

  deactivateObjective(key: string): Observable<AdminCurriculumObjectiveDto> {
    return this.http.post<AdminCurriculumObjectiveDto>(`${this.api}/admin/curriculum/objectives/${key}/deactivate`, {});
  }

  previewRouting(request: AdminRoutingPreviewRequest): Observable<AdminRoutingPreviewResult> {
    return this.http.post<AdminRoutingPreviewResult>(`${this.api}/admin/curriculum/routing-preview`, request);
  }
}
