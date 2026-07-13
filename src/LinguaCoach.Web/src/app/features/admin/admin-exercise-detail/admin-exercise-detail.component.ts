import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import { ExerciseDto } from '../../../core/models/admin-exercise.models';
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

/**
 * Phase K4 — Exercise detail as its own routed page (/admin/exercises/:id), replacing the old
 * in-place slide-in drawer. Approve/Reject/Generate Module all live here now.
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
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminTextareaComponent,
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

  generatingModule = signal(false);
  lastActionWasGenerateModule = signal(false);

  constructor(
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
    this.exerciseSvc.get(this.itemId).subscribe({
      next: item => { this.item.set(item); this.loading.set(false); this.rejectReasonDraft = ''; },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Exercise.'); },
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
        this.lastActionWasGenerateModule.set(false);
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
        this.lastActionWasGenerateModule.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Exercise rejected.');
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  generateModule(): void {
    const item = this.item();
    if (!item) return;
    this.generatingModule.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromExercise({ exerciseId: item.id }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastActionWasGenerateModule.set(true);
        this.actionSuccess.set('Module draft generated from this Exercise — pending review.');
      },
      error: err => {
        this.generatingModule.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate a Module.');
      },
    });
  }

  goToModules(): void {
    this.router.navigateByUrl('/admin/modules');
  }
}
