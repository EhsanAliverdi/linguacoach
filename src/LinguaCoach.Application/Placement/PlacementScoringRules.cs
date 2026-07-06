namespace LinguaCoach.Application.Placement;

/// <summary>
/// Backend-only scoring artifact for a Form.io-authored placement item, keyed by Form.io
/// component key. Deserialized from PlacementItemDefinition.ScoringRulesJson /
/// PlacementAssessmentItem.ScoringRulesJsonSnapshot — never returned to students.
/// </summary>
public sealed record ScoringRulesDocument(
    Dictionary<string, ComponentScoringRule> Components,
    /// <summary>Backend-only TTS script for listening-skill items — converted to speech
    /// server-side (AdaptivePlacementAudioService) and never rendered as text to the student.</summary>
    string? ListeningAudioScript = null);

/// <summary>Supported scoring kinds. single_choice/multiple_choice compare against Form.io
/// radio/selectboxes values; text_exact is a case-sensitive trim compare; text_normalized
/// case-folds and collapses whitespace before comparing (used for gap-fill answers).</summary>
public static class ScoringRuleKinds
{
    public const string SingleChoice = "single_choice";
    public const string MultipleChoice = "multiple_choice";
    public const string TextExact = "text_exact";
    public const string TextNormalized = "text_normalized";
}

public sealed record ComponentScoringRule(
    string Kind,
    string? CorrectAnswer = null,
    IReadOnlyList<string>? CorrectAnswers = null,
    double Points = 1.0,
    string? Skill = null,
    string? CefrLevel = null,
    /// <summary>When true, this component is excluded from deterministic scoring (e.g. a
    /// free-text writing/speaking response awaiting manual or AI evaluation). Items whose every
    /// component requires manual/AI evaluation should be seeded with IsEnabled=false until the
    /// adaptive engine can evaluate them.</summary>
    bool RequiresManualOrAiEvaluation = false);
