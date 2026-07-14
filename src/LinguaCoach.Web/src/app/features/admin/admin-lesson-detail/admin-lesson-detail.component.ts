import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminService } from '../../../core/services/admin.service';
import { LessonDto } from '../../../core/models/admin-lesson.models';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';
import { DiagnosticIssue } from '../../../core/models/admin-repair.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
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

interface ExerciseTypeCount {
  activityType: string;
  displayName: string;
  count: number;
}

/**
 * Phase K3 — Lesson detail as its own routed page (/admin/lesson-library/:id), replacing the old
 * in-place slide-in drawer.
 *
 * Phase K5 — product decision: "Generate Exercise" is now "Generate Exercises" — the admin picks
 * how many of each Exercise type to create in one go (e.g. 5 gap_fill + 5 multiple_choice_single
 * = 10 Exercises), pre-filled from each type's own DefaultItemsPerPractice in the Exercise Types
 * catalog. "Generate Module" is gone entirely — a Module is now created (or extended)
 * automatically every time Exercises are generated from this Lesson, see
 * AdminExerciseService.generateActivitiesFromLesson. Also adds admin Edit — AI-generated Lesson
 * content is not immutable.
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
    SpAdminModalComponent,
    SpAdminNumberInputComponent,
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
  archiving = signal(false);

  moduleReviewRoute = signal<string | null>(null);

  // ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────
  issues = signal<DiagnosticIssue[]>([]);
  repairing = signal(false);
  repairSuccess = signal('');
  repairError = signal('');

  constructor(
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private adminSvc: AdminService,
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
    this.lessonSvc.diagnose(this.itemId).subscribe({
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
    this.lessonSvc.repair(this.itemId).subscribe({
      next: result => {
        this.repairing.set(false);
        this.repairSuccess.set(`Fixed ${result.issuesFixed.length} issue(s)` + (result.providerName ? ` using ${result.providerName}/${result.modelName}.` : '.'));
        this.load();
      },
      error: err => { this.repairing.set(false); this.repairError.set(err.error?.error ?? 'Could not repair this Lesson.'); },
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

  goToExercises(): void { this.router.navigateByUrl('/admin/exercises'); }
  goToModule(): void {
    const route = this.moduleReviewRoute();
    if (route) this.router.navigateByUrl(route);
  }

  // ── Phase K5 — Generate Exercises (count + type per type, Module auto-created/extended) ───────

  generateModalOpen = signal(false);
  generateSubmitting = signal(false);
  generateError = signal('');
  typeCountsLoading = signal(false);
  typeCounts = signal<ExerciseTypeCount[]>([]);

  /** Phase K15 — the picker is now fully catalog-driven: an Exercise Type shows up here only if
   *  it is Enabled in /admin/exercise-types AND its primarySkill/secondarySkills match this
   *  Lesson's skill. No more hardcoded type lists in the frontend — flip a type on in the
   *  catalog (once its composer ships, see the K15-K19 build-out plan) and it appears here with
   *  zero further frontend changes. Unknown/blank Lesson skill falls back to showing every
   *  enabled+ready type, since the backend remains the real gate either way. */
  openGenerate(): void {
    this.generateError.set('');
    this.generateModalOpen.set(true);
    this.typeCountsLoading.set(true);
    const skill = (this.item()?.skill ?? '').trim().toLowerCase();
    this.adminSvc.listExerciseTypes().subscribe({
      next: (types: ExerciseTypeDefinition[]) => {
        this.typeCountsLoading.set(false);
        const eligible = types.filter(t => t.isAvailableForGeneration);
        const matching = skill
          ? eligible.filter(t => t.primarySkill?.toLowerCase() === skill
              || (t.secondarySkills ?? []).some(s => s.toLowerCase() === skill))
          : eligible;
        this.typeCounts.set(matching.map(t => ({
          activityType: t.key,
          displayName: t.displayName,
          count: t.defaultItemsPerPractice,
        })));
      },
      error: () => {
        this.typeCountsLoading.set(false);
        this.generateError.set('Could not load Exercise Types.');
      },
    });
  }

  closeGenerate(): void {
    this.generateModalOpen.set(false);
  }

  setTypeCount(activityType: string, count: number): void {
    this.typeCounts.update(list => list.map(t => t.activityType === activityType ? { ...t, count } : t));
  }

  get totalRequested(): number {
    return this.typeCounts().reduce((sum, t) => sum + (t.count > 0 ? t.count : 0), 0);
  }

  submitGenerate(): void {
    const item = this.item();
    if (!item) return;
    const specs = this.typeCounts()
      .filter(t => t.count > 0)
      .map(t => ({ activityType: t.activityType, count: t.count }));
    if (specs.length === 0) {
      this.generateError.set('Pick a count greater than 0 for at least one Exercise type.');
      return;
    }
    this.generateSubmitting.set(true);
    this.generateError.set('');
    this.exerciseSvc.generateActivitiesFromLesson({ lessonId: item.id, specs }).subscribe({
      next: result => {
        this.generateSubmitting.set(false);
        this.generateModalOpen.set(false);
        this.moduleReviewRoute.set(result.moduleReviewRoute);
        this.actionSuccess.set(
          `${result.activities.length} Exercise(s) generated — pending review. A Module linking this Lesson to its Exercises was created/updated automatically.`);
      },
      error: err => {
        this.generateSubmitting.set(false);
        this.generateError.set(err.error?.error ?? 'Could not generate Exercises.');
      },
    });
  }

  goToEdit(): void {
    this.router.navigateByUrl(`/admin/lesson-library/${this.itemId}/edit`);
  }

  deleteItem(): void {
    this.archiving.set(true);
    this.actionError.set('');
    this.lessonSvc.archive([this.itemId]).subscribe({
      next: () => { this.archiving.set(false); this.backToList(); },
      error: err => { this.archiving.set(false); this.actionError.set(err.error?.error ?? 'Could not delete this Lesson.'); },
    });
  }

  restoreItem(): void {
    this.archiving.set(true);
    this.actionError.set('');
    this.lessonSvc.unarchive([this.itemId]).subscribe({
      next: () => { this.archiving.set(false); this.load(); },
      error: err => { this.archiving.set(false); this.actionError.set(err.error?.error ?? 'Could not restore this Lesson.'); },
    });
  }
}
