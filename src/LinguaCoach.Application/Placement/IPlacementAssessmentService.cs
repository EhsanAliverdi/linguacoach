namespace LinguaCoach.Application.Placement;

public interface IPlacementAssessmentService
{
    Task<PlacementAssessmentSummaryDto> StartAssessmentAsync(Guid studentProfileId, string source, CancellationToken ct = default);
    Task<PlacementAssessmentSummaryDto?> GetLatestAssessmentAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<PlacementHistoryItemDto>> GetHistoryAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<PlacementAssessmentSummaryDto> CompleteAssessmentAsync(Guid assessmentId, CancellationToken ct = default);
    Task AbandonAssessmentAsync(Guid assessmentId, CancellationToken ct = default);

    // Phase 13B — Real response submission and adaptive progression
    Task<SubmitResponseResult> SubmitResponseAsync(Guid assessmentId, Guid itemId, string response, int? durationSeconds, CancellationToken ct = default);
    Task<PlacementNextItemDto?> GetNextItemAsync(Guid assessmentId, CancellationToken ct = default);
    Task<PlacementAssessmentProgressDto> GetProgressAsync(Guid assessmentId, CancellationToken ct = default);
}
