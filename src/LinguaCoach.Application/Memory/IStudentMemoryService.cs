using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.Memory;

public sealed record ActivityMemoryUpdateRequest(
    StudentProfile StudentProfile,
    LearningActivity Activity,
    LearningModule? Module,
    ActivityAttempt Attempt,
    string FeedbackJson,
    double? Score,
    string? CorrelationId);

public sealed record PlacementMemorySeed(
    Guid StudentProfileId,
    string EstimatedLevel,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> WeakSkillKeys,
    IReadOnlyList<string> StrongSkillKeys);

public interface IStudentMemoryService
{
    Task<UserLearningSummary> GetOrCreateWithBootstrapAsync(Guid studentProfileId, CancellationToken ct = default);
    Task UpdateMemoryAsync(ActivityMemoryUpdateRequest request, CancellationToken ct = default);
    Task<string> BuildAdaptiveContextJsonAsync(Guid studentProfileId, int moduleCount, CancellationToken ct = default);
    Task SeedFromPlacementAsync(PlacementMemorySeed seed, CancellationToken ct = default);
}
