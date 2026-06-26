import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminUsagePoliciesComponent } from './admin-usage-policies.component';
import { UsageGovernanceService, UsagePolicy, UsagePolicyRule, FeatureDefinition } from '../../../core/services/usage-governance.service';

function makeRule(overrides: Partial<UsagePolicyRule> = {}): UsagePolicyRule {
  return {
    id: 'rule-1',
    featureKey: 'writing.evaluate',
    trackingEnabled: true,
    enforcementMode: 'TrackOnly',
    unitType: 'Count',
    dailyLimit: null,
    weeklyLimit: null,
    monthlyLimit: 10,
    dailyCostLimit: null,
    monthlyCostLimit: null,
    warningThresholdPercent: 80,
    isActive: true,
    ...overrides,
  };
}

function makePolicy(overrides: Partial<UsagePolicy> = {}): UsagePolicy {
  return {
    id: 'policy-1',
    name: 'Default Pilot Student',
    description: 'Default policy',
    scopeType: 'Global',
    isDefault: true,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    rules: [],
    ...overrides,
  };
}

function makeFeature(overrides: Partial<FeatureDefinition> = {}): FeatureDefinition {
  return {
    id: 'feat-1',
    key: 'writing.evaluate',
    name: 'Evaluate Writing',
    description: null,
    category: 'ExpensiveAi',
    defaultEnforcementMode: 'TrackOnly',
    unitType: 'Count',
    isExpensive: true,
    isStudentVisible: true,
    isEnabledByDefault: true,
    ...overrides,
  };
}

describe('AdminUsagePoliciesComponent', () => {
  let svc: jasmine.SpyObj<UsageGovernanceService>;

  beforeEach(() => {
    svc = jasmine.createSpyObj('UsageGovernanceService', [
      'listUsagePolicies', 'listFeatureDefinitions', 'createUsagePolicy', 'updateUsagePolicy',
    ]);
    svc.listUsagePolicies.and.returnValue(of([makePolicy()]));
    svc.listFeatureDefinitions.and.returnValue(of([makeFeature()]));

    TestBed.configureTestingModule({
      imports: [AdminUsagePoliciesComponent],
      providers: [{ provide: UsageGovernanceService, useValue: svc }],
    });
  });

  // ── 1. Policy list renders ────────────────────────────────────────────────

  it('renders policy list on init', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-page-header')).toBeTruthy();
    expect(html.querySelector('sp-admin-table')).toBeTruthy();
    expect(html.textContent).toContain('Default Pilot Student');
  });

  // ── 2. Stat cards render ──────────────────────────────────────────────────

  it('renders stat cards after load', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    const statCards = html.querySelectorAll('sp-admin-kpi-card');
    expect(statCards.length).toBeGreaterThanOrEqual(3);
  });

  it('computed totalPolicies equals policies count', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    expect(c.totalPolicies()).toBe(1);
  });

  it('computed activePolicies counts only active policies', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    c.policies.set([makePolicy({ isActive: true }), makePolicy({ id: 'p2', isActive: false })]);
    expect(c.activePolicies()).toBe(1);
  });

  it('computed defaultPolicy returns the default policy', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    expect(c.defaultPolicy()?.name).toBe('Default Pilot Student');
  });

  it('computed defaultPolicy returns null when no default', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    c.policies.set([makePolicy({ isDefault: false })]);
    expect(c.defaultPolicy()).toBeNull();
  });

  // ── 3. Create form shown ──────────────────────────────────────────────────

  it('shows create form when New Policy clicked', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    fixture.detectChanges();

    expect(c.policyDrawerOpen()).toBeTrue();
    expect(c.editingId()).toBeNull();
  });

  // ── 4. Edit form pre-fills values ────────────────────────────────────────

  it('pre-fills edit form with policy values', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    const policy = makePolicy({ name: 'Low Cost Student', isDefault: false });

    c.openEdit(policy);

    expect(c.formName()).toBe('Low Cost Student');
    expect(c.formIsDefault()).toBeFalse();
    expect(c.editingId()).toBe(policy.id);
  });

  // ── 5. Create policy calls service ───────────────────────────────────────

  it('calls createUsagePolicy on save when creating', () => {
    svc.createUsagePolicy.and.returnValue(of(makePolicy({ id: 'new-id', name: 'New Policy' })));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    c.formName.set('New Policy');
    c.save();

    expect(svc.createUsagePolicy).toHaveBeenCalledWith(jasmine.objectContaining({ name: 'New Policy' }));
  });

  // ── 6. Update policy calls service ───────────────────────────────────────

  it('calls updateUsagePolicy on save when editing', () => {
    const policy = makePolicy();
    svc.updateUsagePolicy.and.returnValue(of(policy));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openEdit(policy);
    c.formName.set('Renamed Policy');
    c.save();

    expect(svc.updateUsagePolicy).toHaveBeenCalledWith(
      policy.id,
      jasmine.objectContaining({ name: 'Renamed Policy' })
    );
  });

  // ── 7. Error state renders ────────────────────────────────────────────────

  it('shows error message when load fails', () => {
    svc.listUsagePolicies.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Server error');
  });

  // ── 8. Save blocked when name empty ──────────────────────────────────────

  it('does not call service when name is empty', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    c.formName.set('   ');
    c.save();

    expect(svc.createUsagePolicy).not.toHaveBeenCalled();
    expect(c.saveError()).toBeTruthy();
  });

  // ── 9. Cancel hides form ──────────────────────────────────────────────────

  it('hides form on cancel', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    expect(c.policyDrawerOpen()).toBeTrue();
    c.closePolicyDrawer();
    expect(c.policyDrawerOpen()).toBeFalse();
  });

  // ── 10. Page body wrapper present ────────────────────────────────────────

  it('renders sp-admin-page-body wrapper', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-page-body')).toBeTruthy();
  });

  // ── 11. Scope type options available ─────────────────────────────────────

  it('exposes scopeTypeOptions with Global and Student', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    const values = c.scopeTypeOptions.map(o => o.value);
    expect(values).toContain('Global');
    expect(values).toContain('Student');
  });

  // ── 12. Create payload includes correct scope type ────────────────────────

  it('includes formScopeType in create payload', () => {
    svc.createUsagePolicy.and.returnValue(of(makePolicy({ id: 'new-id' })));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    c.formName.set('Scoped Policy');
    c.formScopeType.set('Global');
    c.save();

    expect(svc.createUsagePolicy).toHaveBeenCalledWith(
      jasmine.objectContaining({ scopeType: 'Global' })
    );
  });

  // ── 13. Rule expand/collapse ──────────────────────────────────────────────

  it('toggleExpanded sets expandedPolicyId to policy id', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.toggleExpanded('policy-1');
    expect(c.expandedPolicyId()).toBe('policy-1');
  });

  it('toggleExpanded collapses when same id toggled again', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.toggleExpanded('policy-1');
    c.toggleExpanded('policy-1');
    expect(c.expandedPolicyId()).toBeNull();
  });

  // ── 14. Feature name lookup ───────────────────────────────────────────────

  it('featureName returns display name from features signal', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    c.features.set([makeFeature({ key: 'writing.evaluate', name: 'Evaluate Writing' })]);

    expect(c.featureName('writing.evaluate')).toBe('Evaluate Writing');
  });

  it('featureName falls back to key when feature not found', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    expect(c.featureName('unknown.feature')).toBe('unknown.feature');
  });

  // ── 15. enforcementBadgeTone maps modes correctly ────────────────────────

  it('enforcementBadgeTone maps HardLimit to danger', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.enforcementBadgeTone('HardLimit')).toBe('danger');
  });

  it('enforcementBadgeTone maps SoftWarning to warning', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.enforcementBadgeTone('SoftWarning')).toBe('warning');
  });

  it('enforcementBadgeTone maps TrackOnly to success', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.enforcementBadgeTone('TrackOnly')).toBe('success');
  });

  // ── 16. ruleLimitSummary produces correct labels ──────────────────────────

  it('ruleLimitSummary includes monthly limit label', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    const rule = makeRule({ monthlyLimit: 50 });
    expect(c.ruleLimitSummary(rule)).toContain('Monthly: 50');
  });

  it('ruleLimitSummary returns empty array when no limits set', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;
    const rule = makeRule({ dailyLimit: null, weeklyLimit: null, monthlyLimit: null, dailyCostLimit: null, monthlyCostLimit: null });
    expect(c.ruleLimitSummary(rule).length).toBe(0);
  });

  // ── 17. Empty state renders ───────────────────────────────────────────────

  it('shows empty state when no policies', () => {
    svc.listUsagePolicies.and.returnValue(of([]));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-empty-state')).toBeTruthy();
  });
});
