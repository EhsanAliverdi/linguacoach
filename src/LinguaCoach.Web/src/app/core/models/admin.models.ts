export interface StudentListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  includeArchived?: boolean;
  lifecycleStage?: string;
  onboardingStatus?: string;
  cefrLevel?: string;
  sortBy?: string;
  sortDir?: string;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AdminActivityHistoryItem {
  attemptId: string;
  activityId: string;
  activityTitle: string;
  activityType: string;
  score: number | null;
  percentage: number | null;
  passed: boolean | null;
  completed: boolean | null;
  createdAt: string;
}

export interface StudentAuditHistoryItem {
  id: string;
  source: 'AdminAuditLog' | 'StudentResetLog';
  action: string;
  actorId?: string;
  actorEmail?: string;
  timestamp: string;
  summary?: string;
  reason?: string;
  oldValue?: string;
  newValue?: string;
  correlationId?: string;
  details?: string;
}

export interface AdminStats {
  totalStudents: number;
  onboardedStudents: number;
  totalActivityAttempts: number;
}

export interface StudentListItem {
  studentProfileId: string;
  userId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  onboardingStatus: string;
  lifecycleStage: string;
  cefrLevel: string | null;
  careerContext: string | null;
  learningGoal: string | null;
  learningGoalDescription: string | null;
  difficultSituationsText: string | null;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
  createdAt: string;
  // Student-authored learning preferences (read-only for admin)
  preferredName: string | null;
  supportLanguageCode: string | null;
  supportLanguageName: string | null;
  difficultyPreference: string | null;
  translationHelpPreference: string | null;
  focusAreas: string[];
  customFocusArea: string | null;
  learningGoals: string[];
  customLearningGoal: string | null;
  learningPreferencesUpdatedAt: string | null;
}

export interface UpdateStudentProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  displayName?: string | null;
  careerContext?: string | null;
  learningGoal?: string | null;
  learningGoalDescription?: string | null;
  difficultSituationsText?: string | null;
  preferredSessionDurationMinutes?: number | null;
  professionalExperienceLevel?: number | null;
  roleFamiliarity?: number | null;
}

export type StudentLifecycleStageName =
  | 'Created'
  | 'PasswordChangeRequired'
  | 'OnboardingRequired'
  | 'OnboardingInProgress'
  | 'PlacementRequired'
  | 'PlacementInProgress'
  | 'PlacementCompleted'
  | 'CourseReady'
  | 'InLesson'
  | 'ActiveLearning'
  | 'Paused'
  | 'Archived';

export interface ResetStudentRequest {
  targetStage: StudentLifecycleStageName;
  clearOnboardingAnswers: boolean;
  clearPlacementResults: boolean;
  clearCoursesAndSessions: boolean;
  clearActivityAttempts: boolean;
  clearVocabulary: boolean;
  clearLearningMemory: boolean;
  clearAudioFiles: boolean;
  clearProgressData: boolean;
  reason: string;
}

export interface ClearedItemsResult {
  onboardingAnswers: boolean;
  placementResults: boolean;
  coursesAndSessions: boolean;
  activityAttempts: boolean;
  vocabulary: boolean;
  learningMemory: boolean;
  audioFilesDeleted: number;
  progressData: boolean;
}

export interface ResetStudentResponse {
  studentId: string;
  previousStage: StudentLifecycleStageName;
  newStage: StudentLifecycleStageName;
  clearedItems: ClearedItemsResult;
  resetLogId: string;
  performedByAdminId: string;
  performedAtUtc: string;
  correlationId: string;
}

export interface PromptTemplateItem {
  id: string;
  key: string;
  version: number;
  isActive: boolean;
  maxInputTokens: number | null;
  maxOutputTokens: number | null;
}

export interface PromptTemplateDetail extends PromptTemplateItem {
  content: string;
}

export interface ModelTestStatus {
  modelName: string;
  ok: boolean;
  latencyMs: number;
  error: string | null;
  testedAt: string; // ISO date or default(DateTime) = "0001-01-01..."
}

export interface AiProviderCatalogItem {
  providerName: string;
  models: string[];
  hasApiKey: boolean;
  modelTests: ModelTestStatus[];
  apiEndpoint: string | null;
}

export interface AiConfigCategoryItem {
  id: string;
  categoryKey: string;
  displayName: string;
  providerName: string | null;
  modelName: string | null;
  voiceName: string | null;
}

export interface AiModelPricingItem {
  providerName: string;
  modelName: string;
  inputPer1KTokens: number;
  outputPer1KTokens: number;
  currency: string;
  source: string;
  isConfigured: boolean;
  inputPer1KCharacters?: number | null;
}

export interface AiModelPricingOverrideItem {
  id: string;
  providerName: string;
  modelName: string;
  inputPricePer1KTokens: number;
  outputPricePer1KTokens: number;
  currency: string;
  isActive: boolean;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  createdByAdminUserId: string | null;
  updatedByAdminUserId: string | null;
  inputPricePer1KCharacters?: number | null;
}

export interface CreatePricingOverrideRequest {
  providerName: string;
  modelName: string;
  inputPricePer1KTokens: number;
  outputPricePer1KTokens: number;
  currency: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
  notes?: string | null;
  inputPricePer1KCharacters?: number | null;
}

export interface UpdatePricingOverrideRequest {
  inputPricePer1KTokens: number;
  outputPricePer1KTokens: number;
  currency: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
  notes?: string | null;
  inputPricePer1KCharacters?: number | null;
}

export interface UpdateAiCategoryRequest {
  providerName?: string | null;
  modelName?: string | null;
  voiceName?: string | null;
}

export interface CategoryTestResult {
  categoryKey: string;
  providerName: string;
  modelName: string | null;
  voiceName: string | null;
  ok: boolean;
  latencyMs: number;
  error: string | null;
}

export interface AdminStudentLearningMemory {
  journeySummary: string | null;
  strongSkills: string[];
  weakSkills: string[];
  recurringMistakes: string[];
  nextRecommendedFocus: string[];
  coveredScenarioCount: number;
  skillProfile: { skillKey: string; skillLabel: string; isWeak: boolean }[];
}

export interface ExerciseTypeDefinition {
  key: string;
  displayName: string;
  description: string;
  primarySkill: string;
  secondarySkills: string[];
  category: string;
  isEnabled: boolean;
  implementationStatus: string;
  isAvailableForGeneration: boolean;
  rendererKey: string;
  evaluatorKey: string;
  generationPromptKey: string;
  legacyActivityType: string | null;
  exercisePatternKey: string | null;
  estimatedDurationMinutes: number;
  requiresAudio: boolean;
  requiresImage: boolean;
  minItemsPerPractice: number;
  defaultItemsPerPractice: number;
  maxItemsPerPractice: number;
  minOptionsPerItem: number;
  defaultOptionsPerItem: number;
  maxOptionsPerItem: number;
}

export interface StudentOnboardingProgressInfo {
  currentStepKey: string | null;
  completedStepKeys: string[];
  percentageComplete: number;
  startedAt: string;
  completedAt: string | null;
  isComplete: boolean;
  preliminaryCefrLevel: string | null;
}

export interface AdminStudentDetail {
  studentProfileId: string;
  userId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  preferredName: string | null;
  lifecycleStage: string;
  onboardingStatus: string;
  lastCompletedStep: string | null;
  cefrLevel: string | null;
  careerContext: string | null;
  learningGoal: string | null;
  learningGoalDescription: string | null;
  difficultSituationsText: string | null;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
  createdAt: string;
  archivedAt: string | null;
  supportLanguageCode: string | null;
  supportLanguageName: string | null;
  difficultyPreference: string | null;
  translationHelpPreference: string | null;
  focusAreas: string[];
  customFocusArea: string | null;
  learningGoals: string[];
  customLearningGoal: string | null;
  learningPreferencesUpdatedAt: string | null;
  onboardingProgress: StudentOnboardingProgressInfo | null;
  // Phase 14B — learning readiness
  isLearningReady: boolean;
  lastPlacementCompletedAt: string | null;
  learningPlanExists: boolean;
  // Sprint 11 — the real per-student weighted goal vector driving Today/Practice Gym's
  // "goal match" selection signal, previously invisible anywhere in admin.
  goalWeights: AdminStudentGoalWeight[];
}

export interface AdminStudentGoalWeight {
  goalTag: string;
  weight: number;
  source: string;
  updatedAtUtc: string;
}

// Sprint 11 — restores per-student mastery visibility (deleted in Phase I2C along with the
// readiness pool it served). Backed by the same deterministic mastery evaluator Today/Practice
// Gym's weakness-match selection already uses.
export interface AdminMasterySkillGraphNode {
  key: string;
  title: string | null;
  skill: string | null;
  cefrLevel: string | null;
}

export interface AdminStudentMastery {
  evaluatedAtUtc: string;
  mastered: AdminMasterySkillGraphNode[];
  completed: AdminMasterySkillGraphNode[];
  weak: AdminMasterySkillGraphNode[];
  atRisk: AdminMasterySkillGraphNode[];
}

// Sprint 11 — unified data-integrity sweep.
export interface DataIntegrityCategoryResult {
  category: string;
  description: string;
  totalChecked: number;
  issuesFound: number;
  healthy: boolean;
}

export interface DataIntegritySweepResult {
  ranAtUtc: string;
  categories: DataIntegrityCategoryResult[];
  allHealthy: boolean;
}

// ── Admin notification center ──────────────────────────────────────────────

export interface AdminNotificationItem {
  id: string;
  recipientUserId: string;
  recipientEmail: string;
  title: string;
  body: string;
  channel: string;
  category: string;
  severity: string;
  status: string;
  deepLinkUrl: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
  expiresAtUtc: string | null;
}

export interface AdminOutboxItem {
  id: string;
  notificationId: string | null;
  recipientUserId: string;
  recipientEmail: string;
  channel: string;
  status: string;
  attemptCount: number;
  createdAtUtc: string;
  nextAttemptAtUtc: string | null;
  lastAttemptAtUtc: string | null;
  processedAtUtc: string | null;
  lastError: string | null;
}

export interface AdminNotificationListQuery {
  page?: number;
  pageSize?: number;
  recipientUserId?: string;
  channel?: string;
  status?: string;
  category?: string;
  severity?: string;
  from?: string;
  to?: string;
  search?: string;
}

export interface AdminOutboxListQuery {
  page?: number;
  pageSize?: number;
  recipientUserId?: string;
  channel?: string;
  status?: string;
  from?: string;
  to?: string;
  dueOnly?: boolean;
  failedOnly?: boolean;
}

// ── Notification configuration ────────────────────────────────────────────────

export interface AdminChannelStatus {
  channel: string;
  enabled: boolean;
  statusLabel: string;
}

export interface AdminEmailConfigStatus {
  enabled: boolean;
  configured: boolean;
  statusLabel: string;
  provider: string | null;
  host: string | null;
  port: number;
  fromAddress: string | null;
  fromDisplayName: string | null;
  useSsl: boolean;
  hasUsername: boolean;
  hasPassword: boolean;
}

export interface AdminDispatchJobStatus {
  enabled: boolean;
  intervalDescription: string;
  batchSize: number;
}

export interface AdminSmsConfigStatus {
  enabled: boolean;
  configured: boolean;
  statusLabel: string;
  provider: string | null;
  senderId: string | null;
  hasApiKey: boolean;
}

export interface AdminNotificationConfigStatus {
  inApp: AdminChannelStatus;
  email: AdminEmailConfigStatus;
  sms: AdminSmsConfigStatus;
  dispatchJob: AdminDispatchJobStatus;
}

export interface AdminNotificationConfigStatusV2 extends AdminNotificationConfigStatus {
  source: 'AppSettings' | 'Database' | 'Mixed';
}

export interface AdminUpdateEmailConfigRequest {
  isEnabled: boolean;
  provider?: string | null;
  host?: string | null;
  port?: number | null;
  useSsl?: boolean | null;
  fromAddress?: string | null;
  fromDisplayName?: string | null;
  username?: string | null;
  newSecret?: string | null;
  clearSecret?: boolean;
}

export interface AdminUpdateSmsConfigRequest {
  isEnabled: boolean;
  provider?: string | null;
  senderId?: string | null;
  newSecret?: string | null;
  clearSecret?: boolean;
}

export interface AdminUpdateInAppConfigRequest {
  isEnabled: boolean;
}

export interface AdminUpdateConfigResult {
  succeeded: boolean;
  message: string;
  source: string;
}

export interface AdminTestEmailResult {
  succeeded: boolean;
  wasSkipped: boolean;
  message: string | null;
}

export interface AdminSendNotificationRequest {
  recipientUserIds: string[];
  channels: string[];
  title: string;
  body: string;
  category?: string;
  severity?: string;
  deepLinkUrl?: string | null;
  expiresAtUtc?: string | null;
}

export interface AdminSendNotificationResult {
  requestedRecipientCount: number;
  queuedCount: number;
  skippedCount: number;
  channelsQueued: string[];
  errors: string[];
}

// ── Notification templates ────────────────────────────────────────────────────

export interface AdminTemplateItem {
  id: string;
  templateKey: string;
  channel: string;
  name: string;
  subject: string | null;
  title: string | null;
  body: string;
  category: string;
  severity: string;
  isActive: boolean;
  version: number;
  supportedVariablesJson: string | null;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface AdminTemplateListQuery {
  page?: number;
  pageSize?: number;
  channel?: string;
  category?: string;
  isActive?: boolean;
  search?: string;
}

export interface AdminCreateTemplateRequest {
  templateKey: string;
  channel: string;
  name: string;
  body: string;
  subject?: string | null;
  title?: string | null;
  category?: string;
  severity?: string;
  description?: string | null;
  supportedVariablesJson?: string | null;
}

export interface AdminUpdateTemplateRequest {
  name: string;
  body: string;
  subject?: string | null;
  title?: string | null;
  category?: string;
  severity?: string;
  description?: string | null;
  supportedVariablesJson?: string | null;
}

export interface AdminTemplatePreviewResult {
  succeeded: boolean;
  renderedSubject: string | null;
  renderedTitle: string | null;
  renderedBody: string;
  missingVariables: string[];
}

export interface UpdateExerciseTypeRequest {
  isEnabled?: boolean;
  minItemsPerPractice?: number;
  defaultItemsPerPractice?: number;
  maxItemsPerPractice?: number;
  minOptionsPerItem?: number;
  defaultOptionsPerItem?: number;
  maxOptionsPerItem?: number;
}

// ── Security settings (Phase 10Auth-F-6) ─────────────────────────────────────

export interface AdminPasswordPolicySettings {
  requiredLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireDigit: boolean;
  requireNonAlphanumeric: boolean;
}

export interface AdminLockoutSettings {
  maxFailedAccessAttempts: number;
  lockoutDurationMinutes: number;
}

export interface AdminRateLimitPolicyInfo {
  policyName: string;
  permitLimit: number;
  windowMinutes: number;
  keyedBy: string;
}

export interface AdminJwtSettings {
  accessTokenExpiryHours: number;
  issuerConfigured: boolean;
  audienceConfigured: boolean;
}

export interface AdminRefreshTokenSettings {
  expiryDays: number;
  rotationEnabled: boolean;
  revokeOnPasswordChange: boolean;
  revokeOnPasswordReset: boolean;
}

export interface AdminSecurityHeadersSettings {
  xContentTypeOptionsEnabled: boolean;
  xFrameOptionsEnabled: boolean;
  referrerPolicyEnabled: boolean;
  permissionsPolicyEnabled: boolean;
  cspStatus: string;
  hstsStatus: string;
}

export interface AdminGoogleExternalLoginSettings {
  enabled: boolean;
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  allowAutoLinkByEmail: boolean;
  allowStudentAutoProvisioning: boolean;
  allowedDomains: string[];
}

export interface AdminExternalLoginSettings {
  google: AdminGoogleExternalLoginSettings;
}

export interface AdminSecuritySettings {
  passwordPolicy: AdminPasswordPolicySettings;
  lockout: AdminLockoutSettings;
  rateLimitPolicies: AdminRateLimitPolicyInfo[];
  jwt: AdminJwtSettings;
  refreshToken: AdminRefreshTokenSettings;
  securityHeaders: AdminSecurityHeadersSettings;
  externalLogin: AdminExternalLoginSettings;
}

export interface AdminAuthEventItem {
  id: string;
  eventType: string;
  outcome: string;
  userId: string | null;
  emailOrUserName: string | null;
  failureReasonCode: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  occurredAtUtc: string;
}

export interface AdminAuthEventListQuery {
  page?: number;
  pageSize?: number;
  userId?: string;
  email?: string;
  eventType?: string;
  outcome?: string;
  from?: string;
  to?: string;
}

/** Phase H6 (renamed I4 Pass 3) — admin preview of the Today Plan module selector's decision for
 * a student. */
export interface AdminTodayPlanSelectedModule {
  moduleId: string;
  title: string;
  cefrLevel: string | null;
  skill: string | null;
  estimatedMinutes: number | null;
  reason: string;
}

export interface AdminTodayPlanModulePreview {
  selectedModules: AdminTodayPlanSelectedModule[];
  fallbackRequired: boolean;
  fallbackReason: string | null;
  selectionReason: string | null;
  targetCefrLevel: string | null;
  totalEstimatedMinutes: number;
  warnings: string[];
}

/** Phase H7 — admin preview of the Practice Gym module selector's decision for a student. */
export interface AdminPracticeGymSuggestedModule {
  moduleId: string;
  title: string;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  estimatedMinutes: number | null;
  reason: string;
  isReview: boolean;
  isScaffold: boolean;
  isRemediation: boolean;
  /** Phase H10 — whether this suggestion's Exercise can actually be launched right now. */
  canLaunch: boolean;
  unsupportedReason: string | null;
}

export interface AdminPracticeGymModulePreview {
  suggestions: AdminPracticeGymSuggestedModule[];
  fallbackRequired: boolean;
  fallbackReason: string | null;
  selectionReason: string | null;
  targetCefrLevel: string | null;
  warnings: string[];
}

// ── Admin student practice summary ────────────────────────────────────────────

export interface AdminStudentPracticeSuggestionItem {
  title: string;
  primarySkill: string | null;
  callToAction: string;
  explanation: string;
  routingReason: string;
  targetCefrLevel: string;
  estimatedDurationMinutes: number | null;
}

export interface AdminStudentPracticeSummary {
  status: string; // Ready | ReviewOnly | Preparing | NotAvailable
  reviewQueueCount: number;
  reservedCount: number;
  weakestSkill: string | null;
  topSuggestion: AdminStudentPracticeSuggestionItem | null;
  isReplenishmentRecommended: boolean;
}

// ── Admin student progress summary (Phase 15F) ───────────────────────────────

export interface AdminStudentProgressSummary {
  currentCefrLevel: string | null;
  placementCefrLevel: string | null;
  placementCompletedAt: string | null;
  masteredObjectivesCount: number;
  inProgressObjectivesCount: number;
  reviewQueueCount: number;
  totalObjectives: number;
  completionPercentage: number;
  strongestSkill: string | null;
  weakestSkill: string | null;
  weakSkillsCount: number;
  lastLearningActivityAt: string | null;
  currentLearningPhase: string;
}

// ── Phase 20A — Admin AI Operations Dashboard ────────────────────────────────

export interface AiOperationsProviderCount {
  provider: string;
  calls: number;
  successful: number;
  fallback: number;
  costUsd: number;
}

export interface AiOperationsFeatureCount {
  feature: string;
  calls: number;
  successful: number;
  costUsd: number;
}

export interface AiOperationsProviderUsageSummary {
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  fallbackCalls: number;
  totalCostUsd: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalTokens: number;
  zeroCostCallCount: number;
  byProvider: AiOperationsProviderCount[];
  byFeature: AiOperationsFeatureCount[];
}

export interface AiOperationsProviderModelCount {
  providerName: string;
  modelName: string | null;
  count: number;
}

export interface AiOperationsSpeakingSummary {
  configEnabled: boolean;
  providerName: string;
  pendingCount: number;
  completedCount: number;
  failedCount: number;
  notSupportedCount: number;
  oldestPendingAgeMinutes: number | null;
  providerModelDistribution: AiOperationsProviderModelCount[];
  latestFailureReasons: string[];
}

export interface AiOperationsWritingSummary {
  configEnabled: boolean;
  providerName: string | null;
  modelName: string | null;
  pendingCount: number;
  evaluatingCount: number;
  completedCount: number;
  failedCount: number;
  notSupportedCount: number;
  oldestPendingAgeMinutes: number | null;
  latestFailureReasons: string[];
}

export interface AiOperationsPatternFailureBreakdown {
  patternKey: string;
  totalFailures: number;
  abandonedCount: number;
  latestError: string | null;
}

export interface AiOperationsCefrFailureBreakdown {
  cefrLevel: string;
  totalFailures: number;
}

export interface AiOperationsProviderFailureBreakdown {
  providerName: string;
  modelName: string;
  totalFailures: number;
  abandonedCount: number;
}

export interface AiOperationsValidationFailureItem {
  timestampUtc: string;
  patternKey: string | null;
  activityTypeName: string;
  cefrLevel: string | null;
  objectiveKey: string | null;
  validationErrors: string;
  attemptNumber: number;
  providerName: string | null;
  modelName: string | null;
  generationSource: string | null;
  correlationId: string | null;
}

export interface AiOperationsGenerationQualitySummary {
  totalValidationFailures: number;
  abandonedGenerations: number;
  recentFailureCount: number;
  retentionDays: number;
  patternBreakdown: AiOperationsPatternFailureBreakdown[];
  cefrBreakdown: AiOperationsCefrFailureBreakdown[];
  providerBreakdown: AiOperationsProviderFailureBreakdown[];
  latestFailures: AiOperationsValidationFailureItem[];
}

export interface AiOperationsSignalGateSummary {
  speakingCefrUpdatesEnabled: boolean;
  writingCefrUpdatesEnabled: boolean;
  speakingObjectiveCompletionEnabled: boolean;
  writingObjectiveCompletionEnabled: boolean;
  speakingLearningPlanAutoRegenEnabled: boolean;
  writingLearningPlanAutoRegenEnabled: boolean;
  speakingPositiveSignalsEnabled: boolean;
  writingPositiveSignalsEnabled: boolean;
  speakingReviewSignalsEnabled: boolean;
  writingReviewSignalsEnabled: boolean;
  anyInvariantViolationsDetected: boolean;
}

export interface AiOperationsRecentFailureItem {
  timestampUtc: string;
  area: 'Speaking' | 'Writing' | 'Generation';
  studentProfileId: string | null;
  evaluationId: string | null;
  providerName: string | null;
  modelName: string | null;
  reason: string;
  status: string;
}

export interface AdminAiOperationsSummary {
  generatedAtUtc: string;
  overallStatus: 'Healthy' | 'Degraded' | 'AttentionNeeded';
  warnings: string[];
  unavailableSections: string[];
  providerUsage: AiOperationsProviderUsageSummary;
  speakingEvaluationSummary: AiOperationsSpeakingSummary;
  writingEvaluationSummary: AiOperationsWritingSummary;
  generationQualitySummary: AiOperationsGenerationQualitySummary;
  signalGateSummary: AiOperationsSignalGateSummary;
  recentFailures: AiOperationsRecentFailureItem[];
}

export interface MasteryValidationSummary {
  totalStudentsEvaluated: number;
  totalObjectivesEvaluated: number;
  countInsufficientEvidence: number;
  countMastered: number;
  countNeedsReview: number;
  countNeedsPractice: number;
  countAtRisk: number;
  masteredExcludedFromNewLearning: number;
  warnings: string[];
  generatedAt: string;
}

// ── Dashboard aggregate ────────────────────────────────────────────────────────

export interface ActivityTrendBucket { date: string; activityCount: number; completedCount: number; failedCount: number; }
export interface AdminDashboardActivityTrendResponse { period: string; buckets: ActivityTrendBucket[]; }

export interface ScoreDistributionBucket { label: string; minScore: number; maxScore: number; count: number; }
export interface AdminDashboardScoreDistributionResponse { period: string; totalScoredAttempts: number; buckets: ScoreDistributionBucket[]; averageScore: number | null; }

export interface AdminAggAiUsageTrendBucket { date: string; requestCount: number; successfulCalls: number; failedCalls: number; inputTokens: number; outputTokens: number; totalTokens: number; cost: number; }
export interface AdminAiUsageTrendResponse { period: string; buckets: AdminAggAiUsageTrendBucket[]; }

export interface AiUsageCategoryBreakdownItem { category: string; requestCount: number; totalTokens: number; cost: number; failedCalls: number; }
export interface AdminAiUsageCategoryBreakdownResponse { period: string; categories: AiUsageCategoryBreakdownItem[]; }

// ── Delivery health (rehaul 2026-07-17) ─────────────────────────────────────────
// Fleet-wide, read-only aggregate over the bank-first module-assignment pipeline. Shared shape
// for both Today Plan (`selectedCount`) and Practice Gym (`selectedCount` maps to "suggested").
// Replaces the deleted legacy lesson-generation-buffer/readiness-pool health surfaces — see
// docs/reviews/2026-07-10-phase-i2b-*.md, -i2c-*.md and
// docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md.

export interface AdminDeliveryHealthToday {
  eligibleStudents: number;
  selectedCount: number;
  fallbackOnlyCount: number;
  noAssignmentCount: number;
}

export interface AdminDeliveryHealthCefrBucket {
  cefrLevel: string;
  eligibleStudents: number;
  selectedCount: number;
  fallbackOnlyCount: number;
}

export interface AdminDeliveryHealthTrendBucket {
  date: string;
  selectedCount: number;
  fallbackOnlyCount: number;
}

export interface AdminDeliveryHealthFallbackReason {
  reason: string;
  count: number;
}

export interface AdminDeliveryHealthBankCoverage {
  cefrLevel: string;
  eligibleStudents: number;
  approvedModuleCount: number;
}

export interface AdminDeliveryHealth {
  today: AdminDeliveryHealthToday;
  byCefrLevel: AdminDeliveryHealthCefrBucket[];
  trend: AdminDeliveryHealthTrendBucket[];
  topFallbackReasons: AdminDeliveryHealthFallbackReason[];
  bankCoverage: AdminDeliveryHealthBankCoverage[];
}

// Phase 13A — Adaptive Placement Engine

export interface AdminPlacementSkillResult {
  skill: string;
  estimatedCefrLevel: string;
  confidence: number;
  evidenceCount: number;
  strengths: string | null;
  weaknesses: string | null;
  recommendedObjectiveKeys: string[];
}

export interface AdminPlacementAssessmentSummary {
  assessmentId: string;
  studentProfileId: string;
  status: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  expiredAtUtc: string | null;
  overallCefrLevel: string | null;
  overallConfidence: number | null;
  isProvisional: boolean;
  resultSummary: string | null;
  source: string | null;
  skillResults: AdminPlacementSkillResult[];
  learningPlanRegenerated: boolean;
  learningPlanRegenerationWarning: string | null;
  itemCount: number;
}

export interface AdminPlacementHistoryItem {
  assessmentId: string;
  status: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  overallCefrLevel: string | null;
  overallConfidence: number | null;
  isProvisional: boolean;
  itemCount: number;
}

export interface AdminPlacementLatestResponse {
  hasPlacement: boolean;
  assessmentId?: string;
  studentProfileId?: string;
  status?: string;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  expiredAtUtc?: string | null;
  overallCefrLevel?: string | null;
  overallConfidence?: number | null;
  isProvisional?: boolean;
  resultSummary?: string | null;
  source?: string | null;
  skillResults?: AdminPlacementSkillResult[];
  learningPlanRegenerated?: boolean;
  learningPlanRegenerationWarning?: string | null;
  itemCount?: number;
}

// Phase 13B — Response Submission and Real Scoring

export interface AdminPlacementItemHistory {
  itemId: string;
  skill: string;
  targetCefrLevel: string;
  itemType: string;
  prompt: string;
  response: string | null;
  isCorrect: boolean | null;
  score: number | null;
  evaluatedAtUtc: string | null;
  evaluationNotes: string | null;
  durationSeconds: number | null;
  itemOrder: number;
}

export interface AdminPlacementSkillProgress {
  skill: string;
  currentEstimatedLevel: string;
  confidence: number;
  evidenceCount: number;
  consecutiveSuccesses: number;
  consecutiveFailures: number;
}

export interface AdminPlacementProgress {
  assessmentId: string;
  status: string;
  answeredCount: number;
  totalItemCount: number;
  estimatedRemainingItems: number;
  currentSkill: string | null;
  currentCefrLevel: string | null;
  overallConfidence: number;
  skillProgress: AdminPlacementSkillProgress[];
  itemHistory: AdminPlacementItemHistory[];
  completionReason: string | null;
}

export interface AdminPlacementNextItem {
  itemId: string;
  skill: string;
  targetCefrLevel: string;
  itemType: string;
  prompt: string;
  itemOrder: number;
  answeredCount: number;
  estimatedRemainingItems: number;
}

export interface AdminPlacementSubmitResult {
  itemId: string;
  isCorrect: boolean;
  score: number;
  evaluationNotes: string;
  assessmentComplete: boolean;
  completionReason: string | null;
  nextItem: AdminPlacementNextItem | null;
  summary: AdminPlacementAssessmentSummary | null;
}

// Phase 15E — Learning Plan / Journey admin parity

export interface AdminLearningPlanObjective {
  objectiveKey: string;
  title: string | null;
  skill: string;
  cefrLevel: string;
  status: string;
  sequenceNumber: number;
  isReview: boolean;
  isBlocked: boolean;
}

export interface AdminLearningPlanProgress {
  planStatus: string;
  currentCefrLevel: string;
  currentLearningPhase: string;
  totalObjectives: number;
  completionPercentage: number;
  lastCompletedAt: string | null;
  currentObjective: AdminLearningPlanObjective | null;
  upcomingObjectives: AdminLearningPlanObjective[];
  completedObjectives: AdminLearningPlanObjective[];
  reviewObjectives: AdminLearningPlanObjective[];
}

// Phase 16E/16F — Speaking submission visibility + evaluation

export interface AdminStudentSpeakingAttempt {
  attemptId: string;
  activityId: string;
  activityTitle: string | null;
  activityType: string | null;
  submittedAt: string;
  mimeType: string | null;
  /** Submitted | PendingEvaluation | Evaluated | EvaluationFailed | EvaluationUnavailable */
  status: string;
  /** Pending | Evaluating | Completed | Failed | Skipped | NotSupported. Null if no evaluation record. */
  evaluationStatus: string | null;
  evaluationProvider: string | null;
  evaluationModel: string | null;
  evaluationCompletedAt: string | null;
  evaluationFeedbackText: string | null;
  evaluationSuggestedImprovement: string | null;
  evaluationFailureReason: string | null;
  overallScore: number | null;
  // Phase 16H — dry-run signal preview. Never applied to mastery, CEFR, or Learning Plan.
  dryRunOutcome: string | null;
  dryRunConfidence: string | null;
  dryRunCandidateSkill: string | null;
  dryRunBlockedReason: string | null;
  // Phase 16J — applied signal detail
  isApplied: boolean;
  appliedSignalType: string | null;
  appliedSignalConfidence: string | null;
  appliedSignalBlockedReason: string | null;
  appliedAt: string | null;
  /** Always false — structural invariant. For UI display only. */
  signalUpdatesCefr: boolean;
  /** Always false — structural invariant. For UI display only. */
  signalCompletesObjectives: boolean;
}

export interface AdminStudentSpeakingAttemptsResult {
  /** Ready | Empty | NotFound */
  status: string;
  attempts: AdminStudentSpeakingAttempt[];
}

// Phase 16H — Speaking evaluation quality summary

export interface SpeakingProviderModelCount {
  providerName: string;
  modelName: string | null;
  count: number;
}

export interface SpeakingEvaluationQualityMetrics {
  total: number;
  completed: number;
  failed: number;
  notSupported: number;
  pending: number;
  completionRate: number;
  failureRate: number;
  averageOverallScore: number | null;
  averageFluencyScore: number | null;
  averageCompletenessScore: number | null;
  averageRelevanceScore: number | null;
  averagePronunciationScore: number | null;
  nullOverallScoreRate: number;
  nullFluencyScoreRate: number;
  nullCompletenessScoreRate: number;
  nullRelevanceScoreRate: number;
  dryRunCandidatePositiveSignals: number;
  dryRunCandidateReviewSignals: number;
  dryRunCandidateNoSignals: number;
  dryRunBlocked: number;
  // Phase 16J — applied / blocked breakdown
  dryRunCandidates: number;
  applied: number;
  blockedByConfig: number;
  blockedByConfidence: number;
  blockedByMissingScore: number;
  blockedByUnsupportedStatus: number;
  blockedByFailedEval: number;
  duplicateSkipped: number;
  appliedReview: number;
  appliedPositive: number;
  providerModelDistribution: SpeakingProviderModelCount[];
  latestFailureReasons: string[];
  latestBlockedReasons: string[];
}

export interface AdminSpeakingEvaluationQualitySummary {
  configStatus: string;
  providerName: string;
  enabled: boolean;
  supportsTranscript: boolean;
  supportsPronunciationScore: boolean;
  // Phase 16J — mastery signal config and thresholds
  applyMasterySignals: boolean;
  allowReviewSignals: boolean;
  allowPositiveSignals: boolean;
  minimumConfidenceRequired: string;
  minPositiveOverall: number;
  minReviewOverallMax: number;
  quality: SpeakingEvaluationQualityMetrics;
}

// Phase 16I — Speaking evaluation mastery signal integration
export interface AdminSpeakingAppliedSignalSummary {
  masteryIntegrationEnabled: boolean;
  reviewSignalsAllowed: boolean;
  positiveSignalsAllowed: boolean;
  objectiveCompletionAllowed: boolean;
  cefrUpdateAllowed: boolean;
  minimumConfidenceRequired: string;
  totalCompletedEvaluations: number;
  candidateSignals: number;
  appliedSignals: number;
  blockedByConfig: number;
  blockedByConfidence: number;
  blockedBySignalType: number;
  blockedByFailedOrUnsupported: number;
  blockedByMissingScore: number;
  duplicateSkipped: number;
  noSignal: number;
  failedApplication: number;
}

// Phase 16J — Signal safety verification
export interface AdminSignalSafetySummary {
  cefrUpdatesDisabled: boolean;
  objectiveCompletionsDisabled: boolean;
  learningPlanAutoRegenDisabled: boolean;
  signalApplicationEnabled: boolean;
  positiveSignalsEnabled: boolean;
  reviewSignalsEnabled: boolean;
  totalApplied: number;
  positiveApplied: number;
  reviewApplied: number;
  invariantViolationsDetected: boolean;
}

// Phase 16J — Per-attempt applied signal detail
export interface AdminStudentSpeakingAttemptAppliedSignal {
  signalType: string | null;
  confidence: string | null;
  blockedReason: string | null;
  appliedAt: string | null;
  isApplied: boolean;
  signalUpdatesCefr: boolean;
  signalCompletesObjectives: boolean;
}

// Phase 17A/17C — Writing evaluation per-student item

export interface AdminWritingEvaluationItemDto {
  evaluationId: string;
  attemptId: string;
  activityId: string;
  activityTitle: string | null;
  activityType: string | null;
  status: string;
  providerName: string | null;
  modelName: string | null;
  submittedAtUtc: string | null;
  completedAtUtc: string | null;
  overallScore: number | null;
  grammarScore: number | null;
  vocabularyScore: number | null;
  coherenceScore: number | null;
  taskCompletionScore: number | null;
  feedbackText: string | null;
  suggestedImprovement: string | null;
  correctedText: string | null;
  failureReason: string | null;
}

// Phase 17B — Writing evaluation quality summary and dry-run signal

export interface WritingEvaluationQualitySummaryDto {
  configEnabled: boolean;
  providerName: string | null;
  modelName: string | null;
  totalEvaluations: number;
  pendingCount: number;
  evaluatingCount: number;
  completedCount: number;
  failedCount: number;
  notSupportedCount: number;
  completionRate: number;
  failureRate: number;
  nullOverallScoreRate: number;
  nullGrammarScoreRate: number;
  nullVocabularyScoreRate: number;
  nullCoherenceScoreRate: number;
  nullTaskCompletionScoreRate: number;
  correctedTextAvailabilityRate: number;
  averageOverallScore: number | null;
  averageGrammarScore: number | null;
  averageVocabularyScore: number | null;
  averageCoherenceScore: number | null;
  averageTaskCompletionScore: number | null;
  dryRunCandidateCount: number;
  dryRunBlockedCount: number;
  dryRunOutcomeBreakdown: Record<string, number>;
  latestFailureReasons: string[];
  note: string;
}

export interface WritingEvaluationDryRunSignalDto {
  evaluationId: string;
  attemptId: string;
  studentId: string;
  activityId: string;
  createdAt: string;
  providerName: string | null;
  modelName: string | null;
  sourceStatus: string;
  candidateSkill: string;
  overallScore: number | null;
  grammarScore: number | null;
  vocabularyScore: number | null;
  coherenceScore: number | null;
  taskCompletionScore: number | null;
  confidenceBand: string;
  outcome: string;
  suggestedMasteryDelta: number | null;
  suggestedReviewNeed: boolean;
  acceptedForFutureSignal: boolean;
  blockedReason: string | null;
  notes: string | null;
}

export interface WritingEvaluationWithDryRunDto {
  evaluationId: string;
  attemptId: string;
  studentId: string;
  activityId: string;
  status: string;
  providerName: string | null;
  modelName: string | null;
  completedAtUtc: string | null;
  overallScore: number | null;
  grammarScore: number | null;
  vocabularyScore: number | null;
  coherenceScore: number | null;
  taskCompletionScore: number | null;
  feedbackText: string | null;
  suggestedImprovement: string | null;
  correctedText: string | null;
  failureReason: string | null;
  dryRunSignal: WritingEvaluationDryRunSignalDto | null;
}

// Phase 17C — Writing evaluation signal application

export interface WritingSignalApplicationSummaryDto {
  masteryIntegrationEnabled: boolean;
  reviewSignalsAllowed: boolean;
  positiveSignalsAllowed: boolean;
  objectiveCompletionAllowed: boolean;
  cefrUpdateAllowed: boolean;
  minimumConfidenceRequired: string;
  totalCompletedEvaluations: number;
  candidateSignals: number;
  appliedSignals: number;
  blockedByConfig: number;
  blockedByConfidence: number;
  blockedBySignalType: number;
  blockedByFailedOrUnsupported: number;
  blockedByMissingScore: number;
  duplicateSkipped: number;
  noSignal: number;
  failedApplication: number;
}

export interface WritingSignalSafetySummaryDto {
  cefrUpdatesDisabled: boolean;
  objectiveCompletionsDisabled: boolean;
  learningPlanAutoRegenDisabled: boolean;
  signalApplicationEnabled: boolean;
  positiveSignalsEnabled: boolean;
  reviewSignalsEnabled: boolean;
  totalApplied: number;
  positiveApplied: number;
  reviewApplied: number;
  invariantViolationsDetected: boolean;
}

// ── Runtime settings / feature gates (Phase 20B) ───────────────────────────────

// Phase I2C: 'reviewScaffoldPracticeGymPilot' removed from this union — the readiness-pool
// feature gate groups on that category were deleted. 'activityFeedback' (a legitimate backend
// FeatureGateCategory value, pre-existing) is not listed here either — out of scope for this pass.
export type FeatureGateCategory = 'readinessPoolLessonGeneration' | 'aiSignalSafety';
export type FeatureGateDataType = 'boolean' | 'integer' | 'string' | 'stringArray';
export type FeatureGateRiskLevel = 'low' | 'medium' | 'high' | 'critical';
export type FeatureGateValueSource = 'appSettings' | 'databaseOverride' | 'default' | 'hardcoded';

export interface FeatureGateSettingValue {
  key: string;
  displayName: string;
  description: string;
  dataType: FeatureGateDataType;
  effectiveValueJson: string;
  defaultValueJson: string;
  valueSource: FeatureGateValueSource;
  isEditableAtRuntime: boolean;
  isRuntimeEffective: boolean;
  riskLevel: FeatureGateRiskLevel;
  requiresConfirmation: boolean;
  minValue: number | null;
  maxValue: number | null;
  maxLength: number | null;
  allowedValues: string[] | null;
}

export interface FeatureGateGroup {
  groupKey: string;
  displayName: string;
  description: string;
  category: FeatureGateCategory;
  isReadOnly: boolean;
  requiresRestart: boolean;
  productionChangeAllowed: boolean;
  dependencies: string[];
  warningText: string | null;
  settings: FeatureGateSettingValue[];
  lastChangedByUserId: string | null;
  lastChangedAtUtc: string | null;
  lastChangeReason: string | null;
  hasActiveOverride: boolean;
}

export interface UpdateFeatureGateRequest {
  values: Record<string, unknown>;
  reason: string;
  confirmationText?: string | null;
}

export interface ResetFeatureGateRequest {
  reason: string;
}

// ── Student pilot readiness (Phase 20D) ─────────────────────────────────────────

export type ReadinessCheckStatus = 'pass' | 'warning' | 'fail' | 'notApplicable' | 'notImplemented';
export type ReadinessCheckSeverity = 'info' | 'warning' | 'blocking';
export type ReadinessOverallStatus = 'ready' | 'needsAttention' | 'blocked' | 'notStarted';
export type ReadinessRepairRiskLevel = 'low' | 'medium' | 'high';

export interface StudentReadinessCheck {
  key: string;
  displayName: string;
  category: string;
  status: ReadinessCheckStatus;
  severity: ReadinessCheckSeverity;
  message: string;
  technicalDetail: string | null;
  recommendedActionKey: string | null;
  canRepair: boolean;
  repairRiskLevel: ReadinessRepairRiskLevel | null;
  lastCheckedAtUtc: string;
}

export interface StudentReadinessSummary {
  studentId: string;
  studentEmail: string | null;
  generatedAtUtc: string;
  readyForPilot: boolean;
  readinessStatus: ReadinessOverallStatus;
  blockingIssueCount: number;
  warningCount: number;
  infoCount: number;
  lastRepairAtUtc: string | null;
  checks: StudentReadinessCheck[];
  recommendedActions: string[];
  unavailableSections: string[];
}

export interface StudentReadinessRepairRequest {
  actionKey: string;
  reason?: string | null;
  dryRun: boolean;
}

export interface StudentReadinessRepairResult {
  actionKey: string;
  dryRun: boolean;
  changedCount: number;
  skippedCount: number;
  warnings: string[];
  errors: string[];
  beforeSummary: string | null;
  afterSummary: string | null;
  auditLogId: string | null;
}

// ── Adaptive Curriculum Sprint 1 — skill graph ──────────────────────────────────
// See docs/architecture/adaptive-curriculum-skill-graph.md. Additive only — nothing outside
// /admin/skill-graph reads these yet.

export interface SkillGraphTaxonomy {
  cefrLevels: string[];
  skills: string[];
  subskillsBySkill: Record<string, string[]>;
  // Phase 6.1 — shared 13-value ContextTag/FocusTag vocabulary, for the Nodes table's tag filters.
  contextTags: string[];
  focusTags: string[];
}

export interface SkillGraphNodeListItem {
  id: string;
  key: string;
  title: string;
  description: string;
  cefrLevel: string;
  skill: string;
  subskill: string | null;
  difficultyBand: number;
  reviewStatus: 'NotRequired' | 'PendingReview' | 'Approved' | 'Rejected';
  isActive: boolean;
  rejectionReason: string | null;
  createdAt: string;
  contextTags: string[];
  focusTags: string[];
  // Content-coverage merge (2026-07-23) — replaces the deleted separate "Content coverage" table.
  linkedModuleCount: number;
}

export interface SkillGraphNodeListResponse {
  items: SkillGraphNodeListItem[];
  totalCount: number;
  totalPages: number;
  page: number;
  pageSize: number;
}

export interface SkillGraphNodePrerequisiteRef {
  id: string;
  key: string;
  title: string;
}

export interface SkillGraphNodeDetail extends SkillGraphNodeListItem {
  descriptionForAi: string | null;
  reviewedByUserId: string | null;
  approvedAtUtc: string | null;
  rejectedAtUtc: string | null;
  prerequisites: SkillGraphNodePrerequisiteRef[];
  // Editability audit (2026-07-23) — nodes that list this one as a prerequisite.
  dependents: SkillGraphNodePrerequisiteRef[];
  // Content-coverage merge (2026-07-23) — real Modules linked to this node.
  linkedModules: SkillGraphLinkedModuleRef[];
}

// Editability audit (2026-07-23) — manual node create/edit + manual edge management.

export interface CreateSkillGraphNodeRequest {
  title: string;
  description: string;
  cefrLevel: string;
  skill: string;
  subskill: string | null;
  difficultyBand: number;
  descriptionForAi: string | null;
  contextTags: string[];
  focusTags: string[];
  // Create node UX audit (2026-07-23) — place the node in the graph at creation time, both
  // directions: what it depends on, and what depends on it (a node can have several
  // prerequisites and be the prerequisite for several other nodes — genuine many-to-many).
  prerequisiteNodeIds?: string[];
  dependentNodeIds?: string[];
}

export interface CreateSkillGraphNodeResponse {
  id: string;
  key: string;
  droppedPrerequisites: { prerequisiteNodeId: string; error: string }[];
  droppedDependents: { dependentNodeId: string; error: string }[];
}

export interface UpdateSkillGraphNodeRequest {
  title: string;
  description: string;
  cefrLevel: string;
  skill: string;
  subskill: string | null;
  difficultyBand: number;
  descriptionForAi: string | null;
}

export interface SkillGraphIsolatedNode {
  id: string;
  key: string;
  title: string;
  cefrLevel: string;
  skill: string;
  reviewStatus: string;
}

export interface SkillGraphIsolatedNodesResponse {
  isolatedCount: number;
  isolated: SkillGraphIsolatedNode[];
}

// Phase 6.2 — Node-to-Node AI placement suggestions (advisory only, never auto-applied).
export interface SkillGraphPlacementSuggestion {
  id: string;
  key: string;
  title: string;
  confidence: number;
}

export interface SkillGraphPlacementSuggestionResponse {
  success: boolean;
  prerequisites: SkillGraphPlacementSuggestion[];
  dependents: SkillGraphPlacementSuggestion[];
  error: string | null;
}

// Phase 6.3a — deterministic (no AI) redundant-edge detection. Advisory only, never auto-applied.
export interface GraphChangeEdgeRef {
  nodeId: string;
  nodeTitle: string;
  prerequisiteNodeId: string;
  prerequisiteNodeTitle: string;
}

export interface GraphChangeSuggestion {
  type: string;
  description: string;
  proposedEdgesToAdd: GraphChangeEdgeRef[];
  proposedEdgesToRemove: GraphChangeEdgeRef[];
}

export interface GraphChangeSuggestionsResponse {
  count: number;
  suggestions: GraphChangeSuggestion[];
}

export interface SkillGraphDraftResponse {
  queued: boolean;
  createdCount: number;
  droppedEdgeCount?: number;
  error: string | null;
}

export interface SkillGraphBatchActionResponse {
  requestedCount: number;
  succeeded: number;
  failed: number;
  limitReached: boolean;
}

export interface SkillGraphCoverageEntry {
  cefrLevel: string;
  skill: string;
  approvedCount: number;
  pendingCount: number;
  hasGap: boolean;
}

export interface SkillGraphCoverageResponse {
  matrix: SkillGraphCoverageEntry[];
}

// Sprint 2 — Module-to-node content coverage (distinct from node-existence coverage above).

export interface SkillGraphRetagModuleResult {
  moduleId: string;
  moduleTitle: string;
  matchedCount: number;
  error: string | null;
}

export interface SkillGraphRetagResponse {
  sweptCount: number;
  results: SkillGraphRetagModuleResult[];
  remainingUntaggedModuleCount: number;
}

export interface SkillGraphLinkedModuleRef {
  id: string;
  title: string;
}

// Sprint 14.2 — every approved node (not just gap ones), with its real linked-Module list.
export interface SkillGraphCoverageNode {
  id: string;
  key: string;
  title: string;
  cefrLevel: string;
  skill: string;
  contextTags: string[];
  focusTags: string[];
  linkedModuleCount: number;
  linkedModules: SkillGraphLinkedModuleRef[];
}

export interface SkillGraphContentCoverageResponse {
  totalApprovedNodes: number;
  nodesWithContent: number;
  nodesWithoutContentCount: number;
  nodes: SkillGraphCoverageNode[];
}

// Sprint 13 — bulk nodes+edges payload for the Cytoscape/Dagre graph view.

export interface SkillGraphNode {
  id: string;
  key: string;
  title: string;
  cefrLevel: string;
  skill: string;
  subskill: string | null;
  difficultyBand: number;
  reviewStatus: 'NotRequired' | 'PendingReview' | 'Approved' | 'Rejected';
  contextTags: string[];
  focusTags: string[];
}

export interface SkillGraphEdge {
  nodeId: string;
  prerequisiteNodeId: string;
}

export interface SkillGraphResponse {
  nodes: SkillGraphNode[];
  edges: SkillGraphEdge[];
}
