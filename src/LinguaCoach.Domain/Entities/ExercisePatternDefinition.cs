using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Defines one named exercise pattern — the exact interaction format for a lesson step.
///
/// ExercisePatternDefinition is the connective layer between:
///   - SessionExercise (which slot in the lesson)
///   - IAiActivityGenerator (which prompt key to use, what schema to expect)
///   - Frontend renderer (which component to instantiate, via InteractionMode)
///   - ActivitySubmitHandler (how to evaluate the answer, via MarkingMode)
///
/// Records are seeded by ExercisePatternSeeder at startup.
/// Pattern keys are canonical string constants defined in ExercisePatternKey.
/// </summary>
public sealed class ExercisePatternDefinition : BaseEntity
{
    /// <summary>Unique stable key. Maps to ExercisePatternKey constants.</summary>
    public string Key { get; private set; }

    /// <summary>Human-readable display name shown in admin and debugging.</summary>
    public string Name { get; private set; }

    /// <summary>Primary skill trained by this pattern (e.g. "Vocabulary", "Listening", "Writing").</summary>
    public string PrimarySkill { get; private set; }

    /// <summary>JSON array of secondary skills. E.g. ["Grammar","Tone"].</summary>
    public string SecondarySkillsJson { get; private set; }

    /// <summary>JSON array of ExerciseKind int values this pattern is valid for.</summary>
    public string CompatibleKindsJson { get; private set; }

    /// <summary>The broad ActivityType this pattern resolves to for activity generation.</summary>
    public ActivityType ActivityType { get; private set; }

    /// <summary>Drives frontend renderer selection — which Angular component to instantiate.</summary>
    public InteractionMode InteractionMode { get; private set; }

    /// <summary>Drives answer evaluation routing — how the submitted answer is marked.</summary>
    public MarkingMode MarkingMode { get; private set; }

    /// <summary>Typical minutes for a student to complete one instance of this pattern.</summary>
    public int EstimatedMinutes { get; private set; }

    /// <summary>Key in ai_prompts table used to generate content for this pattern.</summary>
    public string AiGeneratePromptKey { get; private set; }

    /// <summary>Key in ai_prompts table used to evaluate student answers for this pattern.</summary>
    public string AiEvaluatePromptKey { get; private set; }

    /// <summary>True if this pattern requires a TTS audio clip to be generated.</summary>
    public bool RequiresAudio { get; private set; }

    /// <summary>True if this pattern is set in a workplace scenario context.</summary>
    public bool WorkplaceContext { get; private set; }

    /// <summary>Why this pattern is used in a lesson — shown in admin and used for diagnostics.</summary>
    public string TeachingPurpose { get; private set; }

    /// <summary>False = soft-deleted / retired. Not surfaced in session generation.</summary>
    public bool IsActive { get; private set; }

    private ExercisePatternDefinition()
    {
        Key = string.Empty;
        Name = string.Empty;
        PrimarySkill = string.Empty;
        SecondarySkillsJson = "[]";
        CompatibleKindsJson = "[]";
        AiGeneratePromptKey = string.Empty;
        AiEvaluatePromptKey = string.Empty;
        TeachingPurpose = string.Empty;
    }

    public ExercisePatternDefinition(
        string key,
        string name,
        string primarySkill,
        string secondarySkillsJson,
        string compatibleKindsJson,
        ActivityType activityType,
        InteractionMode interactionMode,
        MarkingMode markingMode,
        int estimatedMinutes,
        string aiGeneratePromptKey,
        string aiEvaluatePromptKey,
        string teachingPurpose,
        bool requiresAudio = false,
        bool workplaceContext = true)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(primarySkill))
            throw new ArgumentException("PrimarySkill is required.", nameof(primarySkill));
        if (estimatedMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedMinutes), "EstimatedMinutes must be positive.");
        if (string.IsNullOrWhiteSpace(aiGeneratePromptKey))
            throw new ArgumentException("AiGeneratePromptKey is required.", nameof(aiGeneratePromptKey));
        if (string.IsNullOrWhiteSpace(aiEvaluatePromptKey))
            throw new ArgumentException("AiEvaluatePromptKey is required.", nameof(aiEvaluatePromptKey));

        Key = key.Trim();
        Name = name.Trim();
        PrimarySkill = primarySkill.Trim();
        SecondarySkillsJson = string.IsNullOrWhiteSpace(secondarySkillsJson) ? "[]" : secondarySkillsJson.Trim();
        CompatibleKindsJson = string.IsNullOrWhiteSpace(compatibleKindsJson) ? "[]" : compatibleKindsJson.Trim();
        ActivityType = activityType;
        InteractionMode = interactionMode;
        MarkingMode = markingMode;
        EstimatedMinutes = estimatedMinutes;
        AiGeneratePromptKey = aiGeneratePromptKey.Trim();
        AiEvaluatePromptKey = aiEvaluatePromptKey.Trim();
        TeachingPurpose = teachingPurpose.Trim();
        RequiresAudio = requiresAudio;
        WorkplaceContext = workplaceContext;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void UpdateInteractionMode(InteractionMode interactionMode) => InteractionMode = interactionMode;
}
