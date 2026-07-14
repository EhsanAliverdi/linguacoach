import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { ExerciseDto } from '../../../core/models/admin-exercise.models';
import { FormioBuilderComponent } from '../../../shared/formio/formio-builder.component';
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
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter(v => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

/**
 * Phase K5 — Exercise edit as its own routed page (/admin/exercises/:id/edit), replacing the
 * earlier in-place modal.
 */
@Component({
  selector: 'app-admin-exercise-edit',
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
    SpAdminTextareaComponent,
    FormioBuilderComponent,
  ],
  templateUrl: './admin-exercise-edit.component.html',
})
export class AdminExerciseEditComponent implements OnInit {
  itemId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  item: ExerciseDto | null = null;

  title = '';
  description = '';
  instructions = '';
  cefrLevel = '';
  skill = '';
  subskill = '';
  contextTagsDraft = '';
  focusTagsDraft = '';
  difficultyBand: number | null = null;
  estimatedMinutes: number | null = null;

  formSchema: any = null;

  constructor(
    private exerciseSvc: AdminExerciseService,
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
    this.exerciseSvc.get(this.itemId).subscribe({
      next: item => {
        this.loading.set(false);
        this.item = item;
        this.title = item.title;
        this.description = item.description ?? '';
        this.instructions = item.instructions;
        this.cefrLevel = item.cefrLevel ?? '';
        this.skill = item.skill ?? '';
        this.subskill = item.subskill ?? '';
        this.contextTagsDraft = parseJsonArray(item.contextTagsJson).join(', ');
        this.focusTagsDraft = parseJsonArray(item.focusTagsJson).join(', ');
        this.difficultyBand = item.difficultyBand;
        this.estimatedMinutes = item.estimatedMinutes;
        try {
          this.formSchema = item.formSchemaJson ? JSON.parse(item.formSchemaJson) : { display: 'form', components: [] };
        } catch {
          this.formSchema = { display: 'form', components: [] };
        }
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Exercise for editing.'); },
    });
  }

  onSchemaChange(schema: any): void {
    this.formSchema = schema;
  }

  cancel(): void {
    this.router.navigateByUrl(`/admin/exercises/${this.itemId}`);
  }

  private parseTags(raw: string): string[] {
    return raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  save(): void {
    if (!this.item) return;
    if (!this.title.trim() || !this.instructions.trim()) {
      this.error.set('Title and Instructions are required.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.exerciseSvc.update(this.item.id, {
      title: this.title.trim(),
      instructions: this.instructions.trim(),
      description: this.description.trim() || null,
      // Full-replace PUT — formSchemaJson is the only one of these four editable in this UI so
      // far; the other three must be passed through unchanged or the backend would null them out.
      formSchemaJson: this.formSchema ? JSON.stringify(this.formSchema) : null,
      answerKeyJson: this.item.answerKeyJson,
      scoringRulesJson: this.item.scoringRulesJson,
      feedbackPlanJson: this.item.feedbackPlanJson,
      cefrLevel: this.cefrLevel.trim() || null,
      skill: this.skill.trim() || null,
      subskill: this.subskill.trim() || null,
      contextTags: this.parseTags(this.contextTagsDraft),
      focusTags: this.parseTags(this.focusTagsDraft),
      difficultyBand: this.difficultyBand,
      estimatedMinutes: this.estimatedMinutes,
    }).subscribe({
      next: updated => {
        this.saving.set(false);
        this.router.navigateByUrl(`/admin/exercises/${updated.id}`);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.error ?? 'Could not save changes.'); },
    });
  }
}
