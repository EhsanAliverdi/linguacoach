using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class ExerciseTypeDefinition : BaseEntity
{
    public string Key { get; private set; }
    public string DisplayName { get; private set; }
    public string Description { get; private set; }
    public string PrimarySkill { get; private set; }
    public string SecondarySkillsJson { get; private set; }
    public string Category { get; private set; }
    public bool IsEnabled { get; private set; }
    public string ImplementationStatus { get; private set; }
    public string RendererKey { get; private set; }
    public string EvaluatorKey { get; private set; }
    public string GenerationPromptKey { get; private set; }
    public ActivityType? LegacyActivityType { get; private set; }
    public string? ExercisePatternKey { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public bool RequiresAudio { get; private set; }
    public bool RequiresImage { get; private set; }
    public bool SupportsPracticeGym { get; private set; }
    public bool SupportsTodayLesson { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool IsAvailableForGeneration =>
        IsEnabled && ImplementationStatus.Equals("ready", StringComparison.OrdinalIgnoreCase);

    private ExerciseTypeDefinition()
    {
        Key = string.Empty;
        DisplayName = string.Empty;
        Description = string.Empty;
        PrimarySkill = string.Empty;
        SecondarySkillsJson = "[]";
        Category = string.Empty;
        ImplementationStatus = "planned";
        RendererKey = string.Empty;
        EvaluatorKey = string.Empty;
        GenerationPromptKey = string.Empty;
    }

    public ExerciseTypeDefinition(
        string key,
        string displayName,
        string description,
        string primarySkill,
        string secondarySkillsJson,
        string category,
        bool isEnabled,
        string implementationStatus,
        string rendererKey,
        string evaluatorKey,
        string generationPromptKey,
        ActivityType? legacyActivityType,
        string? exercisePatternKey,
        int estimatedDurationMinutes,
        bool requiresAudio,
        bool requiresImage,
        bool supportsPracticeGym,
        bool supportsTodayLesson)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(primarySkill)) throw new ArgumentException("PrimarySkill is required.", nameof(primarySkill));
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
        if (estimatedDurationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedDurationMinutes));

        Key = key.Trim();
        DisplayName = displayName.Trim();
        Description = description.Trim();
        PrimarySkill = primarySkill.Trim().ToLowerInvariant();
        SecondarySkillsJson = string.IsNullOrWhiteSpace(secondarySkillsJson) ? "[]" : secondarySkillsJson.Trim();
        Category = category.Trim();
        IsEnabled = isEnabled;
        ImplementationStatus = implementationStatus.Trim().ToLowerInvariant();
        RendererKey = rendererKey.Trim();
        EvaluatorKey = evaluatorKey.Trim();
        GenerationPromptKey = generationPromptKey.Trim();
        LegacyActivityType = legacyActivityType;
        ExercisePatternKey = string.IsNullOrWhiteSpace(exercisePatternKey) ? null : exercisePatternKey.Trim();
        EstimatedDurationMinutes = estimatedDurationMinutes;
        RequiresAudio = requiresAudio;
        RequiresImage = requiresImage;
        SupportsPracticeGym = supportsPracticeGym;
        SupportsTodayLesson = supportsTodayLesson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSafeSurfaceFlags(bool? supportsPracticeGym, bool? supportsTodayLesson)
    {
        if (supportsPracticeGym.HasValue) SupportsPracticeGym = supportsPracticeGym.Value;
        if (supportsTodayLesson.HasValue) SupportsTodayLesson = supportsTodayLesson.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SyncCatalogMetadata(ExerciseTypeDefinition source)
    {
        DisplayName = source.DisplayName;
        Description = source.Description;
        PrimarySkill = source.PrimarySkill;
        SecondarySkillsJson = source.SecondarySkillsJson;
        Category = source.Category;
        ImplementationStatus = source.ImplementationStatus;
        RendererKey = source.RendererKey;
        EvaluatorKey = source.EvaluatorKey;
        GenerationPromptKey = source.GenerationPromptKey;
        LegacyActivityType = source.LegacyActivityType;
        ExercisePatternKey = source.ExercisePatternKey;
        EstimatedDurationMinutes = source.EstimatedDurationMinutes;
        RequiresAudio = source.RequiresAudio;
        RequiresImage = source.RequiresImage;
        UpdatedAt = DateTime.UtcNow;
    }
}
