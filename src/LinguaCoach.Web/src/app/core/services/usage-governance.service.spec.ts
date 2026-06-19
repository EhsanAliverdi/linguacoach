import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { UsageGovernanceService } from './usage-governance.service';

describe('UsageGovernanceService', () => {
  let svc: UsageGovernanceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [UsageGovernanceService],
    });
    svc = TestBed.inject(UsageGovernanceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('listFeatureDefinitions calls GET /api/admin/feature-definitions', () => {
    svc.listFeatureDefinitions().subscribe(r => expect(r).toEqual([]));
    const req = http.expectOne('/api/admin/feature-definitions');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('listUsagePolicies calls GET /api/admin/usage-policies', () => {
    svc.listUsagePolicies().subscribe(r => expect(r).toEqual([]));
    const req = http.expectOne('/api/admin/usage-policies');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('createUsagePolicy calls POST /api/admin/usage-policies', () => {
    const body = { name: 'Test', description: null, scopeType: 'Student', isDefault: false, isActive: true, rules: [] };
    svc.createUsagePolicy(body).subscribe();
    const req = http.expectOne('/api/admin/usage-policies');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('Test');
    req.flush({ id: 'x', ...body, createdAt: '', updatedAt: '' });
  });

  it('updateUsagePolicy calls PUT /api/admin/usage-policies/:id', () => {
    const id = 'policy-123';
    const body = { name: 'Updated', description: null, isDefault: false, isActive: true };
    svc.updateUsagePolicy(id, body).subscribe();
    const req = http.expectOne(`/api/admin/usage-policies/${id}`);
    expect(req.request.method).toBe('PUT');
    req.flush({ id, ...body, scopeType: 'Student', createdAt: '', updatedAt: '', rules: [] });
  });

  it('assignStudentPolicy calls PUT /api/admin/students/:id/usage-policy', () => {
    const studentId = 'student-1';
    const policyId = 'policy-1';
    svc.assignStudentPolicy(studentId, policyId, null).subscribe();
    const req = http.expectOne(`/api/admin/students/${studentId}/usage-policy`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.policyId).toBe(policyId);
    req.flush(null);
  });

  it('getStudentUsage calls GET /api/admin/students/:id/usage with period=today', () => {
    const studentId = 'student-2';
    svc.getStudentUsage(studentId, 'today').subscribe();
    const req = http.expectOne(r => r.url === `/api/admin/students/${studentId}/usage`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('period')).toBe('today');
    req.flush({ totalTokens: 0 });
  });

  it('addRule calls POST /api/admin/usage-policies/:policyId/rules', () => {
    const policyId = 'policy-1';
    const body = {
      featureKey: 'writing.evaluate', trackingEnabled: true,
      enforcementMode: 'HardLimit', unitType: 'Count',
      dailyLimit: 5, weeklyLimit: null, monthlyLimit: null,
      dailyCostLimit: null, monthlyCostLimit: null,
      warningThresholdPercent: 80, isActive: true,
    };
    svc.addRule(policyId, body).subscribe();
    const req = http.expectOne(`/api/admin/usage-policies/${policyId}/rules`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.featureKey).toBe('writing.evaluate');
    expect(req.request.body.dailyLimit).toBe(5);
    req.flush({ id: 'rule-1', ...body });
  });

  it('updateRule calls PUT /api/admin/usage-policies/:policyId/rules/:ruleId', () => {
    const policyId = 'policy-1';
    const ruleId = 'rule-1';
    const body = {
      trackingEnabled: false, enforcementMode: 'TrackOnly', unitType: 'Tokens',
      dailyLimit: null, weeklyLimit: null, monthlyLimit: 100,
      dailyCostLimit: null, monthlyCostLimit: null,
      warningThresholdPercent: 70, isActive: true,
    };
    svc.updateRule(policyId, ruleId, body).subscribe();
    const req = http.expectOne(`/api/admin/usage-policies/${policyId}/rules/${ruleId}`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.enforcementMode).toBe('TrackOnly');
    expect(req.request.body.monthlyLimit).toBe(100);
    req.flush({ id: ruleId, featureKey: 'writing.evaluate', ...body });
  });

  it('deleteRule calls DELETE /api/admin/usage-policies/:policyId/rules/:ruleId', () => {
    const policyId = 'policy-1';
    const ruleId = 'rule-1';
    svc.deleteRule(policyId, ruleId).subscribe();
    const req = http.expectOne(`/api/admin/usage-policies/${policyId}/rules/${ruleId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
