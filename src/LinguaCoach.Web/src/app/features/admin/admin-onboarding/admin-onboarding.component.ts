import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  AdminOnboardingFlowSummary,
  AdminOnboardingFlowDto,
  AdminOnboardingStepDto,
  StepRequest,
  STEP_TYPES,
  REQUIREMENT_TYPES,
  ANSWER_MAPPINGS,
} from '../../../core/models/admin-onboarding.models';
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

  readonly stepTypeOptions = STEP_TYPES.map(t => ({ value: t, label: t }));
  readonly requirementTypeOptions = REQUIREMENT_TYPES.map(t => ({ value: t, label: t }));
  readonly answerMappingOptions = ANSWER_MAPPINGS.map(t => ({ value: t, label: t }));

  readonly activeFlowSummary = computed(() =>
    this.flows().find(f => f.isActive) ?? null
  );

  readonly totalSteps = computed(() => this.activeFlow()?.steps.length ?? 0);
  readonly enabledSteps = computed(() => this.activeFlow()?.steps.filter(s => s.isEnabled).length ?? 0);

  readonly stepColumns = [
    { key: 'stepOrder', label: '#', width: '48px' },
    { key: 'stepKey', label: 'Key' },
    { key: 'title', label: 'Title' },
    { key: 'stepType', label: 'Type' },
    { key: 'requirementType', label: 'Required?' },
    { key: 'answerMapping', label: 'Mapping' },
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

  openAddStep(): void {
    this.editingStep.set(null);
    this.stepForm = this.emptyStepForm();
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
    };
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  closeSlideOver(): void {
    this.slideOverOpen.set(false);
    this.editingStep.set(null);
  }

  saveStep(): void {
    const flow = this.activeFlow();
    if (!flow) return;
    this.actionError.set('');

    const editing = this.editingStep();
    const obs = editing
      ? this.svc.updateStep(flow.flowId, editing.stepKey, this.stepForm)
      : this.svc.addStep(flow.flowId, this.stepForm);

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
    };
  }
}
