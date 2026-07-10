import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  ModuleDto,
  ModulePreviewResult,
  ModulePreviewSubmitResult,
  MODULE_REVIEW_STATUSES,
} from '../../../core/models/admin-module.models';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
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
    SpAdminDrawerComponent,
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
    FormioRendererComponent,
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

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  detail = signal<ModuleDto | null>(null);
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  // ── Generate-from-items modal (simple: admin types Lesson + Exercise ids) ──
  generateModalOpen = signal(false);
  generating = signal(false);
  generateLessonId = '';
  generateExerciseId = '';
  generateTitle = '';

  // ── Phase J3 — Preview as Learner modal ─────────────────────────────────
  previewModalOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  previewData = signal<ModulePreviewResult | null>(null);
  previewSchema = signal<any>(null);
  previewSubmitting = signal(false);
  previewResult = signal<ModulePreviewSubmitResult | null>(null);

  constructor(private moduleSvc: AdminModuleService, private route: ActivatedRoute) {}

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
    this.loadAll();
  }

  openDrawer(item: ModuleDto): void {
    this.detail.set(item);
    this.rejectReasonDraft = '';
    this.actionError.set('');
    this.drawerOpen.set(true);
  }

  openDrawerById(id: string): void {
    this.moduleSvc.get(id).subscribe({
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
    this.moduleSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.detail.set(updated);
        this.actionSuccess.set('Module approved.');
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
    this.moduleSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.detail.set(updated);
        this.actionSuccess.set('Module rejected.');
        this.loadAll();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  openGenerateModal(): void {
    this.generateLessonId = '';
    this.generateExerciseId = '';
    this.generateTitle = '';
    this.actionError.set('');
    this.generateModalOpen.set(true);
  }

  closeGenerateModal(): void {
    this.generateModalOpen.set(false);
  }

  /** Phase H5 — simple selection form: admin types an approved Lesson id and an approved
   *  Exercise id. Both must already be Approved — the backend rejects otherwise. */
  submitGenerateFromItems(): void {
    if (!this.generateLessonId.trim() || !this.generateExerciseId.trim()) {
      this.actionError.set('Both a Lesson id and an Exercise id are required.');
      return;
    }
    this.generating.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromItems({
      lessonLinks: [{ lessonId: this.generateLessonId.trim(), role: 'Primary' }],
      exerciseLinks: [{ exerciseId: this.generateExerciseId.trim(), role: 'PrimaryPractice' }],
      title: this.generateTitle.trim() || null,
    }).subscribe({
      next: () => {
        this.generating.set(false);
        this.generateModalOpen.set(false);
        this.actionSuccess.set('Module draft generated — pending review.');
        this.loadAll();
      },
      error: err => {
        this.generating.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate a Module.');
      },
    });
  }

  /** Phase J3 — loads this Module's linked Lesson + Exercise for rendering, regardless of the
   *  Module's own review status (the whole point is previewing BEFORE approval). Never creates
   *  a LearningActivity/attempt — read/score-only, separate from the real student runtime. */
  openPreview(item: ModuleDto): void {
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewData.set(null);
    this.previewSchema.set(null);
    this.previewResult.set(null);
    this.previewModalOpen.set(true);

    this.moduleSvc.preview(item.id).subscribe({
      next: result => {
        this.previewLoading.set(false);
        this.previewData.set(result);
        if (result.exercise?.formSchemaJson) {
          try {
            this.previewSchema.set(JSON.parse(result.exercise.formSchemaJson));
          } catch {
            this.previewSchema.set(null);
          }
        }
      },
      error: err => {
        this.previewLoading.set(false);
        this.previewError.set(err.error?.error ?? 'Could not load the preview.');
      },
    });
  }

  closePreview(): void {
    this.previewModalOpen.set(false);
  }

  /** Fired by the FormioRendererComponent's (submit) output with the submission's data object
   *  (component key -> submitted value). Scores it using the same engine the real student runtime
   *  uses — never a simplified/separate scoring path. */
  onPreviewExerciseSubmit(answers: Record<string, unknown>): void {
    const module = this.previewData();
    if (!module) return;

    this.previewSubmitting.set(true);
    this.previewError.set('');
    this.moduleSvc.previewSubmit(module.moduleId, { answers }).subscribe({
      next: result => {
        this.previewSubmitting.set(false);
        this.previewResult.set(result);
      },
      error: err => {
        this.previewSubmitting.set(false);
        this.previewError.set(err.error?.error ?? 'Could not score the preview submission.');
      },
    });
  }
}
