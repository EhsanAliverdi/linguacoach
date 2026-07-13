import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  LessonDto,
  LESSON_REVIEW_STATUSES,
} from '../../../core/models/admin-lesson.models';
import { RESOURCE_BANK_CEFR_LEVELS } from '../../../core/models/admin-resource-import.models';
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
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

interface BulkItemResult { success: boolean; title: string; error: string | null; }

/**
 * Phase H3 — Lesson foundation admin page. Reviewable teaching/explanation blocks generated
 * from (or manually authored about) published Resource Bank rows — the "Learn" half of a future
 * Module. Nothing here creates an Exercise/Module row or assigns anything to a student.
 */
@Component({
  selector: 'app-admin-lessons',
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
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-lessons.component.html',
})
export class AdminLessonsComponent implements OnInit {
  items = signal<LessonDto[]>([]);
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

  readonly statusOptions = [{ value: 'all', label: 'All statuses' }, ...LESSON_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  // ── Phase K3 — bulk selection + bulk actions (client-side sequential loop over the existing
  // single-item endpoints, continue-on-error per item) ────────────────────────────────────────
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

  constructor(
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private moduleSvc: AdminModuleService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const status = this.statusFilter() === 'all' ? undefined : this.statusFilter();
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    this.lessonSvc
      .list(this.page(), this.pageSize, status, cefrLevel, undefined, undefined, undefined, undefined, undefined, this.searchQuery() || undefined)
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load Lessons.'); },
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

  openDetail(item: LessonDto): void {
    this.router.navigate(['/admin/lesson-library', item.id]);
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

  private selectedItems(): LessonDto[] {
    const ids = this.selectedIds();
    return this.items().filter(i => ids.has(i.id));
  }

  private runBulk(verb: string, call: (item: LessonDto) => Observable<unknown>): void {
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
    this.runBulk('approvals', item => this.lessonSvc.approve(item.id));
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
    this.runBulk('rejections', item => this.lessonSvc.reject(item.id, this.bulkRejectReasonDraft.trim()));
  }

  bulkGenerateActivity(): void {
    this.runBulk('Exercise generations', item => this.exerciseSvc.generateFromLesson({ lessonId: item.id }));
  }

  bulkGenerateModule(): void {
    this.runBulk('Module generations', item => this.moduleSvc.generateFromLesson({ lessonId: item.id }));
  }
}
