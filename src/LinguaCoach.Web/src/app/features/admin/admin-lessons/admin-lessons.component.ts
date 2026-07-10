import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter(v => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

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

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  detail = signal<LessonDto | null>(null);
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  // ── Phase H4 — Generate Exercise from this Lesson ───────────────────────
  generatingActivity = signal(false);
  lastActionKind = signal<'activity' | 'module' | null>(null);

  // ── Phase H5 — Generate Module from this Lesson ─────────────────────────
  generatingModule = signal(false);

  constructor(
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private moduleSvc: AdminModuleService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();

    const deepLinkId = this.route.snapshot.queryParamMap.get('id');
    if (deepLinkId) this.openDrawerById(deepLinkId);
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
    this.loadAll();
  }

  openDrawer(item: LessonDto): void {
    this.detail.set(item);
    this.rejectReasonDraft = '';
    this.actionError.set('');
    this.drawerOpen.set(true);
  }

  openDrawerById(id: string): void {
    this.lessonSvc.get(id).subscribe({
      next: item => this.openDrawer(item),
      error: () => { /* deep-linked item no longer exists — ignore, list still loads */ },
    });
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  examplesFor(item: LessonDto): string[] {
    return parseJsonArray(item.examplesJson);
  }

  commonMistakesFor(item: LessonDto): string[] {
    return parseJsonArray(item.commonMistakesJson);
  }

  contextTagsFor(item: LessonDto): string[] {
    return parseJsonArray(item.contextTagsJson);
  }

  focusTagsFor(item: LessonDto): string[] {
    return parseJsonArray(item.focusTagsJson);
  }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'PendingReview') return 'warning';
    return 'neutral';
  }

  approveSelected(): void {
    const item = this.detail();
    if (!item) return;
    this.approving.set(true);
    this.actionError.set('');
    this.lessonSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.lastActionKind.set(null);
        this.detail.set(updated);
        this.actionSuccess.set('Lesson approved.');
        this.loadAll();
      },
      error: err => { this.approving.set(false); this.actionError.set(err.error?.error ?? 'Could not approve.'); },
    });
  }

  rejectSelected(): void {
    const item = this.detail();
    if (!item) return;
    if (!this.rejectReasonDraft.trim()) {
      this.actionError.set('A rejection reason is required.');
      return;
    }
    this.rejecting.set(true);
    this.actionError.set('');
    this.lessonSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.lastActionKind.set(null);
        this.detail.set(updated);
        this.actionSuccess.set('Lesson rejected.');
        this.loadAll();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  /** Phase H4 — generates an Exercise from this Lesson's own linked resources, using its
   *  own CEFR/skill/subskill/tags as defaults. Always stages a pending-review Exercise; the Lesson
   *  itself is never modified. */
  generateActivity(): void {
    const item = this.detail();
    if (!item) return;
    this.generatingActivity.set(true);
    this.actionError.set('');
    this.exerciseSvc.generateFromLesson({ lessonId: item.id }).subscribe({
      next: () => {
        this.generatingActivity.set(false);
        this.lastActionKind.set('activity');
        this.actionSuccess.set('Exercise draft generated from this Lesson — pending review.');
      },
      error: err => {
        this.generatingActivity.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate an Exercise.');
      },
    });
  }

  goToExercises(): void {
    this.router.navigateByUrl('/admin/exercises');
  }

  /** Phase H5 — generates a Module from this (approved) Lesson's compatible approved
   *  Exercise(s). Rejected with a clear message if the Lesson itself isn't approved yet, or
   *  no compatible approved Exercise exists. */
  generateModule(): void {
    const item = this.detail();
    if (!item) return;
    this.generatingModule.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromLesson({ lessonId: item.id }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastActionKind.set('module');
        this.actionSuccess.set('Module draft generated from this Lesson — pending review.');
      },
      error: err => {
        this.generatingModule.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate a Module.');
      },
    });
  }

  goToModules(): void {
    this.router.navigateByUrl('/admin/modules');
  }
}
