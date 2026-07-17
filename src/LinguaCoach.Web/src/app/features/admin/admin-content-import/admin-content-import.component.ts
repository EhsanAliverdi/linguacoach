import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
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
  SpAdminTabItem,
  SpAdminTabsComponent,
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
    SpAdminTabsComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-content-import.component.html',
})
export class AdminContentImportComponent implements OnInit {
  readonly pipelineSteps = ['Submit content', 'Review Import Execution Plan', 'Approve → candidates → review'];
  readonly stepperIndex = computed(() => (this.submitting() ? 1 : 0));

  // ── Page tabs (Phase J4B) ────────────────────────────────────────────────
  activeTab = signal<ImportPageTab>('new');
  readonly tabItems: SpAdminTabItem[] = [
    { value: 'new', label: 'New Import' },
    { value: 'history', label: 'Import History' },
  ];

  // ── Unified submission fields (Phase 4.2) ────────────────────────────────
  noteDraft = '';
  pastedText = '';
  selectedFiles: File[] = [];

  // Phase 4.7 bugfix — these were previously Angular computed() signals, but they read plain
  // (non-signal) fields (selectedFiles/pastedText/selectedSourceId are ngModel-bound instance
  // properties, not signals). A computed() with zero signal dependencies memoizes its first
  // result forever and never re-evaluates, so the Submit button silently stayed disabled after
  // the very first render pass in the real (non-unit-test) app — plain methods recompute on every
  // call, matching how they're already invoked in the template (`canSubmit()`, `selectedFilesLabel()`).
  selectedFilesLabel(): string | null {
    return this.selectedFiles.length === 0 ? null
      : this.selectedFiles.length === 1 ? this.selectedFiles[0].name
      : `${this.selectedFiles.length} files selected`;
  }

  canSubmit(): boolean {
    return !!this.selectedSourceId && (this.pastedText.trim().length > 0 || this.selectedFiles.length > 0);
  }

  submitting = signal(false);
  submitStage = signal('Submitting…');
  submitError = signal('');

  // ── Phase 4.7 (2026-07-17 reliable large uploads) — resumable, chunked ZIP upload state.
  // uploadedBytes/totalBytes drive a real byte-level progress bar (not a static stage string);
  // activeUploadSessionId being set is what makes the Cancel button available. ──
  uploadedBytes = signal(0);
  totalUploadBytes = signal(0);
  readonly uploadPercent = computed(() => {
    const total = this.totalUploadBytes();
    return total === 0 ? 0 : Math.min(100, Math.round((this.uploadedBytes() / total) * 100));
  });
  activeUploadSessionId = signal<string | null>(null);
  cancellingUpload = signal(false);

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
      void this.submitZip(this.selectedFiles[0]);
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

  /** Phase 4.7 (2026-07-17 reliable large uploads) — sessionStorage key so a page refresh (or a
   *  manual retry after a failed part) can resume the same upload session instead of restarting
   *  it. Keyed by source+name+size, since re-selecting the same file is what the browser actually
   *  lets a user do after a refresh (there is no way to reattach a File handle automatically). */
  private resumeKey(file: File): string {
    return `import-upload-session:${this.selectedSourceId}:${file.name}:${file.size}`;
  }

  private saveResumeState(file: File, sessionId: string): void {
    sessionStorage.setItem(this.resumeKey(file), sessionId);
  }

  private clearResumeState(file: File): void {
    sessionStorage.removeItem(this.resumeKey(file));
  }

  private async submitZip(file: File): Promise<void> {
    if (file.size > ZIP_MAX_SINGLE_FILE_BYTES) {
      this.submitError.set('This archive exceeds the configured package size limit. Split it into smaller archives.');
      return;
    }

    this.submitting.set(true);
    this.submitError.set('');
    this.totalUploadBytes.set(file.size);
    this.uploadedBytes.set(0);

    try {
      // ── Resume: if a session for this exact source+file+size is already in progress, reuse it
      // and skip parts already uploaded, rather than restarting from byte zero. ──
      let sessionId = sessionStorage.getItem(this.resumeKey(file));
      let partSizeBytes: number;
      let totalPartsExpected: number;
      const alreadyUploaded = new Set<number>();

      if (sessionId) {
        try {
          const status = await firstValueFrom(this.packageSvc.getUploadSessionStatus(sessionId));
          if (status.status === 'Aborted' || status.status === 'Completed') throw new Error('stale-session');
          partSizeBytes = status.partSizeBytes;
          totalPartsExpected = status.totalPartsExpected;
          for (const p of status.uploadedParts) { alreadyUploaded.add(p.partNumber); this.uploadedBytes.update(b => b + p.sizeBytes); }
          this.submitStage.set('Resuming upload…');
        } catch {
          sessionId = null; // stale/expired/aborted — fall through and create a fresh session
        }
      }

      if (!sessionId) {
        this.submitStage.set('Starting upload session…');
        const created = await firstValueFrom(this.packageSvc.createUploadSession(
          this.selectedSourceId, file.name, file.size, null, this.noteDraft.trim() || undefined));
        sessionId = created.sessionId;
        partSizeBytes = created.partSizeBytes;
        totalPartsExpected = created.totalPartsExpected;
        this.saveResumeState(file, sessionId);
        this.submitStage.set('Uploading…');
      }

      this.activeUploadSessionId.set(sessionId);

      for (let partNumber = 1; partNumber <= totalPartsExpected!; partNumber++) {
        if (alreadyUploaded.has(partNumber)) continue;
        if (this.activeUploadSessionId() === null) return; // cancelled mid-loop

        const start = (partNumber - 1) * partSizeBytes!;
        const end = Math.min(start + partSizeBytes!, file.size);
        const chunk = file.slice(start, end);

        let uploadedThisPartSoFar = 0;
        await firstValueFrom(new Observable<void>(observer => {
          const sub = this.packageSvc.uploadSessionPart(sessionId!, partNumber, chunk).subscribe({
            next: event => {
              if (event.type === HttpEventType.UploadProgress && event.total) {
                this.uploadedBytes.update(b => b - uploadedThisPartSoFar + event.loaded);
                uploadedThisPartSoFar = event.loaded;
              } else if (event.type === HttpEventType.Response) {
                observer.next(); observer.complete();
              }
            },
            error: err => observer.error(err),
          });
          return () => sub.unsubscribe();
        }));
      }

      if (this.activeUploadSessionId() === null) return; // cancelled

      this.submitStage.set('Verifying upload…');
      const manifest = await firstValueFrom(this.packageSvc.completeUploadSession(sessionId));
      this.clearResumeState(file);
      this.activeUploadSessionId.set(null);

      if (!manifest.isAccepted) {
        this.submitting.set(false);
        this.submitError.set(manifest.rejectionReason ?? 'Package was rejected during inspection.');
        return;
      }

      this.submitStage.set('Generating plan…');
      try {
        await firstValueFrom(this.packageSvc.generatePlan(manifest.importPackageId));
      } catch (err: any) {
        this.submitError.set(err?.error?.error ?? 'Could not generate a plan automatically — retry from the plan page.');
      }
      this.submitting.set(false);
      this.router.navigate(['/admin/content/import/packages', manifest.importPackageId, 'plan']);
    } catch (err: any) {
      this.submitting.set(false);
      this.activeUploadSessionId.set(null);
      this.submitError.set(
        err?.error?.error ?? 'Upload failed. Fix the connection issue and click Submit again — the upload will resume, not restart.');
    }
  }

  /** Cancels an in-progress chunked upload — aborts the server-side session (deleting any
   *  already-received parts) and clears local resume state, so a subsequent submit starts fresh. */
  cancelUpload(): void {
    const sessionId = this.activeUploadSessionId();
    if (!sessionId) return;
    this.cancellingUpload.set(true);
    this.activeUploadSessionId.set(null);
    this.packageSvc.abortUploadSession(sessionId).subscribe({
      next: () => {
        this.cancellingUpload.set(false);
        this.submitting.set(false);
        if (this.selectedFiles[0]) this.clearResumeState(this.selectedFiles[0]);
        this.submitError.set('Upload cancelled.');
      },
      error: () => {
        this.cancellingUpload.set(false);
        this.submitting.set(false);
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
