import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  AdminOnboardingFlowSummary,
  AdminOnboardingFlowDto,
  AdminOnboardingStepDto,
  AdminOnboardingCategoryDto,
  StepRequest,
  CategoryRequest,
  STEP_TYPES,
  REQUIREMENT_TYPES,
  ANSWER_MAPPINGS,
} from '../../../core/models/admin-onboarding.models';
import { QuestionContent } from '../../../shared/question/question-content.models';
import { QuestionEditorComponent, EditableQuestionType } from '../../../shared/question/question-editor.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

function emptyQuestionContent(): QuestionContent {
  return { type: 'single_choice', id: 'q1', questionText: '', choices: [{ key: 'A', label: '' }, { key: 'B', label: '' }] };
}

@Component({
  selector: 'app-admin-onboarding',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
    QuestionEditorComponent,
  ],
  templateUrl: './admin-onboarding.component.html',
})
export class AdminOnboardingComponent implements OnInit {
  flows = signal<AdminOnboardingFlowSummary[]>([]);
  activeFlow = signal<AdminOnboardingFlowDto | null>(null);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  slideOverOpen = signal(false);
  editingStep = signal<AdminOnboardingStepDto | null>(null);
  stepForm: StepRequest = this.emptyStepForm();

  categorySlideOverOpen = signal(false);
  editingCategory = signal<AdminOnboardingCategoryDto | null>(null);
  categoryForm: CategoryRequest = this.emptyCategoryForm();

  /** Step types the shared QuestionEditorComponent can author for onboarding — profile-capture
   * questions only (no passage/audio groups, those are placement-specific). */
  readonly questionEditorTypes: EditableQuestionType[] = ['single_choice', 'multiple_choice', 'free_text'];
  questionContent = signal<QuestionContent>(emptyQuestionContent());

  readonly stepTypeOptions = STEP_TYPES.map(t => ({ value: t, label: t }));
  readonly requirementTypeOptions = REQUIREMENT_TYPES.map(t => ({ value: t, label: t }));
  readonly answerMappingOptions = ANSWER_MAPPINGS.map(t => ({ value: t, label: t }));

  readonly activeFlowSummary = computed(() =>
    this.flows().find(f => f.isActive) ?? null
  );

  readonly categories = computed(() => this.activeFlow()?.categories ?? []);
  readonly categoryOptions = computed(() => this.categories().map(c => ({ value: c.categoryId, label: c.name })));

  readonly totalSteps = computed(() => this.activeFlow()?.steps.length ?? 0);
  readonly enabledSteps = computed(() => this.activeFlow()?.steps.filter(s => s.isEnabled).length ?? 0);

  readonly stepColumns = [
    { key: 'stepOrder', label: '#', width: '48px' },
    { key: 'stepKey', label: 'Key' },
    { key: 'title', label: 'Title' },
    { key: 'category', label: 'Category' },
    { key: 'stepType', label: 'Type' },
    { key: 'requirementType', label: 'Required?' },
    { key: 'answerMapping', label: 'Mapping' },
    { key: 'isEnabled', label: 'Enabled' },
    { key: '_actions', label: '' },
  ];

  readonly categoryColumns = [
    { key: 'categoryOrder', label: '#', width: '48px' },
    { key: 'name', label: 'Name' },
    { key: 'description', label: 'Description' },
    { key: 'isEnabled', label: 'Enabled' },
    { key: '_actions', label: '' },
  ];

  constructor(private svc: AdminOnboardingService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.listFlows().subscribe({
      next: flows => {
        this.flows.set(flows);
        const active = flows.find(f => f.isActive);
        if (active) {
          this.svc.getActiveFlow().subscribe({
            next: flow => { this.activeFlow.set(flow); this.loading.set(false); },
            error: err => { this.activeFlow.set(null); this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load active flow detail.'); },
          });
        } else {
          this.activeFlow.set(null);
          this.loading.set(false);
        }
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load onboarding flows.'); },
    });
  }

  categoryName(categoryId: string | null | undefined): string {
    if (!categoryId) return '—';
    return this.categories().find(c => c.categoryId === categoryId)?.name ?? '—';
  }

  // ── Steps ──────────────────────────────────────────────────────────────────

  openAddStep(): void {
    this.editingStep.set(null);
    this.stepForm = { ...this.emptyStepForm(), categoryId: this.categories()[0]?.categoryId ?? null };
    this.questionContent.set(emptyQuestionContent());
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  openEditStep(step: AdminOnboardingStepDto): void {
    this.editingStep.set(step);
    this.stepForm = {
      stepKey: step.stepKey,
      title: step.title,
      description: step.description,
      stepType: step.stepType,
      requirementType: step.requirementType,
      answerMapping: step.answerMapping,
      stepOrder: step.stepOrder,
      isEnabled: step.isEnabled,
      options: step.options ? [...step.options] : null,
      categoryId: step.categoryId ?? null,
    };
    this.questionContent.set(step.content ?? emptyQuestionContent());
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  isGenericQuestionType(stepType: string): boolean {
    return stepType === 'SingleChoice' || stepType === 'MultipleChoice' || stepType === 'FreeText';
  }

  onStepTypeChange(stepType: string): void {
    this.stepForm = { ...this.stepForm, stepType };
    if (this.isGenericQuestionType(stepType)) {
      this.questionContent.set(emptyQuestionContent());
    }
  }

  onQuestionContentChange(content: QuestionContent): void {
    this.questionContent.set(content);
  }

  closeSlideOver(): void {
    this.slideOverOpen.set(false);
    this.editingStep.set(null);
  }

  saveStep(): void {
    const flow = this.activeFlow();
    if (!flow) return;
    this.actionError.set('');

    const request: StepRequest = {
      ...this.stepForm,
      content: this.isGenericQuestionType(this.stepForm.stepType) ? this.questionContent() : null,
    };

    const editing = this.editingStep();
    const obs = editing
      ? this.svc.updateStep(flow.flowId, editing.stepKey, request)
      : this.svc.addStep(flow.flowId, request);

    obs.subscribe({
      next: () => {
        this.actionSuccess.set(editing ? 'Step updated.' : 'Step added.');
        this.slideOverOpen.set(false);
        this.loadAll();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save step.'),
    });
  }

  removeStep(step: AdminOnboardingStepDto): void {
    const flow = this.activeFlow();
    if (!flow) return;
    this.actionError.set('');
    this.svc.removeStep(flow.flowId, step.stepKey).subscribe({
      next: () => { this.actionSuccess.set('Step removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove step.'),
    });
  }

  // ── Categories ─────────────────────────────────────────────────────────────

  openAddCategory(): void {
    this.editingCategory.set(null);
    this.categoryForm = { ...this.emptyCategoryForm(), categoryOrder: this.categories().length + 1 };
    this.actionError.set('');
    this.actionSuccess.set('');
    this.categorySlideOverOpen.set(true);
  }

  openEditCategory(category: AdminOnboardingCategoryDto): void {
    this.editingCategory.set(category);
    this.categoryForm = {
      name: category.name,
      description: category.description,
      categoryOrder: category.categoryOrder,
      isEnabled: category.isEnabled,
    };
    this.actionError.set('');
    this.actionSuccess.set('');
    this.categorySlideOverOpen.set(true);
  }

  closeCategorySlideOver(): void {
    this.categorySlideOverOpen.set(false);
    this.editingCategory.set(null);
  }

  saveCategory(): void {
    const flow = this.activeFlow();
    if (!flow) return;
    this.actionError.set('');

    const editing = this.editingCategory();
    const obs = editing
      ? this.svc.updateCategory(flow.flowId, editing.categoryId, this.categoryForm)
      : this.svc.addCategory(flow.flowId, this.categoryForm);

    obs.subscribe({
      next: () => {
        this.actionSuccess.set(editing ? 'Category updated.' : 'Category added.');
        this.categorySlideOverOpen.set(false);
        this.loadAll();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save category.'),
    });
  }

  removeCategory(category: AdminOnboardingCategoryDto): void {
    const flow = this.activeFlow();
    if (!flow) return;
    this.actionError.set('');
    this.svc.removeCategory(flow.flowId, category.categoryId).subscribe({
      next: () => { this.actionSuccess.set('Category removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove category.'),
    });
  }

  activateFlow(flowId: string): void {
    this.actionError.set('');
    this.svc.activateFlow(flowId).subscribe({
      next: () => { this.actionSuccess.set('Flow activated.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not activate flow.'),
    });
  }

  stepTone(step: AdminOnboardingStepDto): 'success' | 'neutral' {
    return step.isEnabled ? 'success' : 'neutral';
  }

  categoryTone(category: AdminOnboardingCategoryDto): 'success' | 'neutral' {
    return category.isEnabled ? 'success' : 'neutral';
  }

  private emptyStepForm(): StepRequest {
    return {
      stepKey: '',
      title: '',
      description: null,
      stepType: 'Welcome',
      requirementType: 'AdminConfigured',
      answerMapping: 'None',
      stepOrder: 1,
      isEnabled: true,
      options: null,
      categoryId: null,
    };
  }

  private emptyCategoryForm(): CategoryRequest {
    return { name: '', description: null, categoryOrder: 1, isEnabled: true };
  }
}
