import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ImportExecutionGroupInstruction,
  ImportExecutionPlanDto,
  ImportPackageManifestSummaryDto,
  ImportPlanPreviewResult,
  ImportSttOperationSummaryDto,
  RequestImportPackageUploadResult,
} from '../models/admin-import-package.models';

/**
 * Mandatory Import Execution Plan addendum (2026-07-15) — large-scale ZIP package upload via
 * presigned PUT, manifest inspection, automatic plan generation, and the approve/reject/
 * cost-ceiling-resume actions. See AdminImportPackageController.
 */
@Injectable({ providedIn: 'root' })
export class AdminImportPackageService {
  private readonly base = `${environment.apiUrl}/admin/import-packages`;

  constructor(private http: HttpClient) {}

  requestUpload(cefrResourceSourceId: string, originalFileName: string, declaredSizeBytes: number, notes?: string):
    Observable<RequestImportPackageUploadResult> {
    return this.http.post<RequestImportPackageUploadResult>(`${this.base}/upload-request`, {
      cefrResourceSourceId, originalFileName, declaredSizeBytes, notes: notes ?? null,
    });
  }

  /** Phase 4.2 — the canonical entry point for pasted text and/or loose (non-ZIP) files. Creates
   *  the ImportPackage + its assets + an accepted manifest synchronously; never creates a
   *  candidate and never calls AI. */
  submit(cefrResourceSourceId: string, pastedText: string | null, files: File[], notes?: string):
    Observable<ImportPackageManifestSummaryDto> {
    const form = new FormData();
    form.append('cefrResourceSourceId', cefrResourceSourceId);
    if (pastedText) form.append('pastedText', pastedText);
    for (const file of files) form.append('files', file, file.name);
    if (notes) form.append('notes', notes);
    return this.http.post<ImportPackageManifestSummaryDto>(`${this.base}/submit`, form);
  }

  /** Uploads directly to storage via the presigned URL — never through the API, so large
   *  archives never hit Kestrel's request-body size limits. */
  putToStorage(uploadUrl: string, file: File): Observable<unknown> {
    return this.http.put(uploadUrl, file, { headers: { 'Content-Type': 'application/zip' } });
  }

  confirmUpload(packageId: string): Observable<ImportPackageManifestSummaryDto> {
    return this.http.post<ImportPackageManifestSummaryDto>(`${this.base}/${packageId}/confirm-upload`, {});
  }

  getManifest(packageId: string): Observable<ImportPackageManifestSummaryDto> {
    return this.http.get<ImportPackageManifestSummaryDto>(`${this.base}/${packageId}/manifest`);
  }

  /** Automatic — deterministic clustering + a bounded AI review + cost/time estimate. No manual
   *  sample-selection input; the system always does this itself. */
  generatePlan(packageId: string, changeReason?: string): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan`, { changeReason: changeReason ?? null });
  }

  getPlan(packageId: string): Observable<ImportExecutionPlanDto> {
    return this.http.get<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan`);
  }

  /** The one and only "Approve and Start Processing" action — never implicit. Requires the
   *  plan's current concurrencyStamp; the backend rejects a stale one with 409. */
  approvePlan(packageId: string, planId: string, approvedCostCeiling: number, expectedConcurrencyStamp: string): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(
      `${this.base}/${packageId}/plan/${planId}/approve`, { approvedCostCeiling, expectedConcurrencyStamp });
  }

  rejectPlan(packageId: string, planId: string, reason: string): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan/${planId}/reject`, { reason });
  }

  /** Phase 4.4B — the audited, concurrency-checked ceiling amendment (Phase 4.4C removed the
   *  prior unaudited approve-revised-ceiling endpoint this superseded): requires the plan to
   *  actually be paused for cost, the new ceiling to exceed the current one, and a reason. On
   *  success, persists an immutable amendment audit row and resumes the package — never
   *  automatic, never silent. A stale expectedConcurrencyStamp is rejected with 409. */
  amendCostCeiling(
    packageId: string, planId: string, expectedConcurrencyStamp: string, newApprovedCostCeiling: number, reason: string,
  ): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(
      `${this.base}/${packageId}/plan/${planId}/amend-ceiling`,
      { expectedConcurrencyStamp, newApprovedCostCeiling, reason });
  }

  /** Phase 4.4A — saves an edited draft's group instructions. Requires the plan's current
   *  concurrencyStamp; a stale one is rejected with 409 (another admin/process changed it
   *  first). Re-validates and recalculates the estimate server-side. */
  updatePlanDraft(
    packageId: string, planId: string, expectedConcurrencyStamp: string,
    groupInstructions: ImportExecutionGroupInstruction[],
  ): Observable<ImportExecutionPlanDto> {
    return this.http.put<ImportExecutionPlanDto>(
      `${this.base}/${packageId}/plan/${planId}`, { expectedConcurrencyStamp, groupInstructions });
  }

  /** Phase 4.4A — creates a new Draft revision copying an existing (typically Approved) plan's
   *  instructions, for further editing, without mutating the original approved row. */
  revisePlan(packageId: string, planId: string, changeReason: string): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan/${planId}/revise`, { changeReason });
  }

  /** Phase 4.4A — bounded sample preview of the (possibly unsaved) draft's mapping. Zero
   *  persistence, zero AI/STT calls, zero candidate creation. */
  previewPlanDraft(
    packageId: string, groupInstructions: ImportExecutionGroupInstruction[], maxSampleRowsPerGroup?: number,
  ): Observable<ImportPlanPreviewResult> {
    return this.http.post<ImportPlanPreviewResult>(
      `${this.base}/${packageId}/plan/preview`, { groupInstructions, maxSampleRowsPerGroup: maxSampleRowsPerGroup ?? null });
  }

  /** Phase 4.4C — read-only visibility into the durable STT operation ledger for this plan. No
   *  provider credentials, no full transcript text. */
  getSttOperations(packageId: string, planId: string): Observable<ImportSttOperationSummaryDto[]> {
    return this.http.get<ImportSttOperationSummaryDto[]>(`${this.base}/${packageId}/plan/${planId}/stt-operations`);
  }
}
