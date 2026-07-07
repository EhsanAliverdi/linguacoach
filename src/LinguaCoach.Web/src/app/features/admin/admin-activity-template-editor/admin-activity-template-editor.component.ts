import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminActivityTemplateService } from '../../../core/services/admin-activity-template.service';
import {
  AdminActivityTemplateDto,
  ActivityTemplateCreateRequest,
  ActivityTemplateUpdateRequest,
  ACTIVITY_TEMPLATE_SKILLS,
  ACTIVITY_TEMPLATE_CEFR_LEVELS,
} from '../../../core/models/admin-activity-template.models';
import { FormioBuilderComponent } from '../../../shared/formio/formio-builder.component';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
} from '../../../design-system/admin';
import { SpAdminTextareaComponent } from '../../../design-system/admin/components/textarea/sp-admin-textarea.component';

const EMPTY_SCHEMA = { display: 'form', components: [] };

/**
 * Dedicated activity template designer page — Phase 4 of the AI bank-first teaching
 * architecture (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md). Hand-authored
 * only: no AI generation wires into this page yet. FormIoBaseSchemaJson is student-safe;
 * ScoringModelJson/ValidationRulesJson/GenerationInstructions are backend-only free-text JSON
 * fields authored directly by the admin (unlike placement items, templates are not necessarily
 * single-answer quiz content, so there is no Quiz-tab annotation split here).
 */
@Component({
  selector: 'app-admin-activity-template-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
    FormioBuilderComponent,
    FormioRendererComponent,
  ],
  templateUrl: './admin-activity-template-editor.component.html',
})
export class AdminActivityTemplateEditorComponent implements OnInit {
  templateId!: string;
  isNew = false;

  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  previewOpen = signal(false);

  aiPreviewOpen = signal(false);
  aiPreviewLoading = signal(false);
  aiPreviewError = signal('');
  aiPreviewSchema = signal<any>(null);
  aiPreviewMeta = signal<{ providerName: string; modelName: string } | null>(null);

  reviewStatus = signal<string>('NotRequired');
  isPublished = signal(false);
  versionNumber = signal(1);

  templateForm = this.emptyTemplateForm();

  formioSchema = signal<any>({ ...EMPTY_SCHEMA });

  readonly formSkillOptions = ACTIVITY_TEMPLATE_SKILLS.map(s => ({ value: s, label: s }));
  readonly cefrLevelOptions = ACTIVITY_TEMPLATE_CEFR_LEVELS.map(l => ({ value: l, label: l }));

  constructor(
    private svc: AdminActivityTemplateService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.templateId = this.route.snapshot.paramMap.get('templateId') ?? 'new';
    this.isNew = this.templateId === 'new';

    if (this.isNew) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.svc.get(this.templateId).subscribe({
      next: template => {
        this.loadTemplate(template);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.status === 404 ? 'Activity template not found.' : (err.error?.error ?? 'Could not load activity template.'));
      },
    });
  }

  private loadTemplate(t: AdminActivityTemplateDto): void {
    this.templateForm = {
      key: t.key,
      skill: t.skill,
      cefrLevel: t.cefrLevel,
      activityType: t.activityType,
      subskill: t.subskill,
      patternKey: t.patternKey,
      contextTagsJson: t.contextTagsJson,
      focusTagsJson: t.focusTagsJson,
      curriculumObjectiveKey: t.curriculumObjectiveKey,
      generationInstructions: t.generationInstructions,
      scoringModelJson: t.scoringModelJson,
      validationRulesJson: t.validationRulesJson,
      estimatedDurationSeconds: t.estimatedDurationSeconds,
      assetRequirementsJson: t.assetRequirementsJson,
    };
    this.reviewStatus.set(t.reviewStatus);
    this.isPublished.set(t.isPublished);
    this.versionNumber.set(t.versionNumber);
    this.formioSchema.set(t.formIoBaseSchemaJson ? this.tryParse(t.formIoBaseSchemaJson) : { ...EMPTY_SCHEMA });
  }

  private tryParse(json: string): any {
    try {
      return JSON.parse(json) ?? { ...EMPTY_SCHEMA };
    } catch {
      return { ...EMPTY_SCHEMA };
    }
  }

  onFormioSchemaChange(schema: any): void {
    this.formioSchema.set(schema);
  }

  openPreview(): void {
    this.previewOpen.set(true);
  }

  closePreview(): void {
    this.previewOpen.set(false);
  }

  saveTemplate(): void {
    this.actionError.set('');

    const formIoBaseSchemaJson = JSON.stringify(this.formioSchema());

    if (this.isNew) {
      const request: ActivityTemplateCreateRequest = { ...this.templateForm, formIoBaseSchemaJson };
      this.svc.add(request).subscribe({
        next: () => this.router.navigate(['/admin/activity-templates']),
        error: err => this.actionError.set(err.error?.error ?? 'Could not save template.'),
      });
      return;
    }

    const { key, ...rest } = this.templateForm;
    const request: ActivityTemplateUpdateRequest = { ...rest, formIoBaseSchemaJson };
    this.svc.update(this.templateId, request).subscribe({
      next: () => this.router.navigate(['/admin/activity-templates']),
      error: err => this.actionError.set(err.error?.error ?? 'Could not save template.'),
    });
  }

  approve(): void {
    this.runReviewAction(() => this.svc.setReviewStatus(this.templateId, { action: 'approve' }));
  }

  reject(): void {
    const reason = window.prompt('Reason for rejecting this template:');
    if (!reason) return;
    this.runReviewAction(() => this.svc.setReviewStatus(this.templateId, { action: 'reject', reason }));
  }

  resetToPendingReview(): void {
    this.runReviewAction(() => this.svc.setReviewStatus(this.templateId, { action: 'reset' }));
  }

  togglePublished(): void {
    this.actionError.set('');
    this.svc.setPublished(this.templateId, { publish: !this.isPublished() }).subscribe({
      next: t => { this.isPublished.set(t.isPublished); this.reviewStatus.set(t.reviewStatus); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not update publish state.'),
    });
  }

  generateAiPreview(): void {
    this.aiPreviewError.set('');
    this.aiPreviewSchema.set(null);
    this.aiPreviewMeta.set(null);
    this.aiPreviewLoading.set(true);
    this.aiPreviewOpen.set(true);

    this.svc.generatePreview(this.templateId, {}).subscribe({
      next: result => {
        this.aiPreviewLoading.set(false);
        this.aiPreviewSchema.set(this.tryParse(result.generatedSchemaJson));
        this.aiPreviewMeta.set({ providerName: result.providerName, modelName: result.modelName });
      },
      error: err => {
        this.aiPreviewLoading.set(false);
        this.aiPreviewError.set(err.error?.error ?? 'Could not generate AI preview.');
      },
    });
  }

  closeAiPreview(): void {
    this.aiPreviewOpen.set(false);
  }

  private runReviewAction(action: () => Observable<AdminActivityTemplateDto>): void {
    this.actionError.set('');
    action().subscribe({
      next: t => { this.reviewStatus.set(t.reviewStatus); this.isPublished.set(t.isPublished); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not update review status.'),
    });
  }

  private emptyTemplateForm(): ActivityTemplateCreateRequest {
    return {
      key: '',
      skill: 'speaking',
      cefrLevel: 'A1',
      activityType: '',
      contextTagsJson: '[]',
      focusTagsJson: '[]',
    };
  }
}
