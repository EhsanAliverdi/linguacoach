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

    /// <summary>
    /// The ExercisePatternKey this activity was generated from, when generated via a
    /// session exercise step. Null for Practice Gym activities generated without a pattern.
    /// Set by ExercisePrepareHandler. Used for renderer dispatch and evaluation routing.
    /// </summary>
    public string? ExercisePatternKey { get; private set; }

    /// <summary>Student-safe Form.io schema for a Form.io-rendered activity instance (Practice
    /// Gym Form.io pilot — docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
    /// Null for all legacy/ModuleStageSchema activities. Never contains a correct answer.</summary>
    public string? FormIoSchemaJson { get; private set; }

    /// <summary>Backend-only: scoring rules for FormIoSchemaJson, keyed by Form.io component
    /// key (same shape as PlacementItemDefinition.ScoringRulesJson). Never returned to students.</summary>
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Phase D2 — JSON array of published Resource Bank entries (CefrVocabularyEntry/
    /// CefrGrammarProfileEntry/CefrReadingReference) offered to the AI prompt as supporting
    /// material at materialization time: [{type, id, sourceId, contentFingerprint,
    /// selectionReason}, ...]. Admin-only debugging/traceability — never shown to students, and
    /// distinct from AiGeneratedContentJson (the actual student-facing content). Null when no
    /// bank resources were used (legacy generation, or a pattern D1/D2 doesn't support).
    /// Deliberately a separate field from StudentActivityReadinessItem/StudentActivityUsageLog/
    /// ActivityFeedbackSignal's SourceBankItemId — that column is FK-constrained to
    /// PlacementItemDefinition and cannot hold a Phase E Cefr* bank row id.</summary>
    public string? BankResourceProvenanceJson { get; private set; }

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
        Guid? sourceWritingScenarioId = null,
        string? exercisePatternKey = null)
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
        ExercisePatternKey = string.IsNullOrWhiteSpace(exercisePatternKey) ? null : exercisePatternKey.Trim();
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

    /// <summary>Sets the Form.io student-safe schema and backend-only scoring rules for this
    /// activity — used only by the Practice Gym Form.io pilot generation path.</summary>
    public void SetFormIoContent(string formIoSchemaJson, string? scoringRulesJson)
    {
        if (string.IsNullOrWhiteSpace(formIoSchemaJson))
            throw new ArgumentException("FormIoSchemaJson is required.", nameof(formIoSchemaJson));

        FormIoSchemaJson = formIoSchemaJson;
        ScoringRulesJson = scoringRulesJson;
    }

    /// <summary>Records which published Resource Bank entries (if any) were offered to the AI
    /// prompt for this activity — see <see cref="BankResourceProvenanceJson"/>.</summary>
    public void SetBankResourceProvenance(string bankResourceProvenanceJson)
    {
        if (string.IsNullOrWhiteSpace(bankResourceProvenanceJson))
            throw new ArgumentException("BankResourceProvenanceJson is required.", nameof(bankResourceProvenanceJson));
        BankResourceProvenanceJson = bankResourceProvenanceJson;
    }
}
