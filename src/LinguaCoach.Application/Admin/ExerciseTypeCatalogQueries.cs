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
    bool SupportsTodayLesson,
    int MinItemsPerPractice = 1,
    int DefaultItemsPerPractice = 1,
    int MaxItemsPerPractice = 1,
    int MinOptionsPerItem = 0,
    int DefaultOptionsPerItem = 0,
    int MaxOptionsPerItem = 0);

public sealed record UpdateExerciseTypeDefinitionCommand(
    string Key,
    bool? IsEnabled,
    bool? SupportsPracticeGym,
    bool? SupportsTodayLesson,
    int? MinItemsPerPractice = null,
    int? DefaultItemsPerPractice = null,
    int? MaxItemsPerPractice = null,
    int? MinOptionsPerItem = null,
    int? DefaultOptionsPerItem = null,
    int? MaxOptionsPerItem = null);

public interface IExerciseTypeCatalogService
{
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetEnabledAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetGenerationEligibleAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default);
    Task<ExerciseTypeDefinitionDto?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<ExerciseTypeDefinitionDto> UpdateAsync(UpdateExerciseTypeDefinitionCommand command, CancellationToken ct = default);
}
