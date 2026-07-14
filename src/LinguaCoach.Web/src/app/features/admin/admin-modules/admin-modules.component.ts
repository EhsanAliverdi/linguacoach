import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import {
  ModuleDto,
  MODULE_REVIEW_STATUSES,
} from '../../../core/models/admin-module.models';
import { LessonDto } from '../../../core/models/admin-lesson.models';
import { ExerciseDto } from '../../../core/models/admin-exercise.models';
import { RESOURCE_BANK_CEFR_LEVELS } from '../../../core/models/admin-resource-import.models';
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
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

interface BulkItemResult { success: boolean; title: string; error: string | null; }

/**
 * Phase H5 — Module foundation admin page. Reusable, reviewable learning units
 * combining Lessons + Exercises + a module-level feedback plan. Nothing here
 * assigns anything to a student or changes Today/Practice Gym runtime selection.
 */
@Component({
  selector: 'app-admin-modules',
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
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-modules.component.html',
})
export class AdminModulesComponent implements OnInit {
  items = signal<ModuleDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  searchQuery = signal('');
  statusFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly statusOptions = [{ value: 'all', label: 'All statuses' }, ...MODULE_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  // ── Phase K4 — bulk selection + bulk actions ────────────────────────────────
  selectedIds = signal<Set<string>>(new Set());
  bulkRunning = signal(false);
  bulkResultSummary = signal('');
  bulkRejectModalOpen = signal(false);
  bulkRejectReasonDraft = '';

  readonly selectedCount = computed(() => this.selectedIds().size);
  readonly allVisibleSelected = computed(() => {
    const items = this.items();
    return items.length > 0 && items.every(i => this.selectedIds().has(i.id));
  });

  // ── Phase K3 — real Create Module modal: searchable Lesson/Exercise dropdowns instead of
  // pasting raw GUIDs. Backed by moduleSvc.create() (POST /api/admin/modules), which — unlike
  // generate-from-items — does NOT require the Lesson/Exercise to already be approved. ─────────
  createModalOpen = signal(false);
  creating = signal(false);
  createTitle = '';
  createCefrLevel = '';

  lessonSearchQuery = '';
  lessonResults = signal<LessonDto[]>([]);
  lessonSearching = signal(false);
  selectedLesson = signal<LessonDto | null>(null);
  private lessonSearchDebounce?: ReturnType<typeof setTimeout>;

  exerciseSearchQuery = '';
  exerciseResults = signal<ExerciseDto[]>([]);
  exerciseSearching = signal(false);
  selectedExercise = signal<ExerciseDto | null>(null);
  private exerciseSearchDebounce?: ReturnType<typeof setTimeout>;

  // ── Phase K9/K11 — top-level issue count + bulk "Fix All with AI" (runs in a root service so
  // its progress toast survives navigating away from this page). ────────────────────────────
  issuesSummary = signal<IssuesSummary | null>(null);

  constructor(
    private moduleSvc: AdminModuleService,
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    public bulkRepair: AdminBulkRepairService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();
    this.loadIssuesSummary();
  }

  loadIssuesSummary(): void {
    this.moduleSvc.issuesSummary().subscribe({
      next: summary => this.issuesSummary.set(summary),
      error: () => this.issuesSummary.set(null),
    });
  }

  fixAllWithAi(): void {
    this.bulkRepair.run({
      entityLabel: 'Module',
      listWithIssues: () => this.moduleSvc.listWithIssues(),
      repairOne: id => this.moduleSvc.repair(id),
      onDone: () => { this.loadAll(); this.loadIssuesSummary(); },
    });
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const status = this.statusFilter() === 'all' ? undefined : this.statusFilter();
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    this.moduleSvc
      .list(this.page(), this.pageSize, status, cefrLevel, undefined, undefined, undefined, undefined,
        undefined, undefined, undefined, this.searchQuery() || undefined)
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load Modules.'); },
      });
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.selectedIds.set(new Set());
    this.loadAll();
  }

  openDetail(item: ModuleDto): void {
    this.router.navigate(['/admin/modules', item.id]);
  }

  rowActions(_item: ModuleDto): SpAdminRowAction[] {
    return [
      { id: 'view', label: 'View', icon: 'view' },
      { id: 'edit', label: 'Edit', icon: 'edit' },
      { id: 'delete', label: 'Delete', icon: 'delete', tone: 'danger', dividerBefore: true },
    ];
  }

  onRowAction(actionId: string, item: ModuleDto): void {
    switch (actionId) {
      case 'view': this.openDetail(item); break;
      case 'edit': this.router.navigate(['/admin/modules', item.id, 'edit']); break;
      case 'delete': this.deleteItem(item); break;
    }
  }

  private deleteItem(item: ModuleDto): void {
    this.moduleSvc.archive([item.id]).subscribe({
      next: () => { this.actionSuccess.set(`"${item.title}" deleted.`); this.loadAll(); },
      error: err => { this.actionError.set(err.error?.error ?? 'Could not delete this Module.'); },
    });
  }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'PendingReview') return 'warning';
    return 'neutral';
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

  private selectedItems(): ModuleDto[] {
    const ids = this.selectedIds();
    return this.items().filter(i => ids.has(i.id));
  }

  private runBulk(verb: string, call: (item: ModuleDto) => Observable<unknown>): void {
    const targets = this.selectedItems();
    if (targets.length === 0) return;
    this.bulkRunning.set(true);
    this.actionError.set('');
    this.bulkResultSummary.set('');

    from(targets).pipe(
      concatMap(item => call(item).pipe(
        map((): BulkItemResult => ({ success: true, title: item.title, error: null })),
        catchError((err: { error?: { error?: string } }) =>
          of<BulkItemResult>({ success: false, title: item.title, error: err.error?.error ?? 'failed' })),
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

  bulkApprove(): void {
    this.runBulk('approvals', item => this.moduleSvc.approve(item.id));
  }

  openBulkReject(): void {
    if (this.selectedCount() === 0) return;
    this.bulkRejectReasonDraft = '';
    this.actionError.set('');
    this.bulkRejectModalOpen.set(true);
  }

  closeBulkReject(): void {
    this.bulkRejectModalOpen.set(false);
  }

  confirmBulkReject(): void {
    if (!this.bulkRejectReasonDraft.trim()) {
      this.actionError.set('A rejection reason is required.');
      return;
    }
    this.bulkRejectModalOpen.set(false);
    this.runBulk('rejections', item => this.moduleSvc.reject(item.id, this.bulkRejectReasonDraft.trim()));
  }

  bulkDelete(): void {
    this.runBulk('deletions', item => this.moduleSvc.archive([item.id]));
  }

  openCreateModal(): void {
    this.createTitle = '';
    this.createCefrLevel = '';
    this.lessonSearchQuery = '';
    this.lessonResults.set([]);
    this.selectedLesson.set(null);
    this.exerciseSearchQuery = '';
    this.exerciseResults.set([]);
    this.selectedExercise.set(null);
    this.actionError.set('');
    this.createModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.createModalOpen.set(false);
  }

  onLessonSearch(event: Event): void {
    this.lessonSearchQuery = (event.target as HTMLInputElement).value;
    clearTimeout(this.lessonSearchDebounce);
    this.lessonSearchDebounce = setTimeout(() => this.runLessonSearch(), 250);
  }

  private runLessonSearch(): void {
    const query = this.lessonSearchQuery.trim();
    if (!query) { this.lessonResults.set([]); return; }
    this.lessonSearching.set(true);
    this.lessonSvc.list(1, 10, undefined, undefined, undefined, undefined, undefined, undefined, undefined, query).subscribe({
      next: result => { this.lessonSearching.set(false); this.lessonResults.set(result.items); },
      error: () => { this.lessonSearching.set(false); this.lessonResults.set([]); },
    });
  }

  pickLesson(lesson: LessonDto): void {
    this.selectedLesson.set(lesson);
    this.lessonResults.set([]);
    this.lessonSearchQuery = '';
  }

  clearLesson(): void {
    this.selectedLesson.set(null);
  }

  onExerciseSearch(event: Event): void {
    this.exerciseSearchQuery = (event.target as HTMLInputElement).value;
    clearTimeout(this.exerciseSearchDebounce);
    this.exerciseSearchDebounce = setTimeout(() => this.runExerciseSearch(), 250);
  }

  private runExerciseSearch(): void {
    const query = this.exerciseSearchQuery.trim();
    if (!query) { this.exerciseResults.set([]); return; }
    this.exerciseSearching.set(true);
    this.exerciseSvc.list(1, 10, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, query).subscribe({
      next: result => { this.exerciseSearching.set(false); this.exerciseResults.set(result.items); },
      error: () => { this.exerciseSearching.set(false); this.exerciseResults.set([]); },
    });
  }

  pickExercise(exercise: ExerciseDto): void {
    this.selectedExercise.set(exercise);
    this.exerciseResults.set([]);
    this.exerciseSearchQuery = '';
  }

  clearExercise(): void {
    this.selectedExercise.set(null);
  }

  /** Phase K3 — real create: a Lesson and an Exercise picked from live search dropdowns (not
   *  pasted GUIDs), submitted to the previously-unreachable POST /api/admin/modules endpoint.
   *  Neither needs to already be approved — that's the create handler's own design (see
   *  AdminCreateModuleHandler's requireApproved: false), distinct from generate-from-* actions. */
  submitCreateModule(): void {
    if (!this.createTitle.trim()) {
      this.actionError.set('A title is required.');
      return;
    }
    const lesson = this.selectedLesson();
    const exercise = this.selectedExercise();
    if (!lesson || !exercise) {
      this.actionError.set('Both a Lesson and an Exercise must be selected.');
      return;
    }
    this.creating.set(true);
    this.actionError.set('');
    this.moduleSvc.create({
      title: this.createTitle.trim(),
      lessonLinks: [{ lessonId: lesson.id, role: 'Primary' }],
      exerciseLinks: [{ exerciseId: exercise.id, role: 'PrimaryPractice', required: true }],
      cefrLevel: this.createCefrLevel || null,
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.createModalOpen.set(false);
        this.actionSuccess.set('Module created — pending review.');
        this.loadAll();
      },
      error: err => {
        this.creating.set(false);
        this.actionError.set(err.error?.error ?? 'Could not create the Module.');
      },
    });
  }

}
