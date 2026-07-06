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
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const EMPTY_SCHEMA = { display: 'form', components: [] };

/** Wraps any existing flat components in a single wizard page when switching from single-page
 *  to multi-step display, so no authored fields are lost. */
function ensureWizardHasPage(schema: any): any {
  const comps: any[] = Array.isArray(schema?.components) ? schema.components : [];
  const hasPanel = comps.some((c: any) => c?.type === 'panel');
  if (hasPanel) return schema;
  return {
    ...schema,
    components: [{
      type: 'panel', breadcrumb: 'Page 1', title: 'Page 1',
      label: 'Page 1', key: 'page1', components: comps,
    }],
  };
}

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
    SpAdminTextareaComponent,
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
  scoringRulesJson = signal('');

  /** Single page vs multi-step wizard — switching requires a full builder rebuild. */
  formDisplay: 'form' | 'wizard' = 'form';

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
    const schema = draft ? this.tryParse(draft.formIoSchemaJson) : EMPTY_SCHEMA;
    this.draftSchema.set(schema);
    this.formDisplay = schema?.display === 'wizard' ? 'wizard' : 'form';
    this.rendererKind.set(draft?.rendererKind ?? 'FormIo');
    this.scoringRulesJson.set(draft?.scoringRulesJson ?? '');
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

  onDisplayChange(display: 'form' | 'wizard'): void {
    let schema = this.builderRef ? this.builderRef.getSchema() : this.draftSchema();
    schema = { ...schema, display };
    if (display === 'wizard') schema = ensureWizardHasPage(schema);
    this.draftSchema.set(schema);
    this.builderRef?.rebuild(schema);
  }

  saveDraft(): void {
    this.actionError.set('');
    const schema = this.builderRef ? this.builderRef.getSchema() : this.draftSchema();
    this.svc.saveDraft(this.templateId, {
      formIoSchemaJson: JSON.stringify(schema),
      scoringRulesJson: this.scoringRulesJson().trim() || undefined,
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
