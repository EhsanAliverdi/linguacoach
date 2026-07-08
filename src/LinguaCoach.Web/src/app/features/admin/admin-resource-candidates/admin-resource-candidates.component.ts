import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminResourceCandidateService } from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceCandidateDto,
  RESOURCE_CANDIDATE_TYPES,
  RESOURCE_VALIDATION_STATUSES,
  RESOURCE_REVIEW_STATUSES,
} from '../../../core/models/admin-resource-import.models';
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
    this.drawerOpen.set(true);
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
    return [{ id: 'view', label: 'View', icon: 'view', tone: 'default' }];
  }

  onRowAction(actionId: string, item: AdminResourceCandidateDto): void {
    if (actionId === 'view') this.openDrawer(item);
  }
}
