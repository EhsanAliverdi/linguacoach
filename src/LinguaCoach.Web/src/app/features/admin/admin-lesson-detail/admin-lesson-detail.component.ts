import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import { LessonDto } from '../../../core/models/admin-lesson.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
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
 * Phase K3 — Lesson detail as its own routed page (/admin/lesson-library/:id), replacing the old
 * in-place slide-in drawer. Approve/Reject/Generate Exercise/Generate Module all live here now.
 */
@Component({
  selector: 'app-admin-lesson-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-lesson-detail.component.html',
})
export class AdminLessonDetailComponent implements OnInit {
  itemId = '';
  item = signal<LessonDto | null>(null);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  generatingActivity = signal(false);
  generatingModule = signal(false);
  lastActionKind = signal<'activity' | 'module' | null>(null);

  constructor(
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private moduleSvc: AdminModuleService,
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
      next: item => { this.item.set(item); this.loading.set(false); this.rejectReasonDraft = ''; },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Lesson.'); },
    });
  }

  backToList(): void {
    this.router.navigateByUrl('/admin/lesson-library');
  }

  examplesFor(item: LessonDto): string[] { return parseJsonArray(item.examplesJson); }
  commonMistakesFor(item: LessonDto): string[] { return parseJsonArray(item.commonMistakesJson); }
  contextTagsFor(item: LessonDto): string[] { return parseJsonArray(item.contextTagsJson); }
  focusTagsFor(item: LessonDto): string[] { return parseJsonArray(item.focusTagsJson); }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'PendingReview') return 'warning';
    return 'neutral';
  }

  approveSelected(): void {
    const item = this.item();
    if (!item) return;
    this.approving.set(true);
    this.actionError.set('');
    this.lessonSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Lesson approved.');
      },
      error: err => { this.approving.set(false); this.actionError.set(err.error?.error ?? 'Could not approve.'); },
    });
  }

  rejectSelected(): void {
    const item = this.item();
    if (!item) return;
    if (!this.rejectReasonDraft.trim()) {
      this.actionError.set('A rejection reason is required.');
      return;
    }
    this.rejecting.set(true);
    this.actionError.set('');
    this.lessonSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Lesson rejected.');
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  generateActivity(): void {
    const item = this.item();
    if (!item) return;
    this.generatingActivity.set(true);
    this.actionError.set('');
    this.exerciseSvc.generateFromLesson({ lessonId: item.id }).subscribe({
      next: () => {
        this.generatingActivity.set(false);
        this.lastActionKind.set('activity');
        this.actionSuccess.set('Exercise draft generated from this Lesson — pending review.');
      },
      error: err => { this.generatingActivity.set(false); this.actionError.set(err.error?.error ?? 'Could not generate an Exercise.'); },
    });
  }

  goToExercises(): void { this.router.navigateByUrl('/admin/exercises'); }

  generateModule(): void {
    const item = this.item();
    if (!item) return;
    this.generatingModule.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromLesson({ lessonId: item.id }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastActionKind.set('module');
        this.actionSuccess.set('Module draft generated from this Lesson — pending review.');
      },
      error: err => { this.generatingModule.set(false); this.actionError.set(err.error?.error ?? 'Could not generate a Module.'); },
    });
  }

  goToModules(): void { this.router.navigateByUrl('/admin/modules'); }
}
