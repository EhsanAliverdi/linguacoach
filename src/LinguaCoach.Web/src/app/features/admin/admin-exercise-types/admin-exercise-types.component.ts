import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, computed, signal } from '@angular/core';
import { AdminService } from '../../../core/services/admin.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonGroupComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCodePillComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSlideOverComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction, SpAdminSelectOption, SpAdminButtonGroupAction, SpAdminTableColumn, SpAdminTableFilter } from '../../../design-system/admin';
import {
  SpAdminDistributionBreakdownComponent,
  SpAdminDistributionItem,
  SpAdminDistributionTone,
} from '../../../design-system/admin/components/distribution-breakdown/sp-admin-distribution-breakdown.component';

const SKILL_ORDER = ['listening', 'speaking', 'vocabulary', 'writing', 'reading', 'reflection'];

const SKILL_META: Record<string, { bg: string; color: string; short: string; tone: SpAdminDistributionTone }> = {
  listening:  { bg: '#EDEBFF', color: '#5B4BE8', short: 'Li', tone: 'indigo'  },
  speaking:   { bg: '#FFEAE4', color: '#FF7A59', short: 'Sp', tone: 'orange'  },
  vocabulary: { bg: '#F2E9FF', color: '#B45CF0', short: 'Vo', tone: 'violet'  },
  writing:    { bg: '#E0F6EE', color: '#13B07C', short: 'Wr', tone: 'green'   },
  reading:    { bg: '#FFF1DC', color: '#F0982C', short: 'Re', tone: 'amber'   },
  reflection: { bg: '#F1F5F9', color: '#8B85A0', short: 'Rf', tone: 'slate'   },
};

function skillMeta(skill: string): { bg: string; color: string; short: string; tone: SpAdminDistributionTone } {
  return SKILL_META[skill?.toLowerCase()] ?? {
    bg: '#F1F5F9', color: '#8B85A0',
    short: (skill ?? '?').slice(0, 2).toUpperCase(),
    tone: 'slate',
  };
}

@Component({
  selector: 'app-admin-exercise-types',
  standalone: true,
  templateUrl: './admin-exercise-types.component.html',
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonGroupComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCodePillComponent,
    SpAdminDistributionBreakdownComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSlideOverComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTruncatedTextComponent,
  ],
})
export class AdminExerciseTypesComponent implements OnInit {
  readonly exerciseTypeColumns: SpAdminTableColumn[] = [
    { key: 'exercise', label: 'Exercise' },
    { key: 'skill', label: 'Skill' },
    { key: 'status', label: 'Status' },
    { key: 'generation', label: 'Generation' },
    { key: 'itemsMDM', label: 'Items M/D/M' },
    { key: 'optionsMDM', label: 'Options M/D/M' },
    { key: 'actions', label: '', align: 'right' },
  ];

  exerciseTypes     = signal<ExerciseTypeDefinition[]>([]);
  savingKey         = signal<string | null>(null);
  error             = signal<string | null>(null);
  loading           = signal(true);
  page              = signal(1);
  searchQuery       = signal('');
  skillFilter       = signal('');
  statusFilter      = signal('');

  configOpen  = signal(false);
  configType  = signal<ExerciseTypeDefinition | null>(null);
  configForm  = signal<Partial<ExerciseTypeDefinition>>({});
  configError = signal<string | null>(null);

  readonly pageSize = 20;

  readonly statusOptions: SpAdminSelectOption[] = [
    { value: 'enabled',         label: 'Enabled' },
    { value: 'disabled',        label: 'Disabled' },
    { value: 'ready',           label: 'Ready' },
    { value: 'not_implemented', label: 'Not implemented' },
  ];

  readonly skillOptions = computed<SpAdminSelectOption[]>(() => {
    const skills = new Set(this.exerciseTypes().map(t => t.primarySkill?.toLowerCase()).filter(Boolean) as string[]);
    const ordered = SKILL_ORDER.filter(s => skills.has(s));
    const extra   = [...skills].filter(s => !SKILL_ORDER.includes(s)).sort();
    return [...ordered, ...extra].map(s => ({ value: s, label: this.skillLabel(s) }));
  });

  readonly typeSummary = computed(() => {
    const all = this.exerciseTypes();
    return {
      total:   all.length,
      enabled: all.filter(t => t.isEnabled).length,
      ready:   all.filter(t => t.implementationStatus === 'ready').length,
      skills:  new Set(all.map(t => t.primarySkill?.toLowerCase()).filter(Boolean)).size,
    };
  });

  readonly skillDistributionItems = computed<SpAdminDistributionItem[]>(() => {
    const all   = this.exerciseTypes();
    const total = all.length || 1;
    const counts: Record<string, number> = {};
    for (const t of all) {
      const s = t.primarySkill?.toLowerCase() ?? 'unknown';
      counts[s] = (counts[s] ?? 0) + 1;
    }
    const ordered = SKILL_ORDER.filter(s => counts[s]);
    const extra   = Object.keys(counts).filter(s => !SKILL_ORDER.includes(s)).sort();
    return [...ordered, ...extra].map(s => ({
      key:     s,
      label:   this.skillLabel(s),
      value:   counts[s],
      percent: Math.round((counts[s] / total) * 100),
      tone:    skillMeta(s).tone,
    }));
  });

  readonly distributionStatusLabel = computed(() => {
    const { total, ready } = this.typeSummary();
    if (total === 0) return '';
    return ready === total ? `All ${total} types ready` : `${ready} of ${total} ready`;
  });

  readonly distributionStatusTone = computed<'green' | 'amber' | 'slate'>(() => {
    const { total, ready } = this.typeSummary();
    if (total === 0) return 'slate';
    return ready === total ? 'green' : 'amber';
  });

  readonly filteredExerciseTypes = computed(() => {
    const q      = this.searchQuery().toLowerCase().trim();
    const skill  = this.skillFilter().toLowerCase();
    const status = this.statusFilter();
    return this.exerciseTypes().filter(t => {
      if (q && !t.displayName.toLowerCase().includes(q) && !t.key.toLowerCase().includes(q)) return false;
      if (skill  && t.primarySkill?.toLowerCase() !== skill)                return false;
      if (status === 'enabled'         && !t.isEnabled)                     return false;
      if (status === 'disabled'        &&  t.isEnabled)                     return false;
      if (status === 'ready'           && t.implementationStatus !== 'ready') return false;
      if (status === 'not_implemented' && t.implementationStatus === 'ready') return false;
      return true;
    });
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredExerciseTypes().length / this.pageSize))
  );

  readonly pagedExerciseTypes = computed(() => {
    const p     = Math.min(this.page(), this.totalPages());
    const start = (p - 1) * this.pageSize;
    return this.filteredExerciseTypes().slice(start, start + this.pageSize);
  });

  readonly configActions = computed<SpAdminButtonGroupAction[]>(() => [
    {
      id: 'save', label: 'Save changes', variant: 'primary',
      loading: this.savingKey() === this.configType()?.key,
      disabled: !!this.savingKey(),
    },
    { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
  ]);

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.admin.listExerciseTypes().subscribe({
      next:  items => { this.exerciseTypes.set(items); this.page.set(1); this.loading.set(false); },
      error: ()    => { this.error.set('Could not load exercise types.'); this.loading.set(false); },
    });
  }

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  onSkillFilterChange(value: string): void  { this.skillFilter.set(value);  this.page.set(1); }
  onStatusFilterChange(value: string): void { this.statusFilter.set(value); this.page.set(1); }

  exerciseTypesFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'skill', label: 'Skill', options: this.skillOptions(), value: this.skillFilter(), placeholder: 'All skills' },
    { key: 'status', label: 'Status', options: this.statusOptions, value: this.statusFilter(), placeholder: 'All statuses' },
  ]);

  onExerciseTypesFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'skill') this.onSkillFilterChange(event.value);
    else if (event.key === 'status') this.onStatusFilterChange(event.value);
  }

  rowActions(_type: ExerciseTypeDefinition): SpAdminRowAction[] {
    return [{ id: 'configure', label: 'Configure', icon: 'edit', tone: 'default' }];
  }

  onRowAction(actionId: string, type: ExerciseTypeDefinition): void {
    if (actionId === 'configure') this.openConfig(type);
  }

  openConfig(type: ExerciseTypeDefinition): void {
    this.configType.set(type);
    this.configForm.set({ ...type });
    this.configError.set(null);
    this.configOpen.set(true);
  }

  closeConfig(): void {
    this.configOpen.set(false);
    this.configError.set(null);
  }

  onConfigAction(actionId: string): void {
    if (actionId === 'save')   this.saveConfig();
    if (actionId === 'cancel') this.closeConfig();
  }

  saveConfig(): void {
    const type = this.configType();
    const form = this.configForm() as ExerciseTypeDefinition;
    if (!type) return;
    const err = this.configCountError(form);
    if (err) { this.configError.set(err); return; }
    this.savingKey.set(type.key);
    this.configError.set(null);
    this.admin.updateExerciseType(type.key, {
      isEnabled:               form.isEnabled,
      minItemsPerPractice:     form.minItemsPerPractice,
      defaultItemsPerPractice: form.defaultItemsPerPractice,
      maxItemsPerPractice:     form.maxItemsPerPractice,
      minOptionsPerItem:       form.minOptionsPerItem,
      defaultOptionsPerItem:   form.defaultOptionsPerItem,
      maxOptionsPerItem:       form.maxOptionsPerItem,
    }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(i => i.key === updated.key ? updated : i));
        this.savingKey.set(null);
        this.closeConfig();
      },
      error: () => {
        this.savingKey.set(null);
        this.configError.set('Could not save. Please try again.');
      },
    });
  }

  configCountError(form: ExerciseTypeDefinition): string | null {
    const { minItemsPerPractice: mn, defaultItemsPerPractice: df, maxItemsPerPractice: mx,
            minOptionsPerItem: on_, defaultOptionsPerItem: od, maxOptionsPerItem: ox } = form;
    if ([mn, df, mx, on_, od, ox].some(v => v == null || v < 0)) return 'No negative values.';
    if (!(mn <= df && df <= mx))   return 'Items: min ≤ default ≤ max.';
    if (!(on_ <= od && od <= ox))  return 'Options: min ≤ default ≤ max.';
    return null;
  }

  skillLabel(skill: string): string {
    return skill ? skill.charAt(0).toUpperCase() + skill.slice(1) : '';
  }

  skillBg(skill: string):    string { return skillMeta(skill).bg; }
  skillColor(skill: string): string { return skillMeta(skill).color; }
  skillShort(skill: string): string { return skillMeta(skill).short; }

  patchForm(patch: Partial<ExerciseTypeDefinition>): void {
    this.configForm.update(f => ({ ...f, ...patch }));
  }
}
