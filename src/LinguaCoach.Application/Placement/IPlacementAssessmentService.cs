using System.Text.Json;

namespace LinguaCoach.Application.Placement;

public interface IPlacementAssessmentService
{
    Task<PlacementAssessmentSummaryDto> StartAssessmentAsync(Guid studentProfileId, string source, CancellationToken ct = default);
    Task<PlacementAssessmentSummaryDto?> GetLatestAssessmentAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<PlacementHistoryItemDto>> GetHistoryAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<PlacementAssessmentSummaryDto> CompleteAssessmentAsync(Guid assessmentId, CancellationToken ct = default);
    Task AbandonAssessmentAsync(Guid assessmentId, CancellationToken ct = default);

    // Phase 13B — Real response submission and adaptive progression
    // skillFilter (added for per-skill placement cards): when supplied, item selection is
    // scoped to that one skill instead of the globally least-evidenced skill.
    // submissionData is the raw Form.io submission.data dictionary (Form.io-native migration) —
    // replaces the old single-string response.
    Task<SubmitResponseResult> SubmitResponseAsync(Guid assessmentId, Guid itemId, IReadOnlyDictionary<string, JsonElement> submissionData, int? durationSeconds, string? skillFilter = null, CancellationToken ct = default);
    Task<PlacementNextItemDto?> GetNextItemAsync(Guid assessmentId, string? skillFilter = null, CancellationToken ct = default);
    Task<PlacementAssessmentProgressDto> GetProgressAsync(Guid assessmentId, CancellationToken ct = default);

    /// <summary>Per-skill status (percent complete / completed) for the placement cards page.</summary>
    Task<IReadOnlyList<PlacementSkillStatusDto>> GetSkillStatusAsync(Guid studentProfileId, CancellationToken ct = default);
}
