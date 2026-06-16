using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Memory;

public interface IStudentLearningLedger
{
    /// <summary>
    /// Records a learning event. Best-effort — never throws. Logs failures.
    /// </summary>
    Task RecordAsync(StudentLearningEvent learningEvent, CancellationToken ct = default);

    /// <summary>
    /// Returns recent events for a student, newest first.
    /// Used by DynamicPatternSelector and future curriculum queries.
    /// </summary>
    Task<IReadOnlyList<StudentLearningEvent>> GetRecentAsync(
        Guid studentProfileId,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Returns recent events for the given pattern keys, newest first.
    /// </summary>
    Task<IReadOnlyList<StudentLearningEvent>> GetRecentByPatternKeysAsync(
        Guid studentProfileId,
        IEnumerable<string> patternKeys,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Returns distinct pattern keys practised recently, newest first.
    /// Used by DynamicPatternSelector to avoid repetition.
    /// </summary>
    Task<IReadOnlyList<string>> GetRecentPatternKeysAsync(
        Guid studentProfileId,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Returns events tagged with NeedsReview or Failed outcome, newest first.
    /// </summary>
    Task<IReadOnlyList<StudentLearningEvent>> GetWeakEventsAsync(
        Guid studentProfileId,
        int limit = 20,
        CancellationToken ct = default);
}
