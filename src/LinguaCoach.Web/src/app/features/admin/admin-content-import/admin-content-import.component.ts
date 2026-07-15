import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminResourceImportRunService, AdminResourceSourceService } from '../../../core/services/admin-resource-import.service';
import { AdminImportPackageService } from '../../../core/services/admin-import-package.service';
import {
  AdminResourceImportRunDto,
  AdminResourceSourceDto,
  ResourceSourceRequest,
} from '../../../core/models/admin-resource-import.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminFileDropzoneComponent,
  SpAdminFormFieldComponent,
  SpAdminIconComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNativeSelectComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSectionCardComponent,
  SpAdminStepperComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

type ImportPageTab = 'new' | 'history';

const RECENT_RUNS_PAGE_SIZE = 10;
const ZIP_MAX_SINGLE_FILE_BYTES = 2_000_000_000;

/**
 * Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — the single
 * unified Import submission workflow. Replaces the two-parallel-workflow page from Phase 4 (a
 * paste/file card that created candidates immediately, alongside a separate ZIP-only plan-gated
 * card) with one form: paste content and/or attach files (including a single .zip for a large
 * package). Every submission creates an ImportPackage and always routes to the plan review page —
 * nothing is ever staged as a candidate directly from this page, and no AI call happens here.
 *
 * A single non-ZIP submission (paste and/or loose files) goes straight to
 * `AdminImportPackageService.submit()`. A single selected `.zip` file instead uses the existing
 * presigned-upload flow (`requestUpload` → PUT to storage → `confirmUpload`), preserving the
 * large-archive path built in Phase 4 unchanged.
 */
@Component({
  selector: 'app-admin-content-import',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminEmptyStateComponent,
    SpAdminFileDropzoneComponent,
    SpAdminFormFieldComponent,
    SpAdminIconComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSectionCardComponent,
    SpAdminStepperComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-content-import.component.html',
})
export class AdminContentImportComponent implements OnInit {
  readonly pipelineSteps = ['Submit content', 'Review Import Execution Plan', 'Approve → candidates → review'];
  readonly stepperIndex = computed(() => (this.submitting() ? 1 : 0));

  // ── Page tabs (Phase J4B) ────────────────────────────────────────────────
  activeTab = signal<ImportPageTab>('new');

  // ── Unified submission fields (Phase 4.2) ────────────────────────────────
  noteDraft = '';
  pastedText = '';
  selectedFiles: File[] = [];

  readonly selectedFilesLabel = computed(() =>
    this.selectedFiles.length === 0 ? null
      : this.selectedFiles.length === 1 ? this.selectedFiles[0].name
      : `${this.selectedFiles.length} files selected`);

  readonly canSubmit = computed(() =>
    !!this.selectedSourceId && (this.pastedText.trim().length > 0 || this.selectedFiles.length > 0));

  submitting = signal(false);
  submitStage = signal('Submitting…');
  submitError = signal('');

  // ── Source ────────────────────────────────────────────────────────────
  sources = signal<AdminResourceSourceDto[]>([]);
  loadingSources = signal(false);
  readonly sourceOptions = computed(() => this.sources().map(s => ({ value: s.sourceId, label: s.name })));

  selectedSourceId = '';
  addingSource = signal(false);
  newSourceName = '';
  creatingSource = signal(false);
  newSourceError = signal('');

  // ── Import History tab ───────────────────────────────────────────────────
  recentRuns = signal<AdminResourceImportRunDto[]>([]);
  loadingRecentRuns = signal(false);
  runsPage = signal(1);
  readonly runsPageSize = RECENT_RUNS_PAGE_SIZE;
  runsTotalCount = signal(0);
  readonly runsTotalPages = computed(() => Math.max(1, Math.ceil(this.runsTotalCount() / this.runsPageSize)));

  constructor(
    private importRunSvc: AdminResourceImportRunService,
    private sourceSvc: AdminResourceSourceService,
    private packageSvc: AdminImportPackageService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.loadSources();
    this.loadRecentRuns();

    if (this.route.snapshot.queryParamMap.get('tab') === 'history') {
      this.activeTab.set('history');
    }
  }

  selectTab(tab: ImportPageTab): void {
    this.activeTab.set(tab);
  }

  // ── Source picker ────────────────────────────────────────────────────────

  loadSources(): void {
    this.loadingSources.set(true);
    this.sourceSvc.list(1, 200).subscribe({
      next: result => {
        this.sources.set(result.items);
        this.loadingSources.set(false);
        if (!this.selectedSourceId && result.items.length > 0) this.selectedSourceId = result.items[0].sourceId;
      },
      error: () => { this.loadingSources.set(false); },
    });
  }

  startAddSource(): void {
    this.newSourceName = '';
    this.newSourceError.set('');
    this.addingSource.set(true);
  }

  cancelAddSource(): void {
    this.addingSource.set(false);
  }

  createSource(): void {
    if (!this.newSourceName.trim()) {
      this.newSourceError.set('Source name is required.');
      return;
    }
    this.creatingSource.set(true);
    this.newSourceError.set('');
    const request: ResourceSourceRequest = {
      name: this.newSourceName.trim(),
      licenseType: 'AdminUpload',
      sourceUrl: null,
      usageRestrictionNotes: null,
      languageCode: 'en',
      allowsStudentDisplay: true,
      allowsCommercialUse: true,
      attributionText: null,
      sourceVersion: null,
      downloadUrl: null,
    };
    this.sourceSvc.add(request).subscribe({
      next: created => {
        this.sourceSvc.approve(created.sourceId).subscribe({
          next: approved => {
            this.creatingSource.set(false);
            this.addingSource.set(false);
            this.sources.update(items => [approved, ...items]);
            this.selectedSourceId = approved.sourceId;
          },
          error: err => {
            this.creatingSource.set(false);
            this.addingSource.set(false);
            this.sources.update(items => [created, ...items]);
            this.selectedSourceId = created.sourceId;
            this.submitError.set(err.error?.error ?? 'Source created but could not be auto-approved; approve it before submitting.');
          },
        });
      },
      error: err => {
        this.creatingSource.set(false);
        this.newSourceError.set(err.error?.error ?? 'Could not create source.');
      },
    });
  }

  // ── File selection ───────────────────────────────────────────────────────

  onFilesSelected(file: File | null): void {
    this.selectedFiles = file ? [file] : [];
    this.submitError.set('');
  }

  private get isZipSubmission(): boolean {
    return this.selectedFiles.length === 1
      && this.pastedText.trim().length === 0
      && this.selectedFiles[0].name.toLowerCase().endsWith('.zip');
  }

  // ── Unified submit (Phase 4.2) — every submission becomes an ImportPackage and always routes
  // to the plan review page. A single .zip goes through the existing presigned-upload flow; any
  // other combination of pasted text and/or loose files goes through the inline submit endpoint. ──

  submit(): void {
    this.submitError.set('');
    if (!this.canSubmit()) {
      this.submitError.set('A source and at least pasted content or one file are required.');
      return;
    }

    if (this.isZipSubmission) {
      this.submitZip(this.selectedFiles[0]);
      return;
    }

    this.submitting.set(true);
    this.submitStage.set('Submitting…');
    this.packageSvc.submit(
      this.selectedSourceId,
      this.pastedText.trim() || null,
      this.selectedFiles,
      this.noteDraft.trim() || undefined,
    ).subscribe({
      next: result => {
        this.submitting.set(false);
        if (!result.isAccepted) {
          this.submitError.set(result.rejectionReason ?? 'Submission was rejected.');
          return;
        }
        this.router.navigate(['/admin/content/import/packages', result.importPackageId, 'plan']);
      },
      error: err => {
        this.submitting.set(false);
        this.submitError.set(err.error?.error ?? 'Submission failed.');
      },
    });
  }

  private submitZip(file: File): void {
    if (file.size > ZIP_MAX_SINGLE_FILE_BYTES) {
      this.submitError.set('This archive exceeds the configured package size limit. Split it into smaller archives.');
      return;
    }

    this.submitting.set(true);
    this.submitStage.set('Requesting upload URL…');

    this.packageSvc.requestUpload(this.selectedSourceId, file.name, file.size, this.noteDraft.trim() || undefined).subscribe({
      next: uploadResult => {
        this.submitStage.set('Uploading to storage…');
        this.packageSvc.putToStorage(uploadResult.uploadUrl, file).subscribe({
          next: () => {
            this.submitStage.set('Inspecting package…');
            this.packageSvc.confirmUpload(uploadResult.importPackageId).subscribe({
              next: manifest => {
                if (!manifest.isAccepted) {
                  this.submitting.set(false);
                  this.submitError.set(manifest.rejectionReason ?? 'Package was rejected during inspection.');
                  return;
                }
                this.submitStage.set('Generating plan…');
                this.packageSvc.generatePlan(uploadResult.importPackageId).subscribe({
                  next: () => {
                    this.submitting.set(false);
                    this.router.navigate(['/admin/content/import/packages', uploadResult.importPackageId, 'plan']);
                  },
                  error: err => {
                    this.submitting.set(false);
                    this.router.navigate(['/admin/content/import/packages', uploadResult.importPackageId, 'plan']);
                    this.submitError.set(err.error?.error ?? 'Could not generate a plan automatically — retry from the plan page.');
                  },
                });
              },
              error: err => {
                this.submitting.set(false);
                this.submitError.set(err.error?.error ?? 'Could not confirm the upload.');
              },
            });
          },
          error: () => {
            this.submitting.set(false);
            this.submitError.set(
              'Upload to storage failed. If this environment uses local file storage (no MinIO), ' +
              'direct browser upload is not yet supported there — configure MinIO to use large-package upload.');
          },
        });
      },
      error: err => {
        this.submitting.set(false);
        this.submitError.set(err.error?.error ?? 'Could not start the upload.');
      },
    });
  }

  // ── Import History tab ───────────────────────────────────────────────────

  loadRecentRuns(): void {
    this.loadingRecentRuns.set(true);
    this.importRunSvc.list(this.runsPage(), this.runsPageSize).subscribe({
      next: result => {
        this.recentRuns.set(result.items);
        this.runsTotalCount.set(result.totalCount);
        this.loadingRecentRuns.set(false);
      },
      error: () => { this.loadingRecentRuns.set(false); },
    });
  }

  onRunsPageChange(page: number): void {
    this.runsPage.set(page);
    this.loadRecentRuns();
  }

  /** Selecting a run from Import History navigates to that run's own Import Review page. */
  selectRun(runId: string): void {
    this.router.navigate(['/admin/content/import/runs', runId]);
  }
}
