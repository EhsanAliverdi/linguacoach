import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
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
  ResourceBankVocabularyListResult,
  ResourceBankVocabularyDetailDto,
  ResourceBankGrammarListResult,
  ResourceBankGrammarDetailDto,
  ResourceBankReadingReferenceListResult,
  ResourceBankReadingReferenceDetailDto,
  ResourceBankReadingPassageListResult,
  ResourceBankReadingPassageDetailDto,
  UnifiedResourceBankListResult,
  UnifiedResourceBankItemType,
  ContentImportRequestBody,
  ContentImportResult,
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

  import(sourceId: string, importMode: string, file: File, notes?: string): Observable<ResourceImportResult> {
    const form = new FormData();
    form.append('sourceId', sourceId);
    form.append('importMode', importMode);
    form.append('file', file, file.name);
    if (notes) form.append('notes', notes);
    return this.http.post<ResourceImportResult>(this.base, form);
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
    return this.http.get<AdminResourceCandidateListResult>(this.base, { params });
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
}

/** Phase E5 — read-only browse/search over the published Cefr* bank tables. Browse/search only:
 *  no edit/delete methods exist here — all mutation happens through AdminResourceCandidateService's
 *  approve/reject/publish workflow. */
@Injectable({ providedIn: 'root' })
export class AdminResourceBankService {
  private readonly base = `${environment.apiUrl}/admin/resource-banks`;

  constructor(private http: HttpClient) {}

  private listParams(page: number, pageSize: number, search?: string, cefrLevel?: string, sourceId?: string): HttpParams {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (cefrLevel && cefrLevel !== 'all') params = params.set('cefrLevel', cefrLevel);
    if (sourceId) params = params.set('sourceId', sourceId);
    return params;
  }

  listVocabulary(page: number, pageSize: number, search?: string, cefrLevel?: string, sourceId?: string):
    Observable<ResourceBankVocabularyListResult> {
    return this.http.get<ResourceBankVocabularyListResult>(
      `${this.base}/vocabulary`, { params: this.listParams(page, pageSize, search, cefrLevel, sourceId) });
  }

  getVocabularyDetail(id: string): Observable<ResourceBankVocabularyDetailDto> {
    return this.http.get<ResourceBankVocabularyDetailDto>(`${this.base}/vocabulary/${id}`);
  }

  listGrammar(page: number, pageSize: number, search?: string, cefrLevel?: string, sourceId?: string):
    Observable<ResourceBankGrammarListResult> {
    return this.http.get<ResourceBankGrammarListResult>(
      `${this.base}/grammar`, { params: this.listParams(page, pageSize, search, cefrLevel, sourceId) });
  }

  getGrammarDetail(id: string): Observable<ResourceBankGrammarDetailDto> {
    return this.http.get<ResourceBankGrammarDetailDto>(`${this.base}/grammar/${id}`);
  }

  listReadingReferences(page: number, pageSize: number, search?: string, cefrLevel?: string, sourceId?: string):
    Observable<ResourceBankReadingReferenceListResult> {
    return this.http.get<ResourceBankReadingReferenceListResult>(
      `${this.base}/reading-references`, { params: this.listParams(page, pageSize, search, cefrLevel, sourceId) });
  }

  getReadingReferenceDetail(id: string): Observable<ResourceBankReadingReferenceDetailDto> {
    return this.http.get<ResourceBankReadingReferenceDetailDto>(`${this.base}/reading-references/${id}`);
  }

  listReadingPassages(page: number, pageSize: number, search?: string, cefrLevel?: string, sourceId?: string):
    Observable<ResourceBankReadingPassageListResult> {
    return this.http.get<ResourceBankReadingPassageListResult>(
      `${this.base}/reading-passages`, { params: this.listParams(page, pageSize, search, cefrLevel, sourceId) });
  }

  getReadingPassageDetail(id: string): Observable<ResourceBankReadingPassageDetailDto> {
    return this.http.get<ResourceBankReadingPassageDetailDto>(`${this.base}/reading-passages/${id}`);
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
}
