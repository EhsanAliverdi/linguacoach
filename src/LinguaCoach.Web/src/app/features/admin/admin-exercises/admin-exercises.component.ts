import { Component, OnInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminService } from '../../../core/services/admin.service';
import {
  ExerciseDto,
  ACTIVITY_REVIEW_STATUSES,
} from '../../../core/models/admin-exercise.models';
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
  SpAdminTableActionsComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

interface BulkItemResult { success: boolean; title: string; error: string | null; }

/**
 * Phase H4 — Exercise foundation admin page. Reviewable, editable practice task designs
 * generated from (or authored about) published Resource Bank rows, optionally linked to a Lesson
 * — the "Practice" half of a future Module. Nothing here creates a Module row or assigns
 * anything to a student.
 */
@Component({
  selector: 'app-admin-exercises',
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
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-exercises.component.html',
})
export class AdminExercisesComponent implements OnInit {
  @ViewChild('exercisesTableRef') exercisesTableRef?: SpAdminTableComponent;

  readonly exerciseColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'activityType', label: 'Type' },
    { key: 'rendererType', label: 'Renderer' },
    { key: 'cefrLevel', label: 'Level' },
    { key: 'lessonId', label: 'Lesson' },
    { key: 'reviewStatus', label: 'Status' },
    { key: 'createdAt', label: 'Created' },
    { key: 'actions', label: 'Actions', align: 'right' },
  ];

  exercisesBulkEditMode = signal(false);
  onExercisesBulkEditModeChange(enabled: boolean): void {
    this.exercisesBulkEditMode.set(enabled);
    if (!enabled) this.clearSelection();
  }

  onExercisesSelectionChange(indices: number[]): void {
    const rows = this.items();
    const ids = indices.map(i => rows[i]?.id).filter((id): id is string => !!id);
    this.selectedIds.set(new Set(ids));
  }

  onRowClick(row: unknown): void {
    this.openDetail(row as unknown as ExerciseDto);
  }

  items = signal<ExerciseDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  searchQuery = signal('');
  statusFilter = signal<string>('all');
  activityTypeFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly statusOptions = [{ value: 'all', label: 'All statuses' }, ...ACTIVITY_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  // Sprint 12 — was a hardcoded 3-item const (gap_fill/multiple_choice_single/short_answer) that
  // never matched the real ~25+ ActivityType catalog; now pulled live from
  // GET api/admin/exercise-types, same source admin-lesson-detail's generate modal already uses.
  activityTypeOptions = signal([{ value: 'all', label: 'All types' }]);

  // ── Phase K4 — bulk selection + bulk actions ────────────────────────────────
  selectedIds = signal<Set<string>>(new Set());
  bulkRunning = signal(false);
  bulkResultSummary = signal('');
  bulkRejectModalOpen = signal(false);
  bulkRejectReasonDraft = '';

  readonly selectedCount = computed(() => this.selectedIds().size);

  // ── Phase K9/K11 — top-level issue count + bulk "Fix All with AI" (runs in a root service so
  // its progress toast survives navigating away from this page). ────────────────────────────
  issuesSummary = signal<IssuesSummary | null>(null);

  constructor(
    private exerciseSvc: AdminExerciseService,
    private adminSvc: AdminService,
    public bulkRepair: AdminBulkRepairService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();
    this.loadIssuesSummary();
    this.loadActivityTypeOptions();
  }

  private loadActivityTypeOptions(): void {
    this.adminSvc.listExerciseTypes().subscribe({
      next: types => this.activityTypeOptions.set([
        { value: 'all', label: 'All types' },
        ...types.map(t => ({ value: t.key, label: t.displayName })),
      ]),
      error: () => { /* keep the 'All types' fallback */ },
    });
  }

  loadIssuesSummary(): void {
    this.exerciseSvc.issuesSummary().subscribe({
      next: summary => this.issuesSummary.set(summary),
      error: () => this.issuesSummary.set(null),
    });
  }

  fixAllWithAi(): void {
    this.bulkRepair.run({
      entityLabel: 'Exercise',
      listWithIssues: () => this.exerciseSvc.listWithIssues(),
      repairOne: id => this.exerciseSvc.repair(id),
      onDone: () => { this.loadAll(); this.loadIssuesSummary(); },
    });
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const status = this.statusFilter() === 'all' ? undefined : this.statusFilter();
    const activityType = this.activityTypeFilter() === 'all' ? undefined : this.activityTypeFilter();
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    this.exerciseSvc
      .list(this.page(), this.pageSize, status, activityType, undefined, cefrLevel, undefined, undefined,
        undefined, undefined, undefined, undefined, this.searchQuery() || undefined)
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load Exercises.'); },
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

  onActivityTypeFilterChange(value: string): void {
    this.activityTypeFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  exercisesFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'status', label: 'Status', options: this.statusOptions, value: this.statusFilter() },
    { key: 'activityType', label: 'Type', options: this.activityTypeOptions(), value: this.activityTypeFilter() },
    { key: 'cefrLevel', label: 'Level', options: this.cefrLevelOptions, value: this.cefrLevelFilter() },
  ]);

  onExercisesFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'status') this.onStatusFilterChange(event.value);
    else if (event.key === 'activityType') this.onActivityTypeFilterChange(event.value);
    else if (event.key === 'cefrLevel') this.onCefrLevelFilterChange(event.value);
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.selectedIds.set(new Set());
    this.loadAll();
  }

  openDetail(item: ExerciseDto): void {
    this.router.navigate(['/admin/exercises', item.id]);
  }

  rowActions(_item: ExerciseDto): SpAdminRowAction[] {
    return [
      { id: 'view', label: 'View', icon: 'view' },
      { id: 'edit', label: 'Edit', icon: 'edit' },
      { id: 'delete', label: 'Delete', icon: 'delete', tone: 'danger', dividerBefore: true },
    ];
  }

  onRowAction(actionId: string, item: ExerciseDto): void {
    switch (actionId) {
      case 'view': this.openDetail(item); break;
      case 'edit': this.router.navigate(['/admin/exercises', item.id, 'edit']); break;
      case 'delete': this.deleteItem(item); break;
    }
  }

  private deleteItem(item: ExerciseDto): void {
    this.exerciseSvc.archive([item.id]).subscribe({
      next: () => { this.actionSuccess.set(`"${item.title}" deleted.`); this.loadAll(); },
      error: err => { this.actionError.set(err.error?.error ?? 'Could not delete this Exercise.'); },
    });
  }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'PendingReview') return 'warning';
    return 'neutral';
  }

  // ── Selection ────────────────────────────────────────────────────────────────

  clearSelection(): void {
    this.selectedIds.set(new Set());
    this.exercisesTableRef?.clearSelection();
  }

  // ── Bulk actions ─────────────────────────────────────────────────────────────

  private selectedItems(): ExerciseDto[] {
    const ids = this.selectedIds();
    return this.items().filter(i => ids.has(i.id));
  }

  private runBulk(verb: string, call: (item: ExerciseDto) => Observable<unknown>): void {
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
    this.runBulk('approvals', item => this.exerciseSvc.approve(item.id));
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
    this.runBulk('rejections', item => this.exerciseSvc.reject(item.id, this.bulkRejectReasonDraft.trim()));
  }

  bulkDelete(): void {
    this.runBulk('deletions', item => this.exerciseSvc.archive([item.id]));
  }
}
