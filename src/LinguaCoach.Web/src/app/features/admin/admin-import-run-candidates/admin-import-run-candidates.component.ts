import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable } from 'rxjs';
import {
  AdminResourceCandidateService,
  AdminResourceImportRunService,
} from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceCandidateDto,
  AdminResourceImportRunDto,
  AdminResourceCandidateReviewSummaryDto,
  BatchResourceCandidateActionResult,
  ResourceCandidatePreviewDto,
  ResourceCandidatePublishResult,
  UpdateCandidateContentRequestBody,
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

  // ── Phase K2 — review-state summary + batch selection/actions ──────────────
  summary = signal<AdminResourceCandidateReviewSummaryDto | null>(null);
  summaryLoading = signal(false);
  selectedIds = signal<Set<string>>(new Set());
  batchActionRunning = signal(false);
  lastBatchResult = signal<BatchResourceCandidateActionResult | null>(null);

  readonly selectedCount = computed(() => this.selectedIds().size);
  readonly allVisibleSelected = computed(() => {
    const items = this.candidates();
    return items.length > 0 && items.every(c => this.selectedIds().has(c.candidateId));
  });

  // ── Reject modal ─────────────────────────────────────────────────────────
  rejectModalOpen = signal(false);
  rejectTargetId = signal<string | null>(null);
  rejectReasonDraft = '';
  rejecting = signal(false);

  // ── Phase 3 — Skip modal (reason optional, distinct from Reject) ───────────
  skipModalOpen = signal(false);
  skipTargetId = signal<string | null>(null);
  skipReasonDraft = '';
  skipping = signal(false);

  // ── Phase 3 — batch reject modal (reason required, mirrors the row-level one) ──
  batchRejectModalOpen = signal(false);
  batchRejectReasonDraft = '';

  // ── Phase 3 — candidate content editing ─────────────────────────────────────
  editModalOpen = signal(false);
  editTargetId = signal<string | null>(null);
  editSaving = signal(false);
  editError = signal('');
  editDraft: {
    canonicalText: string;
    normalizedJson: string;
    cefrLevel: string;
    primarySkill: string;
    subskill: string;
    difficultyBand: string;
    contextTags: string;
    focusTags: string;
  } = { canonicalText: '', normalizedJson: '', cefrLevel: '', primarySkill: '', subskill: '', difficultyBand: '', contextTags: '', focusTags: '' };

  // ── Preview drawer ───────────────────────────────────────────────────────
  previewDrawerOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  preview = signal<ResourceCandidatePreviewDto | null>(null);
  previewRawJsonOpen = signal(false);
  previewCandidateId = signal<string | null>(null);

  // ── ListeningPassage audio (Phase J5c) ──────────────────────────────────────
  audioUrl = signal<string | null>(null);
  audioLoading = signal(false);
  audioUploading = signal(false);
  audioUploadError = signal('');

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
    this.loadSummary();
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

  loadSummary(): void {
    this.summaryLoading.set(true);
    this.candidateSvc.summary(this.runId).subscribe({
      next: result => { this.summaryLoading.set(false); this.summary.set(result); },
      error: () => { this.summaryLoading.set(false); },
    });
  }

  private refreshAfterAction(): void {
    this.loadCandidates();
    this.loadSummary();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.selectedIds.set(new Set());
    this.loadCandidates();
  }

  // ── Phase K2 — selection ────────────────────────────────────────────────────

  isSelected(candidateId: string): boolean {
    return this.selectedIds().has(candidateId);
  }

  toggleSelected(candidateId: string): void {
    const next = new Set(this.selectedIds());
    if (next.has(candidateId)) next.delete(candidateId); else next.add(candidateId);
    this.selectedIds.set(next);
  }

  toggleSelectAllVisible(): void {
    if (this.allVisibleSelected()) {
      this.selectedIds.set(new Set());
      return;
    }
    this.selectedIds.set(new Set(this.candidates().map(c => c.candidateId)));
  }

  /** Selects only the publishable (Passed/NeedsReview, not-yet-published) rows on this page. */
  selectAllPublishableVisible(): void {
    const ids = this.candidates().filter(c => c.canAttemptPublish && !c.isPublished).map(c => c.candidateId);
    this.selectedIds.set(new Set(ids));
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  // ── Phase K2 — batch actions (operate on the current selection — see the page-scope hint in
  // the template next to the batch toolbar). ──────────────────────────────────

  batchApproveSelected(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.runBatchAction(this.candidateSvc.batchApprove(ids), 'approved');
  }

  batchPublishSelected(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.runBatchAction(this.candidateSvc.batchPublish(ids), 'published');
  }

  /** Phase 4.2 — the backend's combined batch approve-and-publish shortcut was removed; composes
   *  batchApprove() then batchPublish() client-side, same as the single-item action above. */
  batchApproveAndPublishSelected(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.batchActionRunning.set(true);
    this.actionError.set('');
    this.lastBatchResult.set(null);
    this.candidateSvc.batchApprove(ids).subscribe({
      next: () => this.runBatchAction(this.candidateSvc.batchPublish(ids), 'approved & published'),
      error: err => {
        this.batchActionRunning.set(false);
        this.actionError.set(err.error?.error ?? 'Could not approve the selected candidates.');
      },
    });
  }

  /** Phase 3 — "Publish Approved": a separate action from "Approve & Publish selected" — this one
   *  never approves anything, it only attempts to publish candidates already Approved. */
  batchPublishApprovedSelected(): void {
    this.batchPublishSelected();
  }

  batchSkipSelected(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.runBatchAction(this.candidateSvc.batchSkip(ids), 'skipped');
  }

  openBatchReject(): void {
    if (this.selectedCount() === 0) return;
    this.batchRejectReasonDraft = '';
    this.actionError.set('');
    this.batchRejectModalOpen.set(true);
  }

  closeBatchReject(): void {
    this.batchRejectModalOpen.set(false);
  }

  confirmBatchReject(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    if (!this.batchRejectReasonDraft.trim()) {
      this.actionError.set('A reason is required to reject candidates.');
      return;
    }
    this.batchRejectModalOpen.set(false);
    this.runBatchAction(this.candidateSvc.batchReject(ids, this.batchRejectReasonDraft.trim()), 'rejected');
  }

  private runBatchAction(obs: Observable<BatchResourceCandidateActionResult>, verb: string): void {
    this.batchActionRunning.set(true);
    this.actionError.set('');
    this.lastBatchResult.set(null);
    obs.subscribe({
      next: result => {
        this.batchActionRunning.set(false);
        this.lastBatchResult.set(result);
        this.actionSuccess.set(
          `${result.succeededCount + result.alreadyPublishedCount} of ${result.requestedCount} candidate(s) ${verb}` +
          (result.failedCount > 0 ? ` — ${result.failedCount} failed, see details below.` : '.'));
        this.selectedIds.set(new Set());
        this.refreshAfterAction();
      },
      error: err => {
        this.batchActionRunning.set(false);
        this.actionError.set(err.error?.error ?? `Could not ${verb.replace('&', 'and')} the selected candidates.`);
      },
    });
  }

  validationStatusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Passed') return 'success';
    if (status === 'Failed') return 'danger';
    if (status === 'NeedsReview') return 'warning';
    return 'neutral';
  }

  reviewStatusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'Skipped') return 'warning';
    return 'neutral';
  }

  rowActions(item: AdminResourceCandidateDto): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [
      { id: 'preview', label: 'Preview', icon: 'view', tone: 'default' },
      { id: 'analyze', label: 'Analyze', icon: 'sparkles', tone: 'default' },
    ];
    if (!item.isPublished) {
      actions.push({ id: 'edit', label: 'Edit', icon: 'edit', tone: 'default' });
      // Phase K2 — hard-blocked candidates (Failed/Pending ValidationStatus) never show the
      // publish action; the specific blocker (item.publishBlockReason) is shown inline instead.
      if (item.canAttemptPublish) {
        actions.push({ id: 'approve-and-publish', label: 'Approve & Publish', icon: 'check', tone: 'default' });
      }
      if (item.reviewStatus !== 'Rejected') actions.push({ id: 'reject', label: 'Reject', icon: 'delete', tone: 'danger' });
      if (item.reviewStatus !== 'Skipped') actions.push({ id: 'skip', label: 'Skip', icon: 'clock', tone: 'default' });
    }
    return actions;
  }

  onRowAction(actionId: string, item: AdminResourceCandidateDto): void {
    if (actionId === 'preview') this.openPreview(item.candidateId);
    if (actionId === 'analyze') this.analyzeCandidate(item);
    if (actionId === 'approve-and-publish') this.approveAndPublish(item);
    if (actionId === 'reject') this.openReject(item);
    if (actionId === 'skip') this.openSkip(item);
    if (actionId === 'edit') this.openEdit(item);
  }

  // ── Phase 3 — Skip ───────────────────────────────────────────────────────

  openSkip(item: AdminResourceCandidateDto): void {
    this.skipTargetId.set(item.candidateId);
    this.skipReasonDraft = '';
    this.actionError.set('');
    this.skipModalOpen.set(true);
  }

  closeSkip(): void {
    this.skipModalOpen.set(false);
  }

  confirmSkip(): void {
    const id = this.skipTargetId();
    if (!id) return;
    this.skipping.set(true);
    this.candidateSvc.skip(id, this.skipReasonDraft.trim() || null).subscribe({
      next: () => {
        this.skipping.set(false);
        this.skipModalOpen.set(false);
        this.actionSuccess.set('Candidate skipped.');
        this.refreshAfterAction();
      },
      error: err => { this.skipping.set(false); this.actionError.set(err.error?.error ?? 'Could not skip candidate.'); },
    });
  }

  // ── Phase 3 — candidate content editing ─────────────────────────────────────

  openEdit(item: AdminResourceCandidateDto): void {
    this.editTargetId.set(item.candidateId);
    this.editError.set('');
    this.editDraft = {
      canonicalText: item.canonicalText,
      normalizedJson: this.prettyJson(item.normalizedJson),
      cefrLevel: item.cefrLevel ?? '',
      primarySkill: item.primarySkill ?? '',
      subskill: item.subskill ?? '',
      difficultyBand: item.difficultyBand?.toString() ?? '',
      contextTags: this.parseTagsToCsv(item.contextTagsJson),
      focusTags: this.parseTagsToCsv(item.focusTagsJson),
    };
    this.editModalOpen.set(true);
  }

  closeEdit(): void {
    this.editModalOpen.set(false);
  }

  confirmEdit(): void {
    const id = this.editTargetId();
    if (!id) return;

    let normalizedJson: string | null = null;
    const trimmedJson = this.editDraft.normalizedJson.trim();
    if (trimmedJson) {
      try {
        normalizedJson = JSON.stringify(JSON.parse(trimmedJson));
      } catch {
        this.editError.set('Content JSON is not valid JSON — fix it before saving.');
        return;
      }
    }

    const difficultyBand = this.editDraft.difficultyBand.trim()
      ? Number(this.editDraft.difficultyBand.trim())
      : null;
    if (difficultyBand !== null && (!Number.isInteger(difficultyBand) || difficultyBand < 1 || difficultyBand > 5)) {
      this.editError.set('Difficulty band must be a whole number between 1 and 5.');
      return;
    }

    const body: UpdateCandidateContentRequestBody = {
      canonicalText: this.editDraft.canonicalText.trim() || null,
      normalizedJson,
      cefrLevel: this.editDraft.cefrLevel.trim() || null,
      primarySkill: this.editDraft.primarySkill.trim() || null,
      subskill: this.editDraft.subskill.trim() || null,
      difficultyBand,
      contextTags: this.parseCsvToTags(this.editDraft.contextTags),
      focusTags: this.parseCsvToTags(this.editDraft.focusTags),
    };

    this.editSaving.set(true);
    this.editError.set('');
    this.candidateSvc.updateContent(id, body).subscribe({
      next: () => {
        this.editSaving.set(false);
        this.editModalOpen.set(false);
        this.actionSuccess.set('Candidate content updated.');
        this.refreshAfterAction();
      },
      error: err => { this.editSaving.set(false); this.editError.set(err.error?.error ?? 'Could not save candidate content.'); },
    });
  }

  private prettyJson(json: string): string {
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }

  private parseTagsToCsv(tagsJson: string | null): string {
    if (!tagsJson) return '';
    try {
      const tags = JSON.parse(tagsJson);
      return Array.isArray(tags) ? tags.join(', ') : '';
    } catch {
      return '';
    }
  }

  private parseCsvToTags(csv: string): string[] | null {
    const trimmed = csv.trim();
    if (!trimmed) return null;
    return trimmed.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  analyzeCandidate(item: AdminResourceCandidateDto): void {
    this.analyzingId.set(item.candidateId);
    this.actionError.set('');
    this.candidateSvc.analyze(item.candidateId).subscribe({
      next: response => {
        this.analyzingId.set(null);
        this.actionSuccess.set(
          response.analysis.success
            ? `Analysis complete — validation: ${response.validation.status}.`
            : `Analysis could not complete: ${response.analysis.errorMessage}`);
        this.refreshAfterAction();
      },
      error: err => { this.analyzingId.set(null); this.actionError.set(err.error?.error ?? 'Could not analyze candidate.'); },
    });
  }

  /** Phase 4.2 — the backend's combined approve-and-publish shortcut was removed (it bypassed the
   *  separate Phase 3 review/publish lifecycle at the API level); this composes the two remaining,
   *  still-separate approve() and publish() calls client-side so the admin keeps a single click. */
  approveAndPublish(item: AdminResourceCandidateDto): void {
    this.publishingId.set(item.candidateId);
    this.actionError.set('');
    this.lastPublishResult.set(null);
    this.candidateSvc.approve(item.candidateId).subscribe({
      next: () => {
        this.candidateSvc.publish(item.candidateId).subscribe({
          next: result => {
            this.publishingId.set(null);
            this.lastPublishResult.set(result);
            if (result.success) {
              this.actionSuccess.set(`Published as ${result.publishedEntityType}.`);
              this.refreshAfterAction();
            } else {
              this.actionError.set(`Could not publish "${item.canonicalText}": ${result.errors.join('; ')}`);
            }
          },
          error: err => { this.publishingId.set(null); this.actionError.set(err.error?.error ?? 'Could not publish candidate.'); },
        });
      },
      error: err => { this.publishingId.set(null); this.actionError.set(err.error?.error ?? 'Could not approve candidate.'); },
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
        this.refreshAfterAction();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject candidate.'); },
    });
  }

  openPreview(candidateId: string): void {
    this.previewError.set('');
    this.previewRawJsonOpen.set(false);
    this.preview.set(null);
    this.previewCandidateId.set(candidateId);
    this.previewDrawerOpen.set(true);
    this.previewLoading.set(true);
    this.audioUrl.set(null);
    this.audioUploadError.set('');
    this.candidateSvc.preview(candidateId).subscribe({
      next: result => {
        this.previewLoading.set(false);
        this.preview.set(result);
        if (result.renderedPreviewModel.kind === 'ListeningPassage' && result.renderedPreviewModel.hasAudio) {
          this.loadAudioUrl(candidateId);
        }
      },
      error: err => { this.previewLoading.set(false); this.previewError.set(err.error?.error ?? 'Could not load preview.'); },
    });
  }

  closePreview(): void {
    this.previewDrawerOpen.set(false);
  }

  // ── ListeningPassage audio (Phase J5c) ──────────────────────────────────────

  /** A signed MinIO URL (absolute http(s)) can be bound directly to `<audio src>`. The local-
   *  storage streaming fallback is a same-origin `/api/...` path behind admin auth — a plain
   *  `<audio src>` can't send a Bearer token, so that case is fetched as an authenticated blob
   *  instead (see AdminResourceCandidateService.getAudioBlobUrl). */
  loadAudioUrl(candidateId: string): void {
    this.audioLoading.set(true);
    this.candidateSvc.getAudioUrl(candidateId).subscribe({
      next: r => {
        if (/^https?:\/\//i.test(r.url)) {
          this.audioLoading.set(false);
          this.audioUrl.set(r.url);
          return;
        }
        this.candidateSvc.getAudioBlobUrl(candidateId).subscribe({
          next: blobUrl => { this.audioLoading.set(false); this.audioUrl.set(blobUrl); },
          error: () => { this.audioLoading.set(false); this.audioUrl.set(null); },
        });
      },
      error: () => { this.audioLoading.set(false); this.audioUrl.set(null); },
    });
  }

  onAudioFileSelected(event: Event): void {
    const candidateId = this.previewCandidateId();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!candidateId || !file) return;

    this.audioUploading.set(true);
    this.audioUploadError.set('');
    this.candidateSvc.uploadAudio(candidateId, file).subscribe({
      next: () => {
        this.audioUploading.set(false);
        this.loadAudioUrl(candidateId);
        this.openPreview(candidateId);
        this.loadCandidates();
      },
      error: err => { this.audioUploading.set(false); this.audioUploadError.set(err.error?.error ?? 'Could not upload audio.'); },
    });
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
