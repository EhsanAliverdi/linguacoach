import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  UsageGovernanceService,
  UsagePolicy,
  UsagePolicyRule,
  FeatureDefinition,
  CreateUsagePolicyRequest,
  UpdateUsagePolicyRequest,
} from '../../../core/services/usage-governance.service';
import { SpAdminBadgeTone } from '../../../admin/components/badge/sp-admin-badge.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCodePillComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminSelectComponent,
  SpAdminStatCardComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-usage-policies',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCodePillComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminSelectComponent,
    SpAdminStatCardComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-usage-policies.component.html',
  styles: [`
    .sp-up-stats { display:grid; grid-template-columns:repeat(auto-fill,minmax(180px,1fr)); gap:16px; margin-bottom:24px; }
    .sp-up-form-stack { display:flex; flex-direction:column; gap:14px; }
    .sp-up-cb-stack { display:flex; flex-direction:column; gap:10px; }
    .sp-up-actions { display:flex; gap:10px; }
    .sp-up-rules-row td { padding-top:0 !important; background:var(--sp-admin-surface-alt, #f9fafb); }
    .sp-up-rules-inner { padding:12px 16px; display:flex; flex-direction:column; gap:8px; }
    .sp-up-rule-row { display:flex; flex-wrap:wrap; align-items:center; gap:8px; padding:6px 0; border-bottom:1px solid var(--sp-admin-border,#e5e7eb); }
    .sp-up-rule-row:last-child { border-bottom:none; }
    .sp-up-rule-limits { display:flex; flex-wrap:wrap; gap:6px; font-size:12px; color:var(--sp-admin-text-muted,#6b7280); }
    .sp-up-rule-limit { background:var(--sp-admin-surface,#fff); border:1px solid var(--sp-admin-border,#e5e7eb); border-radius:6px; padding:2px 8px; }
    .sp-up-no-rules { font-size:12px; color:var(--sp-admin-text-muted,#6b7280); padding:8px 0; }
    .sp-up-expand-btn { background:none; border:none; cursor:pointer; color:var(--sp-admin-brand,#465fff); font-size:12px; padding:0; }
    .sp-up-expand-btn:hover { text-decoration:underline; }
  `],
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
  expandedPolicyId = signal<string | null>(null);

  formName = signal('');
  formDescription = signal('');
  formScopeType = signal('Student');
  formIsDefault = signal(false);
  formIsActive = signal(true);

  readonly scopeTypeOptions = [
    { value: 'Global', label: 'Global' },
    { value: 'Student', label: 'Student' },
  ];

  // Computed summary stats
  totalPolicies = computed(() => this.policies().length);
  activePolicies = computed(() => this.policies().filter(p => p.isActive).length);
  defaultPolicy = computed(() => this.policies().find(p => p.isDefault) ?? null);

  // Feature name lookup map
  featureNameMap = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const f of this.features()) map[f.key] = f.name;
    return map;
  });

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

  toggleExpanded(policyId: string): void {
    this.expandedPolicyId.set(this.expandedPolicyId() === policyId ? null : policyId);
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

  featureName(key: string): string {
    return this.featureNameMap()[key] ?? key;
  }

  enforcementBadgeTone(mode: string): SpAdminBadgeTone {
    return mode === 'HardLimit' ? 'danger'
      : mode === 'SoftWarning' ? 'warning'
      : mode === 'TrackOnly' ? 'success'
      : 'neutral';
  }

  ruleLimitSummary(rule: UsagePolicyRule): string[] {
    const parts: string[] = [];
    if (rule.dailyLimit != null) parts.push(`Daily: ${rule.dailyLimit}`);
    if (rule.weeklyLimit != null) parts.push(`Weekly: ${rule.weeklyLimit}`);
    if (rule.monthlyLimit != null) parts.push(`Monthly: ${rule.monthlyLimit}`);
    if (rule.dailyCostLimit != null) parts.push(`Daily cost: $${rule.dailyCostLimit}`);
    if (rule.monthlyCostLimit != null) parts.push(`Monthly cost: $${rule.monthlyCostLimit}`);
    return parts;
  }
}
