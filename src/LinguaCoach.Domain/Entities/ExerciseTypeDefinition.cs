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
    public int MinItemsPerPractice { get; private set; }
    public int DefaultItemsPerPractice { get; private set; }
    public int MaxItemsPerPractice { get; private set; }
    public int MinOptionsPerItem { get; private set; }
    public int DefaultOptionsPerItem { get; private set; }
    public int MaxOptionsPerItem { get; private set; }
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
        MinItemsPerPractice = 1;
        DefaultItemsPerPractice = 1;
        MaxItemsPerPractice = 1;
        MinOptionsPerItem = 0;
        DefaultOptionsPerItem = 0;
        MaxOptionsPerItem = 0;
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
        int minItemsPerPractice = 1,
        int defaultItemsPerPractice = 1,
        int maxItemsPerPractice = 1,
        int minOptionsPerItem = 0,
        int defaultOptionsPerItem = 0,
        int maxOptionsPerItem = 0)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(primarySkill)) throw new ArgumentException("PrimarySkill is required.", nameof(primarySkill));
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
        if (estimatedDurationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedDurationMinutes));

        Key = NormalizeKey(key);
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
        ValidateCounts(minItemsPerPractice, defaultItemsPerPractice, maxItemsPerPractice, minOptionsPerItem, defaultOptionsPerItem, maxOptionsPerItem);
        MinItemsPerPractice = minItemsPerPractice;
        DefaultItemsPerPractice = defaultItemsPerPractice;
        MaxItemsPerPractice = maxItemsPerPractice;
        MinOptionsPerItem = minOptionsPerItem;
        DefaultOptionsPerItem = defaultOptionsPerItem;
        MaxOptionsPerItem = maxOptionsPerItem;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateCounts(int minItems, int defaultItems, int maxItems, int minOptions, int defaultOptions, int maxOptions)
    {
        if (minItems < 0 || defaultItems < 0 || maxItems < 0 || minOptions < 0 || defaultOptions < 0 || maxOptions < 0)
            throw new ArgumentOutOfRangeException(nameof(minItems), "Count values must not be negative.");
        if (!(minItems <= defaultItems && defaultItems <= maxItems))
            throw new ArgumentException("Items must satisfy min <= default <= max.");
        if (!(minOptions <= defaultOptions && defaultOptions <= maxOptions))
            throw new ArgumentException("Options must satisfy min <= default <= max.");
    }

    /// <summary>
    /// Updates only the configurable practice/option count settings.
    /// Never changes ImplementationStatus, IsEnabled, or runnable surface flags.
    /// </summary>
    public void UpdateItemCounts(int minItems, int defaultItems, int maxItems, int minOptions, int defaultOptions, int maxOptions)
    {
        ValidateCounts(minItems, defaultItems, maxItems, minOptions, defaultOptions, maxOptions);
        MinItemsPerPractice = minItems;
        DefaultItemsPerPractice = defaultItems;
        MaxItemsPerPractice = maxItems;
        MinOptionsPerItem = minOptions;
        DefaultOptionsPerItem = defaultOptions;
        MaxOptionsPerItem = maxOptions;
        UpdatedAt = DateTime.UtcNow;
    }

    public static string NormalizeKey(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : key.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
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
        MinItemsPerPractice = source.MinItemsPerPractice;
        DefaultItemsPerPractice = source.DefaultItemsPerPractice;
        MaxItemsPerPractice = source.MaxItemsPerPractice;
        MinOptionsPerItem = source.MinOptionsPerItem;
        DefaultOptionsPerItem = source.DefaultOptionsPerItem;
        MaxOptionsPerItem = source.MaxOptionsPerItem;
        UpdatedAt = DateTime.UtcNow;
    }
}
