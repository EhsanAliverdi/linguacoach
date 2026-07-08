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
    /// <summary>Not deterministically scored — the submitted value is an audio storage-key
    /// reference (from a Form.io "speakingResponse" component), routed to
    /// IPlacementSpeakingScorer/ISpeakingEvaluationProvider instead of PlacementScoringService.</summary>
    public const string Speaking = "speaking";
    /// <summary>Positional-order comparison for a stock Form.io "datagrid" component with its
    /// "reorder" setting enabled (Phase C3, 2026-07-08 — reorder_paragraphs template migration).
    /// The submitted value is an array of the datagrid's row objects in the student's chosen
    /// order; each row is expected to carry an "itemId" string identifying which original item it
    /// is. Scored generically — usable by any future reorder-style template, not just
    /// reorder_paragraphs. See <see cref="ComponentScoringRule.CorrectOrder"/>.</summary>
    public const string OrderedSequence = "ordered_sequence";
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
    bool RequiresManualOrAiEvaluation = false,
    /// <summary>Only used by <see cref="ScoringRuleKinds.OrderedSequence"/> — the item ids, in
    /// their correct order. Backend-only; never serialized into the student-facing Form.io schema.
    /// "Points" is applied per correctly-placed position (so the component's max score is
    /// CorrectOrder.Count * Points).</summary>
    IReadOnlyList<string>? CorrectOrder = null);
