import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { ExerciseDto, ExercisePreviewSubmitResult } from '../../../core/models/admin-exercise.models';
import { DiagnosticIssue } from '../../../core/models/admin-repair.models';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

/**
 * Phase K4 — Exercise detail as its own routed page (/admin/exercises/:id), replacing the old
 * in-place slide-in drawer.
 *
 * Phase K5 — removed the manual "Generate Module" action (Modules are now created/extended
 * automatically whenever Exercises are generated from a Lesson — see AdminLessonDetailComponent).
 * Added admin Edit — AI-generated Exercise content is not immutable.
 */
@Component({
  selector: 'app-admin-exercise-detail',
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
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminTextareaComponent,
    FormioRendererComponent,
  ],
  templateUrl: './admin-exercise-detail.component.html',
})
export class AdminExerciseDetailComponent implements OnInit {
  itemId = '';
  item = signal<ExerciseDto | null>(null);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);
  archiving = signal(false);

  parsedSchema = signal<any>(null);

  // ── Phase K7 — Preview as Student modal ─────────────────────────────────
  previewModalOpen = signal(false);
  previewSubmitting = signal(false);
  previewError = signal('');
  previewResult = signal<ExercisePreviewSubmitResult | null>(null);

  // ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────
  issues = signal<DiagnosticIssue[]>([]);
  repairing = signal(false);
  repairSuccess = signal('');
  repairError = signal('');

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
        this.item.set(item);
        this.loading.set(false);
        this.rejectReasonDraft = '';
        try {
          this.parsedSchema.set(item.formSchemaJson ? JSON.parse(item.formSchemaJson) : null);
        } catch {
          this.parsedSchema.set(null);
        }
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Exercise.'); },
    });
    this.exerciseSvc.diagnose(this.itemId).subscribe({
      next: issues => this.issues.set(issues),
      error: () => this.issues.set([]),
    });
  }

  get autoFixableIssues(): DiagnosticIssue[] {
    return this.issues().filter(i => i.autoFixable);
  }

  fixWithAi(): void {
    this.repairing.set(true);
    this.repairError.set('');
    this.repairSuccess.set('');
    this.exerciseSvc.repair(this.itemId).subscribe({
      next: result => {
        this.repairing.set(false);
        this.repairSuccess.set(`Fixed ${result.issuesFixed.length} issue(s)` + (result.providerName ? ` using ${result.providerName}/${result.modelName}.` : '.'));
        this.load();
      },
      error: err => { this.repairing.set(false); this.repairError.set(err.error?.error ?? 'Could not repair this Exercise.'); },
    });
  }

  backToList(): void {
    this.router.navigateByUrl('/admin/exercises');
  }

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
    this.exerciseSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Exercise approved.');
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
    this.exerciseSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Exercise rejected.');
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  goToEdit(): void {
    this.router.navigateByUrl(`/admin/exercises/${this.itemId}/edit`);
  }

  deleteItem(): void {
    this.archiving.set(true);
    this.actionError.set('');
    this.exerciseSvc.archive([this.itemId]).subscribe({
      next: () => { this.archiving.set(false); this.backToList(); },
      error: err => { this.archiving.set(false); this.actionError.set(err.error?.error ?? 'Could not delete this Exercise.'); },
    });
  }

  restoreItem(): void {
    this.archiving.set(true);
    this.actionError.set('');
    this.exerciseSvc.unarchive([this.itemId]).subscribe({
      next: () => { this.archiving.set(false); this.load(); },
      error: err => { this.archiving.set(false); this.actionError.set(err.error?.error ?? 'Could not restore this Exercise.'); },
    });
  }

  openPreview(): void {
    this.previewError.set('');
    this.previewResult.set(null);
    this.previewModalOpen.set(true);
  }

  closePreview(): void {
    this.previewModalOpen.set(false);
  }

  onPreviewSubmit(answers: Record<string, unknown>): void {
    this.previewSubmitting.set(true);
    this.previewError.set('');
    this.exerciseSvc.previewSubmit(this.itemId, { answers }).subscribe({
      next: result => {
        this.previewSubmitting.set(false);
        this.previewResult.set(result);
      },
      error: err => {
        this.previewSubmitting.set(false);
        this.previewError.set(err.error?.error ?? 'Could not score the preview submission.');
      },
    });
  }
}
