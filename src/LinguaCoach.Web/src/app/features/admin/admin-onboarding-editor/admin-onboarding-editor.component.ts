import { Component, OnInit, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  StudentFlowTemplateDetailDto,
  StudentFlowTemplateVersionDto,
} from '../../../core/models/admin-onboarding.models';
import { FormioBuilderComponent } from '../../../shared/formio/formio-builder.component';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import { OnboardingWizardComponent } from '../../student/onboarding/onboarding-wizard/onboarding-wizard.component';
import { FormRendererKind } from '../../../shared/formio/form-renderer-kind.model';
import { countScoredComponents, finalizeQuizAnnotations } from '../../../shared/formio/quiz-scoring-rule.model';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
} from '../../../design-system/admin';

const EMPTY_SCHEMA = { display: 'form', components: [] };

/**
 * Dedicated onboarding template designer page (own route, own full-width canvas) —
 * split out from the templates list so the Form.io builder isn't squeezed under a table.
 */
@Component({
  selector: 'app-admin-onboarding-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    FormioBuilderComponent,
    FormioRendererComponent,
    OnboardingWizardComponent,
  ],
  templateUrl: './admin-onboarding-editor.component.html',
})
export class AdminOnboardingEditorComponent implements OnInit {
  @ViewChild(FormioBuilderComponent) builderRef?: FormioBuilderComponent;

  templateId!: string;

  template = signal<StudentFlowTemplateDetailDto | null>(null);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  /** Current working schema object bound to the builder — seeded from the current draft version,
   * updated live on every builder change event. */
  draftSchema = signal<any>(EMPTY_SCHEMA);

  /** True when this draft has scoring but was authored before the Quiz tab existed — its schema
   * carries no quiz annotations yet, so every question shows as "not scored" until re-saved. */
  needsReauthoring = signal(false);

  readonly scoredSummary = computed(() => countScoredComponents(this.draftSchema()));

  /** Which engine renders this template for students — kept in sync with the loaded draft and
   * included on every save, so Preview shows exactly what the student will see. */
  rendererKind = signal<FormRendererKind>('FormIo');

  previewOpen = signal(false);

  readonly draftVersion = computed<StudentFlowTemplateVersionDto | null>(() => {
    const t = this.template();
    if (!t) return null;
    // `versions` is always sorted newest-first by the backend (OrderByDescending VersionNumber) —
    // versions[0] is the latest version, not versions[length - 1].
    return t.versions.find(v => v.status === 'Draft') ?? t.versions[0] ?? null;
  });

  constructor(
    private svc: AdminOnboardingService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.templateId = this.route.snapshot.paramMap.get('templateId')!;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.getTemplate(this.templateId).subscribe({
      next: detail => {
        this.applyDetail(detail);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load onboarding template.');
      },
    });
  }

  private applyDetail(detail: StudentFlowTemplateDetailDto): void {
    this.template.set(detail);
    // `versions` is always sorted newest-first by the backend — versions[0] is the latest.
    const draft = detail.versions.find(v => v.status === 'Draft') ?? detail.versions[0] ?? null;
    this.needsReauthoring.set(!!draft && !draft.authoringSchemaJson && !!draft.scoringRulesJson);
    const seedJson = draft?.authoringSchemaJson ?? draft?.formIoSchemaJson;
    const schema = seedJson ? this.tryParse(seedJson) : EMPTY_SCHEMA;
    this.draftSchema.set(schema);
    this.rendererKind.set(draft?.rendererKind ?? 'FormIo');
  }

  private tryParse(json: string): any {
    try {
      return JSON.parse(json) ?? EMPTY_SCHEMA;
    } catch {
      return EMPTY_SCHEMA;
    }
  }

  onBuilderSchemaChange(schema: any): void {
    this.draftSchema.set(schema);
  }

  saveDraft(): void {
    this.actionError.set('');
    const schema = this.builderRef ? this.builderRef.getSchema() : this.draftSchema();
    const authoringSchema = finalizeQuizAnnotations(schema);
    this.svc.saveDraft(this.templateId, {
      formIoSchemaJson: JSON.stringify(schema),
      authoringSchemaJson: JSON.stringify(authoringSchema),
      rendererKind: this.rendererKind(),
    }).subscribe({
      next: () => {
        this.actionSuccess.set('Draft saved.');
        this.load();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save draft.'),
    });
  }

  publish(): void {
    this.actionError.set('');
    this.svc.publish(this.templateId).subscribe({
      next: () => {
        this.actionSuccess.set('Template published.');
        this.load();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not publish template.'),
    });
  }

  archive(): void {
    this.actionError.set('');
    this.svc.archive(this.templateId).subscribe({
      next: () => this.router.navigate(['/admin/onboarding']),
      error: err => this.actionError.set(err.error?.error ?? 'Could not archive template.'),
    });
  }

  openPreview(): void {
    if (this.builderRef) this.draftSchema.set(this.builderRef.getSchema());
    this.previewOpen.set(true);
  }

  closePreview(): void {
    this.previewOpen.set(false);
  }

  statusTone(status: string): 'success' | 'neutral' | 'warning' {
    if (status === 'Published') return 'success';
    if (status === 'Draft') return 'warning';
    return 'neutral';
  }
}
