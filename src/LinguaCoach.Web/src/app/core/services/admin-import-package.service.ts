import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ImportExecutionPlanDto,
  ImportPackageManifestSummaryDto,
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

  /** The one and only "Approve and Start Processing" action — never implicit. */
  approvePlan(packageId: string, planId: string, approvedCostCeiling: number): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan/${planId}/approve`, { approvedCostCeiling });
  }

  rejectPlan(packageId: string, planId: string, reason: string): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(`${this.base}/${packageId}/plan/${planId}/reject`, { reason });
  }

  approveRevisedCeiling(packageId: string, planId: string, newApprovedCostCeiling: number): Observable<ImportExecutionPlanDto> {
    return this.http.post<ImportExecutionPlanDto>(
      `${this.base}/${packageId}/plan/${planId}/approve-revised-ceiling`, { approvedCostCeiling: newApprovedCostCeiling });
  }
}
