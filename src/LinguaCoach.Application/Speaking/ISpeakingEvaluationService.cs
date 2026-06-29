namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Orchestrates the speaking evaluation lifecycle.
/// Callers: ActivityController (request on submission), SpeakingEvaluationJob (evaluate pending).
/// </summary>
public interface ISpeakingEvaluationService
{
    /// <summary>
    /// Creates a Pending evaluation record for a submitted audio attempt.
    /// Non-fatal — never throws. Audio submission must never be blocked by evaluation setup.
    /// </summary>
    Task RequestEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        Guid activityId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns evaluation status/result for a specific attempt.
    /// Student-facing — never exposes storage keys or raw provider payloads.
    /// Returns null when no evaluation record exists.
    /// </summary>
    Task<SpeakingEvaluationDto?> GetEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes pending evaluations up to maxBatch. Called by SpeakingEvaluationJob.
    /// Returns count of evaluations processed in this run.
    /// </summary>
    Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default);
}
