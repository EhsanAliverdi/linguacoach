import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FeatureDefinition {
  id: string;
  key: string;
  name: string;
  description: string | null;
  category: string;
  defaultEnforcementMode: string;
  unitType: string;
  isExpensive: boolean;
  isStudentVisible: boolean;
  isEnabledByDefault: boolean;
}

export interface UsagePolicyRule {
  id: string;
  featureKey: string;
  trackingEnabled: boolean;
  enforcementMode: string;
  unitType: string;
  dailyLimit: number | null;
  weeklyLimit: number | null;
  monthlyLimit: number | null;
  dailyCostLimit: number | null;
  monthlyCostLimit: number | null;
  warningThresholdPercent: number;
  isActive: boolean;
}

export interface UsagePolicy {
  id: string;
  name: string;
  description: string | null;
  scopeType: string;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  rules: UsagePolicyRule[];
}

export interface CreateUsagePolicyRequest {
  name: string;
  description: string | null;
  scopeType: string;
  isDefault: boolean;
  isActive: boolean;
  rules: Partial<UsagePolicyRule>[];
}

export interface UpdateUsagePolicyRequest {
  name: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface UsageSummary {
  studentProfileId: string;
  period: string;
  from: string;
  to: string;
  totalTokens: number;
  inputTokens: number;
  outputTokens: number;
  totalCost: number;
  aiCallCount: number;
  lessonGenerations: number;
  practiceGenerations: number;
  writingEvaluations: number;
  speakingEvaluations: number;
  preparedActivitiesCompleted: number;
}

@Injectable({ providedIn: 'root' })
export class UsageGovernanceService {
  constructor(private http: HttpClient) {}

  listFeatureDefinitions(): Observable<FeatureDefinition[]> {
    return this.http.get<FeatureDefinition[]>('/api/admin/feature-definitions');
  }

  listUsagePolicies(): Observable<UsagePolicy[]> {
    return this.http.get<UsagePolicy[]>('/api/admin/usage-policies');
  }

  getUsagePolicy(id: string): Observable<UsagePolicy> {
    return this.http.get<UsagePolicy>(`/api/admin/usage-policies/${id}`);
  }

  createUsagePolicy(req: CreateUsagePolicyRequest): Observable<UsagePolicy> {
    return this.http.post<UsagePolicy>('/api/admin/usage-policies', req);
  }

  updateUsagePolicy(id: string, req: UpdateUsagePolicyRequest): Observable<UsagePolicy> {
    return this.http.put<UsagePolicy>(`/api/admin/usage-policies/${id}`, req);
  }

  assignStudentPolicy(studentId: string, policyId: string, reason: string | null): Observable<void> {
    return this.http.put<void>(`/api/admin/students/${studentId}/usage-policy`, { policyId, reason });
  }

  getStudentUsage(studentId: string, period: string = 'today'): Observable<UsageSummary> {
    return this.http.get<UsageSummary>(`/api/admin/students/${studentId}/usage`, { params: { period } });
  }
}
