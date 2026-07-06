import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import {
  AdminPlacementItemDto,
  PlacementItemRequest,
  PLACEMENT_SKILLS,
  PLACEMENT_CEFR_LEVELS,
  PLACEMENT_ITEM_TYPES,
} from '../../../core/models/admin-placement-item.models';
import { FormioBuilderComponent } from '../../../shared/formio/formio-builder.component';
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

const EMPTY_SCHEMA = { display: 'form', components: [] };

/** Component types that never carry a scorable answer of their own (containers/decoration). */
const NON_ANSWER_TYPES = new Set(['button', 'content', 'panel', 'columns', 'table', 'wizard', 'form']);

/** Recursively flattens a Form.io schema's `components` tree, collecting the `.key` of every
 * leaf input component — mirrors the backend's PlacementFormIoScoringValidator key-extraction so
 * the admin sees the same set of scorable keys the server will validate scoring rules against. */
function flattenComponentKeys(schema: any): string[] {
  const keys: string[] = [];
  const walk = (node: any): void => {
    if (Array.isArray(node)) {
      node.forEach(walk);
      return;
    }
    if (!node || typeof node !== 'object') return;

    if (typeof node.type === 'string' && !NON_ANSWER_TYPES.has(node.type) && typeof node.key === 'string') {
      keys.push(node.key);
    }
    for (const value of Object.values(node)) {
      if (value && typeof value === 'object') walk(value);
    }
  };
  walk(schema?.components ?? []);
  return keys;
}

@Component({
  selector: 'app-admin-placement-items',
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
    FormioBuilderComponent,
  ],
  templateUrl: './admin-placement-items.component.html',
})
export class AdminPlacementItemsComponent implements OnInit {
  items = signal<AdminPlacementItemDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  slideOverOpen = signal(false);
  editingItem = signal<AdminPlacementItemDto | null>(null);

  itemForm: PlacementItemRequest = this.emptyItemForm();

  /** Form.io is the only, always-visible schema editor now — no legacy QuestionEditor toggle. */
  formioSchema = signal<any>({ ...EMPTY_SCHEMA });
  scoringRulesJson = signal('');
  scoringRulesError = signal('');

  /** Component keys read from the current Form.io schema, shown next to the scoring-rules
   * textarea so the admin knows which keys are scorable. */
  readonly schemaComponentKeys = computed(() => flattenComponentKeys(this.formioSchema()));

  skillFilter = signal<string>('all');

  readonly skillOptions = [{ value: 'all', label: 'All skills' }, ...PLACEMENT_SKILLS.map(s => ({ value: s, label: s }))];
  readonly formSkillOptions = PLACEMENT_SKILLS.map(s => ({ value: s, label: s }));
  readonly cefrLevelOptions = PLACEMENT_CEFR_LEVELS.map(l => ({ value: l, label: l }));
  readonly itemTypeOptions = PLACEMENT_ITEM_TYPES.map(t => ({ value: t, label: t }));

  readonly filteredItems = computed(() => {
    const filter = this.skillFilter();
    return filter === 'all' ? this.items() : this.items().filter(i => i.skill === filter);
  });

  readonly totalItems = computed(() => this.items().length);
  readonly enabledItems = computed(() => this.items().filter(i => i.isEnabled).length);
  readonly skillCount = computed(() => new Set(this.items().map(i => i.skill)).size);

  readonly itemColumns = [
    { key: 'skill', label: 'Skill' },
    { key: 'cefrLevel', label: 'Level' },
    { key: 'itemType', label: 'Type' },
    { key: 'prompt', label: 'Prompt' },
    { key: 'isEnabled', label: 'Enabled' },
    { key: '_actions', label: '' },
  ];

  constructor(private svc: AdminPlacementItemService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list().subscribe({
      next: items => { this.items.set(items); this.loading.set(false); },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load placement items.'); },
    });
  }

  openAddItem(): void {
    this.editingItem.set(null);
    this.itemForm = this.emptyItemForm();
    this.formioSchema.set({ ...EMPTY_SCHEMA });
    this.scoringRulesJson.set('');
    this.scoringRulesError.set('');
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  openEditItem(item: AdminPlacementItemDto): void {
    this.editingItem.set(item);
    this.itemForm = {
      skill: item.skill,
      cefrLevel: item.cefrLevel,
      itemType: item.itemType,
      prompt: item.prompt,
      itemOrder: item.itemOrder,
      isEnabled: item.isEnabled,
      formIoSchemaJson: item.formIoSchemaJson ?? JSON.stringify(EMPTY_SCHEMA),
      scoringRulesJson: item.scoringRulesJson ?? '',
    };
    this.formioSchema.set(item.formIoSchemaJson ? this.tryParse(item.formIoSchemaJson) : { ...EMPTY_SCHEMA });
    this.scoringRulesJson.set(item.scoringRulesJson ?? '');
    this.scoringRulesError.set('');
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
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

  closeSlideOver(): void {
    this.slideOverOpen.set(false);
    this.editingItem.set(null);
  }

  /** Client-side mirror of the backend's PlacementFormIoScoringValidator: the scoring rules JSON
   * must parse and declare at least one component, and every referenced component key must exist
   * in the current Form.io schema. This is a UX nicety only — the backend remains the real gate. */
  private validateScoringRules(): string | null {
    const raw = this.scoringRulesJson().trim();
    if (!raw) return 'Scoring rules are required.';

    let parsed: any;
    try {
      parsed = JSON.parse(raw);
    } catch (e) {
      return `Scoring rules JSON is invalid: ${(e as Error).message}`;
    }

    const components = parsed?.components;
    if (!components || typeof components !== 'object' || Array.isArray(components) || Object.keys(components).length === 0) {
      return 'Scoring rules must declare at least one component under "components".';
    }

    const schemaKeys = new Set(this.schemaComponentKeys());
    const orphaned = Object.keys(components).filter(k => !schemaKeys.has(k));
    if (orphaned.length > 0) {
      return `Scoring rules reference component key(s) not present in the Form.io schema: ${orphaned.join(', ')}`;
    }

    return null;
  }

  saveItem(): void {
    this.actionError.set('');
    this.scoringRulesError.set('');

    const scoringError = this.validateScoringRules();
    if (scoringError) {
      this.scoringRulesError.set(scoringError);
      return;
    }

    const request: PlacementItemRequest = {
      ...this.itemForm,
      formIoSchemaJson: JSON.stringify(this.formioSchema()),
      scoringRulesJson: this.scoringRulesJson().trim(),
    };
    const editing = this.editingItem();
    const obs = editing
      ? this.svc.update(editing.itemId, request)
      : this.svc.add(request);

    obs.subscribe({
      next: () => {
        this.actionSuccess.set(editing ? 'Item updated.' : 'Item added.');
        this.slideOverOpen.set(false);
        this.loadAll();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save item.'),
    });
  }

  removeItem(item: AdminPlacementItemDto): void {
    this.actionError.set('');
    this.svc.remove(item.itemId).subscribe({
      next: () => { this.actionSuccess.set('Item removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove item.'),
    });
  }

  itemTone(item: AdminPlacementItemDto): 'success' | 'neutral' {
    return item.isEnabled ? 'success' : 'neutral';
  }

  private emptyItemForm(): PlacementItemRequest {
    return {
      skill: 'grammar',
      cefrLevel: 'A1',
      itemType: 'multiple_choice',
      prompt: '',
      itemOrder: 1,
      isEnabled: true,
      formIoSchemaJson: JSON.stringify(EMPTY_SCHEMA),
      scoringRulesJson: '',
    };
  }
}
