import { Component, OnInit, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import {
  ModuleDto,
  ModulePreviewResult,
  ModulePreviewSubmitResult,
  MODULE_REVIEW_STATUSES,
} from '../../../core/models/admin-module.models';
import { LessonDto } from '../../../core/models/admin-lesson.models';
import { ExerciseDto } from '../../../core/models/admin-exercise.models';
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

  // ── Phase J3 — Preview as Learner modal ─────────────────────────────────
  previewModalOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  previewData = signal<ModulePreviewResult | null>(null);
  previewSchema = signal<any>(null);
  previewSubmitting = signal(false);
  previewResult = signal<ModulePreviewSubmitResult | null>(null);

  /** The generated Form.io schemas (gap_fill/multiple_choice_single, both deterministic and
   *  AI-generated) don't include their own submit button component, and this app's convention
   *  (see PlacementComponent) is for the host page to trigger submission externally via
   *  FormioRendererComponent.submitForm() rather than relying on Form.io to render one. */
  @ViewChild(FormioRendererComponent) previewFormioRenderer?: FormioRendererComponent;

  constructor(
    private moduleSvc: AdminModuleService,
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private route: ActivatedRoute,
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

  /** Triggers Form.io's own submission pipeline (validation + the 'submit' event)
   *  programmatically, since the rendered schema has no submit button of its own. */
  submitPreviewAnswer(): void {
    this.previewFormioRenderer?.submitForm();
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
