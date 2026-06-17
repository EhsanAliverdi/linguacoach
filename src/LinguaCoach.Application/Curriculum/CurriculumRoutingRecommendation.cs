namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Output of ICurriculumRoutingService. Carries the routing decision and all
/// context fields needed to build an AI generation context and prompt.
/// </summary>
public sealed class CurriculumRoutingRecommendation
{
    /// <summary>Normalized CEFR band to use for generation (e.g. B2, never B2+).</summary>
    public required string TargetCefrLevel { get; init; }

    /// <summary>All CEFR levels allowed in this routing decision (usually just TargetCefrLevel).</summary>
    public IReadOnlyList<string> AllowedCefrLevels { get; init; } = [];

    /// <summary>Primary skill selected for this activity.</summary>
    public string? PrimarySkill { get; init; }

    /// <summary>Secondary skills that may also be practiced.</summary>
    public IReadOnlyList<string> SecondarySkills { get; init; } = [];

    /// <summary>Stable key of the selected CurriculumObjective, if one was matched.</summary>
    public string? CurriculumObjectiveKey { get; init; }

    /// <summary>Human-readable title of the selected objective for AI prompt context.</summary>
    public string? CurriculumObjectiveTitle { get; init; }

    /// <summary>Context tags resolved from learner goals (e.g. general_english, travel).</summary>
    public IReadOnlyList<string> ContextTags { get; init; } = [];

    /// <summary>Focus tags from the matched objective or learner focus areas.</summary>
    public IReadOnlyList<string> FocusTags { get; init; } = [];

    /// <summary>Difficulty band 1-5 within the target CEFR level.</summary>
    public int DifficultyBand { get; init; } = 1;

    /// <summary>
    /// Why this routing decision was made.
    /// normal = exact-level match, review/scaffold/remediation = deliberate lower-level,
    /// fallback = no objective matched.
    /// </summary>
    public RoutingReason RoutingReason { get; init; } = RoutingReason.Normal;

    /// <summary>True when content is below the student's current CEFR level.</summary>
    public bool IsLowerLevelContent { get; init; }

    /// <summary>Human-readable explanation for logs and debug UI.</summary>
    public string? Explanation { get; init; }

    /// <summary>Which part of the system requested routing (mirrors request Source).</summary>
    public required string Source { get; init; }

    /// <summary>
    /// Compact text for injection into AI generation context.
    /// Null when no objective was matched.
    /// </summary>
    public string? RoutingContextSummary => CurriculumObjectiveKey is null
        ? null
        : BuildRoutingContextSummary();

    private string BuildRoutingContextSummary()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(CurriculumObjectiveTitle))
            parts.Add($"Curriculum objective: {CurriculumObjectiveTitle}");

        if (ContextTags.Count > 0)
            parts.Add($"Context: {string.Join(", ", ContextTags)}");

        if (RoutingReason != RoutingReason.Normal)
            parts.Add($"Mode: {RoutingReason.ToString().ToLowerInvariant()}");

        if (IsLowerLevelContent)
            parts.Add($"Note: review/scaffold content below current level");

        return string.Join(". ", parts);
    }
}

public enum RoutingReason
{
    Normal,
    Review,
    Scaffold,
    Remediation,
    Fallback
}
