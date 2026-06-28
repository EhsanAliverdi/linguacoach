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

export interface CareerProfileItem {
  id: string;
  name: string;
}

export interface CurriculumWordItem {
  id: string;
  word: string;
  definition: string;
  exampleSentence: string;
  priority: number;
  tags: string;
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
}

export interface UpdatePricingOverrideRequest {
  inputPricePer1KTokens: number;
  outputPricePer1KTokens: number;
  currency: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
  notes?: string | null;
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
  supportsPracticeGym: boolean;
  supportsTodayLesson: boolean;
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
  supportsPracticeGym?: boolean;
  supportsTodayLesson?: boolean;
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

export interface ReadinessPoolSourceHealth {
  source: string;
  targetCount: number;
  readyCount: number;
  reservedCount: number;
  queuedOrGeneratingCount: number;
  failedCount: number;
  staleCount: number;
  expiredCount: number;
  skippedCount: number;
  reviewOnlyCount: number;
  shortfallCount: number;
  needsReplenishment: boolean;
}

// Full pool summary returned by /readiness-pool (includes mastery engine fields from Phase 10Z)
export interface AdminMasteryPoolSummary {
  studentId: string;
  queuedCount: number;
  generatingCount: number;
  readyCount: number;
  reservedCount: number;
  consumedCount: number;
  expiredCount: number;
  failedCount: number;
  staleCount: number;
  skippedCount: number;
  reviewOnlyCount: number;
  masteredCount: number;
  needsReviewCount: number;
  lastEvaluatedAtUtc: string | null;
}

export interface StudentReadinessPoolHealth {
  studentId: string;
  todayLesson: ReadinessPoolSourceHealth;
  practiceGym: ReadinessPoolSourceHealth;
}

export interface AggregatePoolHealthSummary {
  totalStudentsWithItems: number;
  totalQueued: number;
  totalGenerating: number;
  totalReady: number;
  totalReserved: number;
  totalConsumed: number;
  totalExpired: number;
  totalFailed: number;
  totalStale: number;
  totalReviewOnly: number;
  totalSkipped: number;
  studentsWithNoReadyItems: number;
  studentsWithFailedItems: number;
  studentsWithStaleItems: number;
  studentsBelowMinimumThreshold: number;
  averageReadyPerStudent: number;
  oldestReadyItemCreatedAt: string | null;
  newestItemCreatedAt: string | null;
  generatedAt: string;
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

// ── Review scaffold dry-run / mastery diagnostics ────────────────────────────

export interface ReviewScaffoldDryRunSummary {
  generationEnabled: boolean;
  dryRunOnly: boolean;
  status: 'Disabled' | 'DryRun' | 'Enabled';
  studentsConsidered: number;
  studentsEligibleForReview: number;
  estimatedReviewOnlyConversions: number;
  blockedDuplicates: number;
  blockedInactiveObjectives: number;
  estimatedNetNewReviewItems: number;
  warnings: string[];
  generatedAt: string;
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

// ── Generation settings ────────────────────────────────────────────────────────

export interface AdminGenerationSettings {
  readyLessonBufferSize: number;
  refillThreshold: number;
  refillBatchSize: number;
  maxGenerationAttempts: number;
  generationTimeoutSeconds: number;
  ttsTimeoutSeconds: number;
  maxConcurrentGenerationJobs: number;
  maxConcurrentTtsJobs: number;
  enableBackgroundGeneration: boolean;
  enableTtsGeneration: boolean;
  practiceGymReadyExercisesPerType: number;
  practiceGymRefillThresholdPerType: number;
  practiceGymRefillCountPerType: number;
  updatedAtUtc: string | null;
}

export interface AdminUpdateGenerationSettingsRequest {
  readyLessonBufferSize: number;
  refillThreshold: number;
  refillBatchSize: number;
  maxGenerationAttempts: number;
  generationTimeoutSeconds: number;
  ttsTimeoutSeconds: number;
  maxConcurrentGenerationJobs: number;
  maxConcurrentTtsJobs: number;
  enableBackgroundGeneration: boolean;
  enableTtsGeneration: boolean;
  practiceGymReadyExercisesPerType: number;
  practiceGymRefillThresholdPerType: number;
  practiceGymRefillCountPerType: number;
}

// ── Generation batches ─────────────────────────────────────────────────────────

export interface AdminGenerationBatchSummary {
  queued: number;
  running: number;
  failed: number;
  lastSuccessfulGenerationUtc: string | null;
}

export interface AdminReadyBufferEntry {
  studentProfileId: string;
  readyCount: number;
}

export interface AdminGenerationBatchItem {
  id: string;
  studentProfileId: string;
  triggerReason: string;
  status: string;
  requestedSessionCount: number;
  completedSessionCount: number;
  providerName: string | null;
  modelName: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  failureReason: string | null;
  createdAt: string;
}

export interface AdminGenerationBatchesResponse {
  summary: AdminGenerationBatchSummary;
  readyBufferPerStudent: AdminReadyBufferEntry[];
  batches: AdminGenerationBatchItem[];
}

export interface AdminGenerateLessonsResponse {
  queued: boolean;
  requestedCount: number;
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

// Phase 16E — Speaking submission visibility

export interface AdminStudentSpeakingAttempt {
  attemptId: string;
  activityId: string;
  activityTitle: string | null;
  activityType: string | null;
  submittedAt: string;
  mimeType: string | null;
  /** Submitted | PendingEvaluation | Evaluated | Failed */
  status: string;
}

export interface AdminStudentSpeakingAttemptsResult {
  /** Ready | Empty | NotFound */
  status: string;
  attempts: AdminStudentSpeakingAttempt[];
}
