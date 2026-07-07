import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import {
  AdminPlacementItemDto,
  PlacementItemRequest,
  PLACEMENT_SKILLS,
  PLACEMENT_CEFR_LEVELS,
} from '../../../core/models/admin-placement-item.models';
import { FormioBuilderComponent } from '../../../shared/formio/formio-builder.component';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
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

/**
 * Dedicated placement item designer page (own route, own full-width canvas) — split out from
 * the item bank list so the Form.io builder isn't squeezed into a slide-over drawer.
 */
@Component({
  selector: 'app-admin-placement-item-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
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
  templateUrl: './admin-placement-item-editor.component.html',
})
export class AdminPlacementItemEditorComponent implements OnInit {
  itemId!: string;
  isNew = false;

  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  previewOpen = signal(false);

  itemForm: PlacementItemRequest = this.emptyItemForm();

  formioSchema = signal<any>({ ...EMPTY_SCHEMA });
  scoringRulesJson = signal('');
  scoringRulesError = signal('');

  readonly schemaComponentKeys = computed(() => flattenComponentKeys(this.formioSchema()));

  readonly formSkillOptions = PLACEMENT_SKILLS.map(s => ({ value: s, label: s }));
  readonly cefrLevelOptions = PLACEMENT_CEFR_LEVELS.map(l => ({ value: l, label: l }));

  constructor(
    private svc: AdminPlacementItemService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.itemId = this.route.snapshot.paramMap.get('itemId') ?? 'new';
    this.isNew = this.itemId === 'new';

    if (this.isNew) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.svc.get(this.itemId).subscribe({
      next: item => {
        this.loadItem(item);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.status === 404 ? 'Placement item not found.' : (err.error?.error ?? 'Could not load placement item.'));
      },
    });
  }

  private loadItem(item: AdminPlacementItemDto): void {
    this.itemForm = {
      skill: item.skill,
      cefrLevel: item.cefrLevel,
      itemOrder: item.itemOrder,
      isEnabled: item.isEnabled,
      formIoSchemaJson: item.formIoSchemaJson ?? JSON.stringify(EMPTY_SCHEMA),
      scoringRulesJson: item.scoringRulesJson ?? '',
    };
    this.formioSchema.set(item.formIoSchemaJson ? this.tryParse(item.formIoSchemaJson) : { ...EMPTY_SCHEMA });
    this.scoringRulesJson.set(item.scoringRulesJson ?? '');
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
    const obs = this.isNew ? this.svc.add(request) : this.svc.update(this.itemId, request);

    obs.subscribe({
      next: () => this.router.navigate(['/admin/placement-items']),
      error: err => this.actionError.set(err.error?.error ?? 'Could not save item.'),
    });
  }

  private emptyItemForm(): PlacementItemRequest {
    return {
      skill: 'grammar',
      cefrLevel: 'A1',
      itemOrder: 1,
      isEnabled: true,
      formIoSchemaJson: JSON.stringify(EMPTY_SCHEMA),
      scoringRulesJson: '',
    };
  }
}
