import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { IssuesSummary, RepairableItemSummary } from '../models/admin-repair.models';
import {
  AdminTemplateItem, AdminTemplateListQuery,
  AdminCreateTemplateRequest, AdminUpdateTemplateRequest, AdminTemplatePreviewResult,
  StudentListItem, PromptTemplateItem, PromptTemplateDetail,
  AiProviderCatalogItem, AdminStudentLearningMemory, UpdateStudentProfileRequest,
  AiConfigCategoryItem, UpdateAiCategoryRequest, CategoryTestResult,
  ResetStudentRequest, ResetStudentResponse, AdminStats, AdminActivityHistoryItem,
  AdminStudentDetail,
  AdminStudentMastery,
  DataIntegritySweepResult,
  StudentAuditHistoryItem,
  AdminTodayPlanModulePreview, AdminPracticeGymModulePreview,
  AdminDeliveryHealth,
  SkillGraphTaxonomy, SkillGraphNodeListResponse, SkillGraphNodeDetail,
  SkillGraphDraftResponse, SkillGraphBatchActionResponse, SkillGraphCoverageResponse,
  SkillGraphRetagResponse, SkillGraphContentCoverageResponse, SkillGraphResponse,
  CreateSkillGraphNodeRequest, CreateSkillGraphNodeResponse, UpdateSkillGraphNodeRequest, SkillGraphIsolatedNodesResponse,
  StudentListQuery, PagedResponse, AiModelPricingItem,
  AiModelPricingOverrideItem, CreatePricingOverrideRequest, UpdatePricingOverrideRequest,
  AdminNotificationItem, AdminOutboxItem,
  AdminNotificationListQuery, AdminOutboxListQuery,
  AdminSendNotificationRequest, AdminSendNotificationResult,
  AdminNotificationConfigStatus, AdminNotificationConfigStatusV2, AdminTestEmailResult,
  AdminUpdateEmailConfigRequest, AdminUpdateSmsConfigRequest, AdminUpdateInAppConfigRequest,
  AdminUpdateConfigResult,
  AdminSecuritySettings, AdminAuthEventItem, AdminAuthEventListQuery,
  AdminDashboardActivityTrendResponse, AdminDashboardScoreDistributionResponse,
  AdminAiUsageTrendResponse, AdminAiUsageCategoryBreakdownResponse,
  MasteryValidationSummary,
  AdminPlacementLatestResponse,
  AdminPlacementHistoryItem,
  AdminPlacementAssessmentSummary,
  AdminPlacementProgress,
  AdminPlacementSubmitResult,
  AdminStudentPracticeSummary,
  AdminLearningPlanProgress,
  AdminStudentProgressSummary,
  AdminStudentSpeakingAttemptsResult,
  AdminSpeakingEvaluationQualitySummary,
  AdminSpeakingAppliedSignalSummary,
  AdminSignalSafetySummary,
  AdminWritingEvaluationItemDto,
  WritingEvaluationQualitySummaryDto,
  WritingEvaluationWithDryRunDto,
  WritingSignalApplicationSummaryDto,
  WritingSignalSafetySummaryDto,
  AdminAiOperationsSummary,
  FeatureGateGroup,
  UpdateFeatureGateRequest,
  ResetFeatureGateRequest,
  StudentReadinessSummary,
  StudentReadinessRepairRequest,
  StudentReadinessRepairResult,
} from '../models/admin.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  // Students
  listStudents(query: StudentListQuery = {}): Observable<PagedResponse<StudentListItem>> {
    const params = new URLSearchParams();
    if (query.page !== undefined) params.set('page', String(query.page));
    if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
    if (query.search) params.set('search', query.search);
    if (query.includeArchived) params.set('includeArchived', 'true');
    if (query.lifecycleStage) params.set('lifecycleStage', query.lifecycleStage);
    if (query.onboardingStatus) params.set('onboardingStatus', query.onboardingStatus);
    if (query.cefrLevel) params.set('cefrLevel', query.cefrLevel);
    if (query.sortBy) params.set('sortBy', query.sortBy);
    if (query.sortDir) params.set('sortDir', query.sortDir);
    const qs = params.toString();
    return this.http.get<PagedResponse<StudentListItem>>(`${this.api}/students${qs ? '?' + qs : ''}`);
  }
  getStudent(studentProfileId: string): Observable<AdminStudentDetail> {
    return this.http.get<AdminStudentDetail>(`${this.api}/students/${studentProfileId}`);
  }
  updateStudent(studentProfileId: string, data: UpdateStudentProfileRequest): Observable<StudentListItem> {
    return this.http.put<StudentListItem>(`${this.api}/students/${studentProfileId}`, data);
  }
  archiveStudent(studentProfileId: string): Observable<StudentListItem> {
    return this.http.post<StudentListItem>(`${this.api}/students/${studentProfileId}/archive`, null);
  }
  reactivateStudent(studentProfileId: string): Observable<StudentListItem> {
    return this.http.post<StudentListItem>(`${this.api}/students/${studentProfileId}/reactivate`, {});
  }
  pauseStudent(studentProfileId: string): Observable<StudentListItem> {
    return this.http.post<StudentListItem>(`${this.api}/students/${studentProfileId}/pause`, {});
  }
  unpauseStudent(studentProfileId: string): Observable<StudentListItem> {
    return this.http.post<StudentListItem>(`${this.api}/students/${studentProfileId}/unpause`, {});
  }
  getStudentLearningMemory(studentProfileId: string): Observable<AdminStudentLearningMemory> {
    return this.http.get<AdminStudentLearningMemory>(`${this.api}/students/${studentProfileId}/learning-memory`);
  }
  resetStudentPassword(studentProfileId: string, newPassword: string, mustChangePassword = true): Observable<void> {
    return this.http.post<void>(`${this.api}/students/${studentProfileId}/reset-password`, { newPassword, mustChangePassword });
  }
  sendStudentResetLink(studentProfileId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/students/${studentProfileId}/send-reset-link`, null);
  }
  resetStudent(studentProfileId: string, request: ResetStudentRequest): Observable<ResetStudentResponse> {
    return this.http.post<ResetStudentResponse>(`${this.api}/students/${studentProfileId}/reset`, request);
  }
  updateStudentCefr(studentProfileId: string, cefrLevel: string | null, reason?: string): Observable<void> {
    return this.http.put<void>(`${this.api}/students/${studentProfileId}/cefr`, { cefrLevel, reason: reason ?? null });
  }
  getStats(): Observable<AdminStats> {
    return this.http.get<AdminStats>(`${this.api}/stats`);
  }
  getActivityHistory(studentProfileId: string): Observable<AdminActivityHistoryItem[]> {
    return this.http.get<AdminActivityHistoryItem[]>(`${this.api}/students/${studentProfileId}/activity-history`);
  }
  getStudentAuditHistory(studentProfileId: string): Observable<StudentAuditHistoryItem[]> {
    return this.http.get<StudentAuditHistoryItem[]>(`${this.api}/students/${studentProfileId}/audit-history`);
  }
  getStudentMastery(studentProfileId: string): Observable<AdminStudentMastery> {
    return this.http.get<AdminStudentMastery>(`${this.api}/students/${studentProfileId}/mastery`);
  }
  getDataIntegritySweep(): Observable<DataIntegritySweepResult> {
    return this.http.get<DataIntegritySweepResult>(`${this.api}/data-integrity`);
  }
  getStudentPracticeSummary(studentId: string): Observable<AdminStudentPracticeSummary> {
    return this.http.get<AdminStudentPracticeSummary>(`${this.api}/students/${studentId}/practice-summary`);
  }
  getStudentSpeakingAttempts(studentId: string): Observable<AdminStudentSpeakingAttemptsResult> {
    return this.http.get<AdminStudentSpeakingAttemptsResult>(`${this.api}/students/${studentId}/speaking-attempts`);
  }
  getSpeakingEvaluationQualitySummary(): Observable<AdminSpeakingEvaluationQualitySummary> {
    return this.http.get<AdminSpeakingEvaluationQualitySummary>(`${this.api}/speaking-evaluation/quality-summary`);
  }
  getSpeakingAppliedSignalSummary(): Observable<AdminSpeakingAppliedSignalSummary> {
    return this.http.get<AdminSpeakingAppliedSignalSummary>(`${this.api}/speaking-evaluation/applied-signals`);
  }
  getSignalSafetySummary(): Observable<AdminSignalSafetySummary> {
    return this.http.get<AdminSignalSafetySummary>(`${this.api}/speaking-evaluation/signal-safety-summary`);
  }

  // Phase 17A — Per-student writing evaluations
  getStudentWritingEvaluations(studentId: string): Observable<AdminWritingEvaluationItemDto[]> {
    return this.http.get<AdminWritingEvaluationItemDto[]>(`${this.api}/students/${studentId}/writing-evaluations`);
  }

  // Phase 17B — Writing evaluation quality summary and dry-run signal
  getWritingEvaluationQualitySummary(): Observable<WritingEvaluationQualitySummaryDto> {
    return this.http.get<WritingEvaluationQualitySummaryDto>(`${this.api}/writing-evaluation/quality-summary`);
  }
  getWritingEvaluationWithDryRun(id: string): Observable<WritingEvaluationWithDryRunDto> {
    return this.http.get<WritingEvaluationWithDryRunDto>(`${this.api}/writing-evaluation/${id}/dry-run`);
  }

  // Phase 17C — Writing evaluation signal application summary and safety
  getWritingAppliedSignalsSummary(): Observable<WritingSignalApplicationSummaryDto> {
    return this.http.get<WritingSignalApplicationSummaryDto>(`${this.api}/writing-evaluation/applied-signals-summary`);
  }
  getWritingSignalSafetySummary(): Observable<WritingSignalSafetySummaryDto> {
    return this.http.get<WritingSignalSafetySummaryDto>(`${this.api}/writing-evaluation/signal-safety-summary`);
  }

  getStudentProgressSummary(studentId: string): Observable<AdminStudentProgressSummary> {
    return this.http.get<AdminStudentProgressSummary>(`${this.api}/students/${studentId}/progress-summary`);
  }
  getLearningPlanProgress(studentId: string): Observable<AdminLearningPlanProgress> {
    return this.http.get<AdminLearningPlanProgress>(`${this.api}/students/${studentId}/learning-plan-progress`);
  }

  /** Phase H6 (renamed I4 Pass 3) — preview which approved Modules the Today Plan selector would
   * choose today. Read-only. */
  getTodayPlanModulePreview(studentProfileId: string): Observable<AdminTodayPlanModulePreview> {
    return this.http.get<AdminTodayPlanModulePreview>(`${this.api}/today-plan/modules/preview?studentId=${studentProfileId}`);
  }
  /** Phase H7 — preview which approved Modules the Practice Gym selector would suggest. Read-only. */
  getPracticeGymModulePreview(studentProfileId: string): Observable<AdminPracticeGymModulePreview> {
    return this.http.get<AdminPracticeGymModulePreview>(`${this.api}/practice-gym/modules/preview?studentId=${studentProfileId}&maxSuggestions=100`);
  }
  getAiOperationsSummary(): Observable<AdminAiOperationsSummary> {
    return this.http.get<AdminAiOperationsSummary>(`${this.api}/ai-operations/summary`);
  }
  getMasteryValidationSummary(): Observable<MasteryValidationSummary> {
    return this.http.get<MasteryValidationSummary>(`${this.api}/mastery/validation-summary`);
  }

  /** Rehaul (2026-07-17) — fleet-wide Today Plan delivery health (selected-vs-fallback, by CEFR
   * level, trend, top fallback reasons, bank coverage). Read-only. */
  getTodayPlanDeliveryHealth(days?: number): Observable<AdminDeliveryHealth> {
    const qs = days !== undefined ? `?days=${days}` : '';
    return this.http.get<AdminDeliveryHealth>(`${this.api}/today-plan/delivery-health${qs}`);
  }
  /** Rehaul (2026-07-17) — fleet-wide Practice Gym delivery health. Read-only. */
  getPracticeGymDeliveryHealth(days?: number): Observable<AdminDeliveryHealth> {
    const qs = days !== undefined ? `?days=${days}` : '';
    return this.http.get<AdminDeliveryHealth>(`${this.api}/practice-gym/delivery-health${qs}`);
  }

  // Adaptive Curriculum Sprint 1 — skill graph (AI-drafted, admin-batch-approved).
  getSkillGraphTaxonomy(): Observable<SkillGraphTaxonomy> {
    return this.http.get<SkillGraphTaxonomy>(`${this.api}/skill-graph/taxonomy`);
  }
  getSkillGraphNodes(params: { cefrLevel?: string; skill?: string; reviewStatus?: string; search?: string; contextTag?: string; focusTag?: string; page?: number; pageSize?: number }): Observable<SkillGraphNodeListResponse> {
    const qs = Object.entries(params)
      .filter(([, v]) => v !== undefined && v !== '')
      .map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`)
      .join('&');
    return this.http.get<SkillGraphNodeListResponse>(`${this.api}/skill-graph/nodes${qs ? '?' + qs : ''}`);
  }
  getSkillGraphNode(id: string): Observable<SkillGraphNodeDetail> {
    return this.http.get<SkillGraphNodeDetail>(`${this.api}/skill-graph/nodes/${id}`);
  }
  draftSkillGraph(cefrLevel: string, skill: string): Observable<SkillGraphDraftResponse> {
    return this.http.post<SkillGraphDraftResponse>(`${this.api}/skill-graph/draft`, { cefrLevel, skill });
  }
  batchApproveSkillGraphNodes(ids: string[]): Observable<SkillGraphBatchActionResponse> {
    return this.http.post<SkillGraphBatchActionResponse>(`${this.api}/skill-graph/nodes/batch/approve`, { ids });
  }
  batchRejectSkillGraphNodes(ids: string[], reason: string): Observable<SkillGraphBatchActionResponse> {
    return this.http.post<SkillGraphBatchActionResponse>(`${this.api}/skill-graph/nodes/batch/reject`, { ids, reason });
  }
  getSkillGraphCoverage(): Observable<SkillGraphCoverageResponse> {
    return this.http.get<SkillGraphCoverageResponse>(`${this.api}/skill-graph/coverage`);
  }
  retagSkillGraphModules(): Observable<SkillGraphRetagResponse> {
    return this.http.post<SkillGraphRetagResponse>(`${this.api}/skill-graph/retag-modules`, {});
  }
  getSkillGraphContentCoverage(): Observable<SkillGraphContentCoverageResponse> {
    return this.http.get<SkillGraphContentCoverageResponse>(`${this.api}/skill-graph/content-coverage`);
  }
  // Sprint 13 — bulk nodes+edges payload backing the Cytoscape/Dagre graph view.
  getSkillGraph(): Observable<SkillGraphResponse> {
    return this.http.get<SkillGraphResponse>(`${this.api}/skill-graph/graph`);
  }
  // Sprint 14.1 — node context/focus tag diagnose+AI-repair (same shape as Resource Bank's, reused
  // by AdminBulkRepairService for the "Fix All with AI" toast-progress flow).
  getSkillGraphNodeIssuesSummary(): Observable<IssuesSummary> {
    return this.http.get<IssuesSummary>(`${this.api}/skill-graph/nodes/issues-summary`);
  }
  listSkillGraphNodesWithIssues(): Observable<RepairableItemSummary[]> {
    return this.http.get<RepairableItemSummary[]>(`${this.api}/skill-graph/nodes/with-issues`);
  }
  repairSkillGraphNode(id: string): Observable<unknown> {
    return this.http.post(`${this.api}/skill-graph/nodes/${id}/repair`, {});
  }

  // Editability audit (2026-07-23) — manual node create/edit + manual prerequisite-edge management,
  // replacing the AI-drafting-only path that could never create a cross-Skill/cross-CEFR edge.
  createSkillGraphNode(body: CreateSkillGraphNodeRequest): Observable<CreateSkillGraphNodeResponse> {
    return this.http.post<CreateSkillGraphNodeResponse>(`${this.api}/skill-graph/nodes`, body);
  }
  updateSkillGraphNode(id: string, body: UpdateSkillGraphNodeRequest): Observable<{ id: string; key: string }> {
    return this.http.put<{ id: string; key: string }>(`${this.api}/skill-graph/nodes/${id}`, body);
  }
  addSkillGraphPrerequisite(nodeId: string, prerequisiteNodeId: string): Observable<{ added: boolean }> {
    return this.http.post<{ added: boolean }>(`${this.api}/skill-graph/nodes/${nodeId}/prerequisites`, { prerequisiteNodeId });
  }
  removeSkillGraphPrerequisite(nodeId: string, prerequisiteNodeId: string): Observable<{ removed: boolean }> {
    return this.http.delete<{ removed: boolean }>(`${this.api}/skill-graph/nodes/${nodeId}/prerequisites/${prerequisiteNodeId}`);
  }
  getIsolatedSkillGraphNodes(): Observable<SkillGraphIsolatedNodesResponse> {
    return this.http.get<SkillGraphIsolatedNodesResponse>(`${this.api}/skill-graph/nodes/isolated`);
  }

  // Prompts
  listPrompts(): Observable<PromptTemplateItem[]> {
    return this.http.get<PromptTemplateItem[]>(`${this.api}/prompts`);
  }
  getPrompt(id: string): Observable<PromptTemplateDetail> {
    return this.http.get<PromptTemplateDetail>(`${this.api}/prompts/${id}`);
  }
  createPromptVersion(data: { key: string; content: string; maxInputTokens: number; maxOutputTokens: number }): Observable<PromptTemplateDetail> {
    return this.http.post<PromptTemplateDetail>(`${this.api}/prompts`, data);
  }
  activatePrompt(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/prompts/${id}/activate`, null);
  }
  deactivatePrompt(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/prompts/${id}/deactivate`, null);
  }

  // AI provider credentials
  listAiProviders(): Observable<AiProviderCatalogItem[]> {
    return this.http.get<AiProviderCatalogItem[]>(`${this.api}/ai-providers`);
  }
  setProviderApiKey(provider: string, apiKey: string | null): Observable<AiProviderCatalogItem> {
    return this.http.put<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/api-key`, { apiKey });
  }
  setProviderEndpoint(provider: string, apiEndpoint: string | null): Observable<AiProviderCatalogItem> {
    return this.http.put<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/endpoint`, { apiEndpoint });
  }
  testProvider(provider: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/test`, null);
  }
  addProviderModel(provider: string, modelName: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/models`, { modelName });
  }
  testProviderModel(provider: string, modelName: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/models/test`, { modelName });
  }

  // AI config categories
  listAiCategories(): Observable<AiConfigCategoryItem[]> {
    return this.http.get<AiConfigCategoryItem[]>(`${this.api}/ai/categories`);
  }
  updateAiCategory(categoryKey: string, data: UpdateAiCategoryRequest): Observable<AiConfigCategoryItem> {
    return this.http.patch<AiConfigCategoryItem>(`${this.api}/ai/categories/${categoryKey}`, data);
  }
  testAiCategory(categoryKey: string): Observable<CategoryTestResult> {
    return this.http.post<CategoryTestResult>(`${this.api}/ai/categories/${categoryKey}/test`, null);
  }

  // AI model pricing
  listAiPricing(): Observable<AiModelPricingItem[]> {
    return this.http.get<AiModelPricingItem[]>(`${this.api}/ai/pricing`);
  }

  // AI pricing overrides
  listAiPricingOverrides(): Observable<AiModelPricingOverrideItem[]> {
    return this.http.get<AiModelPricingOverrideItem[]>(`${this.api}/ai/pricing/overrides`);
  }

  createAiPricingOverride(cmd: CreatePricingOverrideRequest): Observable<AiModelPricingOverrideItem> {
    return this.http.post<AiModelPricingOverrideItem>(`${this.api}/ai/pricing/overrides`, cmd);
  }

  updateAiPricingOverride(id: string, cmd: UpdatePricingOverrideRequest): Observable<AiModelPricingOverrideItem> {
    return this.http.put<AiModelPricingOverrideItem>(`${this.api}/ai/pricing/overrides/${id}`, cmd);
  }

  deactivateAiPricingOverride(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/ai/pricing/overrides/${id}`);
  }

  // Notification center
  listAdminNotifications(query: AdminNotificationListQuery = {}): Observable<PagedResponse<AdminNotificationItem>> {
    const params = new URLSearchParams();
    if (query.page !== undefined) params.set('page', String(query.page));
    if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
    if (query.recipientUserId) params.set('recipientUserId', query.recipientUserId);
    if (query.channel) params.set('channel', query.channel);
    if (query.status) params.set('status', query.status);
    if (query.category) params.set('category', query.category);
    if (query.severity) params.set('severity', query.severity);
    if (query.from) params.set('from', query.from);
    if (query.to) params.set('to', query.to);
    if (query.search) params.set('search', query.search);
    const qs = params.toString();
    return this.http.get<PagedResponse<AdminNotificationItem>>(`${this.api}/notifications${qs ? '?' + qs : ''}`);
  }

  listAdminOutbox(query: AdminOutboxListQuery = {}): Observable<PagedResponse<AdminOutboxItem>> {
    const params = new URLSearchParams();
    if (query.page !== undefined) params.set('page', String(query.page));
    if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
    if (query.recipientUserId) params.set('recipientUserId', query.recipientUserId);
    if (query.channel) params.set('channel', query.channel);
    if (query.status) params.set('status', query.status);
    if (query.from) params.set('from', query.from);
    if (query.to) params.set('to', query.to);
    if (query.dueOnly) params.set('dueOnly', 'true');
    if (query.failedOnly) params.set('failedOnly', 'true');
    const qs = params.toString();
    return this.http.get<PagedResponse<AdminOutboxItem>>(`${this.api}/notifications/outbox${qs ? '?' + qs : ''}`);
  }

  retryOutboxItem(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notifications/outbox/${id}/retry`, null);
  }

  cancelOutboxItem(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notifications/outbox/${id}/cancel`, null);
  }

  sendAdminNotification(request: AdminSendNotificationRequest): Observable<AdminSendNotificationResult> {
    return this.http.post<AdminSendNotificationResult>(`${this.api}/notifications/send`, request);
  }

  // Notification templates
  listNotificationTemplates(query: AdminTemplateListQuery = {}): Observable<PagedResponse<AdminTemplateItem>> {
    const params = new URLSearchParams();
    if (query.page !== undefined) params.set('page', String(query.page));
    if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
    if (query.channel) params.set('channel', query.channel);
    if (query.category) params.set('category', query.category);
    if (query.isActive !== undefined) params.set('isActive', String(query.isActive));
    if (query.search) params.set('search', query.search);
    const qs = params.toString();
    return this.http.get<PagedResponse<AdminTemplateItem>>(`${this.api}/notifications/templates${qs ? '?' + qs : ''}`);
  }

  getNotificationTemplate(id: string): Observable<AdminTemplateItem> {
    return this.http.get<AdminTemplateItem>(`${this.api}/notifications/templates/${id}`);
  }

  createNotificationTemplate(request: AdminCreateTemplateRequest): Observable<AdminTemplateItem> {
    return this.http.post<AdminTemplateItem>(`${this.api}/notifications/templates`, request);
  }

  updateNotificationTemplate(id: string, request: AdminUpdateTemplateRequest): Observable<AdminTemplateItem> {
    return this.http.put<AdminTemplateItem>(`${this.api}/notifications/templates/${id}`, request);
  }

  deactivateNotificationTemplate(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notifications/templates/${id}/deactivate`, null);
  }

  previewNotificationTemplate(id: string, variables: Record<string, string>): Observable<AdminTemplatePreviewResult> {
    return this.http.post<AdminTemplatePreviewResult>(
      `${this.api}/notifications/templates/${id}/preview`, { variables });
  }

  getNotificationConfig(): Observable<AdminNotificationConfigStatusV2> {
    return this.http.get<AdminNotificationConfigStatusV2>(`${this.api}/notifications/config`);
  }

  updateEmailConfig(req: AdminUpdateEmailConfigRequest): Observable<AdminUpdateConfigResult> {
    return this.http.put<AdminUpdateConfigResult>(`${this.api}/notifications/config/email`, req);
  }

  updateSmsConfig(req: AdminUpdateSmsConfigRequest): Observable<AdminUpdateConfigResult> {
    return this.http.put<AdminUpdateConfigResult>(`${this.api}/notifications/config/sms`, req);
  }

  updateInAppConfig(req: AdminUpdateInAppConfigRequest): Observable<AdminUpdateConfigResult> {
    return this.http.put<AdminUpdateConfigResult>(`${this.api}/notifications/config/in-app`, req);
  }

  testEmail(toAddress: string): Observable<AdminTestEmailResult> {
    return this.http.post<AdminTestEmailResult>(
      `${this.api}/notifications/config/email/test`, { toAddress });
  }

  // ── Security settings ────────────────────────────────────────────────────

  getSecuritySettings(): Observable<AdminSecuritySettings> {
    return this.http.get<AdminSecuritySettings>(`${this.api}/security/settings`);
  }

  listSecurityAuthEvents(query: AdminAuthEventListQuery): Observable<PagedResponse<AdminAuthEventItem>> {
    const params: Record<string, string> = {};
    if (query.page) params['page'] = String(query.page);
    if (query.pageSize) params['pageSize'] = String(query.pageSize);
    if (query.userId) params['userId'] = query.userId;
    if (query.email) params['email'] = query.email;
    if (query.eventType) params['eventType'] = query.eventType;
    if (query.outcome) params['outcome'] = query.outcome;
    if (query.from) params['from'] = query.from;
    if (query.to) params['to'] = query.to;
    return this.http.get<PagedResponse<AdminAuthEventItem>>(
      `${this.api}/security/auth-events`, { params });
  }

  // ── Dashboard aggregate ──────────────────────────────────────────────────────

  getDashboardActivityTrends(period = '30d'): Observable<AdminDashboardActivityTrendResponse> {
    return this.http.get<AdminDashboardActivityTrendResponse>(`${this.api}/dashboard/activity-trends?period=${period}`);
  }

  getDashboardScoreDistribution(period = '30d'): Observable<AdminDashboardScoreDistributionResponse> {
    return this.http.get<AdminDashboardScoreDistributionResponse>(`${this.api}/dashboard/score-distribution?period=${period}`);
  }

  getAiUsageTrends(period = '30d'): Observable<AdminAiUsageTrendResponse> {
    return this.http.get<AdminAiUsageTrendResponse>(`${this.api}/ai-usage/aggregate-trends?period=${period}`);
  }

  getAiUsageCategoryBreakdown(period = '30d'): Observable<AdminAiUsageCategoryBreakdownResponse> {
    return this.http.get<AdminAiUsageCategoryBreakdownResponse>(`${this.api}/ai-usage/by-category?period=${period}`);
  }

  // ── Phase 13A — Adaptive Placement Engine ───────────────────────────────────

  getLatestPlacement(studentProfileId: string): Observable<AdminPlacementLatestResponse> {
    return this.http.get<AdminPlacementLatestResponse>(
      `${this.api}/students/${studentProfileId}/placement/latest`);
  }

  getPlacementHistory(studentProfileId: string): Observable<AdminPlacementHistoryItem[]> {
    return this.http.get<AdminPlacementHistoryItem[]>(
      `${this.api}/students/${studentProfileId}/placement/history`);
  }

  startPlacement(studentProfileId: string): Observable<AdminPlacementAssessmentSummary> {
    return this.http.post<AdminPlacementAssessmentSummary>(
      `${this.api}/students/${studentProfileId}/placement/start`, {});
  }

  completePlacement(studentProfileId: string, assessmentId: string): Observable<AdminPlacementAssessmentSummary> {
    return this.http.post<AdminPlacementAssessmentSummary>(
      `${this.api}/students/${studentProfileId}/placement/${assessmentId}/complete`, {});
  }

  // Phase 13B — Real scoring and adaptive progression

  getPlacementProgress(studentProfileId: string, assessmentId: string): Observable<AdminPlacementProgress> {
    return this.http.get<AdminPlacementProgress>(
      `${this.api}/students/${studentProfileId}/placement/${assessmentId}/progress`);
  }

  submitPlacementResponse(
    studentProfileId: string,
    assessmentId: string,
    itemId: string,
    response: string,
    durationSeconds?: number
  ): Observable<AdminPlacementSubmitResult> {
    return this.http.post<AdminPlacementSubmitResult>(
      `${this.api}/students/${studentProfileId}/placement/${assessmentId}/items/${itemId}/submit`,
      { response, durationSeconds: durationSeconds ?? null });
  }

  // Phase 14A — Admin placement lifecycle actions

  abandonPlacement(studentProfileId: string, assessmentId: string): Observable<{ abandoned: boolean }> {
    return this.http.post<{ abandoned: boolean }>(
      `${this.api}/students/${studentProfileId}/placement/${assessmentId}/abandon`, {});
  }

  expirePlacement(studentProfileId: string, assessmentId: string): Observable<{ expired: boolean }> {
    return this.http.post<{ expired: boolean }>(
      `${this.api}/students/${studentProfileId}/placement/${assessmentId}/expire`, {});
  }

  // Phase 20B — Runtime settings / feature gates

  getFeatureGates(): Observable<FeatureGateGroup[]> {
    return this.http.get<FeatureGateGroup[]>(`${this.api}/runtime-settings/feature-gates`);
  }

  getFeatureGate(groupKey: string): Observable<FeatureGateGroup> {
    return this.http.get<FeatureGateGroup>(`${this.api}/runtime-settings/feature-gates/${groupKey}`);
  }

  updateFeatureGate(groupKey: string, request: UpdateFeatureGateRequest): Observable<FeatureGateGroup> {
    return this.http.put<FeatureGateGroup>(`${this.api}/runtime-settings/feature-gates/${groupKey}/settings`, request);
  }

  resetFeatureGateOverride(groupKey: string, request: ResetFeatureGateRequest): Observable<FeatureGateGroup> {
    return this.http.request<FeatureGateGroup>(
      'DELETE', `${this.api}/runtime-settings/feature-gates/${groupKey}/override`, { body: request });
  }

  // Phase 20D — Student pilot readiness

  getStudentReadiness(studentProfileId: string): Observable<StudentReadinessSummary> {
    return this.http.get<StudentReadinessSummary>(`${this.api}/students/${studentProfileId}/readiness`);
  }

  repairStudentReadiness(studentProfileId: string, request: StudentReadinessRepairRequest): Observable<StudentReadinessRepairResult> {
    return this.http.post<StudentReadinessRepairResult>(
      `${this.api}/students/${studentProfileId}/readiness/repair`, request);
  }

  repairAllSafeStudentReadiness(studentProfileId: string, request: StudentReadinessRepairRequest): Observable<StudentReadinessRepairResult[]> {
    return this.http.post<StudentReadinessRepairResult[]>(
      `${this.api}/students/${studentProfileId}/readiness/repair-safe-all`, request);
  }
}
