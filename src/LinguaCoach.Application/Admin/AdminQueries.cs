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

public interface IAdminStudentQuery
{
    Task<IReadOnlyList<StudentListItem>> ListStudentsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<StudentListItem> UpdateStudentAsync(UpdateStudentProfileCommand command, CancellationToken ct = default);
    Task<StudentListItem> ArchiveStudentAsync(ArchiveStudentCommand command, CancellationToken ct = default);
}

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

public sealed record AiProviderConfigItem(
    Guid Id,
    string FeatureKey,
    string ProviderName,
    string ModelName,
    string? FallbackProviderName,
    string? FallbackModelName,
    bool FallbackEnabled);

public sealed record ModelTestStatus(string ModelName, bool Ok, int LatencyMs, string? Error, DateTime TestedAt);

/// <summary>One entry per provider in the catalog — includes credential and per-model test status.</summary>
public sealed record AiProviderCatalogItem(
    string ProviderName,
    IReadOnlyList<string> Models,
    bool HasApiKey,
    IReadOnlyList<ModelTestStatus> ModelTests);

public sealed record UpdateAiProviderConfigCommand(
    Guid ConfigId,
    string? ProviderName,
    string? ModelName,
    string? FallbackProviderName,
    string? FallbackModelName,
    bool? FallbackEnabled);

public sealed record SetProviderApiKeyCommand(
    string ProviderName,
    string? ApiKey);

public interface IAdminAiConfigHandler
{
    Task<IReadOnlyList<AiProviderConfigItem>> ListConfigsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiProviderCatalogItem>> ListProvidersAsync(CancellationToken ct = default);
    Task<AiProviderConfigItem> UpdateConfigAsync(UpdateAiProviderConfigCommand command, CancellationToken ct = default);
    Task<AiProviderCatalogItem> SetProviderApiKeyAsync(SetProviderApiKeyCommand command, CancellationToken ct = default);
    Task<AiProviderCatalogItem> TestProviderAsync(string providerName, CancellationToken ct = default);
}
