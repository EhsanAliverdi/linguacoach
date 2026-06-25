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
  AddUsagePolicyRuleRequest,
  UpdateUsagePolicyRuleRequest,
} from '../../../core/services/usage-governance.service';
import { SpAdminBadgeTone } from '../../../design-system/admin/components/badge/sp-admin-badge.component';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCodePillComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminIconComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-admin-usage-policies',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminButtonGroupComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCodePillComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminIconComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-usage-policies.component.html',
  styleUrl: './admin-usage-policies.component.css',
})
export class AdminUsagePoliciesComponent implements OnInit {
  policies = signal<UsagePolicy[]>([]);
  features = signal<FeatureDefinition[]>([]);
  loading = signal(true);
  error = signal('');
  saving = signal(false);
  saveError = signal('');
  saveSuccess = signal('');

  // Policy slide-over
  policyDrawerOpen = signal(false);
  editingId = signal<string | null>(null);
  expandedPolicyId = signal<string | null>(null);

  // Policy form fields
  formName = signal('');
  formDescription = signal('');
  formScopeType = signal('Student');
  formIsDefault = signal(false);
  formIsActive = signal(true);

  // Rule slide-over
  ruleDrawerOpen = signal(false);
  ruleDrawerPolicyId = signal<string | null>(null);
  ruleEditingId = signal<string | null>(null);
  ruleSaving = signal(false);
  ruleSaveError = signal('');

  // Delete slide-over
  deleteDrawerOpen = signal(false);
  deleteTargetPolicyId = signal<string | null>(null);
  deleteTargetRuleId = signal<string | null>(null);
  deleteTargetRuleKey = signal('');
  deleteConfirming = signal(false);
  deleteError = signal('');

  // Rule form fields
  ruleFeatureKey = signal('');
  ruleEnforcementMode = signal('TrackOnly');
  ruleUnitType = signal('Count');
  ruleDailyLimit = signal<number | null>(null);
  ruleWeeklyLimit = signal<number | null>(null);
  ruleMonthlyLimit = signal<number | null>(null);
  ruleDailyCostLimit = signal<number | null>(null);
  ruleMonthlyCostLimit = signal<number | null>(null);
  ruleWarningThreshold = signal(80);
  ruleTrackingEnabled = signal(true);
  ruleIsActive = signal(true);

  readonly scopeTypeOptions = [
    { value: 'Global', label: 'Global' },
    { value: 'Student', label: 'Student' },
  ];

  readonly enforcementModeOptions = [
    { value: 'None', label: 'None' },
    { value: 'TrackOnly', label: 'Track Only' },
    { value: 'SoftWarning', label: 'Soft Warning' },
    { value: 'HardLimit', label: 'Hard Limit' },
    { value: 'AdminApprovalRequired', label: 'Admin Approval Required' },
  ];

  readonly unitTypeOptions = [
    { value: 'Count', label: 'Count' },
    { value: 'Tokens', label: 'Tokens' },
    { value: 'InputTokens', label: 'Input Tokens' },
    { value: 'OutputTokens', label: 'Output Tokens' },
    { value: 'Minutes', label: 'Minutes' },
    { value: 'Seconds', label: 'Seconds' },
    { value: 'Characters', label: 'Characters' },
    { value: 'Cost', label: 'Cost ($)' },
  ];

  totalPolicies = computed(() => this.policies().length);
  activePolicies = computed(() => this.policies().filter(p => p.isActive).length);
  defaultPolicy = computed(() => this.policies().find(p => p.isDefault) ?? null);

  featureNameMap = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const f of this.features()) map[f.key] = f.name;
    return map;
  });

  featureOptions = computed(() =>
    this.features().map(f => ({ value: f.key, label: `${f.name} (${f.key})` }))
  );

  policyDrawerTitle = computed(() => this.editingId() ? 'Edit Policy' : 'New Policy');

  ruleDrawerTitle = computed(() => this.ruleEditingId() ? 'Edit Rule' : 'Add Rule');

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

  // ── Policy slide-over ────────────────────────────────────────────────────

  openCreate(): void {
    this.editingId.set(null);
    this.formName.set('');
    this.formDescription.set('');
    this.formScopeType.set('Student');
    this.formIsDefault.set(false);
    this.formIsActive.set(true);
    this.saveError.set('');
    this.saveSuccess.set('');
    this.policyDrawerOpen.set(true);
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
    this.policyDrawerOpen.set(true);
  }

  closePolicyDrawer(): void {
    this.policyDrawerOpen.set(false);
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
        next: () => { this.saving.set(false); this.saveSuccess.set('Policy updated.'); this.policyDrawerOpen.set(false); this.load(); },
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
        next: () => { this.saving.set(false); this.saveSuccess.set('Policy created.'); this.policyDrawerOpen.set(false); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(err.error?.message ?? 'Create failed.'); },
      });
    }
  }

  policyFooterActions = computed(() => [
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
    { id: 'save', label: this.editingId() ? 'Update Policy' : 'Create Policy', variant: 'primary' as const, appearance: 'solid' as const, loading: this.saving(), disabled: this.saving() },
  ]);

  onPolicyFooterAction(id: string): void {
    if (id === 'save') this.save();
    else this.closePolicyDrawer();
  }

  // ── Rule slide-over ──────────────────────────────────────────────────────

  openAddRule(policyId: string): void {
    this.ruleDrawerPolicyId.set(policyId);
    this.ruleEditingId.set(null);
    this.ruleFeatureKey.set('');
    this.ruleEnforcementMode.set('TrackOnly');
    this.ruleUnitType.set('Count');
    this.ruleDailyLimit.set(null);
    this.ruleWeeklyLimit.set(null);
    this.ruleMonthlyLimit.set(null);
    this.ruleDailyCostLimit.set(null);
    this.ruleMonthlyCostLimit.set(null);
    this.ruleWarningThreshold.set(80);
    this.ruleTrackingEnabled.set(true);
    this.ruleIsActive.set(true);
    this.ruleSaveError.set('');
    this.ruleDrawerOpen.set(true);
  }

  openEditRule(policyId: string, rule: UsagePolicyRule): void {
    this.ruleDrawerPolicyId.set(policyId);
    this.ruleEditingId.set(rule.id);
    this.ruleFeatureKey.set(rule.featureKey);
    this.ruleEnforcementMode.set(rule.enforcementMode);
    this.ruleUnitType.set(rule.unitType);
    this.ruleDailyLimit.set(rule.dailyLimit);
    this.ruleWeeklyLimit.set(rule.weeklyLimit);
    this.ruleMonthlyLimit.set(rule.monthlyLimit);
    this.ruleDailyCostLimit.set(rule.dailyCostLimit);
    this.ruleMonthlyCostLimit.set(rule.monthlyCostLimit);
    this.ruleWarningThreshold.set(rule.warningThresholdPercent);
    this.ruleTrackingEnabled.set(rule.trackingEnabled);
    this.ruleIsActive.set(rule.isActive);
    this.ruleSaveError.set('');
    this.ruleDrawerOpen.set(true);
  }

  closeRuleDrawer(): void {
    this.ruleDrawerOpen.set(false);
  }

  saveRule(): void {
    const featureKey = this.ruleFeatureKey().trim();
    if (!featureKey) { this.ruleSaveError.set('Feature is required.'); return; }
    if (!this.ruleEnforcementMode()) { this.ruleSaveError.set('Enforcement mode is required.'); return; }
    if (!this.ruleUnitType()) { this.ruleSaveError.set('Unit type is required.'); return; }

    const daily = this.ruleDailyLimit();
    const weekly = this.ruleWeeklyLimit();
    const monthly = this.ruleMonthlyLimit();
    const dailyCost = this.ruleDailyCostLimit();
    const monthlyCost = this.ruleMonthlyCostLimit();

    if (daily !== null && daily < 0) { this.ruleSaveError.set('Daily limit must be 0 or greater.'); return; }
    if (weekly !== null && weekly < 0) { this.ruleSaveError.set('Weekly limit must be 0 or greater.'); return; }
    if (monthly !== null && monthly < 0) { this.ruleSaveError.set('Monthly limit must be 0 or greater.'); return; }
    if (dailyCost !== null && dailyCost < 0) { this.ruleSaveError.set('Daily cost limit must be 0 or greater.'); return; }
    if (monthlyCost !== null && monthlyCost < 0) { this.ruleSaveError.set('Monthly cost limit must be 0 or greater.'); return; }

    const threshold = this.ruleWarningThreshold();
    if (threshold < 0 || threshold > 100) { this.ruleSaveError.set('Warning threshold must be 0–100.'); return; }

    this.ruleSaving.set(true);
    this.ruleSaveError.set('');

    const policyId = this.ruleDrawerPolicyId()!;
    const editId = this.ruleEditingId();

    if (editId) {
      const req: UpdateUsagePolicyRuleRequest = {
        trackingEnabled: this.ruleTrackingEnabled(),
        enforcementMode: this.ruleEnforcementMode(),
        unitType: this.ruleUnitType(),
        dailyLimit: daily,
        weeklyLimit: weekly,
        monthlyLimit: monthly,
        dailyCostLimit: dailyCost,
        monthlyCostLimit: monthlyCost,
        warningThresholdPercent: threshold,
        isActive: this.ruleIsActive(),
      };
      this.svc.updateRule(policyId, editId, req).subscribe({
        next: updatedRule => {
          this.ruleSaving.set(false);
          this.ruleDrawerOpen.set(false);
          this.updateRuleInPlace(policyId, updatedRule);
          this.saveSuccess.set('Rule updated.');
        },
        error: err => { this.ruleSaving.set(false); this.ruleSaveError.set(err.error?.message ?? 'Save failed.'); },
      });
    } else {
      const req: AddUsagePolicyRuleRequest = {
        featureKey,
        trackingEnabled: this.ruleTrackingEnabled(),
        enforcementMode: this.ruleEnforcementMode(),
        unitType: this.ruleUnitType(),
        dailyLimit: daily,
        weeklyLimit: weekly,
        monthlyLimit: monthly,
        dailyCostLimit: dailyCost,
        monthlyCostLimit: monthlyCost,
        warningThresholdPercent: threshold,
        isActive: this.ruleIsActive(),
      };
      this.svc.addRule(policyId, req).subscribe({
        next: newRule => {
          this.ruleSaving.set(false);
          this.ruleDrawerOpen.set(false);
          this.addRuleInPlace(policyId, newRule);
          this.saveSuccess.set('Rule added.');
        },
        error: err => { this.ruleSaving.set(false); this.ruleSaveError.set(err.error?.message ?? 'Add failed.'); },
      });
    }
  }

  ruleFooterActions = computed(() => [
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
    { id: 'save', label: this.ruleEditingId() ? 'Update Rule' : 'Add Rule', variant: 'primary' as const, appearance: 'solid' as const, loading: this.ruleSaving(), disabled: this.ruleSaving() },
  ]);

  onRuleFooterAction(id: string): void {
    if (id === 'save') this.saveRule();
    else this.closeRuleDrawer();
  }

  // ── Delete slide-over ────────────────────────────────────────────────────

  openDeleteRule(policyId: string, rule: UsagePolicyRule): void {
    this.deleteTargetPolicyId.set(policyId);
    this.deleteTargetRuleId.set(rule.id);
    this.deleteTargetRuleKey.set(rule.featureKey);
    this.deleteError.set('');
    this.deleteDrawerOpen.set(true);
  }

  closeDeleteDrawer(): void {
    this.deleteDrawerOpen.set(false);
  }

  deleteFooterActions = computed(() => [
    { id: 'cancel', label: 'Cancel', variant: 'neutral' as const, appearance: 'outline' as const },
    { id: 'delete', label: 'Delete Rule', variant: 'danger' as const, appearance: 'solid' as const, loading: this.deleteConfirming(), disabled: this.deleteConfirming() },
  ]);

  onDeleteFooterAction(id: string): void {
    if (id === 'delete') this.confirmDelete();
    else this.closeDeleteDrawer();
  }

  confirmDelete(): void {
    const policyId = this.deleteTargetPolicyId()!;
    const ruleId = this.deleteTargetRuleId()!;
    this.deleteConfirming.set(true);
    this.deleteError.set('');

    this.svc.deleteRule(policyId, ruleId).subscribe({
      next: () => {
        this.deleteConfirming.set(false);
        this.deleteDrawerOpen.set(false);
        this.removeRuleInPlace(policyId, ruleId);
        this.saveSuccess.set('Rule deleted.');
      },
      error: err => { this.deleteConfirming.set(false); this.deleteError.set(err.error?.message ?? 'Delete failed.'); },
    });
  }

  // ── Local state update helpers ───────────────────────────────────────────

  private addRuleInPlace(policyId: string, rule: UsagePolicyRule): void {
    this.policies.update(list =>
      list.map(p => p.id === policyId ? { ...p, rules: [...p.rules, rule] } : p)
    );
  }

  private updateRuleInPlace(policyId: string, updated: UsagePolicyRule): void {
    this.policies.update(list =>
      list.map(p => p.id !== policyId ? p : {
        ...p, rules: p.rules.map(r => r.id === updated.id ? updated : r)
      })
    );
  }

  private removeRuleInPlace(policyId: string, ruleId: string): void {
    this.policies.update(list =>
      list.map(p => p.id !== policyId ? p : {
        ...p, rules: p.rules.filter(r => r.id !== ruleId)
      })
    );
  }

  // ── Display helpers ──────────────────────────────────────────────────────

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
    if (rule.dailyCostLimit != null) parts.push(`Daily $${rule.dailyCostLimit}`);
    if (rule.monthlyCostLimit != null) parts.push(`Monthly $${rule.monthlyCostLimit}`);
    return parts;
  }
}
