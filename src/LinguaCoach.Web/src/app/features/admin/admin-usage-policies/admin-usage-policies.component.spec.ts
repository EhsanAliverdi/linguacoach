import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminUsagePoliciesComponent } from './admin-usage-policies.component';
import { UsageGovernanceService, UsagePolicy, FeatureDefinition } from '../../../core/services/usage-governance.service';

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

  // ── 2. Create form shown ──────────────────────────────────────────────────

  it('shows create form when New Policy clicked', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    fixture.detectChanges();

    expect(c.showForm()).toBeTrue();
    expect(c.editingId()).toBeNull();
  });

  // ── 3. Edit form pre-fills values ────────────────────────────────────────

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

  // ── 4. Create policy calls service ───────────────────────────────────────

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

  // ── 5. Update policy calls service ───────────────────────────────────────

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

  // ── 6. Error state renders ────────────────────────────────────────────────

  it('shows error message when load fails', () => {
    svc.listUsagePolicies.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('Server error');
  });

  // ── 7. Save blocked when name empty ──────────────────────────────────────

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

  // ── 8. Cancel hides form ──────────────────────────────────────────────────

  it('hides form on cancel', () => {
    const fixture = TestBed.createComponent(AdminUsagePoliciesComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.openCreate();
    expect(c.showForm()).toBeTrue();
    c.cancel();
    expect(c.showForm()).toBeFalse();
  });
});
