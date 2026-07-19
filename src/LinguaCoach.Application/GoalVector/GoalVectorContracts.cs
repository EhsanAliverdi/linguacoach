namespace LinguaCoach.Application.GoalVector;

/// <summary>
/// Adaptive Curriculum Sprint 3 — a student's own weighted goal vector: explicit input and
/// implicit engagement-drift blended into one score per goal tag. See
/// docs/architecture/adaptive-curriculum-skill-graph.md. Additive only — nothing outside the
/// student's own "My Goals" view and this service consume it yet.
/// </summary>
public interface IStudentGoalVectorService
{
    Task<IReadOnlyList<StudentGoalWeightDto>> GetGoalsAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>Direct, student-initiated set of one goal's weight. Overwrites whatever the current
    /// value is (explicit input always wins over prior implicit drift for that write) —
    /// <see cref="ArgumentException"/> if <paramref name="goalTag"/> isn't a recognized goal tag.</summary>
    Task SetExplicitWeightAsync(Guid studentId, string goalTag, double weight, CancellationToken ct = default);

    /// <summary>Bounded EMA nudge toward 1.0 for every recognized goal tag found in
    /// <paramref name="contextTags"/> — called once per completed activity attempt. Tags in
    /// <paramref name="contextTags"/> that aren't recognized goal tags (e.g. "pronunciation") are
    /// silently ignored, not an error — a Module's ContextTagsJson mixes goal and non-goal tags.</summary>
    Task RecordImplicitEngagementAsync(Guid studentId, IReadOnlyList<string> contextTags, CancellationToken ct = default);
}

public sealed record StudentGoalWeightDto(string GoalTag, double Weight, string Source, DateTimeOffset UpdatedAtUtc);

/// <summary>Sprint 3 — one-time, idempotent backfill: maps the old free-list
/// <c>StudentProfile.LearningGoals</c> (a different, unrelated key vocabulary seeded by
/// OnboardingFlowSeeder.cs) onto the new goal-tag vector where a real mapping exists. Old keys with
/// no goal-tag equivalent (e.g. "pronunciation", "exam_inspired_practice" — skill/format
/// descriptors, not motivations) are intentionally left unmapped, not guessed at.</summary>
public interface IStudentGoalVectorBackfillService
{
    Task<GoalVectorBackfillResult> BackfillFromLearningGoalsAsync(CancellationToken ct = default);
}

public sealed record GoalVectorBackfillResult(
    int StudentsScanned,
    int StudentsWithAtLeastOneMappedGoal,
    int WeightsCreated,
    int WeightsSkippedAlreadySet);
