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

public interface IStudentMemoryService
{
    Task<UserLearningSummary> GetOrCreateWithBootstrapAsync(Guid studentProfileId, CancellationToken ct = default);
    Task UpdateMemoryAsync(ActivityMemoryUpdateRequest request, CancellationToken ct = default);
    Task<string> BuildAdaptiveContextJsonAsync(Guid studentProfileId, int moduleCount, CancellationToken ct = default);
}
