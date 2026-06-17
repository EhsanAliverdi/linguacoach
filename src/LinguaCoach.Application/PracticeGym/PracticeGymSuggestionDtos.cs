namespace LinguaCoach.Application.PracticeGym;

/// <summary>
/// Personalized Practice Gym suggestion response for a student.
/// Items are pre-selected from the readiness pool using profile, routing, and ledger signals.
/// </summary>
public sealed class PracticeGymSuggestionsDto
{
    /// <summary>Normal ready items personalised for the student's current goal/level.</summary>
    public IReadOnlyList<PracticeGymSuggestionItemDto> SuggestedItems { get; init; } = [];

    /// <summary>Reserved (in-progress) items the student can continue.</summary>
    public IReadOnlyList<PracticeGymSuggestionItemDto> ContinueItems { get; init; } = [];

    /// <summary>ReviewOnly / scaffold / remediation items for targeted review.</summary>
    public IReadOnlyList<PracticeGymSuggestionItemDto> ReviewItems { get; init; } = [];

    public int ReadyCount { get; init; }
    public int ReviewOnlyCount { get; init; }
    public int ReservedCount { get; init; }

    public bool IsReplenishmentRecommended { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}

/// <summary>
/// A single Practice Gym suggestion card derived from a readiness pool item.
/// </summary>
public sealed class PracticeGymSuggestionItemDto
{
    public Guid ReadinessItemId { get; init; }

    /// <summary>Display title for the practice card.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Short student-facing description.</summary>
    public string Description { get; init; } = string.Empty;

    public string? PrimarySkill { get; init; }
    public IReadOnlyList<string> SecondarySkills { get; init; } = [];

    public string? PatternKey { get; init; }
    public string? ActivityType { get; init; }

    public string TargetCefrLevel { get; init; } = string.Empty;
    public string? StudentCefrLevelSnapshot { get; init; }

    public string? CurriculumObjectiveKey { get; init; }
    public string? CurriculumObjectiveTitle { get; init; }

    public IReadOnlyList<string> ContextTags { get; init; } = [];
    public IReadOnlyList<string> FocusTags { get; init; } = [];

    /// <summary>Internal routing reason (Normal / Review / Scaffold / Remediation / Fallback).</summary>
    public string RoutingReason { get; init; } = string.Empty;

    /// <summary>True when this content is below the student's current CEFR level.</summary>
    public bool IsLowerLevelContent { get; init; }

    public int DifficultyBand { get; init; }

    /// <summary>Approximate session length in minutes from preference snapshot.</summary>
    public int? EstimatedDurationMinutes { get; init; }

    public string? SupportLanguageName { get; init; }

    /// <summary>Pool lifecycle status (ready / reserved / review_only).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Student-facing label for the card:
    ///   Normal  → "Recommended for your current goal"
    ///   Review  → "Review"
    ///   Scaffold → "Step back to strengthen basics"
    ///   Remediation → "Targeted fix"
    ///   Fallback → "General practice"
    /// </summary>
    public string CallToAction { get; init; } = string.Empty;

    /// <summary>Short student-friendly explanation of why this item was suggested.</summary>
    public string Explanation { get; init; } = string.Empty;

    // Linked materialized entities — non-null when the item is ready to open directly.
    public Guid? LinkedLearningActivityId { get; init; }
    public Guid? LinkedLearningSessionId { get; init; }
    public Guid? LinkedSessionExerciseId { get; init; }
}

/// <summary>
/// Result of starting (reserving) a suggested Practice Gym item.
/// </summary>
public sealed class StartSuggestionResult
{
    public bool Success { get; init; }
    public string? FailureReason { get; init; }

    /// <summary>Navigation target for the student app.</summary>
    public Guid? LearningActivityId { get; init; }
    public Guid? LearningSessionId { get; init; }
    public Guid? SessionExerciseId { get; init; }

    /// <summary>True when the item was already reserved (idempotent re-start).</summary>
    public bool AlreadyReserved { get; init; }
}
