namespace LinguaCoach.Application.Writing;

/// <summary>
/// Orchestrates the writing evaluation lifecycle.
/// Callers: ActivitySubmitHandler (request on submission), WritingEvaluationJob (evaluate pending).
/// </summary>
public interface IWritingEvaluationService
{
    /// <summary>
    /// Creates a Pending evaluation record for a submitted written attempt.
    /// Non-fatal — never throws. Writing submission must never be blocked by evaluation setup.
    /// </summary>
    Task RequestEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        Guid activityId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns evaluation status/result for a specific attempt.
    /// Student-facing — never exposes raw provider payloads.
    /// Returns null when no evaluation record exists.
    /// </summary>
    Task<WritingEvaluationDto?> GetEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes pending evaluations up to maxBatch. Called by WritingEvaluationJob.
    /// Returns count of evaluations processed in this run.
    /// </summary>
    Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default);
}
