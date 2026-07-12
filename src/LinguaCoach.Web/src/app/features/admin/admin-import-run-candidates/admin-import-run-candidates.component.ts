import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminResourceCandidateService,
  AdminResourceImportRunService,
} from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceCandidateDto,
  AdminResourceImportRunDto,
  ResourceCandidatePreviewDto,
  ResourceCandidatePublishResult,
} from '../../../core/models/admin-resource-import.models';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminDrawerComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSectionCardComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';
import { FormsModule } from '@angular/forms';

const CANDIDATES_PAGE_SIZE = 50;

/**
 * Phase J4B (follow-up) — "Candidates for selected run" moved out of Import History into its own
 * page, reached via /admin/content/import/runs/:runId. Previously this rendered inline below the
 * runs table on the same page; per admin feedback, selecting a run from a list should navigate to
 * a distinct page rather than expanding content in place.
 */
@Component({
  selector: 'app-admin-import-run-candidates',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminDrawerComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSectionCardComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
    FormioRendererComponent,
  ],
  templateUrl: './admin-import-run-candidates.component.html',
})
export class AdminImportRunCandidatesComponent implements OnInit {
  runId = '';
  run = signal<AdminResourceImportRunDto | null>(null);
  runLoading = signal(false);
  runError = signal('');

  page = signal(1);
  readonly pageSize = CANDIDATES_PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  candidates = signal<AdminResourceCandidateDto[]>([]);
  candidatesLoading = signal(false);
  candidatesError = signal('');

  actionError = signal('');
  actionSuccess = signal('');
  analyzingId = signal<string | null>(null);
  publishingId = signal<string | null>(null);
  lastPublishResult = signal<ResourceCandidatePublishResult | null>(null);

  // ── Reject modal ─────────────────────────────────────────────────────────
  rejectModalOpen = signal(false);
  rejectTargetId = signal<string | null>(null);
  rejectReasonDraft = '';
  rejecting = signal(false);

  // ── Preview drawer ───────────────────────────────────────────────────────
  previewDrawerOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  preview = signal<ResourceCandidatePreviewDto | null>(null);
  previewRawJsonOpen = signal(false);

  constructor(
    private importRunSvc: AdminResourceImportRunService,
    private candidateSvc: AdminResourceCandidateService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.runId = this.route.snapshot.paramMap.get('runId') ?? '';
    if (!this.runId) return;
    this.loadRun();
    this.loadCandidates();
  }

  backToHistory(): void {
    this.router.navigate(['/admin/content/import'], { queryParams: { tab: 'history' } });
  }

  loadRun(): void {
    this.runLoading.set(true);
    this.runError.set('');
    this.importRunSvc.get(this.runId).subscribe({
      next: run => { this.run.set(run); this.runLoading.set(false); },
      error: err => { this.runLoading.set(false); this.runError.set(err.error?.error ?? 'Could not load this import run.'); },
    });
  }

  loadCandidates(): void {
    this.candidatesLoading.set(true);
    this.candidatesError.set('');
    this.candidateSvc.list(this.page(), this.pageSize, undefined, this.runId).subscribe({
      next: result => {
        this.candidates.set(result.items);
        this.totalCount.set(result.totalCount);
        this.candidatesLoading.set(false);
      },
      error: err => {
        this.candidatesLoading.set(false);
        this.candidatesError.set(err.error?.error ?? 'Could not load candidates for this import run.');
      },
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadCandidates();
  }

  validationStatusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Passed') return 'success';
    if (status === 'Failed') return 'danger';
    if (status === 'NeedsReview') return 'warning';
    return 'neutral';
  }

  rowActions(item: AdminResourceCandidateDto): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [
      { id: 'preview', label: 'Preview', icon: 'view', tone: 'default' },
      { id: 'analyze', label: 'Analyze', icon: 'sparkles', tone: 'default' },
    ];
    if (!item.isPublished) {
      actions.push({ id: 'approve-and-publish', label: 'Approve & Publish', icon: 'check', tone: 'default' });
      if (item.reviewStatus !== 'Rejected') actions.push({ id: 'reject', label: 'Reject', icon: 'delete', tone: 'danger' });
    }
    return actions;
  }

  onRowAction(actionId: string, item: AdminResourceCandidateDto): void {
    if (actionId === 'preview') this.openPreview(item.candidateId);
    if (actionId === 'analyze') this.analyzeCandidate(item);
    if (actionId === 'approve-and-publish') this.approveAndPublish(item);
    if (actionId === 'reject') this.openReject(item);
  }

  analyzeCandidate(item: AdminResourceCandidateDto): void {
    this.analyzingId.set(item.candidateId);
    this.actionError.set('');
    this.candidateSvc.analyze(item.candidateId).subscribe({
      next: response => {
        this.analyzingId.set(null);
        this.actionSuccess.set(
          response.analysis.success ? 'Analysis complete.' : `Analysis could not complete: ${response.analysis.errorMessage}`);
        this.loadCandidates();
      },
      error: err => { this.analyzingId.set(null); this.actionError.set(err.error?.error ?? 'Could not analyze candidate.'); },
    });
  }

  approveAndPublish(item: AdminResourceCandidateDto): void {
    this.publishingId.set(item.candidateId);
    this.actionError.set('');
    this.lastPublishResult.set(null);
    this.candidateSvc.approveAndPublish(item.candidateId).subscribe({
      next: result => {
        this.publishingId.set(null);
        this.lastPublishResult.set(result);
        if (result.success) {
          this.actionSuccess.set(`Published as ${result.publishedEntityType}.`);
          this.loadCandidates();
        } else {
          this.actionError.set(`Could not publish "${item.canonicalText}": ${result.errors.join('; ')}`);
        }
      },
      error: err => { this.publishingId.set(null); this.actionError.set(err.error?.error ?? 'Could not approve/publish candidate.'); },
    });
  }

  openReject(item: AdminResourceCandidateDto): void {
    this.rejectTargetId.set(item.candidateId);
    this.rejectReasonDraft = '';
    this.actionError.set('');
    this.rejectModalOpen.set(true);
  }

  closeReject(): void {
    this.rejectModalOpen.set(false);
  }

  confirmReject(): void {
    const id = this.rejectTargetId();
    if (!id) return;
    if (!this.rejectReasonDraft.trim()) {
      this.actionError.set('A reason is required to reject a candidate.');
      return;
    }
    this.rejecting.set(true);
    this.candidateSvc.reject(id, this.rejectReasonDraft.trim()).subscribe({
      next: () => {
        this.rejecting.set(false);
        this.rejectModalOpen.set(false);
        this.actionSuccess.set('Candidate rejected.');
        this.loadCandidates();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject candidate.'); },
    });
  }

  openPreview(candidateId: string): void {
    this.previewError.set('');
    this.previewRawJsonOpen.set(false);
    this.preview.set(null);
    this.previewDrawerOpen.set(true);
    this.previewLoading.set(true);
    this.candidateSvc.preview(candidateId).subscribe({
      next: result => { this.previewLoading.set(false); this.preview.set(result); },
      error: err => { this.previewLoading.set(false); this.previewError.set(err.error?.error ?? 'Could not load preview.'); },
    });
  }

  closePreview(): void {
    this.previewDrawerOpen.set(false);
  }

  parsedFormIoSchema(schemaJson: string | null): unknown {
    if (!schemaJson) return null;
    try {
      return JSON.parse(schemaJson);
    } catch {
      return null;
    }
  }
}
