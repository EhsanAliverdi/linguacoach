import { Component, OnInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { AdminUnifiedResourceBankService, AdminResourceCandidateService } from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
  RESOURCE_BANK_CEFR_LEVELS,
  AdminResourceCandidateReviewSummaryDto,
} from '../../../core/models/admin-resource-import.models';
import { IssuesSummary } from '../../../core/models/admin-repair.models';
import { AdminBulkRepairService } from '../../../core/services/admin-bulk-repair.service';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminRowAction,
  SpAdminTableActionsComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
  SpAdminTableFooterComponent,
} from '../../../design-system/admin';

/** Phase H3 — UnifiedResourceBankItemType's member names match PublishedResourceType (backend
 *  Domain enum) and LessonResourceLinkInput's ResourceType 1:1 — no translation table needed. */
const RESOURCE_TYPE_TO_LESSON_TYPE: Record<UnifiedResourceBankItemType, string> = {
  vocabulary: 'Vocabulary',
  grammar: 'Grammar',
  readingReference: 'ReadingReference',
  readingPassage: 'ReadingPassage',
  writing: 'Writing',
  listening: 'Listening',
  speaking: 'Speaking',
};

/** Phase J5a/J5c/J5d — Lesson/Exercise/Module generation (LessonResourceLookup et al.) only knows
 *  how to read Vocabulary/Grammar/ReadingReference/ReadingPassage resources so far; Writing/
 *  Listening/Speaking resources can be imported and published, but Generate Learn/Activity/Module
 *  aren't wired to consume them yet (a separate future phase). Hiding those actions here is more
 *  honest than showing a button that would fail server-side with a generic "not found in Resource
 *  Bank" error. */
const TYPES_SUPPORTING_GENERATION: ReadonlySet<UnifiedResourceBankItemType> =
  new Set(['vocabulary', 'grammar', 'readingReference', 'readingPassage']);

const PAGE_SIZE = 20;

/**
 * Phase H1 — unified admin-facing Resource Bank read model. Aggregates the four typed published
 * bank tables (vocabulary/grammar/reading-references/reading-passages) into one filtered view, so
 * an admin sees "one Resource Bank with typed rows" instead of four separate pages. This is Option
 * B from docs/architecture/product-model-realignment-h0.md §4: a read model over the existing
 * typed tables, not a physical unified table. Read-only, same as the typed pages it aggregates —
 * no edit/delete here, all mutation stays on Resource Candidates (E4). The typed pages this
 * replaces as the primary entry point remain reachable and fully functional.
 *
 * Phase H3 — "Generate Learn" is a real row action (deterministic draft composer, see
 * AdminLessonService). Phase H4 — "Generate Activity" is real too (see
 * AdminExerciseService). Phase H5 — "Generate Module" is now real too: it only
 * succeeds when an already-approved Lesson AND an already-approved Exercise are
 * both linked to this resource (see AdminModuleService.generateFromResource) — a clear
 * validation error explains what's missing otherwise, never a silent no-op.
 */
@Component({
  selector: 'app-admin-resource-bank-unified',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
  ],
  templateUrl: './admin-resource-bank-unified.component.html',
})
export class AdminResourceBankUnifiedComponent implements OnInit {
  @ViewChild('resourceBankTableRef') resourceBankTableRef?: SpAdminTableComponent;

  readonly resourceBankColumns: SpAdminTableColumn[] = [
    { key: 'type', label: 'Type' },
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'cefrLevel', label: 'Level' },
    { key: 'skill', label: 'Skill / Subskill' },
    { key: 'contextTags', label: 'Context' },
    { key: 'focusTags', label: 'Focus' },
    { key: 'difficultyBand', label: 'Difficulty' },
    { key: 'linked', label: 'Learn / Activity / Module' },
    { key: 'actions', label: 'Actions', align: 'right' },
  ];

  resourceBankBulkEditMode = signal(false);
  onResourceBankBulkEditModeChange(enabled: boolean): void {
    this.resourceBankBulkEditMode.set(enabled);
    if (!enabled) this.clearSelection();
  }

  onResourceBankSelectionChange(indices: number[]): void {
    const rows = this.items();
    const ids = indices.map(i => rows[i]?.id).filter((id): id is string => !!id);
    this.selectedIds.set(new Set(ids));
  }

  onRowClick(row: unknown): void {
    this.openDetail(row as unknown as UnifiedResourceBankItemDto);
  }

  items = signal<UnifiedResourceBankItemDto[]>([]);
  loading = signal(true);
  error = signal('');

  searchQuery = signal('');
  typeFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  skillFilter = signal<string>('all');
  // Sprint 12 — "Delete" always only archived; there was previously no way to see archived items
  // or reach Unarchive from this list, and no way to isolate resources with zero downstream
  // Lesson/Exercise ("Unused").
  showArchived = signal(false);
  unusedOnly = signal(false);
  page = signal(1);

  // Sprint 12 — global import-backlog summary (untriaged + stuck-approved-unpublishable counts),
  // reusing the existing per-run summary endpoint with no importRunId/sourceId for a global count.
  importBacklog = signal<AdminResourceCandidateReviewSummaryDto | null>(null);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly typeOptions = [{ value: 'all', label: 'All types' }, ...UNIFIED_RESOURCE_BANK_TYPES];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];
  readonly skillOptions = [
    { value: 'all', label: 'All skills' },
    { value: 'Vocabulary', label: 'Vocabulary' },
    { value: 'Grammar', label: 'Grammar' },
    { value: 'Reading', label: 'Reading' },
    { value: 'Writing', label: 'Writing' },
    { value: 'Listening', label: 'Listening' },
    { value: 'Speaking', label: 'Speaking' },
  ];

  generateSuccess = signal('');
  generateError = signal('');

  // ── Phase K3 — bulk selection + bulk actions (client-side sequential calls over the existing
  // single-item generate endpoints — one resource per call is a hard backend invariant, see
  // generateModule's doc comment, so "bulk" here means "loop the same call," not a new batch
  // endpoint). ──────────────────────────────────────────────────────────────────────────────
  selectedIds = signal<Set<string>>(new Set());
  bulkRunning = signal(false);
  bulkResultSummary = signal('');

  readonly selectedCount = computed(() => this.selectedIds().size);
  readonly selectedSupportGeneration = computed(() => {
    const ids = this.selectedIds();
    return this.items().filter(i => ids.has(i.id) && TYPES_SUPPORTING_GENERATION.has(i.type));
  });

  // ── Phase K9/K11 — top-level issue count + bulk "Fix All with AI" (runs in a root service so
  // its progress toast survives navigating away from this page — see AdminBulkRepairService). ──
  issuesSummary = signal<IssuesSummary | null>(null);

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private lessonSvc: AdminLessonService,
    private candidateSvc: AdminResourceCandidateService,
    public bulkRepair: AdminBulkRepairService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    const requestedType = this.route.snapshot.queryParamMap.get('type');
    if (requestedType && UNIFIED_RESOURCE_BANK_TYPES.some(t => t.value === requestedType)) {
      this.typeFilter.set(requestedType);
    }
    this.loadAll();
    this.loadIssuesSummary();
    this.loadImportBacklog();
  }

  loadImportBacklog(): void {
    this.candidateSvc.summary().subscribe({
      next: summary => this.importBacklog.set(summary),
      error: () => this.importBacklog.set(null),
    });
  }

  loadIssuesSummary(): void {
    this.bankSvc.issuesSummary().subscribe({
      next: summary => this.issuesSummary.set(summary),
      error: () => this.issuesSummary.set(null),
    });
  }

  fixAllWithAi(): void {
    this.bulkRepair.run({
      entityLabel: 'Resource Bank',
      listWithIssues: () => this.bankSvc.listWithIssues(),
      repairOne: id => this.bankSvc.repair(id),
      onDone: () => { this.loadAll(); this.loadIssuesSummary(); },
    });
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const type = this.typeFilter() === 'all' ? undefined : (this.typeFilter() as UnifiedResourceBankItemType);
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    const skill = this.skillFilter() === 'all' ? undefined : this.skillFilter();
    this.bankSvc
      .list(
        this.page(), this.pageSize, type, cefrLevel, skill, undefined, undefined, undefined, undefined,
        this.searchQuery() || undefined, undefined, this.showArchived(), this.unusedOnly())
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load the resource bank.'); },
      });
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onTypeFilterChange(value: string): void {
    this.typeFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onSkillFilterChange(value: string): void {
    this.skillFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  resourceBankFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'type', label: 'Type', options: this.typeOptions, value: this.typeFilter() },
    { key: 'cefrLevel', label: 'Level', options: this.cefrLevelOptions, value: this.cefrLevelFilter() },
    { key: 'skill', label: 'Skill', options: this.skillOptions, value: this.skillFilter() },
  ]);

  onResourceBankFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'type') this.onTypeFilterChange(event.value);
    else if (event.key === 'cefrLevel') this.onCefrLevelFilterChange(event.value);
    else if (event.key === 'skill') this.onSkillFilterChange(event.value);
  }

  toggleShowArchived(): void {
    this.showArchived.set(!this.showArchived());
    this.page.set(1);
    this.selectedIds.set(new Set());
    this.loadAll();
  }

  toggleUnusedOnly(): void {
    this.unusedOnly.set(!this.unusedOnly());
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.selectedIds.set(new Set());
    this.loadAll();
  }

  openDetail(item: UnifiedResourceBankItemDto): void {
    this.router.navigate(['/admin/resource-bank', item.id]);
  }

  typeLabel(type: UnifiedResourceBankItemType): string {
    return UNIFIED_RESOURCE_BANK_TYPES.find(t => t.value === type)?.label ?? type;
  }

  rowActions(_item: UnifiedResourceBankItemDto): SpAdminRowAction[] {
    return this.showArchived()
      ? [
          { id: 'view', label: 'View', icon: 'view' },
          { id: 'edit', label: 'Edit', icon: 'edit' },
          { id: 'unarchive', label: 'Unarchive', icon: 'restore', dividerBefore: true },
        ]
      : [
          { id: 'view', label: 'View', icon: 'view' },
          { id: 'edit', label: 'Edit', icon: 'edit' },
          { id: 'archive', label: 'Archive', icon: 'delete', tone: 'danger', dividerBefore: true },
        ];
  }

  onRowAction(actionId: string, item: UnifiedResourceBankItemDto): void {
    switch (actionId) {
      case 'view': this.openDetail(item); break;
      case 'edit': this.router.navigate(['/admin/resource-bank', item.id, 'edit']); break;
      case 'archive': this.archiveItem(item); break;
      case 'unarchive': this.unarchiveItem(item); break;
    }
  }

  private archiveItem(item: UnifiedResourceBankItemDto): void {
    this.bankSvc.archive([item.id]).subscribe({
      next: () => { this.bulkResultSummary.set(`"${item.title}" archived.`); this.loadAll(); },
      error: err => { this.generateError.set(err.error?.error ?? 'Could not archive this item.'); },
    });
  }

  private unarchiveItem(item: UnifiedResourceBankItemDto): void {
    this.bankSvc.unarchive([item.id]).subscribe({
      next: () => { this.bulkResultSummary.set(`"${item.title}" unarchived.`); this.loadAll(); },
      error: err => { this.generateError.set(err.error?.error ?? 'Could not unarchive this item.'); },
    });
  }

  // ── Selection ────────────────────────────────────────────────────────────────

  clearSelection(): void {
    this.selectedIds.set(new Set());
    this.resourceBankTableRef?.clearSelection();
  }

  // ── Bulk actions ─────────────────────────────────────────────────────────────

  private runBulk(verb: string, call: (item: UnifiedResourceBankItemDto) => Observable<unknown>): void {
    const targets = this.selectedSupportGeneration();
    if (targets.length === 0) return;
    this.bulkRunning.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.bulkResultSummary.set('');

    from(targets).pipe(
      concatMap(item => call(item).pipe(
        map(() => ({ success: true, title: item.title, error: null as string | null })),
        catchError((err: { error?: { error?: string } }) =>
          of({ success: false, title: item.title, error: err.error?.error ?? 'failed' })),
      )),
      toArray(),
    ).subscribe(results => {
      this.bulkRunning.set(false);
      const succeeded = results.filter(r => r.success).length;
      const failed = results.length - succeeded;
      this.bulkResultSummary.set(
        `${succeeded} of ${results.length} ${verb} succeeded` +
        (failed > 0 ? ` — ${failed} failed: ${results.filter(r => !r.success).map(r => r.title + ' (' + r.error + ')').join('; ')}` : '.'));
      this.selectedIds.set(new Set());
      this.loadAll();
    });
  }

  bulkGenerateLearn(): void {
    this.runBulk('Lesson generations', item => this.lessonSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }));
  }

  bulkArchive(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.bulkRunning.set(true);
    this.generateError.set('');
    this.bulkResultSummary.set('');
    this.bankSvc.archive(ids).subscribe({
      next: result => {
        this.bulkRunning.set(false);
        this.bulkResultSummary.set(`${result.succeededCount} of ${result.requestedCount} archived.`);
        this.selectedIds.set(new Set());
        this.loadAll();
      },
      error: err => { this.bulkRunning.set(false); this.generateError.set(err.error?.error ?? 'Could not archive the selected items.'); },
    });
  }

  bulkUnarchive(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.bulkRunning.set(true);
    this.generateError.set('');
    this.bulkResultSummary.set('');
    this.bankSvc.unarchive(ids).subscribe({
      next: result => {
        this.bulkRunning.set(false);
        this.bulkResultSummary.set(`${result.succeededCount} of ${result.requestedCount} unarchived.`);
        this.selectedIds.set(new Set());
        this.loadAll();
      },
      error: err => { this.bulkRunning.set(false); this.generateError.set(err.error?.error ?? 'Could not unarchive the selected items.'); },
    });
  }

  goToLessons(): void { this.router.navigateByUrl('/admin/lesson-library'); }
}
