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
import { countScoredComponents, finalizeQuizAnnotations } from '../../../shared/formio/quiz-scoring-rule.model';
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
} from '../../../design-system/admin';

const EMPTY_SCHEMA = { display: 'form', components: [] };

/**
 * Dedicated placement item designer page (own route, own full-width canvas) — split out from
 * the item bank list so the Form.io builder isn't squeezed into a slide-over drawer.
 *
 * Scoring is authored per-component via the Form.io builder's own "Quiz" tab (see
 * shared/formio/quiz-edit-tab.ts) rather than a separate hand-typed JSON textarea — the server
 * (IFormIoQuizSchemaSplitter) is the sole authority splitting the submitted authoringSchemaJson
 * into the student-safe schema and backend-only scoring rules.
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

  /** True when this item has scoring but was authored before the Quiz tab existed — its schema
   * carries no quiz annotations yet, so every question shows as "not scored" until re-saved. */
  needsReauthoring = signal(false);

  // ── Calibration (Phase 7) ──────────────────────────────────────────────────
  reviewStatus = signal<string>('NotRequired');
  itemVersion = signal(1);
  discriminationIndex = signal<number | null>(null);
  calibrationSampleSize = signal<number | null>(null);

  itemForm: PlacementItemRequest = this.emptyItemForm();

  formioSchema = signal<any>({ ...EMPTY_SCHEMA });

  readonly scoredSummary = computed(() => countScoredComponents(this.formioSchema()));

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
      difficultyBand: item.difficultyBand,
      evidenceWeight: item.evidenceWeight,
    };
    this.needsReauthoring.set(!item.authoringSchemaJson && !!item.scoringRulesJson);
    const seedSchema = item.authoringSchemaJson ?? item.formIoSchemaJson;
    this.formioSchema.set(seedSchema ? this.tryParse(seedSchema) : { ...EMPTY_SCHEMA });
    this.reviewStatus.set(item.reviewStatus);
    this.itemVersion.set(item.itemVersion);
    this.discriminationIndex.set(item.discriminationIndex);
    this.calibrationSampleSize.set(item.calibrationSampleSize);
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

  saveItem(): void {
    this.actionError.set('');

    const authoringSchema = finalizeQuizAnnotations(this.formioSchema());
    const request: PlacementItemRequest = {
      ...this.itemForm,
      authoringSchemaJson: JSON.stringify(authoringSchema),
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
      difficultyBand: 1,
      evidenceWeight: 1.0,
    };
  }

  approve(): void {
    this.runReviewAction({ action: 'approve' });
  }

  reject(): void {
    const reason = window.prompt('Reason for rejecting this item:');
    if (!reason) return;
    this.runReviewAction({ action: 'reject', reason });
  }

  resetToPendingReview(): void {
    this.runReviewAction({ action: 'reset' });
  }

  private runReviewAction(request: { action: 'approve' | 'reject' | 'reset'; reason?: string }): void {
    this.actionError.set('');
    this.svc.setReviewStatus(this.itemId, request).subscribe({
      next: item => this.reviewStatus.set(item.reviewStatus),
      error: err => this.actionError.set(err.error?.error ?? 'Could not update review status.'),
    });
  }

  saveCalibrationStats(): void {
    this.actionError.set('');
    this.svc.setCalibrationStats(this.itemId, {
      discriminationIndex: this.discriminationIndex(),
      calibrationSampleSize: this.calibrationSampleSize(),
    }).subscribe({
      next: item => {
        this.discriminationIndex.set(item.discriminationIndex);
        this.calibrationSampleSize.set(item.calibrationSampleSize);
        this.actionSuccess.set('Calibration stats saved.');
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save calibration stats.'),
    });
  }
}
