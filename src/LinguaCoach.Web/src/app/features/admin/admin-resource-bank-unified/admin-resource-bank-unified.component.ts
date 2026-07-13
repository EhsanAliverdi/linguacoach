import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
  RESOURCE_BANK_CEFR_LEVELS,
  QuickWordResult,
} from '../../../core/models/admin-resource-import.models';
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
  SpAdminSelectComponent,
  SpAdminTableComponent,
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
    RouterLink,
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
    SpAdminSelectComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
  ],
  templateUrl: './admin-resource-bank-unified.component.html',
})
export class AdminResourceBankUnifiedComponent implements OnInit {
  items = signal<UnifiedResourceBankItemDto[]>([]);
  loading = signal(true);
  error = signal('');

  searchQuery = signal('');
  typeFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  skillFilter = signal<string>('all');
  page = signal(1);

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
  lastGeneratedKind = signal<'learn' | 'activity' | 'module' | null>(null);

  // ── Phase K3 — bulk selection + bulk actions (client-side sequential calls over the existing
  // single-item generate endpoints — one resource per call is a hard backend invariant, see
  // generateModule's doc comment, so "bulk" here means "loop the same call," not a new batch
  // endpoint). ──────────────────────────────────────────────────────────────────────────────
  selectedIds = signal<Set<string>>(new Set());
  bulkRunning = signal(false);
  bulkResultSummary = signal('');

  readonly selectedCount = computed(() => this.selectedIds().size);
  readonly allVisibleSelected = computed(() => {
    const items = this.items();
    return items.length > 0 && items.every(i => this.selectedIds().has(i.id));
  });
  readonly selectedSupportGeneration = computed(() => {
    const ids = this.selectedIds();
    return this.items().filter(i => ids.has(i.id) && TYPES_SUPPORTING_GENERATION.has(i.type));
  });

  // ── Phase K3 — "quick word" one-shot cascade: word -> Resource Bank item + Lesson + Exercise
  // + Module, all generated and (Lesson/Exercise) auto-approved in one action. ──────────────────
  quickWordModalOpen = signal(false);
  quickWordSubmitting = signal(false);
  quickWordError = signal('');
  quickWordResult = signal<QuickWordResult | null>(null);
  quickWordWord = '';
  quickWordCefrLevel = 'A1';
  quickWordPartOfSpeech = '';
  quickWordDefinition = '';
  readonly quickWordCefrOptions = RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }));

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private moduleSvc: AdminModuleService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    const requestedType = this.route.snapshot.queryParamMap.get('type');
    if (requestedType && UNIFIED_RESOURCE_BANK_TYPES.some(t => t.value === requestedType)) {
      this.typeFilter.set(requestedType);
    }
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const type = this.typeFilter() === 'all' ? undefined : (this.typeFilter() as UnifiedResourceBankItemType);
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    const skill = this.skillFilter() === 'all' ? undefined : this.skillFilter();
    this.bankSvc
      .list(this.page(), this.pageSize, type, cefrLevel, skill, undefined, undefined, undefined, undefined, this.searchQuery() || undefined)
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

  // ── Selection ────────────────────────────────────────────────────────────────

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  toggleSelected(id: string, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.selectedIds());
    if (next.has(id)) next.delete(id); else next.add(id);
    this.selectedIds.set(next);
  }

  toggleSelectAllVisible(): void {
    if (this.allVisibleSelected()) {
      this.selectedIds.set(new Set());
      return;
    }
    this.selectedIds.set(new Set(this.items().map(i => i.id)));
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  // ── Bulk actions ─────────────────────────────────────────────────────────────

  private runBulk(
    verb: string,
    call: (item: UnifiedResourceBankItemDto) => Observable<unknown>,
    kind: 'learn' | 'activity' | 'module' | null,
  ): void {
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
      this.lastGeneratedKind.set(kind);
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
    }), 'learn');
  }

  bulkGenerateActivity(): void {
    this.runBulk('Exercise generations', item => this.exerciseSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }), 'activity');
  }

  bulkGenerateModule(): void {
    this.runBulk('Module generations', item => this.moduleSvc.generateFromResource({
      resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type],
      resourceId: item.id,
    }), 'module');
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

  goToLessons(): void { this.router.navigateByUrl('/admin/lesson-library'); }
  goToActivities(): void { this.router.navigateByUrl('/admin/exercises'); }
  goToModules(): void { this.router.navigateByUrl('/admin/modules'); }

  // ── Phase K3 — quick word modal ─────────────────────────────────────────────

  openQuickWord(): void {
    this.quickWordWord = '';
    this.quickWordCefrLevel = 'A1';
    this.quickWordPartOfSpeech = '';
    this.quickWordDefinition = '';
    this.quickWordError.set('');
    this.quickWordResult.set(null);
    this.quickWordModalOpen.set(true);
  }

  closeQuickWord(): void {
    this.quickWordModalOpen.set(false);
  }

  submitQuickWord(): void {
    if (!this.quickWordWord.trim()) {
      this.quickWordError.set('A word is required.');
      return;
    }
    this.quickWordSubmitting.set(true);
    this.quickWordError.set('');
    this.quickWordResult.set(null);
    this.bankSvc.quickWord({
      word: this.quickWordWord.trim(),
      cefrLevel: this.quickWordCefrLevel,
      partOfSpeech: this.quickWordPartOfSpeech.trim() || null,
      definition: this.quickWordDefinition.trim() || null,
    }).subscribe({
      next: result => {
        this.quickWordSubmitting.set(false);
        this.quickWordResult.set(result);
        this.loadAll();
      },
      error: err => {
        this.quickWordSubmitting.set(false);
        this.quickWordError.set(err.error?.error ?? 'Could not run the quick word pipeline.');
      },
    });
  }
}
