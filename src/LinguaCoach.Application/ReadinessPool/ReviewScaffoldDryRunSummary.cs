namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Read-only simulation of what would happen if EnableReviewScaffoldGeneration were active.
/// Produced by the dry-run endpoint — never mutates the database.
/// </summary>
public sealed record ReviewScaffoldDryRunSummary
{
    /// <summary>Whether EnableReviewScaffoldGeneration is currently on in config.</summary>
    public bool GenerationEnabled { get; init; }

    /// <summary>Whether DryRunOnly is set (generation would be simulated, never written).</summary>
    public bool DryRunOnly { get; init; }

    public string Status => GenerationEnabled
        ? (DryRunOnly ? "DryRun" : "Enabled")
        : "Disabled";

    public int StudentsConsidered { get; init; }

    /// <summary>Students with at least one weak event — would receive review routing.</summary>
    public int StudentsEligibleForReview { get; init; }

    /// <summary>Ready/Reserved items that would be demoted to ReviewOnly due to mastery.</summary>
    public int EstimatedReviewOnlyConversions { get; init; }

    /// <summary>Items skipped because a duplicate ReviewOnly already exists.</summary>
    public int BlockedDuplicates { get; init; }

    /// <summary>Mastered objectives with no active curriculum definition (would be blocked from generation).</summary>
    public int BlockedInactiveObjectives { get; init; }

    /// <summary>Estimated net new ReviewOnly items that would be created after deduplication.</summary>
    public int EstimatedNetNewReviewItems { get; init; }

    // --- Phase 19A: controlled enablement config + counts ---

    /// <summary>Whether generated scaffold items are held back from students until an admin clears the flag.</summary>
    public bool RequireAdminReview { get; init; }

    /// <summary>Maximum scaffold items generated per student per day.</summary>
    public int MaxScaffoldItemsPerStudentPerDay { get; init; }

    /// <summary>Readiness pool sources eligible for scaffold generation.</summary>
    public IReadOnlyList<string> ScaffoldAllowedSources { get; init; } = [];

    /// <summary>Whether Today lesson pool items may receive scaffold generation.</summary>
    public bool AllowTodayLessonInsertion { get; init; }

    /// <summary>Minimum confidence band required before a weak signal triggers generation.</summary>
    public string MinimumConfidenceForReviewNeed { get; init; } = "Medium";

    /// <summary>Current count of Ready/ReviewOnly items still held for admin review.</summary>
    public int AdminReviewRequiredCount { get; init; }

    /// <summary>Scaffold items generated across all students since the start of today (UTC).</summary>
    public int GeneratedTodayCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DateTime GeneratedAt { get; init; }
}
