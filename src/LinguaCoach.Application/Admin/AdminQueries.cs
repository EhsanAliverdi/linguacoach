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
    DateTime CreatedAt);

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

public sealed record ResetStudentPasswordCommand(Guid StudentProfileId, string NewPassword, bool MustChangePassword = true);

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

public interface IAdminStudentQuery
{
    Task<IReadOnlyList<StudentListItem>> ListStudentsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<StudentListItem> UpdateStudentAsync(UpdateStudentProfileCommand command, CancellationToken ct = default);
    Task<StudentListItem> ArchiveStudentAsync(ArchiveStudentCommand command, CancellationToken ct = default);
    Task ResetStudentPasswordAsync(ResetStudentPasswordCommand command, CancellationToken ct = default);
    Task<ResetStudentResponse> ResetStudentAsync(ResetStudentCommand command, CancellationToken ct = default);
    Task<int> CountRecentResetsAsync(Guid adminUserId, TimeSpan window, CancellationToken ct = default);
    Task<AdminStatsItem> GetStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminActivityHistoryItem>> GetActivityHistoryAsync(Guid studentProfileId, CancellationToken ct = default);
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
    int? MaxOutputTokens);

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

// ── Career profiles + curriculum words ───────────────────────────────────────

public sealed record CareerProfileItem(Guid Id, string Name);

public sealed record CurriculumWordItem(
    Guid Id,
    string Word,
    string Definition,
    string ExampleSentence,
    int Priority,
    string Tags);

public sealed record AddCurriculumWordCommand(
    Guid CareerProfileId,
    Guid LanguagePairId,
    string Word,
    string Definition,
    string ExampleSentence,
    int Priority,
    string Tags = "");

public sealed record UpdateCurriculumWordCommand(
    Guid WordId,
    string Definition,
    string ExampleSentence,
    int Priority,
    string Tags);

public interface IAdminCurriculumHandler
{
    Task<IReadOnlyList<CareerProfileItem>> ListCareerProfilesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CurriculumWordItem>> ListWordsAsync(Guid careerProfileId, Guid languagePairId, CancellationToken ct = default);
    Task<CurriculumWordItem> AddWordAsync(AddCurriculumWordCommand command, CancellationToken ct = default);
    Task<CurriculumWordItem> UpdateWordAsync(UpdateCurriculumWordCommand command, CancellationToken ct = default);
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
}

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
