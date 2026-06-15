namespace LinguaCoach.Application.Activity;

/// <summary>
/// A ready item from the Practice Gym pre-generation pool.
/// </summary>
public sealed record PracticeGymPoolItemDto(
    Guid PoolItemId,
    Guid LearningActivityId,
    string ExerciseTypeKey,
    string PrimarySkill);

/// <summary>
/// Looks up and manages pre-generated Practice Gym pool items
/// (backed by <c>PracticeActivityCache</c>). Only ever returns items whose
/// exercise type is enabled, ready, and Practice-Gym-eligible per
/// <see cref="IExerciseTypeRegistry"/>.
/// </summary>
public interface IPracticeGymPoolService
{
    /// <summary>Returns a ready pool item for the exact exercise type, if any, and reserves it.</summary>
    Task<PracticeGymPoolItemDto?> FindReadyForExerciseTypeAsync(Guid studentProfileId, string exerciseTypeKey, CancellationToken ct = default);

    /// <summary>Returns a ready pool item for any registry-eligible exercise type for the given skill, and reserves it.</summary>
    Task<PracticeGymPoolItemDto?> FindReadyForSkillAsync(Guid studentProfileId, string primarySkill, CancellationToken ct = default);

    /// <summary>Marks a reserved pool item as consumed (student opened the activity).</summary>
    Task MarkConsumedAsync(Guid poolItemId, CancellationToken ct = default);

    /// <summary>Marks a pool item as failed (e.g. generation error), so it is excluded from future selection.</summary>
    Task MarkFailedAsync(Guid poolItemId, string reason, CancellationToken ct = default);
}
