import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  ExerciseDto,
  ACTIVITY_REVIEW_STATUSES,
  ACTIVITY_TYPES,
} from '../../../core/models/admin-exercise.models';
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
  templateUrl: './admin-exercises.component.html',
})
export class AdminExercisesComponent implements OnInit {
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
  readonly activityTypeOptions = [{ value: 'all', label: 'All types' }, ...ACTIVITY_TYPES.map(t => ({ value: t, label: t }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  detail = signal<ExerciseDto | null>(null);
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  // ── Phase H5 — Generate Module from this Exercise ─────────────────────────
  generatingModule = signal(false);
  lastActionWasGenerateModule = signal(false);

  constructor(
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

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openDrawer(item: ExerciseDto): void {
    this.detail.set(item);
    this.rejectReasonDraft = '';
    this.actionError.set('');
    this.drawerOpen.set(true);
  }

  openDrawerById(id: string): void {
    this.exerciseSvc.get(id).subscribe({
      next: item => this.openDrawer(item),
      error: () => { /* deep-linked item no longer exists — ignore, list still loads */ },
    });
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
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
    this.exerciseSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.lastActionWasGenerateModule.set(false);
        this.detail.set(updated);
        this.actionSuccess.set('Exercise approved.');
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
    this.exerciseSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.lastActionWasGenerateModule.set(false);
        this.detail.set(updated);
        this.actionSuccess.set('Exercise rejected.');
        this.loadAll();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  /** Phase H5 — generates a Module from this (approved) Exercise's compatible
   *  approved Lesson(s). Rejected with a clear message if the Exercise itself isn't approved
   *  yet, or no compatible approved Lesson exists. */
  generateModule(): void {
    const item = this.detail();
    if (!item) return;
    this.generatingModule.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromExercise({ exerciseId: item.id }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastActionWasGenerateModule.set(true);
        this.actionSuccess.set('Module draft generated from this Exercise — pending review.');
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
