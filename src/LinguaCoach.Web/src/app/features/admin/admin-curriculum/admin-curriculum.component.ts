import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, signal, computed } from '@angular/core';
import {
  CurriculumService,
  AdminCurriculumObjectiveDto,
  CurriculumTaxonomyDto,
  AdminCurriculumObjectiveUpsertRequest,
  AdminRoutingPreviewRequest,
  AdminRoutingPreviewResult,
  CurriculumValidationSummaryDto,
  CurriculumCoverageMatrixDto,
} from '../../../core/services/curriculum.service';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminTableComponent,
  SpAdminTableActionsComponent,
  SpAdminTableFooterComponent,
  SpAdminPaginationComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminSelectOption, SpAdminRowAction } from '../../../design-system/admin';
import {
  SpAdminDistributionBreakdownComponent,
  SpAdminDistributionItem,
  SpAdminDistributionTone,
} from '../../../design-system/admin/components/distribution-breakdown/sp-admin-distribution-breakdown.component';
import { SpAdminAlertComponent } from '../../../design-system/admin/components/alert/sp-admin-alert.component';

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json || json === '[]') return [];
  try { return JSON.parse(json); } catch { return []; }
}

const CEFR_ORDER = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];
const CEFR_TONES: SpAdminDistributionTone[] = ['green', 'teal', 'indigo', 'violet', 'orange', 'amber'];

@Component({
  selector: 'app-admin-curriculum',
  standalone: true,
  templateUrl: './admin-curriculum.component.html',
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminButtonGroupComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminTableActionsComponent,
    SpAdminTableFooterComponent,
    SpAdminPaginationComponent,
    SpAdminTextareaComponent,
    SpAdminDistributionBreakdownComponent,
    SpAdminAlertComponent,
  ],
})
export class AdminCurriculumComponent implements OnInit {
  objectives        = signal<AdminCurriculumObjectiveDto[]>([]);
  allObjectives     = signal<AdminCurriculumObjectiveDto[]>([]);
  taxonomy          = signal<CurriculumTaxonomyDto | null>(null);
  loading           = signal(false);
  saving            = signal(false);
  previewing        = signal(false);
  actionKey         = signal<string | null>(null);
  globalError       = signal<string | null>(null);
  formError         = signal<string | null>(null);
  previewResult     = signal<AdminRoutingPreviewResult | null>(null);
  slideOverOpen     = signal(false);
  previewOpen       = signal(false);
  editMode          = signal<'create' | 'edit'>('create');
  validationSummary = signal<CurriculumValidationSummaryDto | null>(null);
  loadingValidation = signal(false);
  coverageMatrix    = signal<CurriculumCoverageMatrixDto | null>(null);
  loadingCoverage   = signal(false);

  filterCefr          = '';
  filterSkill         = '';
  filterActive        = 'true';
  focusTagsRaw        = '';
  prerequisiteKeysRaw = '';

  readonly cefrOptions = computed<SpAdminSelectOption[]>(() =>
    (this.taxonomy()?.cefrLevels ?? []).map(l => ({ value: l, label: l }))
  );
  readonly skillOptions = computed<SpAdminSelectOption[]>(() =>
    (this.taxonomy()?.skills ?? []).map(s => ({ value: s, label: s }))
  );
  readonly activeOptions: SpAdminSelectOption[] = [
    { value: 'true',  label: 'Active only' },
    { value: 'false', label: 'Inactive only' },
  ];
  readonly sourceOptions: SpAdminSelectOption[] = [
    { value: 'admin_preview', label: 'admin_preview' },
    { value: 'today_lesson',  label: 'today_lesson' },
    { value: 'practice_gym',  label: 'practice_gym' },
    { value: 'on_demand',     label: 'on_demand' },
  ];
  readonly difficultyOptions: SpAdminSelectOption[] = [
    { value: 'gentle',      label: 'Gentle' },
    { value: 'challenging', label: 'Challenging' },
  ];

  readonly objectivesPageSize = 10;
  objectivesPage = signal(1);
  readonly objectivesTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.objectives().length / this.objectivesPageSize))
  );
  readonly objectivesPaged = computed(() => {
    const start = (this.objectivesPage() - 1) * this.objectivesPageSize;
    return this.objectives().slice(start, start + this.objectivesPageSize);
  });
  onObjectivesPageChange(page: number): void { this.objectivesPage.set(page); }

  readonly coverageSummary = computed(() => {
    const all = this.allObjectives();
    return {
      total:     all.length,
      active:    all.filter(o => o.isActive).length,
      cefrBands: new Set(all.map(o => o.cefrLevel)).size,
      skills:    new Set(all.map(o => o.primarySkill)).size,
    };
  });

  readonly cefrDistributionItems = computed<SpAdminDistributionItem[]>(() => {
    const all   = this.allObjectives();
    const total = all.length || 1;
    const counts: Record<string, number> = {};
    for (const o of all) counts[o.cefrLevel] = (counts[o.cefrLevel] ?? 0) + 1;
    return CEFR_ORDER
      .filter(l => counts[l])
      .map((l, i) => ({
        key:     l,
        label:   l,
        value:   counts[l],
        percent: Math.round((counts[l] / total) * 100),
        tone:    CEFR_TONES[i % CEFR_TONES.length],
      }));
  });

  readonly formActions = computed(() => [
    {
      id: 'save', label: this.editMode() === 'create' ? 'Create' : 'Save changes',
      variant: 'primary' as const, loading: this.saving(), disabled: this.saving(),
    },
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  readonly previewActions = computed(() => [
    {
      id: 'run', label: 'Run preview',
      variant: 'primary' as const, loading: this.previewing(), disabled: this.previewing(),
    },
    { id: 'close', label: 'Close', variant: 'neutral' as const, appearance: 'outline' as const },
  ]);

  form: AdminCurriculumObjectiveUpsertRequest = this.emptyForm();
  preview: AdminRoutingPreviewRequest = { allowReviewOrScaffold: false, source: 'admin_preview' };

  readonly parseJsonArray = parseJsonArray;

  constructor(private curriculum: CurriculumService) {}

  ngOnInit(): void {
    this.loadTaxonomy();
    this.load();
    this.loadAll();
    this.loadValidation();
    this.loadCoverage();
  }

  load(): void {
    this.loading.set(true);
    this.globalError.set(null);
    const active = this.filterActive === '' ? undefined : this.filterActive === 'true';
    this.curriculum.listObjectives(
      this.filterCefr  || undefined,
      this.filterSkill || undefined,
      active,
    ).subscribe({
      next:  items => { this.objectives.set(items); this.objectivesPage.set(1); this.loading.set(false); },
      error: ()    => { this.globalError.set('Could not load objectives.'); this.loading.set(false); },
    });
  }

  loadAll(): void {
    this.curriculum.listObjectives(undefined, undefined, undefined).subscribe({
      next: items => this.allObjectives.set(items),
    });
  }

  loadTaxonomy(): void {
    this.curriculum.getTaxonomy().subscribe({
      next: tax => this.taxonomy.set(tax),
    });
  }

  loadValidation(): void {
    this.loadingValidation.set(true);
    this.curriculum.getValidationSummary().subscribe({
      next:  summary => { this.validationSummary.set(summary); this.loadingValidation.set(false); },
      error: ()      => { this.loadingValidation.set(false); },
    });
  }

  loadCoverage(): void {
    this.loadingCoverage.set(true);
    this.curriculum.getCoverageMatrix().subscribe({
      next:  matrix => { this.coverageMatrix.set(matrix); this.loadingCoverage.set(false); },
      error: ()     => { this.loadingCoverage.set(false); },
    });
  }

  startCreate(): void {
    this.form = this.emptyForm();
    this.focusTagsRaw        = '';
    this.prerequisiteKeysRaw = '';
    this.formError.set(null);
    this.editMode.set('create');
    this.slideOverOpen.set(true);
  }

  startEdit(obj: AdminCurriculumObjectiveDto): void {
    this.form = {
      key:                       obj.key,
      title:                     obj.title,
      description:               obj.description,
      cefrLevel:                 obj.cefrLevel,
      primarySkill:              obj.primarySkill,
      secondarySkills:           parseJsonArray(obj.secondarySkillsJson),
      contextTags:               parseJsonArray(obj.contextTagsJson),
      focusTags:                 parseJsonArray(obj.focusTagsJson),
      prerequisiteObjectiveKeys: parseJsonArray(obj.prerequisiteKeysJson),
      recommendedOrder:          obj.recommendedOrder,
      difficultyBand:            obj.difficultyBand,
      isActive:                  obj.isActive,
      isReviewable:              obj.isReviewable,
      isExamInspired:            obj.isExamInspired,
      teachingNotes:             obj.teachingNotes,
      examplePrompts:            obj.examplePrompts,
    };
    this.focusTagsRaw        = this.form.focusTags.join(', ');
    this.prerequisiteKeysRaw = this.form.prerequisiteObjectiveKeys.join(', ');
    this.formError.set(null);
    this.editMode.set('edit');
    this.slideOverOpen.set(true);
  }

  closeSlideOver(): void {
    this.slideOverOpen.set(false);
    this.formError.set(null);
  }

  openPreview(): void {
    this.previewResult.set(null);
    this.previewOpen.set(true);
  }

  save(): void {
    this.form.focusTags                 = this.focusTagsRaw.split(',').map(s => s.trim()).filter(Boolean);
    this.form.prerequisiteObjectiveKeys = this.prerequisiteKeysRaw.split(',').map(s => s.trim()).filter(Boolean);
    this.saving.set(true);
    this.formError.set(null);
    const obs = this.editMode() === 'create'
      ? this.curriculum.createObjective(this.form)
      : this.curriculum.updateObjective(this.form.key, this.form);
    obs.subscribe({
      next:  () => { this.saving.set(false); this.closeSlideOver(); this.load(); this.loadAll(); },
      error: (err) => { this.saving.set(false); this.formError.set(err?.error?.error ?? 'Could not save objective.'); },
    });
  }

  activate(key: string): void {
    this.actionKey.set(key);
    this.curriculum.activateObjective(key).subscribe({
      next:  updated => { this.objectives.update(items => items.map(o => o.key === key ? updated : o)); this.actionKey.set(null); },
      error: ()      => { this.globalError.set('Could not activate objective.'); this.actionKey.set(null); },
    });
  }

  deactivate(key: string): void {
    this.actionKey.set(key);
    this.curriculum.deactivateObjective(key).subscribe({
      next:  updated => { this.objectives.update(items => items.map(o => o.key === key ? updated : o)); this.actionKey.set(null); },
      error: ()      => { this.globalError.set('Could not deactivate objective.'); this.actionKey.set(null); },
    });
  }

  rowActions(obj: AdminCurriculumObjectiveDto): SpAdminRowAction[] {
    return [
      { id: 'edit',   label: 'Edit',
        icon: 'edit', tone: 'default' },
      { id: 'toggle', label: obj.isActive ? 'Deactivate' : 'Activate',
        icon: obj.isActive ? 'deactivate' : 'activate',
        tone: obj.isActive ? 'danger' : 'default',
        disabled: this.actionKey() === obj.key },
    ];
  }

  onRowAction(actionId: string, obj: AdminCurriculumObjectiveDto): void {
    if (actionId === 'edit')   { this.startEdit(obj); return; }
    if (actionId === 'toggle') { obj.isActive ? this.deactivate(obj.key) : this.activate(obj.key); }
  }

  onFormAction(actionId: string): void {
    if (actionId === 'save')   this.save();
    if (actionId === 'cancel') this.closeSlideOver();
  }

  onPreviewAction(actionId: string): void {
    if (actionId === 'run')   this.runPreview();
    if (actionId === 'close') this.previewOpen.set(false);
  }

  runPreview(): void {
    this.previewing.set(true);
    this.previewResult.set(null);
    this.curriculum.previewRouting(this.preview).subscribe({
      next:  result => { this.previewResult.set(result); this.previewing.set(false); },
      error: ()     => { this.previewing.set(false); },
    });
  }

  onTagChecked(field: 'contextTags' | 'secondarySkills', value: string, checked: boolean): void {
    if (checked) {
      if (!this.form[field].includes(value)) this.form[field] = [...this.form[field], value];
    } else {
      this.form[field] = this.form[field].filter(t => t !== value);
    }
  }

  cefrTone(level: string): 'success' | 'info' | 'primary' | 'warning' | 'danger' | 'neutral' {
    if (level === 'A1' || level === 'A2') return 'success';
    if (level === 'B1' || level === 'B2') return 'primary';
    return 'warning';
  }

  private emptyForm(): AdminCurriculumObjectiveUpsertRequest {
    return {
      key: '', title: '', description: '',
      cefrLevel: 'A1', primarySkill: 'speaking',
      secondarySkills: [], contextTags: ['general_english'], focusTags: [],
      prerequisiteObjectiveKeys: [],
      recommendedOrder: 0, difficultyBand: 1,
      isActive: true, isReviewable: false, isExamInspired: false,
      teachingNotes: null, examplePrompts: null,
    };
  }
}
