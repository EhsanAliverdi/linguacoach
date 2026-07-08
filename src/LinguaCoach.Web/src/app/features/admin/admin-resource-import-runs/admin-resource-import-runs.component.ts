import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminResourceImportRunService,
  AdminResourceSourceService,
} from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceImportRunDto,
  AdminResourceRawRecordDto,
  AdminResourceSourceDto,
  RESOURCE_IMPORT_MODES,
  RESOURCE_IMPORT_RUN_STATUSES,
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
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminNativeSelectComponent,
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
  selector: 'app-admin-resource-import-runs',
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
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-resource-import-runs.component.html',
})
export class AdminResourceImportRunsComponent implements OnInit {
  items = signal<AdminResourceImportRunDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  statusFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  overallTotalCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly statusOptions = [{ value: 'all', label: 'All statuses' }, ...RESOURCE_IMPORT_RUN_STATUSES.map(s => ({ value: s, label: s }))];
  readonly importModeOptions = RESOURCE_IMPORT_MODES.map(m => ({ value: m, label: m }));
  readonly rawRecordStatusOptions = [
    { value: 'all', label: 'All statuses' },
    { value: 'Imported', label: 'Imported' },
    { value: 'Parsed', label: 'Parsed' },
    { value: 'Rejected', label: 'Rejected' },
  ];

  readonly sourceOptions = computed(() =>
    this.approvedSources().map(s => ({ value: s.sourceId, label: s.name })));

  // ── Upload form ──────────────────────────────────────────────────────────
  uploadOpen = signal(false);
  approvedSources = signal<AdminResourceSourceDto[]>([]);
  selectedSourceId = '';
  selectedImportMode: string = 'Csv';
  selectedFile: File | null = null;
  uploadNotes = '';
  uploading = signal(false);

  // ── Raw record drawer ────────────────────────────────────────────────────
  drawerOpen = signal(false);
  selectedRun = signal<AdminResourceImportRunDto | null>(null);
  rawRecords = signal<AdminResourceRawRecordDto[]>([]);
  rawRecordsLoading = signal(false);
  rawRecordsFilter = signal<string>('all');

  // Phase E2 — batch AI analysis trigger state (per run row).
  analyzingRunId = signal<string | null>(null);

  constructor(
    private runSvc: AdminResourceImportRunService,
    private sourceSvc: AdminResourceSourceService,
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.runSvc.list(this.page(), this.pageSize, undefined, this.statusFilter()).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.overallTotalCount.set(result.overallTotalCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load import runs.'); },
    });
  }

  onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openUploadForm(): void {
    this.selectedSourceId = '';
    this.selectedImportMode = 'Csv';
    this.selectedFile = null;
    this.uploadNotes = '';
    this.actionError.set('');
    this.uploadOpen.set(true);
    this.sourceSvc.list(1, 200, true).subscribe({
      next: result => this.approvedSources.set(result.items),
      error: () => this.approvedSources.set([]),
    });
  }

  closeUploadForm(): void {
    this.uploadOpen.set(false);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length > 0 ? input.files[0] : null;
  }

  submitUpload(): void {
    if (!this.selectedSourceId || !this.selectedFile) {
      this.actionError.set('Choose an approved source and a file.');
      return;
    }
    this.uploading.set(true);
    this.actionError.set('');
    this.runSvc.import(this.selectedSourceId, this.selectedImportMode, this.selectedFile, this.uploadNotes || undefined).subscribe({
      next: result => {
        this.uploading.set(false);
        this.uploadOpen.set(false);
        this.actionSuccess.set(
          `Import ${result.status}: ${result.succeededCount} staged, ${result.rejectedCount} rejected of ${result.totalRecordCount}.`);
        this.loadAll();
      },
      error: err => { this.uploading.set(false); this.actionError.set(err.error?.error ?? 'Import failed.'); },
    });
  }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Completed') return 'success';
    if (status === 'Failed' || status === 'Cancelled') return 'danger';
    if (status === 'CompletedWithWarnings') return 'warning';
    return 'neutral';
  }

  rowActions(_item: AdminResourceImportRunDto): SpAdminRowAction[] {
    return [
      { id: 'view', label: 'View raw records', icon: 'view', tone: 'default' },
      { id: 'analyze', label: 'Analyze pending candidates', icon: 'sparkles', tone: 'default' },
    ];
  }

  onRowAction(actionId: string, item: AdminResourceImportRunDto): void {
    if (actionId === 'view') this.openDrawer(item);
    if (actionId === 'analyze') this.analyzePendingCandidates(item);
  }

  /** Phase E2 — bounded-batch AI analysis + re-validation of not-yet-analyzed candidates for
   *  this run (server-side cap of 50 per call; re-trigger to sweep the next batch). */
  analyzePendingCandidates(item: AdminResourceImportRunDto): void {
    this.analyzingRunId.set(item.runId);
    this.actionError.set('');
    this.runSvc.analyzePendingCandidates(item.runId).subscribe({
      next: result => {
        this.analyzingRunId.set(null);
        this.actionSuccess.set(
          `Analyzed ${result.candidatesAnalyzed} of ${result.candidatesConsidered} pending candidate(s): `
          + `${result.succeededCount} succeeded, ${result.failedCount} failed.`
          + (result.batchLimitReached ? ' Batch limit reached — run again to continue.' : ''));
      },
      error: err => { this.analyzingRunId.set(null); this.actionError.set(err.error?.error ?? 'Could not analyze pending candidates.'); },
    });
  }

  openDrawer(item: AdminResourceImportRunDto): void {
    this.selectedRun.set(item);
    this.rawRecordsFilter.set('all');
    this.drawerOpen.set(true);
    this.loadRawRecords(item.runId);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  loadRawRecords(runId: string): void {
    this.rawRecordsLoading.set(true);
    const status = this.rawRecordsFilter() === 'all' ? undefined : this.rawRecordsFilter();
    this.runSvc.listRawRecords(runId, 1, 100, status).subscribe({
      next: result => { this.rawRecords.set(result.items); this.rawRecordsLoading.set(false); },
      error: () => { this.rawRecords.set([]); this.rawRecordsLoading.set(false); },
    });
  }

  onRawRecordsFilterChange(value: string): void {
    this.rawRecordsFilter.set(value);
    const run = this.selectedRun();
    if (run) this.loadRawRecords(run.runId);
  }

  extractionStatusTone(status: string): 'success' | 'neutral' | 'danger' {
    if (status === 'Parsed') return 'success';
    if (status === 'Rejected') return 'danger';
    return 'neutral';
  }
}
