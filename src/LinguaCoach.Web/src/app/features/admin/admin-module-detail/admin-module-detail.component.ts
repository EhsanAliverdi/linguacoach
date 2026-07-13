import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  ModuleDto,
  ModulePreviewResult,
  ModulePreviewSubmitResult,
} from '../../../core/models/admin-module.models';
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
 * Phase K4 — Module detail as its own routed page (/admin/modules/:id), replacing the old
 * in-place slide-in drawer. Approve/Reject/Preview as Learner all live here now.
 */
@Component({
  selector: 'app-admin-module-detail',
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
  templateUrl: './admin-module-detail.component.html',
})
export class AdminModuleDetailComponent implements OnInit {
  itemId = '';
  item = signal<ModuleDto | null>(null);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  // ── Phase J3 — Preview as Learner modal ─────────────────────────────────
  previewModalOpen = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  previewData = signal<ModulePreviewResult | null>(null);
  previewSchema = signal<any>(null);
  previewSubmitting = signal(false);
  previewResult = signal<ModulePreviewSubmitResult | null>(null);

  @ViewChild(FormioRendererComponent) previewFormioRenderer?: FormioRendererComponent;

  constructor(
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
    this.moduleSvc.get(this.itemId).subscribe({
      next: item => { this.item.set(item); this.loading.set(false); this.rejectReasonDraft = ''; },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Module.'); },
    });
  }

  backToList(): void {
    this.router.navigateByUrl('/admin/modules');
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
    this.moduleSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Module approved.');
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
    this.moduleSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.item.set(updated);
        this.actionSuccess.set('Module rejected.');
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  openPreview(): void {
    const item = this.item();
    if (!item) return;
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewData.set(null);
    this.previewSchema.set(null);
    this.previewResult.set(null);
    this.previewModalOpen.set(true);

    this.moduleSvc.preview(item.id).subscribe({
      next: result => {
        this.previewLoading.set(false);
        this.previewData.set(result);
        if (result.exercise?.formSchemaJson) {
          try {
            this.previewSchema.set(JSON.parse(result.exercise.formSchemaJson));
          } catch {
            this.previewSchema.set(null);
          }
        }
      },
      error: err => {
        this.previewLoading.set(false);
        this.previewError.set(err.error?.error ?? 'Could not load the preview.');
      },
    });
  }

  closePreview(): void {
    this.previewModalOpen.set(false);
  }

  submitPreviewAnswer(): void {
    this.previewFormioRenderer?.submitForm();
  }

  onPreviewExerciseSubmit(answers: Record<string, unknown>): void {
    const module = this.previewData();
    if (!module) return;

    this.previewSubmitting.set(true);
    this.previewError.set('');
    this.moduleSvc.previewSubmit(module.moduleId, { answers }).subscribe({
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
