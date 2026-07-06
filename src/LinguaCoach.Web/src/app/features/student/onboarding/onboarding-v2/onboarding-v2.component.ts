import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OnboardingV2Service } from '../../../../core/services/onboarding-v2.service';
import { FormioRendererComponent } from '../../../../shared/formio/formio-renderer.component';
import { OnboardingWizardComponent } from '../onboarding-wizard/onboarding-wizard.component';
import { FormRendererKind } from '../../../../shared/formio/form-renderer-kind.model';

/**
 * Onboarding is one admin-authored Form.io wizard schema, rendered by whichever engine the
 * active template version declares via `rendererKind`: 'Custom' (the purpose-built
 * OnboardingWizardComponent — used today) or 'FormIo' (the generic @formio/js-driven renderer,
 * kept as a fallback for any future template that doesn't use the custom UI). Either way this
 * component's job is the same: fetch the active template + any prior draft, periodically persist
 * a draft as the student fills the form in (debounced on `change`), and submit + navigate to
 * /placement on final submit.
 */
@Component({
  selector: 'app-onboarding-v2',
  standalone: true,
  imports: [CommonModule, FormioRendererComponent, OnboardingWizardComponent],
  template: `
    <div *ngIf="loading" class="text-center py-12 text-slate-500">Loading...</div>
    <div *ngIf="error" class="sp-alert-error mb-4">{{ error }}</div>

    <ng-container *ngIf="!loading && schema()">
      <app-onboarding-wizard
        *ngIf="rendererKind() === 'Custom'; else formioRenderer"
        [schema]="schema()"
        [submissionData]="submissionData()"
        [disabled]="submitting()"
        (change)="onChange($event)"
        (submit)="onSubmit($event)" />
      <ng-template #formioRenderer>
        <app-formio-renderer
          [schema]="schema()"
          [submissionData]="submissionData()"
          [disabled]="submitting()"
          (change)="onChange($event)"
          (submit)="onSubmit($event)" />
      </ng-template>
    </ng-container>

    <div *ngIf="submitError" class="sp-alert-error mt-4">{{ submitError }}</div>
  `,
})
export class OnboardingV2Component implements OnInit {
  loading = true;
  error: string | null = null;
  submitError: string | null = null;
  submitting = signal(false);

  schema = signal<any>(null);
  submissionData = signal<any>(null);
  rendererKind = signal<FormRendererKind>('FormIo');

  private draftSaveTimer: ReturnType<typeof setTimeout> | null = null;
  private static readonly DRAFT_SAVE_DEBOUNCE_MS = 2000;

  constructor(
    private svc: OnboardingV2Service,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.error = null;
    this.svc.getActive().subscribe({
      next: active => {
        if (active.isComplete) {
          this.router.navigate(['/placement']);
          return;
        }
        try {
          this.schema.set(JSON.parse(active.formIoSchemaJson));
        } catch {
          this.error = 'Could not load onboarding. Please refresh and try again.';
          this.loading = false;
          return;
        }
        this.rendererKind.set(active.rendererKind ?? 'FormIo');
        this.submissionData.set(active.submissionJson ? this.tryParse(active.submissionJson) : {});
        this.loading = false;
      },
      error: () => {
        this.error = 'Could not load onboarding. Please refresh and try again.';
        this.loading = false;
      },
    });
  }

  private tryParse(json: string): any {
    try {
      return JSON.parse(json);
    } catch {
      return {};
    }
  }

  onChange(data: any): void {
    if (this.draftSaveTimer) clearTimeout(this.draftSaveTimer);
    this.draftSaveTimer = setTimeout(() => {
      this.svc.saveDraft(JSON.stringify(data ?? {})).subscribe({
        // Best-effort autosave — a failure here shouldn't interrupt the student typing;
        // their final submit is what matters and will surface any real error.
        error: () => {},
      });
    }, OnboardingV2Component.DRAFT_SAVE_DEBOUNCE_MS);
  }

  onSubmit(data: any): void {
    if (this.submitting()) return;
    this.submitError = null;
    this.submitting.set(true);
    this.svc.submit(JSON.stringify(data ?? {})).subscribe({
      next: () => this.router.navigate(['/placement']),
      error: err => {
        this.submitError = err?.error?.error ?? 'Could not submit onboarding. Please try again.';
        this.submitting.set(false);
      },
    });
  }
}
