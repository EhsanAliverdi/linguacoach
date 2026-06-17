namespace LinguaCoach.Application.Learning;

/// <summary>
/// Immutable snapshot of the resolved learning goal context for a student profile.
/// Built by ILearningGoalContextResolver. Used in AI prompts, ledger records, and metadata.
/// </summary>
public sealed record ResolvedLearningGoalContext
{
    /// <summary>First structured goal key selected from LearningGoals list, if any.</summary>
    public string? PrimaryGoalKey { get; init; }

    /// <summary>Comma-joined display labels for all resolved LearningGoals.</summary>
    public string? GoalLabels { get; init; }

    /// <summary>Free-text custom goal from CustomLearningGoal, if provided.</summary>
    public string? CustomGoal { get; init; }

    /// <summary>Comma-joined focus area keys from FocusAreas list, if any.</summary>
    public string? FocusAreaKeys { get; init; }

    /// <summary>Comma-joined display labels for all resolved FocusAreas.</summary>
    public string? FocusAreaLabels { get; init; }

    /// <summary>Free-text custom focus area from CustomFocusArea, if provided.</summary>
    public string? CustomFocusArea { get; init; }

    /// <summary>Short bounded string (max 200 chars) for ledger metadata and prompts.</summary>
    public string ContextSummary { get; init; } = string.Empty;

    /// <summary>Which resolution path was used: Explicit, Structured, Legacy, or Fallback.</summary>
    public string Source { get; init; } = "Unknown";

    public string? SupportLanguageCode { get; init; }
    public string? SupportLanguageName { get; init; }
    public string? DifficultyPreference { get; init; }

    /// <summary>True when resolved goals contain workplace/professional/business keywords.</summary>
    public bool WorkplaceSpecific { get; init; }

    /// <summary>True when source was LearningGoalDescription, LearningGoal, or CareerContext.</summary>
    public bool LegacyFallbackUsed { get; init; }
}
