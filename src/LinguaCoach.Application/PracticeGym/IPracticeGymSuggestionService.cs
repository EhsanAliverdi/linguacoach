namespace LinguaCoach.Application.PracticeGym;

/// <summary>
/// Builds personalised Practice Gym suggestion lists from the student readiness pool.
/// Selection uses profile preferences, CEFR routing, focus areas, and ledger signals where available.
/// Does not trigger blocking AI generation — pool must be pre-filled by the replenishment job.
/// </summary>
public interface IPracticeGymSuggestionService
{
    /// <summary>
    /// Returns Suggested, Continue, and Review item lists for the Practice Gym home page.
    /// Triggers a non-blocking replenishment hint when pool is below target.
    /// </summary>
    Task<PracticeGymSuggestionsDto> GetSuggestionsForStudentAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Reserves a ready readiness pool item so the student can start it.
    /// Prevents double-reservation. Returns linked activity/session ids when available.
    /// Returns a failure result (not an exception) for safe/already-consumed items.
    /// </summary>
    Task<StartSuggestionResult> StartSuggestionAsync(Guid studentId, Guid readinessItemId, CancellationToken ct = default);

    /// <summary>
    /// Marks a readiness pool item consumed when the linked activity/session is completed.
    /// No-ops when item is not reserved or does not belong to the student.
    /// </summary>
    Task TryMarkConsumedAsync(Guid studentId, Guid readinessItemId, CancellationToken ct = default);
}
