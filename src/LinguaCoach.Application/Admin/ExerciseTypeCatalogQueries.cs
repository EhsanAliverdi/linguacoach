namespace LinguaCoach.Application.Admin;

public sealed record ExerciseTypeDefinitionDto(
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
    string? LegacyActivityType,
    string? ExercisePatternKey,
    int EstimatedDurationMinutes,
    bool RequiresAudio,
    bool RequiresImage,
    bool SupportsPracticeGym,
    bool SupportsTodayLesson);

public sealed record UpdateExerciseTypeDefinitionCommand(
    string Key,
    bool? IsEnabled,
    bool? SupportsPracticeGym,
    bool? SupportsTodayLesson);

public interface IExerciseTypeCatalogService
{
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetEnabledAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetGenerationEligibleAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default);
    Task<ExerciseTypeDefinitionDto?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<ExerciseTypeDefinitionDto> UpdateAsync(UpdateExerciseTypeDefinitionCommand command, CancellationToken ct = default);
}
