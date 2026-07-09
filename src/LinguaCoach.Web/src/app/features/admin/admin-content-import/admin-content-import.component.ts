import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminContentImportService } from '../../../core/services/admin-resource-import.service';
import {
  CONTENT_IMPORT_COMING_SOON_TYPES,
  CONTENT_IMPORT_INPUT_MODES,
  CONTENT_IMPORT_RESOURCE_TYPES,
  ContentImportInputMode,
  ContentImportResourceType,
  ContentImportResult,
  RESOURCE_BANK_CEFR_LEVELS,
} from '../../../core/models/admin-resource-import.models';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminFormFieldComponent,
  SpAdminFormGridComponent,
  SpAdminInputComponent,
  SpAdminNativeSelectComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

/**
 * Phase H2 — Import Content UX v1. A product-friendly wrapper over the existing Phase E1
 * import pipeline: admin pastes text/CSV/JSON, picks a broad resource type + default metadata,
 * and this page creates pending Resource Candidates through the same gate/staging logic a
 * file upload would use (see AdminResourceImportRunsComponent). Nothing here is published —
 * review still happens on the Resource Candidates page.
 */
@Component({
  selector: 'app-admin-content-import',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminInputComponent,
    SpAdminNativeSelectComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-content-import.component.html',
})
export class AdminContentImportComponent {
  readonly resourceTypeOptions = CONTENT_IMPORT_RESOURCE_TYPES.map(t => ({ value: t.value, label: t.label }));
  readonly inputModeOptions = CONTENT_IMPORT_INPUT_MODES.map(m => ({ value: m.value, label: m.label }));
  readonly cefrOptions = [{ value: '', label: 'No default' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];
  readonly comingSoonTypes = CONTENT_IMPORT_COMING_SOON_TYPES;

  // ── Source/details ──────────────────────────────────────────────────────
  sourceName = '';
  notes = '';

  // ── Content type ─────────────────────────────────────────────────────────
  resourceType: ContentImportResourceType = 'vocabulary';

  // ── Defaults ─────────────────────────────────────────────────────────────
  defaultCefrLevel = '';
  defaultSkill = '';
  defaultSubskill = '';
  defaultContextTags = '';
  defaultFocusTags = '';
  defaultDifficultyBand: number | null = null;

  // ── Input ────────────────────────────────────────────────────────────────
  inputMode: ContentImportInputMode = 'pasted_text';
  content = '';

  get inputHint(): string {
    return CONTENT_IMPORT_INPUT_MODES.find(m => m.value === this.inputMode)?.hint ?? '';
  }

  // ── Submit/result state ─────────────────────────────────────────────────
  submitting = signal(false);
  error = signal('');
  result = signal<ContentImportResult | null>(null);

  constructor(private importSvc: AdminContentImportService, private router: Router) {}

  private parseTags(raw: string): string[] | null {
    const tags = raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
    return tags.length > 0 ? tags : null;
  }

  submit(): void {
    this.error.set('');
    this.result.set(null);

    if (!this.sourceName.trim()) {
      this.error.set('Source name is required.');
      return;
    }
    if (!this.content.trim()) {
      this.error.set('Content is required.');
      return;
    }

    this.submitting.set(true);
    this.importSvc.import({
      sourceName: this.sourceName.trim(),
      resourceType: this.resourceType,
      inputMode: this.inputMode,
      content: this.content,
      defaultCefrLevel: this.defaultCefrLevel || null,
      defaultSkill: this.defaultSkill.trim() || null,
      defaultSubskill: this.defaultSubskill.trim() || null,
      defaultContextTags: this.parseTags(this.defaultContextTags),
      defaultFocusTags: this.parseTags(this.defaultFocusTags),
      defaultDifficultyBand: this.defaultDifficultyBand,
      notes: this.notes.trim() || null,
    }).subscribe({
      next: result => {
        this.submitting.set(false);
        this.result.set(result);
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.error ?? 'Import failed.');
      },
    });
  }

  startAnotherImport(): void {
    this.content = '';
    this.result.set(null);
    this.error.set('');
  }

  goToReview(): void {
    const result = this.result();
    if (result) this.router.navigateByUrl(result.reviewRoute);
  }

  goToResourceBank(): void {
    this.router.navigateByUrl('/admin/resource-bank');
  }
}
