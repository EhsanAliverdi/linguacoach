using LinguaCoach.Application.Learning;

namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Input to ICurriculumRoutingService. Carries all student context needed to
/// select suitable curriculum objectives and CEFR bands for a generation request.
/// </summary>
public sealed class CurriculumRoutingRequest
{
    public required Guid StudentId { get; init; }

    /// <summary>Raw CEFR string from StudentProfile.CefrLevel. May be null, plus-level (B2+), or unknown.</summary>
    public string? CurrentCefrLevel { get; init; }

    /// <summary>Primary skill requested by the caller (e.g. "writing"). Null = let routing decide.</summary>
    public string? PrimarySkill { get; init; }

    /// <summary>Specific exercise pattern key requested (e.g. "email_reply"). Null = any pattern.</summary>
    public string? RequestedPatternKey { get; init; }

    /// <summary>Which part of the system is requesting routing.</summary>
    public required string Source { get; init; }

    /// <summary>Resolved learning goal context from ILearningGoalContextResolver.</summary>
    public ResolvedLearningGoalContext? ResolvedLearningGoalContext { get; init; }

    // Learner preference fields (copied from StudentProfile for convenience)
    public IReadOnlyList<string> LearningGoals { get; init; } = [];
    public string? CustomLearningGoal { get; init; }
    public IReadOnlyList<string> FocusAreas { get; init; } = [];
    public string? CustomFocusArea { get; init; }
    public string? DifficultyPreference { get; init; }
    public int? PreferredSessionDurationMinutes { get; init; }
    public string? SupportLanguageCode { get; init; }
    public string? SupportLanguageName { get; init; }
    public string? TranslationHelpPreference { get; init; }

    /// <summary>
    /// Expresses the caller's intent: NewLearning, Review, Reinforcement, or Preview.
    /// Defaults to NewLearning. Controls mastery filtering and review scaffold eligibility.
    /// Phase 11C.
    /// </summary>
    public RoutingMode Mode { get; init; } = RoutingMode.NewLearning;

    /// <summary>
    /// When true, routing may select lower-level content and will mark it as
    /// review/scaffold/remediation. When false, lower-level content is never silently selected.
    /// </summary>
    public bool AllowReviewOrScaffold { get; init; }

    /// <summary>Optional recent ledger signals for weakness detection.</summary>
    public IReadOnlyList<string> RecentWeakPatternKeys { get; init; } = [];

    /// <summary>
    /// Keys of objectives the student has already mastered.
    /// These are excluded from new-learning routing unless AllowReviewOfMastered is true.
    /// Phase 11B.
    /// </summary>
    public IReadOnlyList<string> MasteredObjectiveKeys { get; init; } = [];

    /// <summary>
    /// When true and the student has mastered an objective, routing may still return it
    /// if the objective is marked IsReviewable. Phase 11B.
    /// </summary>
    public bool AllowReviewOfMastered { get; init; } = false;
}
