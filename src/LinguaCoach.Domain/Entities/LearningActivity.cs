using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single learnable activity. ActivityType determines what kind of practice it is.
/// AiGeneratedContentJson holds the type-specific payload (schema depends on ActivityType).
///
/// WritingScenario payload shape:
/// {
///   "situation": "...",
///   "learningGoal": "...",
///   "targetPhrases": [...],
///   "targetVocabulary": [...],
///   "exampleText": "...",
///   "commonMistakeToAvoid": "...",
///   "instructionInSourceLanguage": "..."
/// }
/// </summary>
public sealed class LearningActivity : BaseEntity
{
    // Null for standalone activities not attached to a module (e.g. AI-generated on-demand).
    public Guid? LearningModuleId { get; private set; }

    public ActivityType ActivityType { get; private set; }
    public ActivitySource Source { get; private set; }

    public string Title { get; private set; }
    public string Difficulty { get; private set; }

    // JSONB — payload schema is ActivityType-specific. See XML doc above.
    public string AiGeneratedContentJson { get; private set; }

    // Reference back to a WritingScenario row when Source = SystemFallback and
    // ActivityType = WritingScenario. Null for AI-generated activities.
    public Guid? SourceWritingScenarioId { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<ActivityAttempt> Attempts => _attempts.AsReadOnly();
    private readonly List<ActivityAttempt> _attempts = [];

    private LearningActivity()
    {
        Title = string.Empty;
        Difficulty = string.Empty;
        AiGeneratedContentJson = "{}";
    }

    public LearningActivity(
        ActivityType activityType,
        ActivitySource source,
        string title,
        string difficulty,
        string aiGeneratedContentJson,
        Guid? learningModuleId = null,
        Guid? sourceWritingScenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(difficulty)) throw new ArgumentException("Difficulty is required.", nameof(difficulty));

        ActivityType = activityType;
        Source = source;
        Title = title.Trim();
        Difficulty = difficulty.Trim();
        AiGeneratedContentJson = string.IsNullOrWhiteSpace(aiGeneratedContentJson) ? "{}" : aiGeneratedContentJson;
        LearningModuleId = learningModuleId;
        SourceWritingScenarioId = sourceWritingScenarioId;
        IsActive = true;
    }

    public void UpdateContent(string aiGeneratedContentJson)
    {
        if (string.IsNullOrWhiteSpace(aiGeneratedContentJson))
            throw new ArgumentException("Content JSON is required.", nameof(aiGeneratedContentJson));
        AiGeneratedContentJson = aiGeneratedContentJson;
        Source = ActivitySource.AiGenerated;
    }

    public void Deactivate() => IsActive = false;
}
