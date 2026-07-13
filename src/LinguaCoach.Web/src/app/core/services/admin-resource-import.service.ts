import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  AdminResourceSourceDto,
  AdminResourceSourceListResult,
  ResourceSourceRequest,
  AdminResourceImportRunDto,
  AdminResourceImportRunListResult,
  AdminResourceRawRecordListResult,
  AdminResourceCandidateDto,
  AdminResourceCandidateListResult,
  ResourceImportResult,
  ResourceCandidateAnalyzeResponse,
  ResourceCandidateValidationResult,
  ResourceCandidateBatchAnalysisResult,
  ResourceCandidatePreviewDto,
  ResourceCandidatePublishResult,
  UnifiedResourceBankListResult,
  UnifiedResourceBankItemType,
  ContentImportInputMode,
  ContentImportRequestBody,
  ContentImportResult,
  ResourceCandidateAudioUploadResult,
  ResourceCandidateAudioUrlResult,
  ResourceImportColumnMappingResult,
  AdminResourceCandidateReviewSummaryDto,
  BatchResourceCandidateActionResult,
} from '../models/admin-resource-import.models';

@Injectable({ providedIn: 'root' })
export class AdminResourceSourceService {
  private readonly base = `${environment.apiUrl}/admin/resource-sources`;

  constructor(private http: HttpClient) {}

  list(page: number, pageSize: number, isImportApproved?: boolean | null, languageCode?: string, search?: string):
    Observable<AdminResourceSourceListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (isImportApproved !== undefined && isImportApproved !== null) params = params.set('isImportApproved', isImportApproved);
    if (languageCode) params = params.set('languageCode', languageCode);
    if (search) params = params.set('search', search);
    return this.http.get<AdminResourceSourceListResult>(this.base, { params });
  }

  get(sourceId: string): Observable<AdminResourceSourceDto> {
    return this.http.get<AdminResourceSourceDto>(`${this.base}/${sourceId}`);
  }

  add(request: ResourceSourceRequest): Observable<AdminResourceSourceDto> {
    return this.http.post<AdminResourceSourceDto>(this.base, request);
  }

  update(sourceId: string, request: ResourceSourceRequest): Observable<AdminResourceSourceDto> {
    return this.http.put<AdminResourceSourceDto>(`${this.base}/${sourceId}`, request);
  }

  approve(sourceId: string, reason?: string): Observable<AdminResourceSourceDto> {
    return this.http.post<AdminResourceSourceDto>(`${this.base}/${sourceId}/approve`, { reason });
  }

  revoke(sourceId: string, reason: string): Observable<AdminResourceSourceDto> {
    return this.http.post<AdminResourceSourceDto>(`${this.base}/${sourceId}/revoke`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class AdminResourceImportRunService {
  private readonly base = `${environment.apiUrl}/admin/resource-import-runs`;

  constructor(private http: HttpClient) {}

  list(page: number, pageSize: number, sourceId?: string, status?: string): Observable<AdminResourceImportRunListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (sourceId) params = params.set('sourceId', sourceId);
    if (status && status !== 'all') params = params.set('status', status);
    return this.http.get<AdminResourceImportRunListResult>(this.base, { params });
  }

  get(runId: string): Observable<AdminResourceImportRunDto> {
    return this.http.get<AdminResourceImportRunDto>(`${this.base}/${runId}`);
  }

  import(
    sourceId: string, importMode: string, file: File, notes?: string,
    columnRenames?: Record<string, string> | null,
  ): Observable<ResourceImportResult> {
    const form = new FormData();
    form.append('sourceId', sourceId);
    form.append('importMode', importMode);
    form.append('file', file, file.name);
    if (notes) form.append('notes', notes);
    if (columnRenames && Object.keys(columnRenames).length > 0) form.append('columnRenamesJson', JSON.stringify(columnRenames));
    return this.http.post<ResourceImportResult>(this.base, form);
  }

  /** Phase K1 — AI-assisted column-mapping proposal for an uploaded file. Never stages anything;
   *  the admin reviews/confirms before the mapping is ever sent back via import()'s columnRenames. */
  proposeMapping(importMode: string, file: File): Observable<ResourceImportColumnMappingResult> {
    const form = new FormData();
    form.append('importMode', importMode);
    form.append('file', file, file.name);
    return this.http.post<ResourceImportColumnMappingResult>(`${this.base}/propose-mapping`, form);
  }

  listRawRecords(runId: string, page = 1, pageSize = 50, extractionStatus?: string): Observable<AdminResourceRawRecordListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (extractionStatus && extractionStatus !== 'all') params = params.set('extractionStatus', extractionStatus);
    return this.http.get<AdminResourceRawRecordListResult>(`${this.base}/${runId}/raw-records`, { params });
  }

  /** Phase E2 — bounded-batch AI analysis + re-validation of all not-yet-analyzed candidates
   *  for this run (capped server-side; re-call to sweep the next batch if the cap was hit). */
  analyzePendingCandidates(runId: string): Observable<ResourceCandidateBatchAnalysisResult> {
    return this.http.post<ResourceCandidateBatchAnalysisResult>(`${this.base}/${runId}/candidates/analyze`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class AdminResourceCandidateService {
  private readonly base = `${environment.apiUrl}/admin/resource-candidates`;

  constructor(private http: HttpClient) {}

  list(
    page: number, pageSize: number, sourceId?: string, importRunId?: string, candidateType?: string,
    validationStatus?: string, reviewStatus?: string, languageCode?: string, cefrLevel?: string, search?: string,
    publishableOnly?: boolean,
  ): Observable<AdminResourceCandidateListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (sourceId) params = params.set('sourceId', sourceId);
    if (importRunId) params = params.set('importRunId', importRunId);
    if (candidateType && candidateType !== 'all') params = params.set('candidateType', candidateType);
    if (validationStatus && validationStatus !== 'all') params = params.set('validationStatus', validationStatus);
    if (reviewStatus && reviewStatus !== 'all') params = params.set('reviewStatus', reviewStatus);
    if (languageCode) params = params.set('languageCode', languageCode);
    if (cefrLevel) params = params.set('cefrLevel', cefrLevel);
    if (search) params = params.set('search', search);
    if (publishableOnly) params = params.set('publishableOnly', publishableOnly);
    return this.http.get<AdminResourceCandidateListResult>(this.base, { params });
  }

  /** Phase K2 — headline review-state counts (passed / needs review / blocked / published),
   *  scoped to one import run when provided. */
  summary(importRunId?: string, sourceId?: string): Observable<AdminResourceCandidateReviewSummaryDto> {
    let params = new HttpParams();
    if (importRunId) params = params.set('importRunId', importRunId);
    if (sourceId) params = params.set('sourceId', sourceId);
    return this.http.get<AdminResourceCandidateReviewSummaryDto>(`${this.base}/summary`, { params });
  }

  get(candidateId: string): Observable<AdminResourceCandidateDto> {
    return this.http.get<AdminResourceCandidateDto>(`${this.base}/${candidateId}`);
  }

  setNotes(candidateId: string, adminNotes: string | null): Observable<AdminResourceCandidateDto> {
    return this.http.put<AdminResourceCandidateDto>(`${this.base}/${candidateId}/notes`, { adminNotes });
  }

  /** Phase E2 — AI analysis (advisory) followed immediately by full deterministic re-validation. */
  analyze(candidateId: string): Observable<ResourceCandidateAnalyzeResponse> {
    return this.http.post<ResourceCandidateAnalyzeResponse>(`${this.base}/${candidateId}/analyze`, {});
  }

  /** Phase E2 — re-runs deterministic rule validation only (no AI call). */
  validate(candidateId: string): Observable<ResourceCandidateValidationResult> {
    return this.http.post<ResourceCandidateValidationResult>(`${this.base}/${candidateId}/validate`, {});
  }

  /** Phase E3 — read-only rendered preview. Never mutates the candidate. */
  preview(candidateId: string): Observable<ResourceCandidatePreviewDto> {
    return this.http.get<ResourceCandidatePreviewDto>(`${this.base}/${candidateId}/preview`);
  }

  /** Phase E4 — admin approval step, separate from ValidationStatus. */
  approve(candidateId: string, notes?: string | null): Observable<AdminResourceCandidateDto> {
    return this.http.post<AdminResourceCandidateDto>(`${this.base}/${candidateId}/approve`, { notes: notes ?? null });
  }

  /** Phase E4 — admin rejection. Reason is required. */
  reject(candidateId: string, reason: string): Observable<AdminResourceCandidateDto> {
    return this.http.post<AdminResourceCandidateDto>(`${this.base}/${candidateId}/reject`, { reason });
  }

  /** Phase E4 — publishes an approved, validated candidate into its target Cefr* bank table.
   *  Idempotent; a failed attempt returns 200 with success=false and a list of reasons. */
  publish(candidateId: string): Observable<ResourceCandidatePublishResult> {
    return this.http.post<ResourceCandidatePublishResult>(`${this.base}/${candidateId}/publish`, {});
  }

  /** Phase I1 — the single-click unified pipeline action: approves then immediately publishes.
   *  Same result shape/semantics as publish() — a failed attempt returns 200 with a reasons list. */
  approveAndPublish(candidateId: string, notes?: string | null): Observable<ResourceCandidatePublishResult> {
    return this.http.post<ResourceCandidatePublishResult>(
      `${this.base}/${candidateId}/approve-and-publish`, { notes: notes ?? null });
  }

  /** Phase J5c — uploads the real audio file backing a ListeningPassage candidate. Publish is
   *  blocked server-side until this has run at least once. */
  uploadAudio(candidateId: string, audioFile: File): Observable<ResourceCandidateAudioUploadResult> {
    const form = new FormData();
    form.append('audioFile', audioFile, audioFile.name);
    return this.http.post<ResourceCandidateAudioUploadResult>(`${this.base}/${candidateId}/audio`, form);
  }

  /** Phase J5c — short-lived signed URL (or local-storage streaming endpoint) for playback. */
  getAudioUrl(candidateId: string): Observable<ResourceCandidateAudioUrlResult> {
    return this.http.get<ResourceCandidateAudioUrlResult>(`${this.base}/${candidateId}/audio-url`);
  }

  /** Phase J5c — fetches the local-storage streaming fallback as an authenticated blob and
   *  returns an object URL. A plain `<audio src>` binding can't send a Bearer token, so a signed
   *  MinIO URL is used directly (see AdminResourceCandidateComponent.loadAudioUrl) while this path
   *  is only for the local-storage-endpoint fallback — same pattern as ActivityService.getAudioBlobUrl. */
  getAudioBlobUrl(candidateId: string): Observable<string> {
    return this.http.get(`${this.base}/${candidateId}/audio`, { responseType: 'blob' }).pipe(
      map(blob => URL.createObjectURL(blob)));
  }

  /** Phase K2 — batch admin approval over an explicit set of candidates. */
  batchApprove(candidateIds: string[], notes?: string | null): Observable<BatchResourceCandidateActionResult> {
    return this.http.post<BatchResourceCandidateActionResult>(
      `${this.base}/batch/approve`, { candidateIds, notes: notes ?? null });
  }

  /** Phase K2 — batch publish over an explicit set of already-approved candidates. Already-
   *  published candidates in the request are a safe no-op (see result.alreadyPublishedCount). */
  batchPublish(candidateIds: string[]): Observable<BatchResourceCandidateActionResult> {
    return this.http.post<BatchResourceCandidateActionResult>(`${this.base}/batch/publish`, { candidateIds });
  }

  /** Phase K2 — the batch equivalent of approveAndPublish(): approves then publishes each
   *  candidate, continue-on-error per item — see result.items for per-candidate outcomes. */
  batchApproveAndPublish(candidateIds: string[], notes?: string | null): Observable<BatchResourceCandidateActionResult> {
    return this.http.post<BatchResourceCandidateActionResult>(
      `${this.base}/batch/approve-and-publish`, { candidateIds, notes: notes ?? null });
  }
}

/** Phase H1 — unified read model over all four typed published bank tables above. Read-only; no
 *  edit/delete/generate methods here — Generate Learn/Activity/Module are H3-H5 concerns, not
 *  implemented yet (the page shows them as disabled placeholders). */
@Injectable({ providedIn: 'root' })
export class AdminUnifiedResourceBankService {
  private readonly base = `${environment.apiUrl}/admin/resource-bank`;

  constructor(private http: HttpClient) {}

  list(
    page: number,
    pageSize: number,
    type?: UnifiedResourceBankItemType,
    cefrLevel?: string,
    skill?: string,
    subskill?: string,
    contextTag?: string,
    focusTag?: string,
    difficultyBand?: number,
    search?: string,
    sourceId?: string,
  ): Observable<UnifiedResourceBankListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (type) params = params.set('type', type);
    if (cefrLevel) params = params.set('cefrLevel', cefrLevel);
    if (skill) params = params.set('skill', skill);
    if (subskill) params = params.set('subskill', subskill);
    if (contextTag) params = params.set('contextTag', contextTag);
    if (focusTag) params = params.set('focusTag', focusTag);
    if (difficultyBand !== undefined && difficultyBand !== null) params = params.set('difficultyBand', difficultyBand);
    if (search) params = params.set('search', search);
    if (sourceId) params = params.set('sourceId', sourceId);
    return this.http.get<UnifiedResourceBankListResult>(this.base, { params });
  }
}

/** Phase H2 — Import Content UX v1. Wraps POST /api/admin/content-imports: paste text/CSV/JSON,
 *  choose a broad resource type + default metadata, get back pending Resource Candidates. */
@Injectable({ providedIn: 'root' })
export class AdminContentImportService {
  private readonly base = `${environment.apiUrl}/admin/content-imports`;

  constructor(private http: HttpClient) {}

  import(body: ContentImportRequestBody): Observable<ContentImportResult> {
    return this.http.post<ContentImportResult>(this.base, body);
  }

  /** Phase K1 — AI-assisted column-mapping proposal for pasted CSV/JSON content. Never stages
   *  anything; the admin reviews/confirms before the mapping is ever sent back via import()'s
   *  columnRenames. Not meaningful for 'pasted_text' mode — the backend returns a trivial success
   *  with no suggestions for that case rather than making an AI call. */
  proposeMapping(inputMode: ContentImportInputMode, content: string): Observable<ResourceImportColumnMappingResult> {
    return this.http.post<ResourceImportColumnMappingResult>(`${this.base}/propose-mapping`, { inputMode, content });
  }
}
