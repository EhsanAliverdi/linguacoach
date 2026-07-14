import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ExerciseDto,
  ExerciseListResult,
  GenerateActivityFromResourcesRequestBody,
  GenerateActivityFromLessonRequestBody,
  GenerateExerciseResult,
  UpdateExerciseRequestBody,
  GenerateActivitiesFromLessonRequestBody,
  GenerateActivitiesFromLessonResult,
  ExerciseArchiveResult,
  ExercisePreviewSubmitRequestBody,
  ExercisePreviewSubmitResult,
  ExerciseRepairResult,
} from '../models/admin-exercise.models';
import { DiagnosticIssue, IssuesSummary, BulkRepairResult, RepairableItemSummary } from '../models/admin-repair.models';

@Injectable({ providedIn: 'root' })
export class AdminExerciseService {
  private readonly base = `${environment.apiUrl}/admin/exercises`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, status?: string, activityType?: string, rendererType?: string,
    cefrLevel?: string, skill?: string, subskill?: string, contextTag?: string, focusTag?: string,
    difficultyBand?: number, lessonId?: string, search?: string,
  ): Observable<ExerciseListResult> {
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
    if (lessonId) params = params.set('lessonId', lessonId);
    if (search) params = params.set('search', search);
    return this.http.get<ExerciseListResult>(this.base, { params });
  }

  get(id: string): Observable<ExerciseDto> {
    return this.http.get<ExerciseDto>(`${this.base}/${id}`);
  }

  generateFromResources(body: GenerateActivityFromResourcesRequestBody): Observable<GenerateExerciseResult> {
    return this.http.post<GenerateExerciseResult>(`${this.base}/generate-from-resources`, body);
  }

  generateFromResourcesWithAi(body: GenerateActivityFromResourcesRequestBody): Observable<GenerateExerciseResult> {
    return this.http.post<GenerateExerciseResult>(`${this.base}/generate-from-resources/ai`, body);
  }

  generateFromLesson(body: GenerateActivityFromLessonRequestBody): Observable<GenerateExerciseResult> {
    return this.http.post<GenerateExerciseResult>(`${this.base}/generate-from-lesson`, body);
  }

  /** Phase K5 — admin picks a count per Exercise type (e.g. 5 gap_fill + 5 multiple_choice_single).
   *  Auto-creates-or-extends the linking Module — no separate "Generate Module" call needed. */
  generateActivitiesFromLesson(body: GenerateActivitiesFromLessonRequestBody): Observable<GenerateActivitiesFromLessonResult> {
    return this.http.post<GenerateActivitiesFromLessonResult>(`${this.base}/generate-from-lesson/batch`, body);
  }

  /** Phase K5 — admin edit of an AI/deterministically-generated Exercise. */
  update(id: string, body: UpdateExerciseRequestBody): Observable<ExerciseDto> {
    return this.http.put<ExerciseDto>(`${this.base}/${id}`, body);
  }

  approve(id: string, notes?: string | null): Observable<ExerciseDto> {
    return this.http.post<ExerciseDto>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, reason: string): Observable<ExerciseDto> {
    return this.http.post<ExerciseDto>(`${this.base}/${id}/reject`, { reason });
  }

  archive(ids: string[]): Observable<ExerciseArchiveResult> {
    return this.http.post<ExerciseArchiveResult>(`${this.base}/archive`, { ids });
  }

  unarchive(ids: string[]): Observable<ExerciseArchiveResult> {
    return this.http.post<ExerciseArchiveResult>(`${this.base}/unarchive`, { ids });
  }

  previewSubmit(id: string, body: ExercisePreviewSubmitRequestBody): Observable<ExercisePreviewSubmitResult> {
    return this.http.post<ExercisePreviewSubmitResult>(`${this.base}/${id}/preview/submit`, body);
  }

  diagnose(id: string): Observable<DiagnosticIssue[]> {
    return this.http.get<DiagnosticIssue[]>(`${this.base}/${id}/diagnostics`);
  }

  repair(id: string): Observable<ExerciseRepairResult> {
    return this.http.post<ExerciseRepairResult>(`${this.base}/${id}/repair`, {});
  }

  issuesSummary(): Observable<IssuesSummary> {
    return this.http.get<IssuesSummary>(`${this.base}/issues-summary`);
  }

  repairAll(): Observable<BulkRepairResult> {
    return this.http.post<BulkRepairResult>(`${this.base}/repair-all`, {});
  }

  listWithIssues(): Observable<RepairableItemSummary[]> {
    return this.http.get<RepairableItemSummary[]>(`${this.base}/with-issues`);
  }
}
