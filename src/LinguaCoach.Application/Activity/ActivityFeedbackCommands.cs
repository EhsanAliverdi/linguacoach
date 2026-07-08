using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

// ── Submit activity feedback (Phase B2) ────────────────────────────────────────

public sealed record SubmitActivityFeedbackCommand(
    Guid UserId,
    Guid LearningActivityId,
    ActivityFeedbackDifficultyRating DifficultyRating,
    ActivityFeedbackClarityRating ClarityRating,
    ActivityFeedbackUsefulnessRating UsefulnessRating,
    ActivityFeedbackRepeatPreference RepeatPreference,
    Guid? ActivityAttemptId = null,
    string? OptionalComment = null);

public sealed record ActivityFeedbackSignalDto(
    Guid Id,
    Guid LearningActivityId,
    Guid? ActivityAttemptId,
    ActivityFeedbackDifficultyRating DifficultyRating,
    ActivityFeedbackClarityRating ClarityRating,
    ActivityFeedbackUsefulnessRating UsefulnessRating,
    ActivityFeedbackRepeatPreference RepeatPreference,
    string? OptionalComment,
    DateTime UpdatedAt);

public interface ISubmitActivityFeedbackHandler
{
    Task<ActivityFeedbackSignalDto> HandleAsync(SubmitActivityFeedbackCommand command, CancellationToken ct = default);
}

// ── Effective feedback policy (Phase B2) ───────────────────────────────────────

/// <summary>Which student-facing flow an activity completion came from — determines which of
/// the two ActivityFeedback policy settings applies. Mirrors the TodayLesson/PracticeGym split
/// already used by <see cref="LearningEventSource"/>.</summary>
public enum ActivityFeedbackSurface
{
    Today,
    PracticeGym,
}

public sealed record ActivityFeedbackPolicyDto(
    ActivityFeedbackPolicy Policy,
    ActivityFeedbackSurface Surface);

public interface IActivityFeedbackPolicyProvider
{
    Task<ActivityFeedbackPolicyDto> GetEffectivePolicyAsync(ActivityFeedbackSurface surface, CancellationToken ct = default);
}
