using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Admin;

// ── Student list ──────────────────────────────────────────────────────────────

public sealed record StudentListItem(
    Guid StudentProfileId,
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string OnboardingStatus,
    string LifecycleStage,
    string? CefrLevel,
    string? CareerContext,
    string? LearningGoal,
    string? LearningGoalDescription,
    string? DifficultSituationsText,
    int? PreferredSessionDurationMinutes,
    ProfessionalExperienceLevel? ProfessionalExperienceLevel,
    RoleFamiliarity? RoleFamiliarity,
    DateTime CreatedAt,
    // Student-authored learning preferences (read-only for admin)
    string? PreferredName,
    string? SupportLanguageCode,
    string? SupportLanguageName,
    string? DifficultyPreference,
    string? TranslationHelpPreference,
    IReadOnlyList<string> FocusAreas,
    string? CustomFocusArea,
    IReadOnlyList<string> LearningGoals,
    string? CustomLearningGoal,
    DateTimeOffset? LearningPreferencesUpdatedAt);

public sealed record StudentListQuery(
    int Page,
    int PageSize,
    string? Search,
    bool IncludeArchived,
    string? LifecycleStage,
    string? OnboardingStatus,
    string? CefrLevel,
    string? SortBy,
    string? SortDir);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record UpdateStudentProfileCommand(
    Guid StudentProfileId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? CareerContext,
    string? LearningGoal,
    string? LearningGoalDescription,
    string? DifficultSituationsText,
    int? PreferredSessionDurationMinutes,
    ProfessionalExperienceLevel? ProfessionalExperienceLevel,
    RoleFamiliarity? RoleFamiliarity);

public sealed record ArchiveStudentCommand(Guid StudentProfileId);
public sealed record ReactivateStudentCommand(Guid StudentProfileId, Guid AdminUserId);
public sealed record PauseStudentCommand(Guid StudentProfileId, Guid AdminUserId);
public sealed record UnpauseStudentCommand(Guid StudentProfileId, Guid AdminUserId);

public sealed record ResetStudentPasswordCommand(Guid StudentProfileId, string NewPassword, bool MustChangePassword = true);

public sealed record SetStudentCefrCommand(Guid StudentProfileId, Guid AdminUserId, string? CefrLevel, string? Reason);

public sealed record AdminStatsItem(
    int TotalStudents,
    int OnboardedStudents,
    int TotalActivityAttempts);

public sealed record AdminActivityHistoryItem(
    Guid AttemptId,
    Guid ActivityId,
    string ActivityTitle,
    string ActivityType,
    double? Score,
    double? Percentage,
    bool? Passed,
    bool? Completed,
    DateTime CreatedAt);

public sealed record StudentAuditHistoryItemDto(
    string Id,
    string Source,
    string Action,
    string? ActorId,
    string? ActorEmail,
    DateTimeOffset Timestamp,
    string? Summary,
    string? Reason,
    string? OldValue,
    string? NewValue,
    string? CorrelationId,
    string? Details);

// ── Student detail ────────────────────────────────────────────────────────────

public sealed record StudentOnboardingProgressInfo(
    string? CurrentStepKey,
    IReadOnlyList<string> CompletedStepKeys,
    int PercentageComplete,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool IsComplete,
    string? PreliminaryCefrLevel);

public sealed record AdminStudentDetailDto(
    Guid StudentProfileId,
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PreferredName,
    string LifecycleStage,
    string OnboardingStatus,
    string? LastCompletedStep,
    string? CefrLevel,
    string? CareerContext,
    string? LearningGoal,
    string? LearningGoalDescription,
    string? DifficultSituationsText,
    int? PreferredSessionDurationMinutes,
    ProfessionalExperienceLevel? ProfessionalExperienceLevel,
    RoleFamiliarity? RoleFamiliarity,
    DateTime CreatedAt,
    DateTimeOffset? ArchivedAt,
    // Student-authored preferences (read-only for admin)
    string? SupportLanguageCode,
    string? SupportLanguageName,
    string? DifficultyPreference,
    string? TranslationHelpPreference,
    IReadOnlyList<string> FocusAreas,
    string? CustomFocusArea,
    IReadOnlyList<string> LearningGoals,
    string? CustomLearningGoal,
    DateTimeOffset? LearningPreferencesUpdatedAt,
    // Onboarding progress (null if no progress row exists)
    StudentOnboardingProgressInfo? OnboardingProgress,
    // Phase 14B — learning readiness
    bool IsLearningReady,
    DateTime? LastPlacementCompletedAt,
    bool LearningPlanExists);

public interface IAdminStudentQuery
{
    Task<IReadOnlyList<StudentListItem>> ListStudentsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<PagedResponse<StudentListItem>> ListStudentsPagedAsync(StudentListQuery query, CancellationToken ct = default);
    Task<AdminStudentDetailDto?> GetStudentDetailAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<StudentListItem> UpdateStudentAsync(UpdateStudentProfileCommand command, CancellationToken ct = default);
    Task<StudentListItem> ArchiveStudentAsync(ArchiveStudentCommand command, CancellationToken ct = default);
    Task<StudentListItem> ReactivateStudentAsync(ReactivateStudentCommand command, CancellationToken ct = default);
    Task<StudentListItem> PauseStudentAsync(PauseStudentCommand command, CancellationToken ct = default);
    Task<StudentListItem> UnpauseStudentAsync(UnpauseStudentCommand command, CancellationToken ct = default);
    Task ResetStudentPasswordAsync(ResetStudentPasswordCommand command, CancellationToken ct = default);
    Task SetStudentCefrAsync(SetStudentCefrCommand command, CancellationToken ct = default);
    Task<ResetStudentResponse> ResetStudentAsync(ResetStudentCommand command, CancellationToken ct = default);
    Task<int> CountRecentResetsAsync(Guid adminUserId, TimeSpan window, CancellationToken ct = default);
    Task<AdminStatsItem> GetStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminActivityHistoryItem>> GetActivityHistoryAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentAuditHistoryItemDto>?> GetStudentAuditHistoryAsync(Guid studentProfileId, CancellationToken ct = default);
}

// ── Student lifecycle reset ─────────────────────────────────────────────────
// See: docs/architecture/student-lifecycle-reset-tools.md

public sealed record ResetStudentCommand(
    Guid StudentProfileId,
    Guid AdminUserId,
    StudentLifecycleStage TargetStage,
    bool ClearOnboardingAnswers,
    bool ClearPlacementResults,
    bool ClearCoursesAndSessions,
    bool ClearActivityAttempts,
    bool ClearVocabulary,
    bool ClearLearningMemory,
    bool ClearAudioFiles,
    bool ClearProgressData,
    string Reason,
    string CorrelationId);

public sealed record ClearedItemsResult(
    bool OnboardingAnswers,
    bool PlacementResults,
    bool CoursesAndSessions,
    bool ActivityAttempts,
    bool Vocabulary,
    bool LearningMemory,
    int AudioFilesDeleted,
    bool ProgressData);

public sealed record ResetStudentResponse(
    Guid StudentId,
    StudentLifecycleStage PreviousStage,
    StudentLifecycleStage NewStage,
    ClearedItemsResult ClearedItems,
    Guid ResetLogId,
    Guid PerformedByAdminId,
    DateTime PerformedAtUtc,
    string CorrelationId);

// ── Prompt templates ──────────────────────────────────────────────────────────

public sealed record PromptTemplateItem(
    Guid Id,
    string Key,
    int Version,
    bool IsActive,
    int? MaxInputTokens,
    int? MaxOutputTokens,
    DateTime SeededAtUtc,
    string? ContentHashShort = null);

public sealed record PromptTemplateDetail(
    Guid Id,
    string Key,
    string Content,
    int Version,
    bool IsActive,
    int? MaxInputTokens,
    int? MaxOutputTokens);

public sealed record CreatePromptVersionCommand(
    string Key,
    string Content,
    int? MaxInputTokens,
    int? MaxOutputTokens);

public sealed record ActivatePromptCommand(Guid PromptId);
public sealed record DeactivatePromptCommand(Guid PromptId);

public interface IAdminPromptHandler
{
    Task<IReadOnlyList<PromptTemplateItem>> ListPromptsAsync(CancellationToken ct = default);
    Task<PromptTemplateDetail> GetPromptAsync(Guid promptId, CancellationToken ct = default);
    Task<PromptTemplateDetail> CreateVersionAsync(CreatePromptVersionCommand command, CancellationToken ct = default);
    Task ActivateAsync(ActivatePromptCommand command, CancellationToken ct = default);
    Task DeactivateAsync(DeactivatePromptCommand command, CancellationToken ct = default);
}

// ── AI provider config ────────────────────────────────────────────────────────

public sealed record ModelTestStatus(string ModelName, bool Ok, int LatencyMs, string? Error, DateTime TestedAt);

/// <summary>One entry per provider in the catalog — includes credential and per-model test status.</summary>
public sealed record AiProviderCatalogItem(
    string ProviderName,
    IReadOnlyList<string> Models,
    bool HasApiKey,
    IReadOnlyList<ModelTestStatus> ModelTests,
    string? ApiEndpoint = null);

public sealed record SetProviderApiKeyCommand(
    string ProviderName,
    string? ApiKey);

public sealed record SetProviderEndpointCommand(
    string ProviderName,
    string? ApiEndpoint);

public sealed record AddProviderModelCommand(
    string ProviderName,
    string ModelName);

public interface IAdminAiConfigHandler
{
    Task<IReadOnlyList<AiProviderCatalogItem>> ListProvidersAsync(CancellationToken ct = default);
    Task<AiProviderCatalogItem> SetProviderApiKeyAsync(SetProviderApiKeyCommand command, CancellationToken ct = default);
    Task<AiProviderCatalogItem> SetProviderEndpointAsync(SetProviderEndpointCommand command, CancellationToken ct = default);
    Task<AiProviderCatalogItem> AddProviderModelAsync(AddProviderModelCommand command, CancellationToken ct = default);
    Task<AiProviderCatalogItem> TestProviderAsync(string providerName, CancellationToken ct = default);
    Task<AiProviderCatalogItem> TestProviderModelAsync(string providerName, string modelName, CancellationToken ct = default);
    Task<IReadOnlyList<AiConfigCategoryItem>> ListCategoriesAsync(CancellationToken ct = default);
    Task<AiConfigCategoryItem> UpdateCategoryAsync(UpdateAiConfigCategoryCommand command, CancellationToken ct = default);
    Task<CategoryTestResult> TestCategoryAsync(string categoryKey, CancellationToken ct = default);
    IReadOnlyList<AiModelPricingItem> ListPricing();
    Task<IReadOnlyList<AiModelPricingOverrideItem>> ListPricingOverridesAsync(CancellationToken ct = default);
    Task<AiModelPricingOverrideItem> CreatePricingOverrideAsync(CreatePricingOverrideCommand command, CancellationToken ct = default);
    Task<AiModelPricingOverrideItem> UpdatePricingOverrideAsync(UpdatePricingOverrideCommand command, CancellationToken ct = default);
    Task DeactivatePricingOverrideAsync(DeactivatePricingOverrideCommand command, CancellationToken ct = default);
}

// ── AI model pricing ─────────────────────────────────────────────────────────

public sealed record AiModelPricingItem(
    string ProviderName,
    string ModelName,
    decimal InputPer1KTokens,
    decimal OutputPer1KTokens,
    string Currency,
    string Source,
    bool IsConfigured);

// ── AI model pricing overrides ────────────────────────────────────────────────

public sealed record AiModelPricingOverrideItem(
    Guid Id,
    string ProviderName,
    string ModelName,
    decimal InputPricePer1KTokens,
    decimal OutputPricePer1KTokens,
    string Currency,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid? CreatedByAdminUserId,
    Guid? UpdatedByAdminUserId);

public sealed record CreatePricingOverrideCommand(
    string ProviderName,
    string ModelName,
    decimal InputPricePer1KTokens,
    decimal OutputPricePer1KTokens,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid AdminUserId);

public sealed record UpdatePricingOverrideCommand(
    Guid Id,
    decimal InputPricePer1KTokens,
    decimal OutputPricePer1KTokens,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid AdminUserId);

public sealed record DeactivatePricingOverrideCommand(
    Guid Id,
    Guid AdminUserId);

// ── AI config categories ──────────────────────────────────────────────────────

public sealed record AiConfigCategoryItem(
    Guid Id,
    string CategoryKey,
    string DisplayName,
    string? ProviderName,
    string? ModelName,
    string? VoiceName);

public sealed record UpdateAiConfigCategoryCommand(
    string CategoryKey,
    string? ProviderName,
    string? ModelName,
    string? VoiceName);

public sealed record CategoryTestResult(
    string CategoryKey,
    string ProviderName,
    string? ModelName,
    string? VoiceName,
    bool Ok,
    int LatencyMs,
    string? Error);

// ── Dashboard aggregate ───────────────────────────────────────────────────────

public sealed record ActivityTrendBucket(string Date, int ActivityCount, int CompletedCount, int FailedCount);
public sealed record AdminDashboardActivityTrendResponse(string Period, IReadOnlyList<ActivityTrendBucket> Buckets);

public sealed record ScoreDistributionBucket(string Label, int MinScore, int MaxScore, int Count);
public sealed record AdminDashboardScoreDistributionResponse(string Period, int TotalScoredAttempts, IReadOnlyList<ScoreDistributionBucket> Buckets, double? AverageScore);

public sealed record AdminAggAiUsageTrendBucket(string Date, int RequestCount, int SuccessfulCalls, int FailedCalls, long InputTokens, long OutputTokens, long TotalTokens, decimal Cost);
public sealed record AdminAiUsageTrendResponse(string Period, IReadOnlyList<AdminAggAiUsageTrendBucket> Buckets);

public sealed record AiUsageCategoryBreakdownItem(string Category, int RequestCount, long TotalTokens, decimal Cost, int FailedCalls);
public sealed record AdminAiUsageCategoryBreakdownResponse(string Period, IReadOnlyList<AiUsageCategoryBreakdownItem> Categories);

public interface IAdminDashboardAggregateHandler
{
    Task<AdminDashboardActivityTrendResponse> GetActivityTrendsAsync(string period, CancellationToken ct = default);
    Task<AdminDashboardScoreDistributionResponse> GetScoreDistributionAsync(string period, CancellationToken ct = default);
    Task<AdminAiUsageTrendResponse> GetAiUsageTrendsAsync(string period, CancellationToken ct = default);
    Task<AdminAiUsageCategoryBreakdownResponse> GetAiUsageCategoryBreakdownAsync(string period, CancellationToken ct = default);
}

// ── Generation quality ────────────────────────────────────────────────────────

public sealed record ValidationFailureItem(
    DateTime TimestampUtc,
    string? PatternKey,
    string ActivityTypeName,
    string? CefrLevel,
    string? ObjectiveKey,
    string ValidationErrors,
    int AttemptNumber,
    string? ProviderName = null,
    string? ModelName = null,
    string? GenerationSource = null,
    string? CorrelationId = null);

public sealed record PatternFailureBreakdownItem(
    string PatternKey,
    int TotalFailures,
    int AbandonedCount,
    string? LatestError);

public sealed record CefrFailureBreakdownItem(
    string CefrLevel,
    int TotalFailures);

public sealed record ProviderModelBreakdownItem(
    string ProviderName,
    string ModelName,
    int TotalFailures,
    int AbandonedCount);

public sealed record AbandonedGenerationWarning(
    bool IsActive,
    double AbandonedRate,
    int AbandonedCount,
    int TotalFailures,
    double WarningThreshold,
    string? Message);

public sealed record GenerationQualitySummary(
    int TotalValidationFailures,
    int AbandonedGenerations,
    int RecentFailureCount,
    IReadOnlyList<ValidationFailureItem> LatestFailures,
    IReadOnlyList<PatternFailureBreakdownItem> PatternBreakdown,
    IReadOnlyList<CefrFailureBreakdownItem> CefrBreakdown,
    IReadOnlyList<PromptTemplateItem> PromptSummary,
    IReadOnlyList<ProviderModelBreakdownItem> ProviderBreakdown,
    AbandonedGenerationWarning AbandonedWarning,
    int RetentionDays);

public interface IAdminGenerationQualityHandler
{
    Task<GenerationQualitySummary> GetSummaryAsync(int recentDays = 30, CancellationToken ct = default);
}
