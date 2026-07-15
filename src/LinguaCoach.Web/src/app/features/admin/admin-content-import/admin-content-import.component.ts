import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminContentImportService,
  AdminResourceImportRunService,
  AdminResourceSourceService,
} from '../../../core/services/admin-resource-import.service';
import { AdminImportPackageService } from '../../../core/services/admin-import-package.service';
import {
  AdminResourceImportRunDto,
  AdminResourceSourceDto,
  ColumnMappingSuggestion,
  CONTENT_IMPORT_RESOURCE_TYPES,
  ContentImportInputMode,
  ContentImportResourceType,
  RESOURCE_IMPORT_RECOGNIZED_FIELDS,
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
  SpAdminModalComponent,
  SpAdminNativeSelectComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSectionCardComponent,
  SpAdminSegmentedToggleComponent,
  SpAdminStepperComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminSegmentedOption } from '../../../design-system/admin';

type ImportEntryMode = 'paste' | 'file';
type ImportPageTab = 'new' | 'history';

const RECENT_RUNS_PAGE_SIZE = 10;

/**
 * Phase I1 — the unified content pipeline page. Merges what used to be three separate admin
 * pages (Import Content, Resource Import Runs, Resource Candidates) into one workflow: import
 * (paste or file upload), staged-candidate review, and approve-and-publish. See docs/architecture
 * for the Phase I1 pipeline unification note.
 *
 * Phase J4B — restructured around two tabs ("New Import" / "Import History") after admin feedback
 * that mixing recent-run chips above the import form made it unclear where a freshly-imported
 * run's candidates would appear versus a historical run's.
 *
 * Phase K23 — "Add content" restyled around a real sp-admin-stepper, a single unified source
 * picker (inline "+ New source" row, no modal), and an sp-admin-segmented-toggle for
 * upload-vs-paste, matching the design reference in import-content.jsx.
 *
 * Phase 3 (2026-07-15 import candidate review workflow) — the candidate review UI that used to
 * render inline on this page (a near-duplicate of AdminImportRunCandidatesComponent, the
 * dedicated /admin/content/import/runs/:runId page) was removed. The intended workflow is
 * Add content -> System generates candidates -> redirect to Import Review — this page now does
 * exactly the "Add content" half and then navigates straight to the run's own review page the
 * moment candidate generation finishes, instead of duplicating the review/edit/approve/reject/
 * skip/publish workflow a second time on this page. See
 * docs/reviews/2026-07-15-phase-3-import-candidate-review-workflow-review.md.
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
    SpAdminModalComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSectionCardComponent,
    SpAdminSegmentedToggleComponent,
    SpAdminStepperComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-content-import.component.html',
})
export class AdminContentImportComponent implements OnInit {
  readonly resourceTypeOptions = CONTENT_IMPORT_RESOURCE_TYPES.map(t => ({ value: t.value, label: t.label }));

  // ── Phase 3 — "Add content" is now the whole New Import tab; review happens on its own page. ──
  readonly pipelineSteps = ['Add content', 'System generates candidates', 'Review (redirects to Import Review)'];
  readonly stepperIndex = computed(() => (this.submitting() || this.fileSubmitting()) ? 1 : 0);

  // ── Page tabs (Phase J4B) ────────────────────────────────────────────────
  activeTab = signal<ImportPageTab>('new');

  // ── Import entry mode toggle (Phase K23 — sp-admin-segmented-toggle) ────
  entryMode = signal<ImportEntryMode>('file');
  readonly entryModeOptions: SpAdminSegmentedOption[] = [
    { value: 'file', label: 'Upload a file' },
    { value: 'paste', label: 'Type it in' },
  ];

  // ── Unified content fields (Phase K23 — shared by both paste and file-upload submit paths) ──
  resourceType: ContentImportResourceType = 'vocabulary';
  noteDraft = '';
  content = '';

  private detectPasteInputMode(content: string): ContentImportInputMode {
    const trimmed = content.trim();
    if (trimmed.startsWith('[') || trimmed.startsWith('{')) return 'json_text';
    const lines = trimmed.split('\n');
    if (lines.length > 1 && lines[0].includes(',')) return 'csv_text';
    return 'pasted_text';
  }

  private detectFileImportMode(file: File): string {
    const name = file.name.toLowerCase();
    if (name.endsWith('.jsonl')) return 'Jsonl';
    if (name.endsWith('.json')) return 'Json';
    return 'Csv';
  }

  /** Phase J5b — "Mixed" doesn't force one type onto every row, so the field's default
   *  "Applied to every item in this import" hint would be actively wrong for it. */
  get resourceTypeHint(): string {
    return this.resourceType === 'mixed'
      ? 'Each row is classified from its own fields — word/lemma → Vocabulary, grammarKey/explanation → Grammar, passage/text → Reading, prompt → Writing.'
      : 'Applied to every item in this import.';
  }

  submitting = signal(false);
  error = signal('');

  // ── Phase K1 — AI-assisted column-mapping review (always shown for CSV/JSON, both paste and
  // file-upload flows; skipped for 'pasted_text' mode, which has no columns to map) ─────────────
  readonly recognizedFieldOptions = [{ value: '', label: 'Leave as-is' }, ...RESOURCE_IMPORT_RECOGNIZED_FIELDS];
  mappingModalOpen = signal(false);
  mappingLoading = signal(false);
  mappingError = signal('');
  mappingRows = signal<{ column: string; field: string; confidence: number | null }[]>([]);
  private pendingSubmitKind: 'paste' | 'file' | null = null;

  // ── Source (Phase K23 — unified: one registered-source list + inline "+ New source" row) ──────
  sources = signal<AdminResourceSourceDto[]>([]);
  loadingSources = signal(false);
  readonly sourceOptions = computed(() => this.sources().map(s => ({ value: s.sourceId, label: s.name })));

  selectedSourceId = '';
  addingSource = signal(false);
  newSourceName = '';
  creatingSource = signal(false);
  newSourceError = signal('');

  // ── File-upload import (Phase I1) ────────────────────────────────────────
  fileImportMode: string = 'Csv';
  selectedFile: File | null = null;
  fileSubmitting = signal(false);
  fileError = signal('');

  // ── Import History tab ───────────────────────────────────────────────────
  recentRuns = signal<AdminResourceImportRunDto[]>([]);
  loadingRecentRuns = signal(false);
  runsPage = signal(1);
  readonly runsPageSize = RECENT_RUNS_PAGE_SIZE;
  runsTotalCount = signal(0);
  readonly runsTotalPages = computed(() => Math.max(1, Math.ceil(this.runsTotalCount() / this.runsPageSize)));

  // ── Mandatory Import Execution Plan addendum (2026-07-15) — large ZIP package upload ────────
  selectedPackageFile: File | null = null;
  packageUploading = signal(false);
  packageUploadStage = signal('Uploading…');
  packageUploadError = signal('');

  constructor(
    private importSvc: AdminContentImportService,
    private importRunSvc: AdminResourceImportRunService,
    private sourceSvc: AdminResourceSourceService,
    private packageSvc: AdminImportPackageService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.loadSources();
    this.loadRecentRuns();

    // A deep link to a specific run (e.g. from a past session, or an old bookmark) is always a
    // "look up this run's history" intent — send it straight to that run's own review page.
    const importRunId = this.route.snapshot.queryParamMap.get('importRunId');
    if (importRunId) {
      this.router.navigate(['/admin/content/import/runs', importRunId], { replaceUrl: true });
      return;
    }

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

  /** Resolves the selected registered source's own name — needed because the paste-import
   *  backend still takes a `sourceName: string`, not an id (see class doc comment). */
  private selectedSourceName(): string | null {
    return this.sources().find(s => s.sourceId === this.selectedSourceId)?.name ?? null;
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
            // Source was created but auto-approve failed — still usable, just surface a note.
            this.creatingSource.set(false);
            this.addingSource.set(false);
            this.sources.update(items => [created, ...items]);
            this.selectedSourceId = created.sourceId;
            this.fileError.set(err.error?.error ?? 'Source created but could not be auto-approved; approve it before uploading.');
          },
        });
      },
      error: err => {
        this.creatingSource.set(false);
        this.newSourceError.set(err.error?.error ?? 'Could not create source.');
      },
    });
  }

  // ── Paste import ─────────────────────────────────────────────────────────

  private detectedPasteInputMode: ContentImportInputMode = 'pasted_text';

  /** Phase K1 — was submitPaste()'s entire body. Pasted-line mode ('pasted_text') has no columns
   *  to map, so it goes straight through unchanged; CSV/JSON text always routes through the
   *  mapping-review step first (see openMappingReview()), matching file-upload's same treatment.
   *  Phase K23 — the format itself is now auto-detected (detectPasteInputMode) rather than
   *  admin-picked. */
  submitPaste(): void {
    this.error.set('');

    if (!this.selectedSourceName()) {
      this.error.set('A source is required — pick one or create a new one.');
      return;
    }
    if (!this.content.trim()) {
      this.error.set('Content is required.');
      return;
    }

    this.detectedPasteInputMode = this.detectPasteInputMode(this.content);
    if (this.detectedPasteInputMode === 'pasted_text') {
      this.runPasteImport(null);
      return;
    }

    this.pendingSubmitKind = 'paste';
    this.mappingError.set('');
    this.mappingLoading.set(true);
    this.mappingModalOpen.set(true);
    this.importSvc.proposeMapping(this.detectedPasteInputMode, this.content).subscribe({
      next: result => { this.mappingLoading.set(false); this.applyMappingResult(result); },
      error: err => { this.mappingLoading.set(false); this.mappingError.set(err.error?.error ?? 'Could not get an AI mapping suggestion.'); this.mappingRows.set([]); },
    });
  }

  /** Phase 3 — the Import Run finishes and redirects straight to its own Import Review page
   *  (/admin/content/import/runs/:runId) rather than reviewing candidates inline here. */
  private runPasteImport(columnRenames: Record<string, string> | null): void {
    this.submitting.set(true);
    this.importSvc.import({
      sourceName: this.selectedSourceName()!,
      resourceType: this.resourceType,
      inputMode: this.detectedPasteInputMode,
      content: this.content,
      defaultCefrLevel: null,
      defaultSkill: null,
      defaultSubskill: null,
      defaultContextTags: null,
      defaultFocusTags: null,
      defaultDifficultyBand: null,
      notes: this.noteDraft.trim() || null,
      columnRenames,
    }).subscribe({
      next: result => {
        this.submitting.set(false);
        this.router.navigate(['/admin/content/import/runs', result.importRunId]);
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.error ?? 'Import failed.');
      },
    });
  }

  // ── File-upload import ───────────────────────────────────────────────────

  private detectedFileImportMode = 'Csv';

  onFileSelected(file: File | null): void {
    this.selectedFile = file;
  }

  /** Phase K1 — file uploads always route through the mapping-review step first (every file
   *  format this endpoint accepts — Csv/Json/Jsonl — has real column ambiguity to review).
   *  Phase K23 — the format itself is now auto-detected from the file extension
   *  (detectFileImportMode) rather than admin-picked. */
  submitFile(): void {
    this.fileError.set('');

    if (!this.selectedSourceId) {
      this.fileError.set('A source is required — pick one or create a new one.');
      return;
    }
    if (!this.selectedFile) {
      this.fileError.set('A file is required.');
      return;
    }

    this.detectedFileImportMode = this.detectFileImportMode(this.selectedFile);
    this.pendingSubmitKind = 'file';
    this.mappingError.set('');
    this.mappingLoading.set(true);
    this.mappingModalOpen.set(true);
    this.importRunSvc.proposeMapping(this.detectedFileImportMode, this.selectedFile).subscribe({
      next: result => { this.mappingLoading.set(false); this.applyMappingResult(result); },
      error: err => { this.mappingLoading.set(false); this.mappingError.set(err.error?.error ?? 'Could not get an AI mapping suggestion.'); this.mappingRows.set([]); },
    });
  }

  /** Phase 3 — the Import Run finishes and redirects straight to its own Import Review page. */
  private runFileImport(columnRenames: Record<string, string> | null): void {
    this.fileSubmitting.set(true);
    this.importRunSvc.import(
      this.selectedSourceId, this.detectedFileImportMode, this.selectedFile!, this.noteDraft.trim() || undefined, columnRenames,
    ).subscribe({
      next: result => {
        this.fileSubmitting.set(false);
        this.router.navigate(['/admin/content/import/runs', result.runId]);
      },
      error: err => {
        this.fileSubmitting.set(false);
        this.fileError.set(err.error?.error ?? 'File import failed.');
      },
    });
  }

  // ── Mandatory Import Execution Plan addendum (2026-07-15) — large ZIP package upload. Uploads
  // directly to storage via a presigned URL (never through this API's own request-body limits),
  // then always routes to the Import Execution Plan page — no candidates are created and nothing
  // is processed until an administrator explicitly approves that plan. ──────────────────────────

  onPackageFileSelected(file: File | null): void {
    this.selectedPackageFile = file;
    this.packageUploadError.set('');
  }

  uploadPackage(): void {
    this.packageUploadError.set('');
    if (!this.selectedSourceId) {
      this.packageUploadError.set('A source is required — pick one or create a new one.');
      return;
    }
    if (!this.selectedPackageFile) {
      this.packageUploadError.set('A ZIP file is required.');
      return;
    }

    const file = this.selectedPackageFile;
    this.packageUploading.set(true);
    this.packageUploadStage.set('Requesting upload URL…');

    this.packageSvc.requestUpload(this.selectedSourceId, file.name, file.size).subscribe({
      next: uploadResult => {
        this.packageUploadStage.set('Uploading to storage…');
        this.packageSvc.putToStorage(uploadResult.uploadUrl, file).subscribe({
          next: () => {
            this.packageUploadStage.set('Inspecting package…');
            this.packageSvc.confirmUpload(uploadResult.importPackageId).subscribe({
              next: manifest => {
                if (!manifest.isAccepted) {
                  this.packageUploading.set(false);
                  this.packageUploadError.set(manifest.rejectionReason ?? 'Package was rejected during inspection.');
                  return;
                }
                this.packageUploadStage.set('Generating plan…');
                this.packageSvc.generatePlan(uploadResult.importPackageId).subscribe({
                  next: () => {
                    this.packageUploading.set(false);
                    this.router.navigate(['/admin/content/import/packages', uploadResult.importPackageId, 'plan']);
                  },
                  error: err => {
                    this.packageUploading.set(false);
                    // Manifest was accepted but plan generation failed — the admin can retry from the plan page.
                    this.router.navigate(['/admin/content/import/packages', uploadResult.importPackageId, 'plan']);
                    this.packageUploadError.set(err.error?.error ?? 'Could not generate a plan automatically — retry from the plan page.');
                  },
                });
              },
              error: err => {
                this.packageUploading.set(false);
                this.packageUploadError.set(err.error?.error ?? 'Could not confirm the upload.');
              },
            });
          },
          error: () => {
            this.packageUploading.set(false);
            this.packageUploadError.set(
              'Upload to storage failed. If this environment uses local file storage (no MinIO), ' +
              'direct browser upload is not yet supported there — configure MinIO to use large-package upload.');
          },
        });
      },
      error: err => {
        this.packageUploading.set(false);
        this.packageUploadError.set(err.error?.error ?? 'Could not start the upload.');
      },
    });
  }

  // ── Phase K1 — mapping-review modal (shared by both paste-CSV/JSON and file-upload flows) ────

  private applyMappingResult(result: { success: boolean; suggestions: ColumnMappingSuggestion[]; errorMessage: string | null }): void {
    if (!result.success) {
      this.mappingError.set(result.errorMessage ?? 'AI suggestion unavailable — you can still map columns manually below, or import unchanged.');
    }
    this.mappingRows.set(result.suggestions.map(s => ({
      column: s.sourceColumn,
      field: s.suggestedField ?? '',
      confidence: s.confidence,
    })));
  }

  onMappingFieldChange(index: number, field: string): void {
    this.mappingRows.update(rows => rows.map((r, i) => i === index ? { ...r, field } : r));
  }

  confirmMapping(): void {
    const renames: Record<string, string> = {};
    for (const row of this.mappingRows()) {
      if (row.field) renames[row.column] = row.field;
    }
    this.mappingModalOpen.set(false);
    const kind = this.pendingSubmitKind;
    this.pendingSubmitKind = null;
    if (kind === 'paste') this.runPasteImport(Object.keys(renames).length > 0 ? renames : null);
    if (kind === 'file') this.runFileImport(Object.keys(renames).length > 0 ? renames : null);
  }

  cancelMapping(): void {
    this.mappingModalOpen.set(false);
    this.pendingSubmitKind = null;
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
