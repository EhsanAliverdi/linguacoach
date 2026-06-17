import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  UsageGovernanceService,
  UsagePolicy,
  FeatureDefinition,
  CreateUsagePolicyRequest,
  UpdateUsagePolicyRequest,
} from '../../../core/services/usage-governance.service';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageHeaderComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-usage-policies',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageHeaderComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-usage-policies.component.html',
})
export class AdminUsagePoliciesComponent implements OnInit {
  policies = signal<UsagePolicy[]>([]);
  features = signal<FeatureDefinition[]>([]);
  loading = signal(true);
  error = signal('');
  saving = signal(false);
  saveError = signal('');
  saveSuccess = signal('');

  showForm = signal(false);
  editingId = signal<string | null>(null);

  formName = signal('');
  formDescription = signal('');
  formScopeType = signal('Student');
  formIsDefault = signal(false);
  formIsActive = signal(true);

  constructor(private svc: UsageGovernanceService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.svc.listUsagePolicies().subscribe({
      next: p => { this.policies.set(p); this.loading.set(false); },
      error: err => { this.error.set(err.error?.message ?? 'Failed to load usage policies.'); this.loading.set(false); },
    });
    this.svc.listFeatureDefinitions().subscribe({
      next: f => this.features.set(f),
    });
  }

  openCreate(): void {
    this.editingId.set(null);
    this.formName.set('');
    this.formDescription.set('');
    this.formScopeType.set('Student');
    this.formIsDefault.set(false);
    this.formIsActive.set(true);
    this.saveError.set('');
    this.saveSuccess.set('');
    this.showForm.set(true);
  }

  openEdit(p: UsagePolicy): void {
    this.editingId.set(p.id);
    this.formName.set(p.name);
    this.formDescription.set(p.description ?? '');
    this.formScopeType.set(p.scopeType);
    this.formIsDefault.set(p.isDefault);
    this.formIsActive.set(p.isActive);
    this.saveError.set('');
    this.saveSuccess.set('');
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
  }

  save(): void {
    const name = this.formName().trim();
    if (!name) { this.saveError.set('Name is required.'); return; }

    this.saving.set(true);
    this.saveError.set('');
    this.saveSuccess.set('');

    const id = this.editingId();
    if (id) {
      const req: UpdateUsagePolicyRequest = {
        name,
        description: this.formDescription() || null,
        isDefault: this.formIsDefault(),
        isActive: this.formIsActive(),
      };
      this.svc.updateUsagePolicy(id, req).subscribe({
        next: () => { this.saving.set(false); this.saveSuccess.set('Policy updated.'); this.showForm.set(false); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(err.error?.message ?? 'Save failed.'); },
      });
    } else {
      const req: CreateUsagePolicyRequest = {
        name,
        description: this.formDescription() || null,
        scopeType: this.formScopeType(),
        isDefault: this.formIsDefault(),
        isActive: this.formIsActive(),
        rules: [],
      };
      this.svc.createUsagePolicy(req).subscribe({
        next: () => { this.saving.set(false); this.saveSuccess.set('Policy created.'); this.showForm.set(false); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(err.error?.message ?? 'Create failed.'); },
      });
    }
  }

  enforcementBadgeColor(mode: string): string {
    return mode === 'HardLimit' ? 'var(--sp-speaking)'
      : mode === 'SoftWarning' ? 'var(--sp-warn)'
      : mode === 'TrackOnly' ? 'var(--sp-success)'
      : 'var(--sp-muted)';
  }
}
