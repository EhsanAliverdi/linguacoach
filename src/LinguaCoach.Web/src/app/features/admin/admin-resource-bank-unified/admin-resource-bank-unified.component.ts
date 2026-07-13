import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
  RESOURCE_BANK_CEFR_LEVELS,
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
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';

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
    SpAdminDrawerComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
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

  // ── Detail drawer (uses the already-loaded row — no extra fetch needed) ────
  drawerOpen = signal(false);
  detail = signal<UnifiedResourceBankItemDto | null>(null);

  // ── Phase H3 — Generate Learn ────────────────────────────────────────────
  generatingLearnId = signal<string | null>(null);
  generateSuccess = signal('');
  generateError = signal('');
  lastGeneratedKind = signal<'learn' | 'activity' | 'module' | null>(null);

  // ── Phase J2a — Generate Learn with AI (separate action, deterministic above is untouched) ──
  generatingLearnAiId = signal<string | null>(null);

  // ── Phase J2b — Generate Activity with AI (separate action, deterministic above is untouched) ──
  generatingActivityAiId = signal<string | null>(null);

  // ── Phase J2c — Generate Module with AI (separate action, deterministic above is untouched) ──
  generatingModuleAiId = signal<string | null>(null);

  // ── Phase H4 — Generate Activity ─────────────────────────────────────────
  generatingActivityId = signal<string | null>(null);

  // ── Phase H5 — Generate Module ───────────────────────────────────────────
  generatingModuleId = signal<string | null>(null);

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
    this.loadAll();
  }

  openDrawer(item: UnifiedResourceBankItemDto): void {
    this.detail.set(item);
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  typeLabel(type: UnifiedResourceBankItemType): string {
    return UNIFIED_RESOURCE_BANK_TYPES.find(t => t.value === type)?.label ?? type;
  }

  rowActions(item: UnifiedResourceBankItemDto): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [{ id: 'view', label: 'View', icon: 'view', tone: 'default' }];
    if (!TYPES_SUPPORTING_GENERATION.has(item.type)) return actions;

    actions.push(
      {
        id: 'generate-learn', label: 'Generate Learn', icon: 'sparkles', tone: 'default', dividerBefore: true,
        disabled: this.generatingLearnId() === item.id,
      },
      {
        id: 'generate-learn-ai', label: 'Generate Learn (AI)', icon: 'sparkles', tone: 'default',
        disabled: this.generatingLearnAiId() === item.id,
      },
      {
        id: 'generate-activity', label: 'Generate Activity', icon: 'sparkles', tone: 'default',
        disabled: this.generatingActivityId() === item.id,
      },
      {
        id: 'generate-activity-ai', label: 'Generate Activity (AI)', icon: 'sparkles', tone: 'default',
        disabled: this.generatingActivityAiId() === item.id,
      },
      {
        id: 'generate-module', label: 'Generate Module', icon: 'sparkles', tone: 'default',
        disabled: this.generatingModuleId() === item.id,
      },
      {
        id: 'generate-module-ai', label: 'Generate Module (AI)', icon: 'sparkles', tone: 'default',
        disabled: this.generatingModuleAiId() === item.id,
      },
    );
    return actions;
  }

  onRowAction(actionId: string, item: UnifiedResourceBankItemDto): void {
    if (actionId === 'view') this.openDrawer(item);
    if (actionId === 'generate-learn') this.generateLearn(item);
    if (actionId === 'generate-learn-ai') this.generateLearnWithAi(item);
    if (actionId === 'generate-activity') this.generateActivity(item);
    if (actionId === 'generate-activity-ai') this.generateActivityWithAi(item);
    if (actionId === 'generate-module') this.generateModule(item);
    if (actionId === 'generate-module-ai') this.generateModuleWithAi(item);
  }

  /** Phase H3 — deterministic draft composer, one resource per call (multi-select is a future
   *  enhancement, not needed for this foundation phase). Always stages a pending-review Lesson
   *  — never publishes, never assigns anything to a student. */
  generateLearn(item: UnifiedResourceBankItemDto): void {
    this.generatingLearnId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.lessonSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: result => {
        this.generatingLearnId.set(null);
        this.lastGeneratedKind.set('learn');
        this.generateSuccess.set(`Lesson draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingLearnId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate a Lesson.');
      },
    });
  }

  goToLessons(): void {
    this.router.navigateByUrl('/admin/lesson-library');
  }

  /** Phase J2a — AI-assisted alternative to generateLearn(). A separate action: on AI
   *  unavailability the backend returns a clear error and no draft is created — the deterministic
   *  generateLearn() action above stays available regardless. */
  generateLearnWithAi(item: UnifiedResourceBankItemDto): void {
    this.generatingLearnAiId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.lessonSvc.generateFromResourcesWithAi({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: result => {
        this.generatingLearnAiId.set(null);
        this.lastGeneratedKind.set('learn');
        this.generateSuccess.set(`AI-generated Lesson draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingLearnAiId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate a Lesson with AI.');
      },
    });
  }

  /** Phase H4 — deterministic draft composer, one resource per call. The activity type is
   *  auto-picked server-side per resource type (gap_fill for Vocabulary/Grammar, short_answer
   *  for ReadingReference/ReadingPassage) — no type picker here, keep the row action a single
   *  click. Always stages a pending-review Activity — never publishes, never assigns anything to
   *  a student. */
  generateActivity(item: UnifiedResourceBankItemDto): void {
    this.generatingActivityId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.exerciseSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: result => {
        this.generatingActivityId.set(null);
        this.lastGeneratedKind.set('activity');
        this.generateSuccess.set(`Exercise draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingActivityId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate an Exercise.');
      },
    });
  }

  goToActivities(): void {
    this.router.navigateByUrl('/admin/exercises');
  }

  /** Phase J2b — AI-assisted alternative to generateActivity(). A separate action: on AI
   *  unavailability the backend returns a clear error and no draft is created — the deterministic
   *  generateActivity() action above stays available regardless. */
  generateActivityWithAi(item: UnifiedResourceBankItemDto): void {
    this.generatingActivityAiId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.exerciseSvc.generateFromResourcesWithAi({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: result => {
        this.generatingActivityAiId.set(null);
        this.lastGeneratedKind.set('activity');
        this.generateSuccess.set(`AI-generated Exercise draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingActivityAiId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate an Exercise with AI.');
      },
    });
  }

  /** Phase H5 — only succeeds when an already-approved Lesson AND an already-approved
   *  Exercise are both linked to this resource; never cascade-generates either. */
  generateModule(item: UnifiedResourceBankItemDto): void {
    this.generatingModuleId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.moduleSvc.generateFromResource({
      resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type],
      resourceId: item.id,
    }).subscribe({
      next: result => {
        this.generatingModuleId.set(null);
        this.lastGeneratedKind.set('module');
        this.generateSuccess.set(`Module draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingModuleId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate a Module.');
      },
    });
  }

  goToModules(): void {
    this.router.navigateByUrl('/admin/modules');
  }

  /** Phase J2c — AI-assisted alternative to generateModule(). Still only succeeds when an
   *  already-approved Lesson AND an already-approved Exercise are both linked to this resource —
   *  AI never cascade-generates either, same hard invariant as the deterministic action. A
   *  separate action: on AI unavailability the backend returns a clear error and no draft is
   *  created — the deterministic generateModule() action above stays available regardless. */
  generateModuleWithAi(item: UnifiedResourceBankItemDto): void {
    this.generatingModuleAiId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.moduleSvc.generateFromResourceWithAi({
      resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type],
      resourceId: item.id,
    }).subscribe({
      next: result => {
        this.generatingModuleAiId.set(null);
        this.lastGeneratedKind.set('module');
        this.generateSuccess.set(`AI-generated Module draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingModuleAiId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate a Module with AI.');
      },
    });
  }
}
