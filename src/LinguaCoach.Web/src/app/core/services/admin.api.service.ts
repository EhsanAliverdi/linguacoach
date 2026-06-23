import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AdminTemplateItem, AdminTemplateListQuery,
  AdminCreateTemplateRequest, AdminUpdateTemplateRequest, AdminTemplatePreviewResult,
  StudentListItem, PromptTemplateItem, PromptTemplateDetail,
  CareerProfileItem, CurriculumWordItem,
  AiProviderCatalogItem, AdminStudentLearningMemory, UpdateStudentProfileRequest,
  AiConfigCategoryItem, UpdateAiCategoryRequest, CategoryTestResult,
  ResetStudentRequest, ResetStudentResponse, AdminStats, AdminActivityHistoryItem,
  AdminStudentDetail,
  StudentAuditHistoryItem, StudentReadinessPoolHealth,
  StudentListQuery, PagedResponse, AiModelPricingItem,
  AiModelPricingOverrideItem, CreatePricingOverrideRequest, UpdatePricingOverrideRequest,
  AdminNotificationItem, AdminOutboxItem,
  AdminNotificationListQuery, AdminOutboxListQuery,
  AdminSendNotificationRequest, AdminSendNotificationResult,
  AdminNotificationConfigStatus, AdminNotificationConfigStatusV2, AdminTestEmailResult,
  AdminUpdateEmailConfigRequest, AdminUpdateSmsConfigRequest, AdminUpdateInAppConfigRequest,
  AdminUpdateConfigResult,
  AdminSecuritySettings, AdminAuthEventItem, AdminAuthEventListQuery,
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
  getStudentReadinessPoolHealth(studentProfileId: string): Observable<StudentReadinessPoolHealth> {
    return this.http.get<StudentReadinessPoolHealth>(`${this.api}/students/${studentProfileId}/readiness-pool/health`);
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

  // Careers + words
  listCareers(): Observable<CareerProfileItem[]> {
    return this.http.get<CareerProfileItem[]>(`${this.api}/careers`);
  }
  listWords(careerId: string, languagePairId: string): Observable<CurriculumWordItem[]> {
    return this.http.get<CurriculumWordItem[]>(`${this.api}/careers/${careerId}/words?languagePairId=${languagePairId}`);
  }
  addWord(careerId: string, data: object): Observable<CurriculumWordItem> {
    return this.http.post<CurriculumWordItem>(`${this.api}/careers/${careerId}/words`, data);
  }
  updateWord(wordId: string, data: object): Observable<CurriculumWordItem> {
    return this.http.put<CurriculumWordItem>(`${this.api}/careers/words/${wordId}`, data);
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
}
