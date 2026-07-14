import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { ResourceBankItemEditDto, RESOURCE_BANK_CEFR_LEVELS } from '../../../core/models/admin-resource-import.models';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

/**
 * Phase K5 — Resource Bank item edit as its own routed page (/admin/resource-bank/:id/edit),
 * replacing the earlier in-place modal. AI/import-generated content is not immutable; an admin
 * can correct it here. Loads the full, untruncated GET .../edit DTO (the list/detail DTO is
 * lossy — unsafe to round-trip).
 */
@Component({
  selector: 'app-admin-resource-bank-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-resource-bank-edit.component.html',
})
export class AdminResourceBankEditComponent implements OnInit {
  itemId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  dto: ResourceBankItemEditDto | null = null;
  contextTagsDraft = '';
  focusTagsDraft = '';

  readonly cefrLevelOptions = RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }));

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.itemId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.itemId) return;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.bankSvc.getEditDto(this.itemId).subscribe({
      next: dto => {
        this.loading.set(false);
        this.dto = dto;
        this.contextTagsDraft = dto.contextTags.join(', ');
        this.focusTagsDraft = dto.focusTags.join(', ');
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this item for editing.'); },
    });
  }

  cancel(): void {
    this.router.navigateByUrl(`/admin/resource-bank/${this.itemId}`);
  }

  private parseTags(raw: string): string[] {
    return raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  save(): void {
    const dto = this.dto;
    if (!dto) return;
    this.saving.set(true);
    this.error.set('');
    this.bankSvc.update(dto.id, {
      cefrLevel: dto.cefrLevel,
      subskill: dto.subskill,
      difficultyBand: dto.difficultyBand,
      contextTags: this.parseTags(this.contextTagsDraft),
      focusTags: this.parseTags(this.focusTagsDraft),
      word: dto.word,
      partOfSpeech: dto.partOfSpeech,
      notes: dto.notes,
      grammarPoint: dto.grammarPoint,
      description: dto.description,
      textType: dto.textType,
      difficultyNotes: dto.difficultyNotes,
      referenceExcerpt: dto.referenceExcerpt,
      title: dto.title,
      passageText: dto.passageText,
      summary: dto.summary,
      promptText: dto.promptText,
      genre: dto.genre,
      suggestedMinWords: dto.suggestedMinWords,
      transcript: dto.transcript,
      suggestedDurationSeconds: dto.suggestedDurationSeconds,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigateByUrl(`/admin/resource-bank/${dto.id}`);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.error ?? 'Could not save changes.'); },
    });
  }
}
