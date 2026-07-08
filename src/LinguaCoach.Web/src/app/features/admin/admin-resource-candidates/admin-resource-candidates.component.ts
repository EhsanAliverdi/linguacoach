import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminResourceCandidateService } from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceCandidateDto,
  ResourceCandidatePreviewDto,
  RESOURCE_CANDIDATE_TYPES,
  RESOURCE_VALIDATION_STATUSES,
  RESOURCE_REVIEW_STATUSES,
} from '../../../core/models/admin-resource-import.models';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminDrawerComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-admin-resource-candidates',
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
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
    FormioRendererComponent,
  ],
  templateUrl: './admin-resource-candidates.component.html',
})
export class AdminResourceCandidatesComponent implements OnInit {
  items = signal<AdminResourceCandidateDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  candidateTypeFilter = signal<string>('all');
  validationStatusFilter = signal<string>('all');
  reviewStatusFilter = signal<string>('all');
  searchQuery = signal('');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  overallTotalCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly candidateTypeOptions = [{ value: 'all', label: 'All types' }, ...RESOURCE_CANDIDATE_TYPES.map(t => ({ value: t, label: t }))];
  readonly validationStatusOptions = [{ value: 'all', label: 'All validation statuses' }, ...RESOURCE_VALIDATION_STATUSES.map(s => ({ value: s, label: s }))];
  readonly reviewStatusOptions = [{ value: 'all', label: 'All review statuses' }, ...RESOURCE_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  selectedCandidate = signal<AdminResourceCandidateDto | null>(null);
  notesDraft = '';
  savingNotes = signal(false);

  // Phase E2 — AI analysis / re-validation trigger state.
  analyzing = signal(false);
  validating = signal(false);
  lastValidationErrors = signal<string[]>([]);
  lastValidationWarnings = signal<string[]>([]);

  // ── Phase E3 — read-only rendered preview drawer ────────────────────────
  previewDrawerOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  preview = signal<ResourceCandidatePreviewDto | null>(null);
  previewRawJsonOpen = signal(false);

  constructor(private svc: AdminResourceCandidateService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list(
      this.page(), this.pageSize, undefined, undefined,
      this.candidateTypeFilter(), this.validationStatusFilter(), this.reviewStatusFilter(),
      undefined, undefined, this.searchQuery(),
    ).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.overallTotalCount.set(result.overallTotalCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load resource candidates.'); },
    });
  }

  onCandidateTypeFilterChange(value: string): void {
    this.candidateTypeFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onValidationStatusFilterChange(value: string): void {
    this.validationStatusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onReviewStatusFilterChange(value: string): void {
    this.reviewStatusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openDrawer(item: AdminResourceCandidateDto): void {
    this.selectedCandidate.set(item);
    this.notesDraft = item.adminNotes ?? '';
    this.actionError.set('');
    this.lastValidationErrors.set(this.parseSummaryList(item.rejectReason, 'errors'));
    this.lastValidationWarnings.set(this.parseSummaryList(item.rejectReason, 'warnings'));
    this.drawerOpen.set(true);
  }

  private parseSummaryList(rejectReasonJson: string | null, key: 'errors' | 'warnings'): string[] {
    if (!rejectReasonJson) return [];
    try {
      const parsed = JSON.parse(rejectReasonJson);
      return Array.isArray(parsed?.[key]) ? parsed[key] : [];
    } catch {
      return [];
    }
  }

  /** Phase E2 — triggers AI analysis + immediate re-validation for the open candidate. */
  analyzeSelected(): void {
    const candidate = this.selectedCandidate();
    if (!candidate) return;
    this.analyzing.set(true);
    this.actionError.set('');
    this.svc.analyze(candidate.candidateId).subscribe({
      next: response => {
        this.analyzing.set(false);
        this.selectedCandidate.set(response.candidate);
        this.lastValidationErrors.set(response.validation.errors);
        this.lastValidationWarnings.set(response.validation.warnings);
        this.actionSuccess.set(
          response.analysis.success ? 'Analysis complete.' : `Analysis could not complete: ${response.analysis.errorMessage}`);
        this.loadAll();
      },
      error: err => { this.analyzing.set(false); this.actionError.set(err.error?.error ?? 'Could not analyze candidate.'); },
    });
  }

  /** Phase E2 — re-runs deterministic rule validation only (no AI call). */
  validateSelected(): void {
    const candidate = this.selectedCandidate();
    if (!candidate) return;
    this.validating.set(true);
    this.actionError.set('');
    this.svc.validate(candidate.candidateId).subscribe({
      next: result => {
        this.validating.set(false);
        this.lastValidationErrors.set(result.errors);
        this.lastValidationWarnings.set(result.warnings);
        this.actionSuccess.set('Validation complete.');
        this.svc.get(candidate.candidateId).subscribe(updated => this.selectedCandidate.set(updated));
        this.loadAll();
      },
      error: err => { this.validating.set(false); this.actionError.set(err.error?.error ?? 'Could not validate candidate.'); },
    });
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  saveNotes(): void {
    const candidate = this.selectedCandidate();
    if (!candidate) return;
    this.savingNotes.set(true);
    this.svc.setNotes(candidate.candidateId, this.notesDraft || null).subscribe({
      next: updated => {
        this.savingNotes.set(false);
        this.selectedCandidate.set(updated);
        this.actionSuccess.set('Notes saved.');
        this.loadAll();
      },
      error: err => { this.savingNotes.set(false); this.actionError.set(err.error?.error ?? 'Could not save notes.'); },
    });
  }

  validationStatusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Passed') return 'success';
    if (status === 'Failed') return 'danger';
    if (status === 'NeedsReview') return 'warning';
    return 'neutral';
  }

  rowActions(_item: AdminResourceCandidateDto): SpAdminRowAction[] {
    return [
      { id: 'view', label: 'View', icon: 'view', tone: 'default' },
      { id: 'preview', label: 'Preview', icon: 'view', tone: 'default' },
      { id: 'analyze', label: 'Analyze', icon: 'sparkles', tone: 'default' },
    ];
  }

  onRowAction(actionId: string, item: AdminResourceCandidateDto): void {
    if (actionId === 'view') this.openDrawer(item);
    if (actionId === 'preview') this.openPreview(item.candidateId);
    if (actionId === 'analyze') {
      this.openDrawer(item);
      this.analyzeSelected();
    }
  }

  /** Phase E3 — opens the read-only rendered preview drawer for one candidate. Read-only: never
   *  mutates the candidate, no approve/reject/publish action exists here (Phase E4). */
  openPreview(candidateId: string): void {
    this.previewError.set('');
    this.previewRawJsonOpen.set(false);
    this.preview.set(null);
    this.previewDrawerOpen.set(true);
    this.previewLoading.set(true);
    this.svc.preview(candidateId).subscribe({
      next: result => { this.previewLoading.set(false); this.preview.set(result); },
      error: err => { this.previewLoading.set(false); this.previewError.set(err.error?.error ?? 'Could not load preview.'); },
    });
  }

  closePreview(): void {
    this.previewDrawerOpen.set(false);
  }

  /** Parses a rendered ActivityTemplateCandidate's Form.io schema JSON string into an object for
   *  app-formio-renderer's `[schema]` input (which expects a parsed object, not a JSON string). */
  parsedFormIoSchema(schemaJson: string | null): unknown {
    if (!schemaJson) return null;
    try {
      return JSON.parse(schemaJson);
    } catch {
      return null;
    }
  }
}
