import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { LessonDto } from '../../../core/models/admin-lesson.models';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminRichTextComponent,
  SpAdminRichTextUploadResult,
  SpAdminSectionHeaderComponent,
} from '../../../design-system/admin';
import { SpLessonStudentPreviewComponent } from '../admin-lesson-detail/lesson-student-preview/sp-lesson-student-preview.component';

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
 * Phase K5 — Lesson edit as its own routed page (/admin/lesson-library/:id/edit), replacing the
 * earlier in-place modal.
 */
@Component({
  selector: 'app-admin-lesson-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminRichTextComponent,
    SpAdminSectionHeaderComponent,
    SpLessonStudentPreviewComponent,
  ],
  templateUrl: './admin-lesson-edit.component.html',
})
export class AdminLessonEditComponent implements OnInit {
  itemId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  item: LessonDto | null = null;

  title = '';
  body = '';
  examples: string[] = [];
  commonMistakes: string[] = [];
  usageNotes = '';
  cefrLevel = '';
  skill = '';
  subskill = '';
  contextTagsDraft = '';
  focusTagsDraft = '';
  difficultyBand: number | null = null;
  estimatedMinutes: number | null = null;

  readonly uploadMedia = (file: File): Observable<SpAdminRichTextUploadResult> => this.lessonSvc.uploadMedia(file);

  // ── "View as Student" — previews the live, unsaved draft (not just the last-saved item) ─────
  studentPreviewOpen = signal(false);
  openStudentPreview(): void { this.studentPreviewOpen.set(true); }
  closeStudentPreview(): void { this.studentPreviewOpen.set(false); }

  get previewLesson(): LessonDto | null {
    if (!this.item) return null;
    return {
      ...this.item,
      title: this.title,
      body: this.body,
      examplesJson: JSON.stringify(this.examples),
      commonMistakesJson: JSON.stringify(this.commonMistakes),
      usageNotes: this.usageNotes || null,
      cefrLevel: this.cefrLevel || null,
      skill: this.skill || null,
      subskill: this.subskill || null,
      difficultyBand: this.difficultyBand,
    };
  }

  constructor(
    private lessonSvc: AdminLessonService,
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
    this.lessonSvc.get(this.itemId).subscribe({
      next: item => {
        this.loading.set(false);
        this.item = item;
        this.title = item.title;
        this.body = item.body;
        this.examples = parseJsonArray(item.examplesJson);
        this.commonMistakes = parseJsonArray(item.commonMistakesJson);
        this.usageNotes = item.usageNotes ?? '';
        this.cefrLevel = item.cefrLevel ?? '';
        this.skill = item.skill ?? '';
        this.subskill = item.subskill ?? '';
        this.contextTagsDraft = parseJsonArray(item.contextTagsJson).join(', ');
        this.focusTagsDraft = parseJsonArray(item.focusTagsJson).join(', ');
        this.difficultyBand = item.difficultyBand;
        this.estimatedMinutes = item.estimatedMinutes;
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Lesson for editing.'); },
    });
  }

  cancel(): void {
    this.router.navigateByUrl(`/admin/lesson-library/${this.itemId}`);
  }

  private parseTags(raw: string): string[] {
    return raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  addExample(): void { this.examples = [...this.examples, '']; }
  removeExample(index: number): void { this.examples = this.examples.filter((_, i) => i !== index); }

  addCommonMistake(): void { this.commonMistakes = [...this.commonMistakes, '']; }
  removeCommonMistake(index: number): void { this.commonMistakes = this.commonMistakes.filter((_, i) => i !== index); }

  private nonEmptyHtml(items: string[]): string[] {
    return items.filter(html => html.replace(/<[^>]*>/g, '').trim().length > 0);
  }

  save(): void {
    if (!this.item) return;
    const plainBody = this.body.replace(/<[^>]*>/g, '').trim();
    if (!this.title.trim() || !plainBody) {
      this.error.set('Title and Body are required.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.lessonSvc.update(this.item.id, {
      title: this.title.trim(),
      body: this.body,
      examples: this.nonEmptyHtml(this.examples),
      commonMistakes: this.nonEmptyHtml(this.commonMistakes),
      usageNotes: this.usageNotes.trim() || null,
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
        this.router.navigateByUrl(`/admin/lesson-library/${updated.id}`);
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.error ?? 'Could not save changes.'); },
    });
  }
}
