import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminImportPackageService } from '../../../core/services/admin-import-package.service';
import {
  ImportExecutionGroupInstruction,
  ImportExecutionPlanDto,
  ImportPackageManifestSummaryDto,
  ImportPlanPreviewResult,
  ImportPlanValidationError,
  RECOGNIZED_FIELD_MAPPING_TARGETS,
  ResourceCandidateType,
  ROUTABLE_RESOURCE_TYPES,
} from '../../../core/models/admin-import-package.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminNativeSelectComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminTextareaComponent,
  SpAdminToggleComponent,
} from '../../../design-system/admin';

const AUDIO_EXTENSIONS = ['.mp3', '.wav', '.m4a', '.ogg'];
const ROOT_GROUP_KEY = '(root)';

/** One folder-group's editable form state — built from ImportExecutionGroupInstruction plus
 *  display-only context (description/fileCount from estimate.detectedGroups, detected CSV
 *  headers from estimate.structuredMappingPreviews). toInstruction() converts back to the exact
 *  typed contract the draft-update/preview/approve endpoints consume. */
interface GroupFormRow {
  groupKey: string;
  description: string;
  fileCount: number;
  sampleRelativePaths: string[];
  isAudioGroup: boolean;
  hasStructuredHeaders: boolean;
  included: boolean;
  resourceType: ResourceCandidateType | '';
  mappings: { source: string; target: string }[];
}

function groupKeyForRelativePath(relativePath: string): string {
  const idx = relativePath.replace(/\\/g, '/').lastIndexOf('/');
  return idx <= 0 ? (idx === -1 ? ROOT_GROUP_KEY : ROOT_GROUP_KEY) : relativePath.slice(0, idx);
}

/**
 * Mandatory Import Execution Plan addendum (2026-07-15) — /admin/content/import/packages/:packageId/plan.
 * Every package (regardless of size) must reach this page before any AI/STT/TTS/background
 * processing begins: it shows the automatically-generated plan (detected structure, proposed
 * decisions, volume/time/cost estimate, risks) and requires an explicit "Approve and Start
 * Processing" action with an admin-set cost ceiling. No pre-checked approval, no implicit start.
 *
 * Phase 4.4A adds the editable-plan workflow: while the plan is Draft/AwaitingApproval, an admin
 * can include/exclude groups, change their Resource-type route, edit CSV field mappings, preview
 * the mapped output, and save through the concurrency-checked draft-update API before approving.
 * Once Approved the plan becomes read-only here; a "Create Revision" action opens a new editable
 * draft without mutating the approved row.
 */
@Component({
  selector: 'app-admin-import-package-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminNativeSelectComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminTextareaComponent,
    SpAdminToggleComponent,
  ],
  templateUrl: './admin-import-package-plan.component.html',
})
export class AdminImportPackagePlanComponent implements OnInit {
  packageId = '';

  loading = signal(true);
  error = signal('');

  manifest = signal<ImportPackageManifestSummaryDto | null>(null);
  plan = signal<ImportExecutionPlanDto | null>(null);

  regenerating = signal(false);

  // ── Phase 4.4A editable draft form state ──────────────────────────────────────────────────
  formRows = signal<GroupFormRow[]>([]);
  dirty = signal(false);
  saving = signal(false);
  saveError = signal('');
  validationErrors = signal<ImportPlanValidationError[]>([]);
  concurrencyConflict = signal(false);

  previewLoading = signal(false);
  previewError = signal('');
  previewResult = signal<ImportPlanPreviewResult | null>(null);

  reviseModalOpen = signal(false);
  reviseReason = '';
  revising = signal(false);
  reviseError = signal('');

  readonly resourceTypeOptions = ROUTABLE_RESOURCE_TYPES;
  readonly listeningOnlyOption = ROUTABLE_RESOURCE_TYPES.filter(o => o.value === 'listeningPassage');
  readonly fieldMappingTargetOptions = RECOGNIZED_FIELD_MAPPING_TARGETS.map(t => ({ value: t, label: t }));

  approveModalOpen = signal(false);
  approvedCostCeiling: number | null = null;
  approving = signal(false);
  approveError = signal('');

  rejectModalOpen = signal(false);
  rejectReason = '';
  rejecting = signal(false);
  rejectError = signal('');

  resumeModalOpen = signal(false);
  resumeCostCeiling: number | null = null;
  resumeReason = '';
  resuming = signal(false);
  resumeError = signal('');
  resumeConcurrencyConflict = signal(false);

  constructor(
    private packageSvc: AdminImportPackageService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.packageId = this.route.snapshot.paramMap.get('packageId') ?? '';
    if (!this.packageId) {
      this.error.set('No package id in the route.');
      this.loading.set(false);
      return;
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.packageSvc.getManifest(this.packageId).subscribe({
      next: manifest => {
        this.manifest.set(manifest);
        this.packageSvc.getPlan(this.packageId).subscribe({
          next: plan => { this.plan.set(plan); this.rebuildForm(plan); this.loading.set(false); },
          error: () => { this.plan.set(null); this.loading.set(false); },
        });
      },
      error: err => {
        this.error.set(err.error?.error ?? 'Could not load this package.');
        this.loading.set(false);
      },
    });
  }

  private rebuildForm(plan: ImportExecutionPlanDto): void {
    this.dirty.set(false);
    this.saveError.set('');
    this.validationErrors.set([]);
    this.concurrencyConflict.set(false);
    this.previewResult.set(null);
    this.previewError.set('');

    if (!plan.isEditable) {
      this.formRows.set([]);
      return;
    }

    const detectedByKey = new Map(plan.estimate.detectedGroups.map(g => [g.groupKey, g]));
    const rows: GroupFormRow[] = plan.groupInstructions.map(instruction => {
      const detected = detectedByKey.get(instruction.groupKey);
      const headers = new Set<string>();
      for (const preview of plan.estimate.structuredMappingPreviews) {
        if (groupKeyForRelativePath(preview.assetRelativePath) === instruction.groupKey) {
          preview.detectedColumns.forEach(c => headers.add(c));
        }
      }
      Object.keys(instruction.fieldMappings).forEach(c => headers.add(c));

      const isAudioGroup = detected?.proposedResourceType === 'listeningPassage'
        || instruction.sampleRelativePaths.some(p => AUDIO_EXTENSIONS.some(ext => p.toLowerCase().endsWith(ext)));

      return {
        groupKey: instruction.groupKey,
        description: detected?.description ?? '',
        fileCount: detected?.fileCount ?? instruction.sampleRelativePaths.length,
        sampleRelativePaths: instruction.sampleRelativePaths,
        isAudioGroup,
        hasStructuredHeaders: headers.size > 0,
        included: instruction.included,
        resourceType: instruction.resourceType ?? '',
        mappings: Array.from(headers).map(source => ({ source, target: instruction.fieldMappings[source] ?? '' })),
      };
    });
    this.formRows.set(rows);
  }

  private toInstructions(): ImportExecutionGroupInstruction[] {
    return this.formRows().map(row => ({
      groupKey: row.groupKey,
      included: row.included,
      resourceType: row.included && row.resourceType ? row.resourceType : null,
      fieldMappings: Object.fromEntries(row.mappings.filter(m => m.target).map(m => [m.source, m.target])),
      sampleRelativePaths: row.sampleRelativePaths,
    }));
  }

  markDirty(): void {
    this.dirty.set(true);
    this.saveError.set('');
    this.validationErrors.set([]);
  }

  errorsForGroup(groupKey: string): ImportPlanValidationError[] {
    return this.validationErrors().filter(e => e.groupKey === groupKey);
  }

  structuralErrors(): ImportPlanValidationError[] {
    return this.validationErrors().filter(e => e.groupKey === null);
  }

  previewDraft(): void {
    this.previewLoading.set(true);
    this.previewError.set('');
    this.packageSvc.previewPlanDraft(this.packageId, this.toInstructions(), 5).subscribe({
      next: result => { this.previewResult.set(result); this.previewLoading.set(false); },
      error: err => {
        this.previewError.set(err.error?.error ?? 'Could not generate a preview.');
        this.previewLoading.set(false);
      },
    });
  }

  saveDraft(): void {
    const plan = this.plan();
    if (!plan) return;
    this.saving.set(true);
    this.saveError.set('');
    this.validationErrors.set([]);
    this.concurrencyConflict.set(false);
    this.packageSvc.updatePlanDraft(this.packageId, plan.planId, plan.concurrencyStamp, this.toInstructions()).subscribe({
      next: updated => {
        this.plan.set(updated);
        this.rebuildForm(updated);
        this.saving.set(false);
      },
      error: err => {
        this.saving.set(false);
        if (err.status === 409) {
          this.concurrencyConflict.set(true);
          this.saveError.set(err.error?.error ?? 'This plan was changed elsewhere since you loaded it.');
          return;
        }
        const errors: ImportPlanValidationError[] | undefined = err.error?.errors;
        if (errors?.length) {
          this.validationErrors.set(errors);
          this.saveError.set(err.error?.error ?? 'This draft is not valid.');
          return;
        }
        this.saveError.set(err.error?.error ?? 'Could not save this draft.');
      },
    });
  }

  discardAndReload(): void {
    this.load();
  }

  /** Sample selection and structure analysis are fully automatic — there is no manual sample
   *  picker here. This button re-runs that same automatic process (e.g. after the archive
   *  contents were corrected upstream). */
  generatePlan(): void {
    this.regenerating.set(true);
    this.error.set('');
    const changeReason = this.plan() ? 'Regenerated by administrator from the Import Plan page.' : undefined;
    this.packageSvc.generatePlan(this.packageId, changeReason).subscribe({
      next: plan => { this.plan.set(plan); this.rebuildForm(plan); this.regenerating.set(false); },
      error: err => {
        this.error.set(err.error?.error ?? 'Could not generate an Import Execution Plan.');
        this.regenerating.set(false);
      },
    });
  }

  openReviseModal(): void {
    this.reviseReason = '';
    this.reviseError.set('');
    this.reviseModalOpen.set(true);
  }

  confirmRevise(): void {
    const plan = this.plan();
    if (!plan || !this.reviseReason.trim()) {
      this.reviseError.set('A reason is required.');
      return;
    }
    this.revising.set(true);
    this.reviseError.set('');
    this.packageSvc.revisePlan(this.packageId, plan.planId, this.reviseReason.trim()).subscribe({
      next: revision => {
        this.plan.set(revision);
        this.rebuildForm(revision);
        this.revising.set(false);
        this.reviseModalOpen.set(false);
      },
      error: err => {
        this.reviseError.set(err.error?.error ?? 'Could not create a revision of this plan.');
        this.revising.set(false);
      },
    });
  }

  openApprove(): void {
    this.approveError.set('');
    this.approvedCostCeiling = this.plan()?.estimate?.cost?.maxCost ?? null;
    this.approveModalOpen.set(true);
  }

  confirmApprove(): void {
    const plan = this.plan();
    if (!plan || this.approvedCostCeiling === null || this.approvedCostCeiling < 0) {
      this.approveError.set('An approved cost ceiling (>= 0) is required.');
      return;
    }
    this.approving.set(true);
    this.approveError.set('');
    this.packageSvc.approvePlan(this.packageId, plan.planId, this.approvedCostCeiling, plan.concurrencyStamp).subscribe({
      next: updated => {
        this.plan.set(updated);
        this.rebuildForm(updated);
        this.approving.set(false);
        this.approveModalOpen.set(false);
      },
      error: err => {
        this.approving.set(false);
        if (err.status === 409) {
          this.approveModalOpen.set(false);
          this.concurrencyConflict.set(true);
          this.saveError.set(err.error?.error ?? 'This plan was changed elsewhere since you loaded it. Reload before approving.');
          return;
        }
        const reasons: string[] | undefined = err.error?.blockingReasons;
        this.approveError.set(reasons?.length ? reasons.join(' ') : (err.error?.error ?? 'Could not approve this plan.'));
      },
    });
  }

  openReject(): void {
    this.rejectReason = '';
    this.rejectError.set('');
    this.rejectModalOpen.set(true);
  }

  confirmReject(): void {
    const plan = this.plan();
    if (!plan || !this.rejectReason.trim()) {
      this.rejectError.set('A rejection reason is required.');
      return;
    }
    this.rejecting.set(true);
    this.rejectError.set('');
    this.packageSvc.rejectPlan(this.packageId, plan.planId, this.rejectReason.trim()).subscribe({
      next: updated => {
        this.plan.set(updated);
        this.rebuildForm(updated);
        this.rejecting.set(false);
        this.rejectModalOpen.set(false);
      },
      error: err => {
        this.rejectError.set(err.error?.error ?? 'Could not reject this plan.');
        this.rejecting.set(false);
      },
    });
  }

  openResume(): void {
    const plan = this.plan();
    this.resumeCostCeiling = plan?.approvedCostCeiling ?? null;
    this.resumeReason = '';
    this.resumeError.set('');
    this.resumeConcurrencyConflict.set(false);
    this.resumeModalOpen.set(true);
  }

  /** Phase 4.4B — audited, concurrency-checked ceiling amendment. Requires a new ceiling strictly
   *  greater than the current one and a reason; never resumes silently or automatically. */
  confirmResume(): void {
    const plan = this.plan();
    if (!plan) return;
    const currentCeiling = plan.approvedCostCeiling ?? 0;
    if (this.resumeCostCeiling === null || this.resumeCostCeiling <= currentCeiling) {
      this.resumeError.set(`The new ceiling must be greater than the current approved ceiling (${currentCeiling}).`);
      return;
    }
    if (!this.resumeReason.trim()) {
      this.resumeError.set('A reason is required to raise the approved cost ceiling.');
      return;
    }
    this.resuming.set(true);
    this.resumeError.set('');
    this.resumeConcurrencyConflict.set(false);
    this.packageSvc.amendCostCeiling(this.packageId, plan.planId, plan.concurrencyStamp, this.resumeCostCeiling, this.resumeReason.trim()).subscribe({
      next: updated => {
        this.plan.set(updated);
        this.rebuildForm(updated);
        this.resuming.set(false);
        this.resumeModalOpen.set(false);
      },
      error: err => {
        this.resuming.set(false);
        if (err.status === 409) {
          this.resumeConcurrencyConflict.set(true);
          this.resumeError.set(err.error?.error ?? 'This plan was changed elsewhere since you loaded it. Reload before amending the ceiling.');
          return;
        }
        this.resumeError.set(err.error?.error ?? 'Could not amend the cost ceiling.');
      },
    });
  }

  statusTone(status: string): 'neutral' | 'success' | 'warning' | 'danger' {
    switch (status) {
      case 'Approved': case 'Completed': return 'success';
      case 'AwaitingApproval': case 'Executing': return 'neutral';
      case 'PausedForCostApproval': return 'warning';
      case 'Rejected': case 'Failed': case 'Cancelled': case 'Superseded': return 'danger';
      default: return 'neutral';
    }
  }

  /** Revision is only offered once execution can no longer be silently invalidated underneath
   *  it — i.e. before the package has started processing an approved plan. */
  canRevise(): boolean {
    const plan = this.plan();
    if (!plan) return false;
    return plan.status === 'Approved';
  }

  backToImport(): void {
    this.router.navigate(['/admin/content/import']);
  }
}
