using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Computed mastery signal for a single skill or curriculum objective.
/// Derived from StudentLearningEvent history — no mutable state, no DB writes.
/// </summary>
public sealed record ObjectiveMasterySignal
{
    public required string ObjectiveKey { get; init; }
    public required string? SkillKey { get; init; }
    public required MasteryStatus MasteryStatus { get; init; }
    public required int EvidenceCount { get; init; }
    public required int ConsecutiveSuccesses { get; init; }
    public required int ConsecutiveFailures { get; init; }
    public required double RecentAverageScore { get; init; }
    public required DateTime? LastSeenUtc { get; init; }
}
