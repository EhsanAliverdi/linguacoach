import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminContentImportService,
  AdminResourceCandidateService,
  AdminResourceImportRunService,
  AdminResourceSourceService,
} from '../../../core/services/admin-resource-import.service';
import {
  AdminResourceCandidateDto,
  AdminResourceImportRunDto,
  AdminResourceSourceDto,
  CONTENT_IMPORT_COMING_SOON_TYPES,
  CONTENT_IMPORT_INPUT_MODES,
  CONTENT_IMPORT_RESOURCE_TYPES,
  ContentImportInputMode,
  ContentImportResourceType,
  ContentImportResult,
  RESOURCE_BANK_CEFR_LEVELS,
  RESOURCE_IMPORT_MODES,
  ResourceCandidatePreviewDto,
  ResourceCandidatePublishResult,
  ResourceImportResult,
  ResourceSourceRequest,
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
  SpAdminFormGridComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminNativeSelectComponent,
  SpAdminNumberInputComponent,
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

type ImportEntryMode = 'paste' | 'file';
type ImportPageTab = 'new' | 'history';

const CANDIDATES_PAGE_SIZE = 50;
const RECENT_RUNS_PAGE_SIZE = 10;

/**
 * Phase I1 — the unified content pipeline page. Merges what used to be three separate admin
 * pages (Import Content, Resource Import Runs, Resource Candidates) into one workflow: import
 * (paste or file upload), staged-candidate review, and approve-and-publish. See docs/architecture
 * for the Phase I1 pipeline unification note.
 *
 * Phase J4B — restructured around two tabs ("New Import" / "Import History") after admin feedback
 * that mixing recent-run chips above the import form made it unclear where a freshly-imported
 * run's candidates would appear versus a historical run's. "New Import" now reads as one linear
 * flow (add content -> staged candidates -> review -> approve & publish); "Import History" is a
 * separate browse/review surface for past runs. No backend behavior changed.
 *
 * Phase J4B (follow-up) — tabs now use the shared sp-admin-tab-bar pattern (see admin-ai-config
 * for the reference usage) instead of a bespoke button pair; selecting a run in Import History
 * navigates to its own page (/admin/content/import/runs/:runId) instead of expanding inline; both
 * the runs table and the run-candidates table are frontend+backend paginated.
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
    SpAdminDrawerComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminNativeSelectComponent,
    SpAdminNumberInputComponent,
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
  templateUrl: './admin-content-import.component.html',
})
export class AdminContentImportComponent implements OnInit {
  readonly resourceTypeOptions = CONTENT_IMPORT_RESOURCE_TYPES.map(t => ({ value: t.value, label: t.label }));
  readonly inputModeOptions = CONTENT_IMPORT_INPUT_MODES.map(m => ({ value: m.value, label: m.label }));
  readonly cefrOptions = [{ value: '', label: 'No default' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];
  readonly comingSoonTypes = CONTENT_IMPORT_COMING_SOON_TYPES;
  readonly fileImportModeOptions = RESOURCE_IMPORT_MODES.map(m => ({ value: m, label: m }));

  // ── Page tabs (Phase J4B) ────────────────────────────────────────────────
  activeTab = signal<ImportPageTab>('new');
  advancedDefaultsOpen = signal(false);

  // ── Import entry mode toggle ────────────────────────────────────────────
  entryMode = signal<ImportEntryMode>('paste');

  // ── Paste-based import (existing Phase H2 form) ─────────────────────────
  sourceName = '';
  notes = '';
  resourceType: ContentImportResourceType = 'vocabulary';
  defaultCefrLevel = '';
  defaultSkill = '';
  defaultSubskill = '';
  defaultContextTags = '';
  defaultFocusTags = '';
  defaultDifficultyBand: number | null = null;
  inputMode: ContentImportInputMode = 'pasted_text';
  content = '';

  get inputHint(): string {
    return CONTENT_IMPORT_INPUT_MODES.find(m => m.value === this.inputMode)?.hint ?? '';
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
  result = signal<ContentImportResult | null>(null);

  // ── File-upload import (Phase I1 — new) ─────────────────────────────────
  sources = signal<AdminResourceSourceDto[]>([]);
  loadingSources = signal(false);
  readonly sourceOptions = computed(() => this.sources().map(s => ({ value: s.sourceId, label: s.name })));

  fileSourceId = '';
  fileImportMode: string = 'Csv';
  fileNotes = '';
  selectedFile: File | null = null;
  fileSubmitting = signal(false);
  fileError = signal('');
  fileResult = signal<ResourceImportResult | null>(null);

  newSourceModalOpen = signal(false);
  newSourceName = '';
  creatingSource = signal(false);
  newSourceError = signal('');

  // ── Pipeline/review section (New Import tab only — Phase J4B follow-up moved the Import
  // History "candidates for selected run" view to its own page) ──────────────────────────
  currentRunId = signal<string | null>(null);
  readonly showPipeline = computed(() => this.currentRunId() !== null);

  recentRuns = signal<AdminResourceImportRunDto[]>([]);
  loadingRecentRuns = signal(false);
  runsPage = signal(1);
  readonly runsPageSize = RECENT_RUNS_PAGE_SIZE;
  runsTotalCount = signal(0);
  readonly runsTotalPages = computed(() => Math.max(1, Math.ceil(this.runsTotalCount() / this.runsPageSize)));

  candidates = signal<AdminResourceCandidateDto[]>([]);
  candidatesLoading = signal(false);
  candidatesError = signal('');
  candidatesTotal = signal(0);
  candidatesPage = signal(1);
  readonly candidatesPageSize = CANDIDATES_PAGE_SIZE;
  readonly candidatesTotalPages = computed(() => Math.max(1, Math.ceil(this.candidatesTotal() / this.candidatesPageSize)));

  actionError = signal('');
  actionSuccess = signal('');
  analyzingId = signal<string | null>(null);
  publishingId = signal<string | null>(null);

  // ── Reject modal ─────────────────────────────────────────────────────────
  rejectModalOpen = signal(false);
  rejectTargetId = signal<string | null>(null);
  rejectReasonDraft = '';
  rejecting = signal(false);

  // ── Preview drawer (ported from AdminResourceCandidatesComponent, Phase E3) ─
  previewDrawerOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  preview = signal<ResourceCandidatePreviewDto | null>(null);
  previewRawJsonOpen = signal(false);

  lastPublishResult = signal<ResourceCandidatePublishResult | null>(null);

  constructor(
    private importSvc: AdminContentImportService,
    private importRunSvc: AdminResourceImportRunService,
    private candidateSvc: AdminResourceCandidateService,
    private sourceSvc: AdminResourceSourceService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.loadSources();
    this.loadRecentRuns();

    // A deep link to a specific run (e.g. from a past session, or an old bookmark) is always a
    // "look up this run's history" intent — send it straight to that run's own candidates page
    // (Phase J4B follow-up) rather than reviving the old inline-in-History-tab behavior.
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
        if (!this.fileSourceId && result.items.length > 0) this.fileSourceId = result.items[0].sourceId;
      },
      error: () => { this.loadingSources.set(false); },
    });
  }

  openNewSourceModal(): void {
    this.newSourceName = '';
    this.newSourceError.set('');
    this.newSourceModalOpen.set(true);
  }

  closeNewSourceModal(): void {
    this.newSourceModalOpen.set(false);
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
            this.newSourceModalOpen.set(false);
            this.sources.update(items => [approved, ...items]);
            this.fileSourceId = approved.sourceId;
          },
          error: err => {
            // Source was created but auto-approve failed — still usable, just surface a note.
            this.creatingSource.set(false);
            this.newSourceModalOpen.set(false);
            this.sources.update(items => [created, ...items]);
            this.fileSourceId = created.sourceId;
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

  private parseTags(raw: string): string[] | null {
    const tags = raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
    return tags.length > 0 ? tags : null;
  }

  submitPaste(): void {
    this.error.set('');
    this.result.set(null);

    if (!this.sourceName.trim()) {
      this.error.set('Source name is required.');
      return;
    }
    if (!this.content.trim()) {
      this.error.set('Content is required.');
      return;
    }

    this.submitting.set(true);
    this.importSvc.import({
      sourceName: this.sourceName.trim(),
      resourceType: this.resourceType,
      inputMode: this.inputMode,
      content: this.content,
      defaultCefrLevel: this.defaultCefrLevel || null,
      defaultSkill: this.defaultSkill.trim() || null,
      defaultSubskill: this.defaultSubskill.trim() || null,
      defaultContextTags: this.parseTags(this.defaultContextTags),
      defaultFocusTags: this.parseTags(this.defaultFocusTags),
      defaultDifficultyBand: this.defaultDifficultyBand,
      notes: this.notes.trim() || null,
    }).subscribe({
      next: result => {
        this.submitting.set(false);
        this.result.set(result);
        this.currentRunId.set(result.importRunId);
        this.candidatesPage.set(1);
        this.loadCandidates();
        this.loadRecentRuns();
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.error ?? 'Import failed.');
      },
    });
  }

  startAnotherPasteImport(): void {
    this.content = '';
    this.result.set(null);
    this.error.set('');
    this.currentRunId.set(null);
  }

  goToResourceBank(): void {
    this.router.navigateByUrl('/admin/resource-bank');
  }

  // ── File-upload import ───────────────────────────────────────────────────

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length > 0 ? input.files[0] : null;
  }

  submitFile(): void {
    this.fileError.set('');
    this.fileResult.set(null);

    if (!this.fileSourceId) {
      this.fileError.set('A source is required — pick one or create a new one.');
      return;
    }
    if (!this.selectedFile) {
      this.fileError.set('A file is required.');
      return;
    }

    this.fileSubmitting.set(true);
    this.importRunSvc.import(this.fileSourceId, this.fileImportMode, this.selectedFile, this.fileNotes.trim() || undefined).subscribe({
      next: result => {
        this.fileSubmitting.set(false);
        this.fileResult.set(result);
        this.currentRunId.set(result.runId);
        this.candidatesPage.set(1);
        this.loadCandidates();
        this.loadRecentRuns();
      },
      error: err => {
        this.fileSubmitting.set(false);
        this.fileError.set(err.error?.error ?? 'File import failed.');
      },
    });
  }

  startAnotherFileImport(): void {
    this.selectedFile = null;
    this.fileResult.set(null);
    this.fileError.set('');
    this.currentRunId.set(null);
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

  /** Selecting a run from Import History navigates to that run's own candidates page (Phase J4B
   *  follow-up) instead of expanding a review panel inline below the runs table. */
  selectRun(runId: string): void {
    this.router.navigate(['/admin/content/import/runs', runId]);
  }

  // ── Candidate pipeline/review (New Import tab) ──────────────────────────

  loadCandidates(): void {
    const runId = this.currentRunId();
    if (!runId) return;
    this.candidatesLoading.set(true);
    this.candidatesError.set('');
    this.candidateSvc.list(this.candidatesPage(), this.candidatesPageSize, undefined, runId).subscribe({
      next: result => {
        this.candidates.set(result.items);
        this.candidatesTotal.set(result.totalCount);
        this.candidatesLoading.set(false);
      },
      error: err => {
        this.candidatesLoading.set(false);
        this.candidatesError.set(err.error?.error ?? 'Could not load candidates for this import run.');
      },
    });
  }

  onCandidatesPageChange(page: number): void {
    this.candidatesPage.set(page);
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

  /** Phase I1 — the primary action: approves then publishes in a single call. A failed publish
   *  returns 200 with success=false and a list of reasons, surfaced via lastPublishResult. */
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

  // ── Preview drawer (ported from AdminResourceCandidatesComponent, Phase E3) ─

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
