using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

public sealed record ExerciseTypeRegistryEntry(
    string Key,
    string DisplayName,
    string Description,
    string PrimarySkill,
    IReadOnlyList<string> SecondarySkills,
    string Category,
    bool IsEnabled,
    string ImplementationStatus,
    bool IsAvailableForGeneration,
    string RendererKey,
    string EvaluatorKey,
    string GenerationPromptKey,
    ActivityType? LegacyActivityType,
    string? ExercisePatternKey,
    int EstimatedDurationMinutes,
    bool RequiresAudio,
    bool RequiresImage,
    int MinItemsPerPractice = 1,
    int DefaultItemsPerPractice = 1,
    int MaxItemsPerPractice = 1,
    int MinOptionsPerItem = 0,
    int DefaultOptionsPerItem = 0,
    int MaxOptionsPerItem = 0);

public interface IExerciseTypeRegistry
{
    Task<ExerciseTypeRegistryEntry?> GetByKeyAsync(string exerciseTypeKey, CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetGenerationEligibleAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetEligibleExerciseTypesForSkillAsync(
        string primarySkill, CancellationToken ct = default);
    Task<string?> ResolveRendererKeyAsync(string exerciseTypeKey, CancellationToken ct = default);
    Task<string?> ResolveEvaluatorKeyAsync(string exerciseTypeKey, CancellationToken ct = default);
    Task<string?> ResolveGenerationPromptKeyAsync(string exerciseTypeKey, CancellationToken ct = default);
    Task<ActivityType?> ResolveLegacyActivityTypeAsync(string exerciseTypeKey, CancellationToken ct = default);
    Task<string?> ResolveExercisePatternKeyAsync(string exerciseTypeKey, CancellationToken ct = default);
}
